using System;
using System.Net;
using vtortola.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace SwinecideServer
{
    public class SwinecideServer
    {
        CancellationTokenSource cancellation;
        WebSocketListener server;
        Queue<String> attackerQueue = null;
        Queue<String> defenderQueue = null;
        Dictionary<WebSocket, String> wsToIdDictionary = null;
        Dictionary<String, Player> idToPlayerDictionary = null;
        
        public void Start()
        {
            attackerQueue = new Queue<String>();
            defenderQueue = new Queue<String>();
            wsToIdDictionary = new Dictionary<WebSocket, String>();
            idToPlayerDictionary = new Dictionary<String, Player>();

            cancellation = new CancellationTokenSource();
            IPEndPoint endpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8005);
            server = new WebSocketListener(endpoint);
            var rfc6455 = new vtortola.WebSockets.Rfc6455.WebSocketFactoryRfc6455(server);
            server.Standards.RegisterStandard(rfc6455);
            server.Start();

            Console.WriteLine("Echo Server started at " + endpoint.ToString());

            var task = Task.Run(() => AcceptWebSocketClientsAsync(server, cancellation.Token));
        }

        public void ReportMatchDone(Match match) {
            try
            {
                // remove dictionary associations
                // theoretically, after removing dictionary associations, this "match" is the final reference to the match.
                idToPlayerDictionary.Remove(match.attacker.getUniqueId());
                idToPlayerDictionary.Remove(match.defender.getUniqueId());
                wsToIdDictionary.Remove(match.attacker.getSocket());
                wsToIdDictionary.Remove(match.defender.getSocket());

                // dispose objects
                match.Dispose();

            }
            catch (Exception aex)
            {
                Console.WriteLine("Error Cleaning Up Match: " + aex.GetBaseException().Message);
            }
            match.attacker.getSocket().Dispose();
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
                        Dictionary<string, dynamic> msgDict = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(msg);
                        String socketId = msgDict["uniqueId"];
                        if (!attackerQueue.Contains(socketId) && !defenderQueue.Contains(socketId))
                        {
                            // The websocket is already in a match
                            if (wsToIdDictionary.ContainsKey(ws))
                            {
                                // Find related match, relay message.
                                idToPlayerDictionary[wsToIdDictionary[ws]].currentMatch.send_message(ws, msg);
                            }
                            // The websocket is not queued and not in a match.
                            else
                            {
                                // Queue the websocket, wait for friend.
                                if (msgDict["msgType"].StartsWith("LogInRequest")) {
                                    Player p = new Player(ws, msgDict["username"], socketId);
                                    wsToIdDictionary.Add(ws, socketId);
                                    idToPlayerDictionary.Add(socketId, p);

                                    if (msgDict["role"].ToLower() == "attacker")
                                    {
                                        Console.WriteLine("An attacker joined the game: " + ws.RemoteEndpoint);
                                        if (defenderQueue.Count != 0)
                                        {
                                            String defenderId = defenderQueue.Dequeue();
                                            Player defender = idToPlayerDictionary[defenderId];

                                            Match newMatch = new Match(p, defender, this);
                                        }
                                        else
                                        {
                                            attackerQueue.Enqueue(socketId);
                                        }
                                    }
                                    else if (msgDict["role"].ToLower() == "defender")
                                    {
                                        Console.WriteLine("A defender joined the game: " + ws.RemoteEndpoint);
                                        if (attackerQueue.Count != 0)
                                        {
                                            String attackerId = attackerQueue.Dequeue();
                                            Player attacker = idToPlayerDictionary[attackerId];

                                            Match newMatch = new Match(attacker, p, this);
                                        }
                                        else
                                        {
                                            defenderQueue.Enqueue(socketId);
                                        }
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
                idToPlayerDictionary[wsToIdDictionary[ws]].currentMatch.ws_disconnected(ws);
            }
        }
    }
}
