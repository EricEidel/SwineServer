using System;
using System.Net;
using System.Linq;
using vtortola.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace SwinecideServer
{
    public class SwinecideServer
    {
        private static string[] ServerStateMsgTypes = { "Creature Died", "LifeReduced" };
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
            }
            catch (Exception aex)
            {
                Console.WriteLine("Error Cleaning Up Match: " + aex.GetBaseException().Message);
            }
            // match.attacker.getSocket().Dispose();
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
                    // Get the JSON Message.
                    String msg = await ws.ReadStringAsync(cancellation).ConfigureAwait(false);
                    if (msg != null)
                    {
                        // Convert the JSON String to a dictionary
                        Dictionary<string, dynamic> msgDict = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(msg); ;
                        // If the dictionary contains a uniqueId, the message is a log-in request.
                        if (msgDict.ContainsKey("uniqueId"))
                        {
                            this.HandleMatchQueue(ws, msgDict);
                        }
                        // Message doesn't contain a UniqueId field.
                        else
                        {
                            // Make sure the websocket is in a match
                            if (wsToIdDictionary.ContainsKey(ws))
                            {
                                idToPlayerDictionary[wsToIdDictionary[ws]].currentMatch.AcceptMessage(ws, msgDict);
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
                if (wsToIdDictionary.ContainsKey(ws))
                {
                    if (idToPlayerDictionary.ContainsKey(wsToIdDictionary[ws]))
                    {
                        idToPlayerDictionary[wsToIdDictionary[ws]].currentMatch.ws_disconnected(ws);
                    }
                }
                if (ws.IsConnected)
                {
                    ws.Dispose();
                }
                
            }
        }

        public void HandleMatchQueue(WebSocket ws,  Dictionary<string, dynamic> msgDict)
        {
            String socketId = msgDict["uniqueId"];
            // Check if ID is not already in Queue.
            if (!attackerQueue.Contains(socketId) && !defenderQueue.Contains(socketId))
            {
                // Check that the ws isn't in a match already
                if (!wsToIdDictionary.ContainsKey(ws))
                {
                    // Queue the websocket, wait for friend.
                    if (msgDict["msgType"].StartsWith("LogInRequest"))
                    {
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
            else
            {
                ws.WriteString("Please wait for a match to start.");
            }
        }
    }
}
