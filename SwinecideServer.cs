using System;
using System.Net;
using vtortola.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace SwinecideServer
{
    public class SwinecideServer
    {
        CancellationTokenSource cancellation;
        WebSocketListener server;
        Queue<WebSocket> attackerQueue = null;
        Queue<WebSocket> defenderQueue = null;
        Dictionary<WebSocket, Match> matchDictionary = null;
        
        public void Start()
        {
            attackerQueue = new Queue<WebSocket>();
            defenderQueue = new Queue<WebSocket>();
            matchDictionary = new Dictionary<WebSocket, Match>();

            cancellation = new CancellationTokenSource();
            IPEndPoint endpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8005);
            server = new WebSocketListener(endpoint);
            var rfc6455 = new vtortola.WebSockets.Rfc6455.WebSocketFactoryRfc6455(server);
            server.Standards.RegisterStandard(rfc6455);
            server.Start();

            Console.WriteLine("Echo Server started at " + endpoint.ToString());

            var task = Task.Run(() => AcceptWebSocketClientsAsync(server, cancellation.Token));
        }

        public void Stop()
        {
            cancellation.Cancel();
            if (server != null)
            {
                server.Stop();
            }
        }

        async Task AcceptWebSocketClientsAsync(WebSocketListener server, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var ws = await server.AcceptWebSocketAsync(token).ConfigureAwait(false);
                    Console.WriteLine("A new connection joined the server: " + ws.RemoteEndpoint);

                    if (ws != null)
                        Task.Run(() => HandleConnectionAsync(ws, token));
                }
                catch (Exception aex)
                {
                    Console.WriteLine("Error Accepting clients: " + aex.GetBaseException().Message);
                }
            }
            Console.WriteLine("Server Stop accepting clients");
        }

        async Task HandleConnectionAsync(WebSocket ws, CancellationToken cancellation)
        {
            try
            {
                while (ws.IsConnected && !cancellation.IsCancellationRequested)
                {
                    String msg = await ws.ReadStringAsync(cancellation).ConfigureAwait(false);
                    if (msg != null)
                    {
                        if (!attackerQueue.Contains(ws) && !defenderQueue.Contains(ws))
                        {
                            // The websocket is already in a match
                            if (matchDictionary.ContainsKey(ws))
                            {
                                // Find related match, relay message.
                                matchDictionary[ws].send_message(ws, "msg");
                            }
                            // The websocket is not queued and not in a match.
                            else
                            {
                                // Queue the websocket, wait for friend.
                                if (msg.ToLower().StartsWith("attacker"))
                                {
                                    Console.WriteLine("An attacker joined the game: " + ws.RemoteEndpoint);
                                    if (defenderQueue.Count != 0) {
                                        WebSocket defender = defenderQueue.Dequeue();
                                        Match newMatch = new Match(ws, defender);
                                        matchDictionary.Add(ws, newMatch);
                                        matchDictionary.Add(defender, newMatch);
                                    }
                                    else {
                                        attackerQueue.Enqueue(ws);
                                    }
                                }
                                else if (msg.ToLower().StartsWith("defender"))
                                {
                                    Console.WriteLine("A defender joined the game: " + ws.RemoteEndpoint);
                                    if (attackerQueue.Count != 0)
                                    {
                                        WebSocket attacker = attackerQueue.Dequeue();
                                        Match newMatch = new Match(attacker, ws);
                                        matchDictionary.Add(ws, newMatch);
                                        matchDictionary.Add(attacker, newMatch);
                                    }
                                    else
                                    {
                                        defenderQueue.Enqueue(ws);
                                    }
                                }
                            }
                        }
                        // Websocket is sending messages before getting put into a match, it's already queued.
                        else
                        {
                            ws.WriteString("Please wait for a match to start.");
                        }                        
                    }
                }
            }
            catch (Exception aex)
            {
                Console.WriteLine("Error Handling connection: " + aex.GetBaseException().Message);
                try { ws.Close(); }
                catch { }
            }
            finally
            {
                matchDictionary[ws].ws_disconnected(ws);
            }
        }
    }
}
