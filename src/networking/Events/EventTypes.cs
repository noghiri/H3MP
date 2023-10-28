namespace H3MP.Networking.Events;

public abstract partial class EventType
{
    public static readonly EventType PlayerPositionEvent =
        new EventType<PlayerPositionEvent>(EventTypeSender.Client, EventTypeMode.UdpBatched);
}