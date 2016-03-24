using System;
using System.IO;
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
        private string messagelog;

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
            this.messagelog = "";
            /*
             * { "msgType":"LogInRequest", "role":"defender" }
             */
            MsgObjects.LogIn temp = new MsgObjects.LogIn("attacker");
            this.SendMessage(attacker.ws, temp.ToJSON());
            temp = new MsgObjects.LogIn("defender");
            this.SendMessage(defender.ws, temp.ToJSON());

            var task = Task.Run(() => ScheduleMatchEnd());
        }

        public async void ScheduleMatchEnd()
        {
            await Task.Delay(Match.MatchLength);
            if (Math.Min(this.attackerLifeRequest, this.defenderLifeRequest) < 0)
            {
                MsgObjects.EndGame temp = new MsgObjects.EndGame("defender");
                this.SendMessage(null, temp.ToJSON());
            }
        }

        public async void EntityRequested(WebSocket ws, int EntityType, int x, int y, int parentId)
        {
            this.entityCounter++;
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
            MsgObjects.NewEntity temp = new MsgObjects.NewEntity(EntityType, this.entityCounter, x, y, parentId);
            this.SendMessage(null, temp.ToJSON());
        }

        public void AcceptMessage(WebSocket ws, Dictionary<string, dynamic> msgDict)
        {
            string msg = JsonConvert.SerializeObject(msgDict, Formatting.None);
            this.messagelog += msg + "\n";

            // Check if message is for server.
            if (msgDict["msgType"] == "CreatureDied")
            {
                // Not doing anything with this yet.
            }
            else if (msgDict["msgType"] == "LifeReduced")
            {
                // Find the match and report that the ws said something about a reduced life.
                this.LifeReduced(ws);
            }
            else if (msgDict["msgType"] == "RequestEntity")
            {
                // Find related match, relay message.
                this.SendToOpponent(ws, msg);
                int type = (int)msgDict["type"];
                int x = (int)msgDict["location"]["x"];
                int y = (int)msgDict["location"]["y"];
                int parentId = (int)msgDict["parent_id"];
                this.EntityRequested(ws, type, x, y, parentId);
            }
            else
            {
                // Find related match, relay message.
                this.SendToOpponent(ws, msg);
            }
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
                MsgObjects.EndGame temp = new MsgObjects.EndGame("attacker");
                this.SendMessage(null, temp.ToJSON());
            }
        }

        public void Dispose()
        {
            this.attacker.ws = null;
            this.defender.ws = null;
            this.attacker.currentMatch = null;
            this.defender.currentMatch = null;
            this.attacker = null;
            this.defender = null;

            string path = @"C:\jsonlog.txt";
            TextWriter tw = new StreamWriter(path, true);
            foreach (string msg in messagelog.Split('\n'))
            {
                tw.WriteLine(msg);
            }
            tw.WriteLine();
            tw.Close();
        }

        private void WriteToAttacker(string msg)
        {
            if (attacker.ws.IsConnected)
            {
                attacker.ws.WriteString(msg);
            }
        }

        private void WriteToDefender(string msg)
        {
            if (defender.ws.IsConnected)
            {
                defender.ws.WriteString(msg);
            }
        }

        public void SendMessage(WebSocket wsTarget, string msg) 
        {
            if (attacker.ws != null && wsTarget == attacker.ws)
            {
                WriteToAttacker(msg);
            }
            else if (defender.ws != null && wsTarget == defender.ws)
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
            if (wsFrom == defender.ws && attacker.ws != null)
            {
                WriteToAttacker(msg);
            }
            else if (wsFrom == attacker.ws & defender.ws != null)
            {
                WriteToDefender(msg);
            }
        }

        public void ws_disconnected(WebSocket ws)
        {
            Dictionary<String, String> tempDict = new Dictionary<string, string>();
            if (ws.RemoteEndpoint.Equals(defender.ws.RemoteEndpoint))
            {
                MsgObjects.LogIn temp = new MsgObjects.LogIn("attacker");
                Console.WriteLine("Defender disconnected - attacker wins!");
                this.SendMessage(attacker.ws, temp.ToJSON());
            }
            else
            {
                MsgObjects.LogIn temp = new MsgObjects.LogIn("defender");
                Console.WriteLine("Attacker disconnected - defender wins!");
                this.SendMessage(defender.ws, temp.ToJSON());
            }
            server.ReportMatchDone(this);

            this.Dispose();
        }
    }
}
