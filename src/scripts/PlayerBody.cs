﻿using FistVR;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace H3MP.Scripts
{
    public class PlayerBody : MonoBehaviour
    {
        public string playerPrefabID;

        [Header("Head settings")]
        public Vector3 headOffset;
        public Transform headTransform;
        [NonSerialized]
        public Transform headToFollow;
        public Renderer[] headRenderers;

        [Header("Hand settings")]
        public Transform[] handTransforms;
        [NonSerialized]
        public Transform[] handsToFollow; // Left, Right

        [Header("Other")]
        public Collider[] colliders;
        public Renderer[] controlRenderers;
        public AIEntity[] entities;
        public Canvas[] canvases;

        [Header("Optionals")]
        public Renderer[] bodyRenderers;
        public Renderer[] handRenderers;
        public Renderer[] coloredParts;
        public Text usernameLabel;
        public Text healthLabel;

        public virtual void Awake()
        {
            GameManager.OnPlayerBodyInit += OnPlayerBodyInit;

            if(Mod.managerObject == null && GM.CurrentPlayerBody != null)
            {
                headToFollow = GM.CurrentPlayerBody.Head.transform;
                handsToFollow = new Transform[2];
                handsToFollow[0] = GM.CurrentPlayerBody.LeftHand;
                handsToFollow[1] = GM.CurrentPlayerBody.RightHand;
                SetHeadVisible(false);
                SetCollidersEnabled(false);
                SetCanvasesEnabled(false);
                SetEntitiesRegistered(false);

                Init();
            }
            //else Connected, let TrackedPlayerBody handle what transform to follow based on controller
            //     OR not connected but no current player body. Setting these will be handled when a player body is initialized
        }

        public virtual void OnPlayerBodyInit(FVRPlayerBody playerBody)
        {
            if (Mod.managerObject == null)
            {
                headToFollow = playerBody.Head.transform;
                handsToFollow = new Transform[2];
                handsToFollow[0] = playerBody.LeftHand;
                handsToFollow[1] = playerBody.RightHand;
                SetHeadVisible(false);
                SetCollidersEnabled(false);
                SetCanvasesEnabled(false);
                SetEntitiesRegistered(false);

                Init();
            }
            //else Connected, let TrackedPlayerBody handle what transform to follow based on controller
        }

        public virtual void Init()
        {
            SetColor(GameManager.colors[GameManager.colorIndex]);
        }

        public virtual void Update()
        {
            // These could only be null briefly if connected until TrackedPlayerBody sets them appropriately
            if (headToFollow != null)
            {
                headTransform.position = headToFollow.position;
                headTransform.localPosition += headOffset;
                headTransform.rotation = headToFollow.rotation;
            }
            if(handsToFollow != null)
            {
                for(int i=0; i < handsToFollow.Length; ++i)
                {
                    if (handsToFollow[i] != null)
                    {
                        handTransforms[i].position = handsToFollow[i].position;
                        handTransforms[i].rotation = handsToFollow[i].rotation;
                    }
                }
            }
        }

        public virtual void SetHeadVisible(bool visible)
        {
            if (headRenderers != null)
            {
                for (int i = 0; i < headRenderers.Length; ++i)
                {
                    if (headRenderers[i] != null)
                    {
                        headRenderers[i].enabled = visible;
                    }
                }
            }
        }

        public virtual void SetColor(Color newColor)
        {
            if (coloredParts != null)
            {
                for (int i = 0; i < coloredParts.Length; ++i)
                {
                    if (coloredParts[i] != null && coloredParts[i].materials != null)
                    {
                        for (int j = 0; j < coloredParts[i].materials.Length; ++j)
                        {
                            if (coloredParts[i].materials[j] != null)
                            {
                                coloredParts[i].materials[j].color = newColor;
                            }
                        }
                    }
                }
            }
        }

        public virtual void SetBodyVisible(bool visible)
        {
            if (bodyRenderers != null)
            {
                for (int i = 0; i < bodyRenderers.Length; ++i)
                {
                    if (bodyRenderers[i] != null)
                    {
                        bodyRenderers[i].enabled = visible;
                    }
                }
            }
        }

        public virtual void SetHandsVisible(bool visible)
        {
            if (handRenderers != null) 
            {
                for (int i = 0; i < handRenderers.Length; ++i)
                {
                    if (handRenderers[i] != null)
                    {
                        handRenderers[i].enabled = visible;
                    }
                }
            }
        }

        public virtual void SetCollidersEnabled(bool enabled)
        {
            if(colliders != null)
            {
                for(int i=0; i < colliders.Length; ++i)
                {
                    if (colliders[i] != null)
                    {
                        colliders[i].enabled = enabled;
                    }
                }
            }
        }

        public virtual void SetCanvasesEnabled(bool enabled)
        {
            if(canvases != null)
            {
                for(int i=0; i < canvases.Length; ++i)
                {
                    if (canvases[i] != null)
                    {
                        canvases[i].enabled = enabled;
                    }
                }
            }
        }

        public void SetEntitiesRegistered(bool registered)
        {
            if (GM.CurrentAIManager != null && entities != null)
            {
                if (registered)
                {
                    for(int i=0; i < entities.Length; ++i)
                    {
                        if (entities[i] != null)
                        {
                            GM.CurrentAIManager.RegisterAIEntity(entities[i]);
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < entities.Length; ++i)
                    {
                        if (entities[i] != null)
                        {
                            GM.CurrentAIManager.DeRegisterAIEntity(entities[i]);
                        }
                    }
                }
            }
        }

        public void SetIFF(int IFF)
        {
            if (entities != null)
            {
                for(int i=0; i < entities.Length; ++i)
                {
                    if (entities[i] != null)
                    {
                        entities[i].IFFCode = IFF;
                    }
                }
            }
        }

        public virtual void OnDestroy()
        {
            GameManager.OnPlayerBodyInit -= OnPlayerBodyInit;
        }
    }
}