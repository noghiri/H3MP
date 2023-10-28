using System;
using System.Collections.Generic;

namespace H3MP.Networking.Events;

public abstract partial class EventType
{
    internal static readonly Dictionary<ushort, EventType> TypeLookup = new();

    private readonly Type _eventType;

    public readonly ushort Id;

    public readonly EventTypeSender Sender;

    public readonly EventTypeMode Mode;

    internal EventType(ushort id, Type eventType, EventTypeSender sender, EventTypeMode mode)
    {
        _eventType = eventType;
        Sender = sender;
        Mode = mode;
        Id = id;

        // Double check there's no collisions, though I don't expect this to happen.
        if (TypeLookup.TryGetValue(Id, out var existingEventType))
        {
            throw new Exception($"Duplicate hashed ID for NetworkEvent type: " +
                                $"'{existingEventType._eventType.FullName}' = {Id}, '{_eventType.FullName}' = {Id}");
        }

        // Add it to the lookup and we're good
        TypeLookup[Id] = this;
    }

    public EventType(Type eventType, EventTypeSender sender, EventTypeMode mode)
        : this(GetStableHashCode(eventType.FullName!), eventType, sender, mode)
    {
    }

    private static ushort GetStableHashCode(string str)
    {
        unchecked
        {
            int hash1 = 5381;
            int hash2 = hash1;

            for (int i = 0; i < str.Length && str[i] != '\0'; i += 2)
            {
                hash1 = ((hash1 << 5) + hash1) ^ str[i];
                if (i == str.Length - 1 || str[i + 1] == '\0')
                    break;
                hash2 = ((hash2 << 5) + hash2) ^ str[i + 1];
            }

            return (ushort)(hash1 + (hash2 * 1566083941));
        }
    }

    internal abstract NetworkEvent MakeInstance();
}

[Flags]
public enum EventTypeSender
{
    // Only the server can send this event
    Server = 0b01,

    // Only the client can send this event
    Client = 0b10,

    // Both the server and client can send this event
    Any = 0b11,
}

public enum EventTypeMode
{
    // Sent immediately with TCP
    Tcp,

    // Sent immediately with UDP
    Udp,

    // Sent with UDP, but events are batched and aggregated before sending
    UdpBatched,
}

public class EventType<T> : EventType where T : NetworkEvent, new()
{
    internal EventType(ushort id, EventTypeSender sender, EventTypeMode mode) : base(id, typeof(T), sender, mode)
    {
    }

    public EventType(EventTypeSender sender, EventTypeMode mode) : base(typeof(T), sender, mode)
    {
    }

    internal override NetworkEvent MakeInstance()
    {
        return new T();
    }
}