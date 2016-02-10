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

            attacker.getSocket().WriteString("A match has begun!");
            defender.getSocket().WriteString("A match has begun!");
        }

        public void Dispose()
        {
            this.attacker.getSocket().Dispose();
            this.defender.getSocket().Dispose();
            this.attacker.currentMatch = null;
            this.defender.currentMatch = null;
            this.attacker = null;
            this.defender = null;
        }

        private void defender_said(string msg)
        {
            attacker.getSocket().WriteString("Message from the other client was: " + msg);
        }

        private void attacker_said(string msg)
        {
            defender.getSocket().WriteString("Message from the other client was: " + msg);
        }

        public void send_message(WebSocket ws, string msg) 
        {
            if (ws == defender.getSocket())
            {
                defender_said(msg);
            }
            else if (ws == attacker.getSocket())
            {
                attacker_said(msg);
            }
            else {
                throw new Exception("Somehow ws in match was neither attacker nor defender.");
            }
            
        }

        public void ws_disconnected(WebSocket ws)
        {
            if (ws.RemoteEndpoint.Equals(defender.getSocket().RemoteEndpoint))
            {
                Console.WriteLine("Defender disconnected - attacker wins!");
                attacker.getSocket().WriteString("Defender disconnected - attacker wins!");
            }
            else
            {
                Console.WriteLine("Attacker disconnected - defender wins!");
                defender.getSocket().WriteString("Attacker disconnected - defender wins!");
            }
            server.ReportMatchDone(this);
        }
    }
}
