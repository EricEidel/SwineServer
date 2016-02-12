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
        public SwinecideServer server;
        public Player defender;
        public Player attacker;

        public Match(Player attacker, Player defender, SwinecideServer server)
        {
            attacker.currentMatch = this;
            defender.currentMatch = this;

            this.attacker = attacker;
            this.defender = defender;
            this.server = server;
            /*
             * { "msgType":"LogInRequest", "role":"defender" }
             */
            Dictionary<String, String> tempDict = new Dictionary<string, string>();
            tempDict.Add("msgType", "LogIn");
            tempDict.Add("role", "attacker");
            attacker.ws.WriteString(JsonConvert.SerializeObject(tempDict, Formatting.Indented));
            tempDict["role"] = "defender";
            defender.ws.WriteString(JsonConvert.SerializeObject(tempDict, Formatting.Indented));
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

        private void defender_said(string msg)
        {
            attacker.ws.WriteString(msg);
        }

        private void attacker_said(string msg)
        {
            defender.ws.WriteString(msg);
        }

        public void send_message(WebSocket ws, string msg) 
        {
            if (ws == defender.ws)
            {
                defender_said(msg);
            }
            else if (ws == attacker.ws)
            {
                attacker_said(msg);
            }
            else {
                throw new Exception("Somehow ws in match was neither attacker nor defender.");
            }
            
        }

        public void ws_disconnected(WebSocket ws)
        {
            Dictionary<String, String> tempDict = new Dictionary<string, string>();
            if (ws.RemoteEndpoint.Equals(defender.ws.RemoteEndpoint))
            {
                Console.WriteLine("Defender disconnected - attacker wins!");
                attacker.ws.WriteString("Defender disconnected - attacker wins!");
            }
            else
            {
                Console.WriteLine("Attacker disconnected - defender wins!");
                defender.ws.WriteString("Attacker disconnected - defender wins!");
            }
            server.ReportMatchDone(this);

            this.Dispose();
        }
    }
}
