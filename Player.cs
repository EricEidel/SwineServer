using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using vtortola.WebSockets;

namespace SwinecideServer
{
    public class Player
    {
        public WebSocket ws;
        public String username;
        public String uniqueId;
        public int rating;
        public Match currentMatch;

        public Player(WebSocket ws, String username, String uniqueId)
        {
            // TODO: Actually write code that'll connect to a DB to see if we can pull rating information.
            this.ws = ws;
            this.username = username;
            this.uniqueId = uniqueId;
            this.rating = 0;
            this.currentMatch = null;
        }

        public String getUsername()
        {
            return this.username;
        }

        public String getUniqueId()
        {
            return this.uniqueId;
        }

        public WebSocket getSocket()
        {
            return this.ws;
        }
    }
}
