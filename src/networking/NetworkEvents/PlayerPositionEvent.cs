using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace H3MP.src.networking.clientEventReg
{
    /// <summary>
    /// Player Position event type, for adding to queue.
    /// </summary>
    class PlayerPositionEvent : NetworkEvent
    {
        public Vector3 playerPos;
        public Quaternion playerRot;
        public Vector3 headPos;
        public Quaternion headRot;
        public Vector3 torsoPos;
        public Quaternion torsoRot;
        public Vector3 leftHandPos;
        public Quaternion leftHandRot;
        public Vector3 rightHandPos;
        public Quaternion rightHandRot;
        public float health;
        public int maxHealth;
        public short additionalData;
        private List<byte> eventBuffer;

        public override void Serialize()
        {
            // Code here to actually write stuff into a byte array for transmission
            
        }

        public override void Deserialize()
        {
            // Code here to actually read stuff from a byte array from transmission 
        }
    }
}
