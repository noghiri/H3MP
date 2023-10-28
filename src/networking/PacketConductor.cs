using H3MP.Networking;
using System.Collections.Generic;
using System.Text;
using H3MP.Networking.Events;

namespace H3MP.src.networking
{
    /// <summary>
    /// A class for packet IO queueing.  Also contains packet byte array handling.
    /// </summary>
    public static class PacketConductor
    {
        public static Queue<NetworkEvent> eventQueueUDPTx;
        public static Queue<NetworkEvent> eventQueueTCPTx;
        public static Queue<Packet> packetQueueRx;

        /// <summary>
        /// Initialization function
        /// </summary>
        public static void OnInit()
        {

        }

        /// <summary>
        /// Call to write out appropriate packets from queue and send them. Filtering will be implemented here.
        /// </summary>
        public static void Send()
        {
            
        }
        /// <summary>
        /// Adds events for eventual UDP transmission to the UDP event queue.
        /// </summary>
        /// <param name="_value"></param>
        public static void ToQueueUDP(NetworkEvent _value)
        {
            eventQueueUDPTx.Enqueue(_value);
        }

        /// <summary>
        /// Adds events for eventual TCP transmission to the TCP event queue.
        /// </summary>
        /// <param name="_value"></param>
        public static void ToQueueTCP(NetworkEvent _value)
        {
            eventQueueTCPTx.Enqueue(_value);
        }

        public static void EnqueueEvent(NetworkEvent evt)
        {
            
        }
    }
}
