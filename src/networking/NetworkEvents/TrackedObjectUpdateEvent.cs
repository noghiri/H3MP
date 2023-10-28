using H3MP.src.networking.clientEventReg;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace H3MP.src.networking.NetworkEvents
{
    class TrackedObjectUpdateEvent : NetworkEvent
    {

        public override void Serialize()
        {
            // Code here to actually write stuff into a byte array for transmission

        }

        public override void Deserialize()
        {
            // Code here to actually read stuff from a byte array from transmission 
        }
    }
}
