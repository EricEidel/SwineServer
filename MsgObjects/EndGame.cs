using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace SwinecideServer.MsgObjects
{
    public class EndGame
    {
        public string msgType;
        public string role;

        public EndGame(string role)
        {
            this.msgType = "EndGame";
            this.role = role;
        }

        public String ToJSON()
        {
            Dictionary<String, dynamic> tempDict = new Dictionary<string, dynamic>();

            tempDict.Add("msgType", this.msgType);
            tempDict.Add("role", this.role);

            return JsonConvert.SerializeObject(tempDict, Formatting.Indented);
        }
    }
}
