using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using vtortola.WebSockets;
using Newtonsoft.Json;

namespace SwinecideServer
{
    public class Match
    {
        // match length in milliseconds.
        private static int MatchLength = 600000;
        private static int AttackerDelay = 5000;
        private static int DefenderDelay = 10000;
        private enum msgTypes { LogInRequest, LogIn, EndGame, EntityRequest, NewEntity, CreatureDied, LifeReduced };
        public SwinecideServer server;
        public Player defender;
        public Player attacker;
        private int attackerLifeRequest;
        private int defenderLifeRequest;
        private int entityCounter;

        public Match(Player attacker, Player defender, SwinecideServer server)
        {
            attacker.currentMatch = this;
            defender.currentMatch = this;

            this.attacker = attacker;
            this.defender = defender;
            this.server = server;
            this.attackerLifeRequest = 0;
            this.defenderLifeRequest = 0;
            this.entityCounter = 1;
            /*
             * { "msgType":"LogInRequest", "role":"defender" }
             */
            string tempString = "role,attacker";
            this.SendMessage(attacker.ws, GenerateMessage(msgTypes.LogIn, tempString));
            tempString = "role,defender";
            this.SendMessage(defender.ws, GenerateMessage(msgTypes.LogIn, tempString));

            var task = Task.Run(() => ScheduleMatchEnd());
        }

        public async void ScheduleMatchEnd()
        {
            await Task.Delay(Match.MatchLength);
            if (Math.Min(this.attackerLifeRequest, this.defenderLifeRequest) < 0)
            {
                this.SendMessage(null, this.GenerateMessage(msgTypes.EndGame, "winner,defender"));
            }
        }

        public async void EntityRequested(WebSocket ws, long EntityType, long x, long y, long parentId)
        {
            this.entityCounter++;
            String kVP = "";
            kVP = "type," + EntityType + " entityID," + this.entityCounter + " location,x," + x + " location,y," + y + " parent_id," + parentId;
            int delay = 0;
            if (ws == this.attacker.getSocket())
            {
                delay = Match.AttackerDelay;
            }
            else
            {
                delay = Match.DefenderDelay;
            }

            await Task.Delay(delay);
            this.SendMessage(null, this.GenerateMessage(msgTypes.NewEntity, kVP));
        }

        public void LifeReduced(WebSocket ws)
        {
            if (ws == this.attacker.getSocket())
            {
                this.attackerLifeRequest++;
            }
            else
            {
                this.defenderLifeRequest++;
            }

            if (Math.Min(this.attackerLifeRequest, this.defenderLifeRequest) >= 3)
            {
                this.SendMessage(null, this.GenerateMessage(msgTypes.EndGame, "winner,attacker"));
            }
        }

        private String GenerateMessage(msgTypes msgType, string keyValuePairs) {
            Dictionary<String, dynamic> tempDict = new Dictionary<string, dynamic>();
            tempDict.Add("msgType", msgType.ToString());

            foreach (string pair in keyValuePairs.Split(' '))
            {
                if (pair.Split(',').Length == 2)
                {
                    tempDict.Add(pair.Split(',')[0], pair.Split(',')[1]);
                }
                else if (pair.Split(',').Length == 3)
                {
                    Dictionary<String, dynamic> internalDict = null;
                    if (tempDict.Keys.Contains(pair.Split(',')[0]))
                    {
                        internalDict = tempDict[pair.Split(',')[0]];
                    }
                    else {
                        internalDict = new Dictionary<string,dynamic>();
                    }
                    internalDict.Add(pair.Split(',')[1], pair.Split(',')[2]);
                }
                else
                {
                    throw new Exception("Generate Message: Bad KVP formatting.");
                }
                
            }

            return JsonConvert.SerializeObject(tempDict, Formatting.Indented);
        }

        public void Dispose()
        {
            this.attacker.ws = null;
            this.defender.ws = null;
            this.attacker.currentMatch = null;
            this.defender.currentMatch = null;
            this.attacker = null;
            this.defender = null;
        }

        private void WriteToAttacker(string msg)
        {
            attacker.ws.WriteString(msg);
        }

        private void WriteToDefender(string msg)
        {
            defender.ws.WriteString(msg);
        }

        public void SendMessage(WebSocket wsTarget, string msg) 
        {
            if (wsTarget == attacker.ws)
            {
                WriteToAttacker(msg);
            }
            else if (wsTarget == defender.ws)
            {
                WriteToDefender(msg);
            }
            // Send to both?
            else if (wsTarget == null)
            {
                WriteToAttacker(msg);
                WriteToDefender(msg);
            }
            else {
                throw new Exception("SendMessage: Somehow ws in match was neither attacker nor defender.");
            }
            
        }

        public void SendToOpponent(WebSocket wsFrom, string msg)
        {
            if (wsFrom == defender.ws)
            {
                WriteToAttacker(msg);
            }
            else if (wsFrom == attacker.ws)
            {
                WriteToDefender(msg);
            }
        }

        public void ws_disconnected(WebSocket ws)
        {
            Dictionary<String, String> tempDict = new Dictionary<string, string>();
            if (ws.RemoteEndpoint.Equals(defender.ws.RemoteEndpoint))
            {
                Console.WriteLine("Defender disconnected - attacker wins!");
                this.SendMessage(attacker.ws, this.GenerateMessage(msgTypes.EndGame, "winner,attacker"));
            }
            else
            {
                Console.WriteLine("Attacker disconnected - defender wins!");
                this.SendMessage(defender.ws, this.GenerateMessage(msgTypes.EndGame, "winner,defender"));
            }
            server.ReportMatchDone(this);

            this.Dispose();
        }
    }
}
