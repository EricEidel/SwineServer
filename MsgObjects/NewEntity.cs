using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace SwinecideServer.MsgObjects
{
    public class NewEntity
    {
        public string msgType;
        public int type;
        public int entityID;
        public Dictionary<string, int> location;
        public int parent_id;

        public NewEntity(int type, int entityID, int x, int y, int parent_id)
        {
            this.msgType = "NewEntity";
            this.type = type;
            this.entityID = entityID;
            this.location = new Dictionary<string, int>();
            this.location.Add("x", x);
            this.location.Add("y", y);
            this.parent_id = parent_id;
        }

        public String ToJSON()
        {
            Dictionary<String, dynamic> tempDict = new Dictionary<string, dynamic>();

            tempDict.Add("msgType", this.msgType);
            tempDict.Add("type", this.type);
            tempDict.Add("entityID", this.entityID);
            tempDict.Add("location", this.location);
            tempDict.Add("parent_id", this.parent_id);

            return JsonConvert.SerializeObject(tempDict, Formatting.Indented);
        }
    }
}
