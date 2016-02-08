using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using vtortola.WebSockets;

namespace SwinecideServer
{
    public class Match
    {
        public WebSocket defender;
        public WebSocket attacker;

        public Match(WebSocket attacker, WebSocket defender)
        {
            this.attacker = attacker;
            this.defender = defender;

            attacker.WriteString("A match has begun!");
            defender.WriteString("A match has begun!");
        }

        private void defender_said(string msg)
        {
            attacker.WriteString("Message from the other client was: " + msg);
        }

        private void attacker_said(string msg)
        {
            defender.WriteString("Message from the other client was: " + msg);
        }

        public void send_message(WebSocket ws, string msg) 
        {
            if (ws == defender)
            {
                defender_said(msg);
            }
            else if (ws == attacker)
            {
                attacker_said(msg);
            }
            else {
                throw new Exception("Somehow ws in match was neither attacker nor defender.");
            }
            
        }

        public void ws_disconnected(WebSocket ws)
        {
            if (ws.RemoteEndpoint.Equals(defender.RemoteEndpoint))
            {
                Console.WriteLine("Defender disconnected - attacker wins!");
                attacker.WriteString("Defender disconnected - attacker wins!");
                attacker.Dispose();
            }
            else
            {
                Console.WriteLine("Attacker disconnected - defender wins!");
                defender.WriteString("Attacker disconnected - defender wins!");
                defender.Dispose();
            }
        }
    }
}
