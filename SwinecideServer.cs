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
        
        public void Start()
        {
            cancellation = new CancellationTokenSource();
            IPEndPoint endpoint = new IPEndPoint(IPAddress.Parse("192.168.0.14"), 8005);
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

        WebSocket attacker = null;
        WebSocket defender = null;
        bool match_not_started = true;
        Match match = null;

        async Task HandleConnectionAsync(WebSocket ws, CancellationToken cancellation)
        {
            try
            {
                while (ws.IsConnected && !cancellation.IsCancellationRequested)
                {
                    String msg = await ws.ReadStringAsync(cancellation).ConfigureAwait(false);
                    if (msg != null)
                    {
                        if (match_not_started)
                        {
                            if (msg.ToLower().StartsWith("attacker"))
                            {
                                attacker = ws;
                                Console.WriteLine("An attacker joined the game: " + attacker.RemoteEndpoint);
                            }
                            else if (msg.ToLower().StartsWith("defender"))
                            {
                                defender = ws;
                                Console.WriteLine("A defender joined the game: " + defender.RemoteEndpoint);
                            }

                            if (attacker != null && defender != null)
                            {
                                match = new Match(attacker, defender);
                                match_not_started = false;

                                Console.WriteLine("A match has started!");
                            }
                        }
                        else
                        {
                            if (ws.RemoteEndpoint.Equals(match.attacker.RemoteEndpoint))
                            {
                                match.attacker_said(msg);
                            }
                            else if (ws.RemoteEndpoint.Equals(match.defender.RemoteEndpoint))
                            {
                                match.defender_said(msg);
                            }
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
                ws.Dispose();
                match.ws_disconnected(ws);
            }
        }
    }
}
