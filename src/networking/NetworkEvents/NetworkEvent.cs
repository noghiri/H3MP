using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace H3MP.src.networking.clientEventReg
{
    /// <summary>
    /// This class defines Network Event classes.
    /// </summary>
    abstract class NetworkEvent
    {
        public abstract void Serialize();
        public abstract void Deserialize();
    }
}
