using H3MP.Networking;
using H3MP.src.networking.clientEventReg;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace H3MP.src.networking
{
    /// <summary>
    /// A class for packet IO queueing.  Also contains packet byte array handling.
    /// </summary>
    class PacketConductor
    {
        public static Queue<NetworkEvent> eventQueueUDPTx;
        public static Queue<NetworkEvent> eventQueueTCPTx;
        public static Queue<Packet> packetQueueRx;
        public List<byte> buffer;

        /// <summary>
        /// Initialization function
        /// </summary>
        public void OnInit()
        {

        }

        /// <summary>
        /// Call to write out appropriate packets from queue and send them. Filtering will be implemented here.
        /// </summary>
        public void Send()
        {
            
        }
        /// <summary>
        /// Adds events for eventual UDP transmission to the UDP event queue.
        /// </summary>
        /// <param name="_value"></param>
        public void ToQueueUDP(NetworkEvent _value)
        {
            eventQueueUDPTx.Enqueue(_value);
        }

        /// <summary>
        /// Adds events for eventual TCP transmission to the TCP event queue.
        /// </summary>
        /// <param name="_value"></param>
        public void ToQueueTCP(NetworkEvent _value)
        {
            eventQueueTCPTx.Enqueue(_value);
        }

        /// <summary>
        /// A generic byte array reader.
        /// </summary>
        public struct Reader
        {
            public readonly byte[] Buffer;
            public int Offset;

            public Reader(byte[] buffer, int offset)
            {
                Buffer = buffer;
                Offset = offset;
            }

            public unsafe void Read<T>(out T value) where T : unmanaged
            {
                fixed (byte* p = &Buffer[Offset]) value = *(T*)p;
                Offset += sizeof(T);
            }
        }

        /// <summary>
        /// A generic byte array writer. 
        /// </summary>
        public struct Writer
        {
            public readonly byte[] Buffer;
            public int Offset;

            public Writer(byte[] buffer, int offset)
            {
                Buffer = buffer;
                Offset = offset;
            }

            public unsafe void Write<T>(T value) where T : unmanaged
            {
                fixed (byte* p = &Buffer[Offset]) *(T*)p = value;
                Offset += sizeof(T);
            }

            public void Write(string value)
            {
                ushort length = (ushort)Encoding.UTF8.GetByteCount(value);
                Write(length);
                Encoding.UTF8.GetBytes(value, 0, value.Length, Buffer, Offset);
                Offset += length;
            }
        }

    }
}
