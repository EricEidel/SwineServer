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
        }

        public void defender_said(string msg)
        {
            send_message(attacker, msg);
        }

        public void attacker_said(string msg)
        {
            send_message(defender, msg);
        }

        private void send_message(WebSocket ws, string msg)
        {
            ws.WriteString("Message from the other client was: " + msg);
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
