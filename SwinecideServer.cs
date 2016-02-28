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
                    String msg = await ws.ReadStringAsync(cancellation).ConfigureAwait(false);
                    if (msg != null)
                    {
                        Dictionary<string, dynamic> msgDict = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(msg);
                        if (msgDict.ContainsKey("uniqueId"))
                        {
                            String socketId = msgDict["uniqueId"];
                            // If ws is not queued for match
                            if (!attackerQueue.Contains(socketId) && !defenderQueue.Contains(socketId))
                            {
                                // If ws is not yet in match
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
                        else
                        {
                            // The websocket is already in a match
                            if (wsToIdDictionary.ContainsKey(ws))
                            {
                                // Check if message is for server.
                                if (msgDict["msgType"] == "CreatureDied")
                                {
                                    // Not doing anything with this yet.
                                }
                                else if (msgDict["msgType"] == "LifeReduced")
                                {
                                    // Find the match and report that the ws said something about a reduced life.
                                    idToPlayerDictionary[wsToIdDictionary[ws]].currentMatch.LifeReduced(ws);
                                }
                                else if (msgDict["msgType"] == "RequestEntity") {
                                    // Find related match, relay message.
                                    idToPlayerDictionary[wsToIdDictionary[ws]].currentMatch.SendToOpponent(ws, msg);
                                    long type = msgDict["type"];
                                    long x = msgDict["location"]["x"];
                                    long y = msgDict["location"]["y"];
                                    long parentId = msgDict["parent_id"];
                                    idToPlayerDictionary[wsToIdDictionary[ws]].currentMatch.EntityRequested(ws, type, x, y, parentId);
                                }
                                else
                                {
                                    // Find related match, relay message.
                                    idToPlayerDictionary[wsToIdDictionary[ws]].currentMatch.SendToOpponent(ws, msg);
                                }
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
    }
}
