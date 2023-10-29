using System;
using System.Collections.Generic;
using System.Net.Sockets;
using H3MP.Networking.Events;
using H3MP.Networking.Serialization;

namespace H3MP.Networking
{
    /// <summary>
    /// A class for packet IO queueing.
    /// </summary>
    public class EventConductor
    {
        public TcpClient TcpClient { get; set; }

        public UdpClient UdpClient { get; set; }

        private Dictionary<EventType, List<NetworkEvent>> _batchedUdpEvents = new();
        
        /// <summary>
        /// Accepts an event and either immediately sends it or queues it for the next send tick
        /// </summary>
        /// <param name="evt"></param>
        public void ConductEvent(NetworkEvent evt)
        {
            // Depending on what type of event this is, we might either fire it off immediately or hold it back until
            // the next send tick
            if (evt.Type.Mode == EventTypeMode.Tcp)
            {
                byte[] data = new byte[2048];
                SerializationWriter writer = new SerializationWriter(data, 0);
                evt.SerializeEvent(ref writer);
                SendTcp(data, data.Length);
            }
            else if (evt.Type.Mode == EventTypeMode.Udp)
            {
                byte[] data = new byte[2048];
                SerializationWriter writer = new SerializationWriter(data, 0);
                evt.SerializeEvent(ref writer);
                SendUdp(data, data.Length);
            } else if (evt.Type.Mode == EventTypeMode.UdpBatched)
            {
                lock (_batchedUdpEvents)
                {
                    _batchedUdpEvents[evt.Type] = evt;
                }
            }
        }

        public void SendBatchedEvents()
        {
            // Make our own copy of the batched events dictionary so that nothing is waiting on us to finish
            Dictionary<EventType, NetworkEvent> copy;
            lock (_batchedUdpEvents)
            {
                copy = new(_batchedUdpEvents);
                _batchedUdpEvents.Clear();
            }
            
            
        }

        private IEnumerable<BatchedData> SplitEvents(IEnumerable<NetworkEvent> events, int mtu)
        {
            byte[] buffer = new byte[2048];
            SerializationWriter writer = new SerializationWriter(buffer, 0);

            int lastOffset = 0;
            
            foreach (NetworkEvent evt in events)
            {
                evt.SerializeEvent(ref writer);

                if (writer.Offset > mtu)
                {
                    yield return new BatchedData(buffer, lastOffset);

                    // Move anything left in the buffer back to the start
                    int remainingData = writer.Offset - lastOffset;
                    Array.Copy(buffer, lastOffset, buffer, 0, remainingData);
                    lastOffset = 0;
                    writer.Offset = remainingData;
                }
            }
        }
        
        protected void SendTcp(byte[] data, int length)
        {
            TcpClient.Client.BeginSend(data, 0, length, SocketFlags.None, null, null);
        }

        protected void SendUdp(byte[] data, int length)
        {
            UdpClient.BeginSend(data, length, null, null);
        }

        private struct BatchedData
        {
            public byte[] Data;
            public int Length;

            public BatchedData(byte[] data, int length)
            {
                Data = data;
                Length = length;
            }
        }
    }
}