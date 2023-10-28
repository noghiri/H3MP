using H3MP.Networking.Serialization;
using UnityEngine;

namespace H3MP.Networking.Events;

public class PlayerPositionEvent : NetworkEvent
{
    public Vector3 PlayerPosition;
    public Quaternion PlayerRotation;

    public Vector3 HeadPosition;
    public Quaternion HeadRotation;

    public Vector3 TorsoPosition;
    public Quaternion TorsoRotation;

    public Vector3 LeftHandPosition;
    public Quaternion LeftHandRotation;

    public Vector3 RightHandPosition;
    public Quaternion RightHandRotation;

    public float Health;
    public int MaxHealth;

    public byte[] AdditionalData = new byte[0];
    
    public PlayerPositionEvent() : base(EventType.PlayerPositionEvent)
    {
        
    }

    protected override void Serialize(ref SerializationWriter writer)
    {
        base.Serialize(ref writer);
    }

    protected override void Deserialize(ref SerializationReader reader)
    {
        base.Deserialize(ref reader);
    }
}