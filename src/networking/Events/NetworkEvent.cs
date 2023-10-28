using System;
using H3MP.Networking.Serialization;

namespace H3MP.Networking.Events;

public abstract class NetworkEvent
{
    public readonly EventType Type;

    public byte SourceClientId;

    protected NetworkEvent(EventType type)
    {
        Type = type;
    }

    protected virtual void Serialize(ref SerializationWriter writer)
    {
        writer.WriteNative<ushort>(0); // Reserve a space for the length
        writer.WriteNative(Type.Id);
        writer.WriteNative(SourceClientId);
    }

    protected virtual void Deserialize(ref SerializationReader reader)
    {
        // Type and length are read in TryParse
        reader.ReadNative(out SourceClientId);
    }

    public void SerializeEvent(ref SerializationWriter writer)
    {
        int startOffset = writer.Offset;
        Serialize(ref writer);
        int size = writer.Offset - startOffset;
        writer.WriteNative((ushort)size, startOffset);
    }

    public static bool TryParse(byte[] data, int offset, out NetworkEvent? networkEvent)
    {
        SerializationReader reader = new SerializationReader(data, offset);
        return TryParse(ref reader, out networkEvent);
    }

    public static bool TryParse(ref SerializationReader reader, out NetworkEvent? networkEvent)
    {
        int startOffset = reader.Offset;
        reader.ReadNative(out ushort eventLength);
        reader.ReadNative(out ushort eventId);

        if (!EventType.TypeLookup.TryGetValue(eventId, out var type))
        {
            networkEvent = null;
            return false;
        }

        networkEvent = type.MakeInstance();
        networkEvent.Deserialize(ref reader);

        int actualLength = reader.Offset - startOffset;
        if (actualLength != eventLength)
            throw new Exception($"Event deserializer did not read the correct number of bytes! " +
                                $"Expected {eventLength}, Actual {actualLength}");

        return true;
    }
}