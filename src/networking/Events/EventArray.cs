using H3MP.Networking.Serialization;

namespace H3MP.Networking.Events;

public class EventArray
{
    public NetworkEvent[] Events = new NetworkEvent[0];

    public void Serialize(ref SerializationWriter writer)
    {
        writer.WriteNative((byte) Events.Length);
        for (int i = 0; i < Events.Length; i++)
        {
            Events[i].SerializeEvent(ref writer);
        }
    }

    public void Deserialize(ref SerializationReader reader)
    {
        reader.ReadNative(out byte eventCount);
        Events = new NetworkEvent[eventCount];
        for (int i = 0; i < eventCount; i++)
        {
            NetworkEvent.TryParse(ref reader, out Events[i]);
        }
    }
}