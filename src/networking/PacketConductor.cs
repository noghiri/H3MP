using H3MP.Networking;
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
        public static Queue<Packet> packetQueueTx;
        public static Queue<Packet> packetQueueRx;

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
        /// A generic packet reader.
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
        /// A generic packet writer.
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
