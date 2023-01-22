﻿using FistVR;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace H3MP
{
    public class H3MP_TrackedItem : MonoBehaviour
    {
        public static float interpolationSpeed = 12f;

        public H3MP_TrackedItemData data;
        public bool awoken;
        public bool sendOnAwake;

        // Unknown tracked ID queues
        public static Dictionary<int, KeyValuePair<int, bool>> unknownTrackedIDs = new Dictionary<int, KeyValuePair<int, bool>>();
        public static Dictionary<int, List<int>> unknownParentTrackedIDs = new Dictionary<int, List<int>>();
        public static Dictionary<int, int> unknownControlTrackedIDs = new Dictionary<int, int>();
        public static List<int> unknownDestroyTrackedIDs = new List<int>();

        // Update
        public delegate bool UpdateData(); // The updateFunc and updateGivenFunc should return a bool indicating whether data has been modified
        public delegate bool UpdateDataWithGiven(byte[] newData);
        public delegate bool FireFirearm();
        public delegate void FirearmUpdateOverrideSetter(FireArmRoundClass roundClass);
        public delegate bool FireSosigGun(float recoilMult);
        public delegate void FireAttachableFirearm(bool firedFromInterface);
        public delegate void FireAttachableFirearmChamberRound(FireArmRoundClass roundClass);
        public delegate FVRFireArmChamber FireAttachableFirearmGetChamber();
        public delegate void UpdateParent();
        public UpdateData updateFunc; // Update the item's data based on its physical state since we are the controller
        public UpdateDataWithGiven updateGivenFunc; // Update the item's data and state based on data provided by another client
        public FireFirearm fireFunc; // Fires the corresponding firearm type
        public FirearmUpdateOverrideSetter setFirearmUpdateOverride; // Set fire update override data
        public FireAttachableFirearm attachableFirearmFunc; // Fires the corresponding attachable firearm type
        public FireAttachableFirearmChamberRound attachableFirearmChamberRoundFunc; // Loads the chamber of the attachable firearm with round of class
        public FireAttachableFirearmGetChamber attachableFirearmGetChamberFunc; // Returns the chamber of the corresponding attachable firearm
        public FireSosigGun sosigWeaponfireFunc; // Fires the corresponding sosig weapon
        public UpdateParent updateParentFunc; // Update the item's state depending on current parent
        public byte currentMountIndex = 255; // Used by attachment, TODO: This limits number of mounts to 255, if necessary could make index into a short
        public UnityEngine.Object dataObject;
        public FVRPhysicalObject physicalObject;

        public bool sendDestroy = true; // To prevent feeback loops
        public static int skipDestroy;

        private void Awake()
        {
            InitItemType();

            awoken = true;
            if (sendOnAwake)
            {
                Debug.Log(gameObject.name + " awoken");
                if (H3MP_ThreadManager.host)
                {
                    // This will also send a packet with the item to be added in the client's global item list
                    H3MP_Server.AddTrackedItem(data, 0);
                }
                else
                {
                    // Tell the server we need to add this item to global tracked items
                    H3MP_ClientSend.TrackedItem(data);
                }
            }
        }

        // MOD: This will check which type this item is so we can keep track of its data more efficiently
        //      A mod with a custom item type which has custom data should postfix this to check if this item is of custom type
        //      to keep a ref to the object itself and set delegate update functions
        private void InitItemType()
        {
            FVRPhysicalObject physObj = GetComponent<FVRPhysicalObject>();

            // For each relevant type for which we may want to store additional data, we set a specific update function and the object ref
            // NOTE: We want to handle a subtype before its parent type (ex.: AttachableFirearmPhysicalObject before FVRFireArmAttachment) 
            // TODO: Maybe instead of having a big if statement like this, put all of them in a dictionnary for faster lookup
            if (physObj is FVRFireArmMagazine)
            {
                updateFunc = UpdateMagazine;
                updateGivenFunc = UpdateGivenMagazine;
                dataObject = physObj as FVRFireArmMagazine;
            }
            else if(physObj is FVRFireArmClip)
            {
                updateFunc = UpdateClip;
                updateGivenFunc = UpdateGivenClip;
                dataObject = physObj as FVRFireArmClip;
            }
            else if(physObj is Speedloader)
            {
                updateFunc = UpdateSpeedloader;
                updateGivenFunc = UpdateGivenSpeedloader;
                dataObject = physObj as Speedloader;
            }
            else if (physObj is ClosedBoltWeapon)
            {
                ClosedBoltWeapon asCBW = (ClosedBoltWeapon)physObj;
                updateFunc = UpdateClosedBoltWeapon;
                updateGivenFunc = UpdateGivenClosedBoltWeapon;
                dataObject = asCBW;
                fireFunc = asCBW.Fire;
                setFirearmUpdateOverride = SetCBWUpdateOverride;
            }
            else if (physObj is BoltActionRifle)
            {
                BoltActionRifle asBAR = (BoltActionRifle)physObj;
                updateFunc = UpdateBoltActionRifle;
                updateGivenFunc = UpdateGivenBoltActionRifle;
                dataObject = asBAR;
                fireFunc = asBAR.Fire;
                setFirearmUpdateOverride = SetBARUpdateOverride;
            }
            else if (physObj is Handgun)
            {
                Handgun asHandgun = (Handgun)physObj;
                updateFunc = UpdateHandgun;
                updateGivenFunc = UpdateGivenHandgun;
                dataObject = asHandgun;
                fireFunc = asHandgun.Fire;
                setFirearmUpdateOverride = SetHandgunUpdateOverride;
            }
            else if (physObj is TubeFedShotgun)
            {
                TubeFedShotgun asTFS = (TubeFedShotgun)physObj;
                updateFunc = UpdateTubeFedShotgun;
                updateGivenFunc = UpdateGivenTubeFedShotgun;
                dataObject = asTFS;
                fireFunc = asTFS.Fire;
                setFirearmUpdateOverride = SetTFSUpdateOverride;
            }
            else if (physObj is Revolver)
            {
                Revolver asRevolver = (Revolver)physObj;
                updateFunc = UpdateRevolver;
                updateGivenFunc = UpdateGivenRevolver;
                dataObject = asRevolver;
                fireFunc = FireRevolver;
                setFirearmUpdateOverride = SetRevolverUpdateOverride;
            }
            else if (physObj is RevolvingShotgun)
            {
                RevolvingShotgun asRS = (RevolvingShotgun)physObj;
                updateFunc = UpdateRevolvingShotgun;
                updateGivenFunc = UpdateGivenRevolvingShotgun;
                dataObject = asRS;
            }
            else if (physObj is BAP)
            {
                BAP asBAP = (BAP)physObj;
                updateFunc = UpdateBAP;
                updateGivenFunc = UpdateGivenBAP;
                dataObject = asBAP;
                fireFunc = asBAP.Fire;
                setFirearmUpdateOverride = SetBAPUpdateOverride;
            }
            else if (physObj is BreakActionWeapon)
            {
                updateFunc = UpdateBreakActionWeapon;
                updateGivenFunc = UpdateGivenBreakActionWeapon;
                dataObject = physObj as BreakActionWeapon;
            }
            else if (physObj is LeverActionFirearm)
            {
                LeverActionFirearm LAF = (LeverActionFirearm)physObj;
                updateFunc = UpdateLeverActionFirearm;
                updateGivenFunc = UpdateGivenLeverActionFirearm;
                dataObject = LAF;
            }
            else if (physObj is Derringer)
            {
                updateFunc = UpdateDerringer;
                updateGivenFunc = UpdateGivenDerringer;
                dataObject = physObj as Derringer;
            }
            else if (physObj is FlameThrower)
            {
                updateFunc = UpdateFlameThrower;
                updateGivenFunc = UpdateGivenFlameThrower;
                dataObject = physObj as FlameThrower;
            }
            else if (physObj is Flaregun)
            {
                updateFunc = UpdateFlaregun;
                updateGivenFunc = UpdateGivenFlaregun;
                dataObject = physObj as Flaregun;
            }
            else if (physObj is FlintlockWeapon)
            {
                updateFunc = UpdateFlintlockWeapon;
                updateGivenFunc = UpdateGivenFlintlockWeapon;
                dataObject = physObj.GetComponentInChildren<FlintlockBarrel>();
            }
            else if (physObj is LAPD2019)
            {
                updateFunc = UpdateLAPD2019;
                updateGivenFunc = UpdateGivenLAPD2019;
                dataObject = physObj as LAPD2019;
            }
            else if (physObj is LAPD2019Battery)
            {
                updateFunc = UpdateLAPD2019Battery;
                updateGivenFunc = UpdateGivenLAPD2019Battery;
                dataObject = physObj as LAPD2019Battery;
            }
            else if (physObj is AttachableFirearmPhysicalObject)
            {
                AttachableFirearmPhysicalObject asAttachableFirearmPhysicalObject = (AttachableFirearmPhysicalObject)physObj;
                if(asAttachableFirearmPhysicalObject.FA is AttachableBreakActions)
                {
                    updateFunc = UpdateAttachableBreakActions;
                    updateGivenFunc = UpdateGivenAttachableBreakActions;
                    attachableFirearmFunc = (asAttachableFirearmPhysicalObject.FA as AttachableBreakActions).Fire;
                    attachableFirearmChamberRoundFunc = AttachableBreakActionsChamberRound;
                    attachableFirearmGetChamberFunc = AttachableBreakActionsGetChamber;
                }
                else if(asAttachableFirearmPhysicalObject.FA is AttachableClosedBoltWeapon)
                {
                    updateFunc = UpdateAttachableClosedBoltWeapon;
                    updateGivenFunc = UpdateGivenAttachableClosedBoltWeapon;
                    attachableFirearmFunc = (asAttachableFirearmPhysicalObject.FA as AttachableClosedBoltWeapon).Fire;
                    attachableFirearmChamberRoundFunc = AttachableClosedBoltWeaponChamberRound;
                    attachableFirearmGetChamberFunc = AttachableClosedBoltWeaponGetChamber;
                }
                else if(asAttachableFirearmPhysicalObject.FA is AttachableTubeFed)
                {
                    updateFunc = UpdateAttachableTubeFed;
                    updateGivenFunc = UpdateGivenAttachableTubeFed;
                    attachableFirearmFunc = (asAttachableFirearmPhysicalObject.FA as AttachableTubeFed).Fire;
                    attachableFirearmChamberRoundFunc = AttachableTubeFedChamberRound;
                    attachableFirearmGetChamberFunc = AttachableTubeFedGetChamber;
                }
                else if(asAttachableFirearmPhysicalObject.FA is GP25)
                {
                    updateFunc = UpdateGP25;
                    updateGivenFunc = UpdateGivenGP25;
                    attachableFirearmFunc = (asAttachableFirearmPhysicalObject.FA as GP25).Fire;
                    attachableFirearmChamberRoundFunc = GP25ChamberRound;
                    attachableFirearmGetChamberFunc = GP25GetChamber;
                }
                else if(asAttachableFirearmPhysicalObject.FA is M203)
                {
                    updateFunc = UpdateM203;
                    updateGivenFunc = UpdateGivenM203;
                    attachableFirearmFunc = (asAttachableFirearmPhysicalObject.FA as M203).Fire;
                    attachableFirearmChamberRoundFunc = M203ChamberRound;
                    attachableFirearmGetChamberFunc = M203GetChamber;
                }
                updateParentFunc = UpdateAttachableFirearmParent;
                dataObject = asAttachableFirearmPhysicalObject.FA;
            }
            else if (physObj is FVRFireArmAttachment)
            {
                FVRFireArmAttachment asAttachment = (FVRFireArmAttachment)physObj;
                updateFunc = UpdateAttachment;
                updateGivenFunc = UpdateGivenAttachment;
                updateParentFunc = UpdateAttachmentParent;
                dataObject = asAttachment;
            }
            else if (physObj is SosigWeaponPlayerInterface)
            {
                SosigWeaponPlayerInterface asInterface = (SosigWeaponPlayerInterface)physObj;
                updateFunc = UpdateSosigWeaponInterface;
                updateGivenFunc = UpdateGivenSosigWeaponInterface;
                dataObject = asInterface;
                sosigWeaponfireFunc = asInterface.W.FireGun;
            }
            /* TODO: All other type of firearms below
            else if (physObj is GBeamer)
            {
                updateFunc = UpdateGBeamer;
                updateGivenFunc = UpdateGivenGBeamer;
                dataObject = physObj as GBeamer;
            }
            else if (physObj is GrappleGun)
            {
                updateFunc = UpdateGrappleGun;
                updateGivenFunc = UpdateGivenGrappleGun;
                dataObject = physObj as GrappleGun;
            }
            else if (physObj is HCB)
            {
                updateFunc = UpdateHCB;
                updateGivenFunc = UpdateGivenHCB;
                dataObject = physObj as HCB;
            }
            else if (physObj is OpenBoltReceiver)
            {
                updateFunc = UpdateOpenBoltReceiver;
                updateGivenFunc = UpdateGivenOpenBoltReceiver;
                dataObject = physObj as OpenBoltReceiver;
            }
            else if (physObj is M72)
            {
                updateFunc = UpdateM72;
                updateGivenFunc = UpdateGivenM72;
                dataObject = physObj as M72;
            }
            else if (physObj is Minigun)
            {
                updateFunc = UpdateMinigun;
                updateGivenFunc = UpdateGivenMinigun;
                dataObject = physObj as Minigun;
            }
            else if (physObj is PotatoGun)
            {
                updateFunc = UpdatePotatoGun;
                updateGivenFunc = UpdateGivenPotatoGun;
                dataObject = physObj as PotatoGun;
            }
            else if (physObj is RemoteMissileLauncher)
            {
                updateFunc = UpdateRemoteMissileLauncher;
                updateGivenFunc = UpdateGivenRemoteMissileLauncher;
                dataObject = physObj as RemoteMissileLauncher;
            }
            else if (physObj is RGM40)
            {
                updateFunc = UpdateRGM40;
                updateGivenFunc = UpdateGivenRGM40;
                dataObject = physObj as RGM40;
            }
            else if (physObj is RollingBlock)
            {
                updateFunc = UpdateRollingBlock;
                updateGivenFunc = UpdateGivenRollingBlock;
                dataObject = physObj as RollingBlock;
            }
            else if (physObj is RPG7)
            {
                updateFunc = UpdateRPG7;
                updateGivenFunc = UpdateGivenRPG7;
                dataObject = physObj as RPG7;
            }
            else if (physObj is SimpleLauncher)
            {
                updateFunc = UpdateSimpleLauncher;
                updateGivenFunc = UpdateGivenSimpleLauncher;
                dataObject = physObj as SimpleLauncher;
            }
            else if (physObj is SimpleLauncher2)
            {
                updateFunc = UpdateSimpleLauncher2;
                updateGivenFunc = UpdateGivenSimpleLauncher2;
                dataObject = physObj as SimpleLauncher2;
            }
            else if (physObj is SingleActionRevolver)
            {
                updateFunc = UpdateSingleActionRevolver;
                updateGivenFunc = UpdateGivenSingleActionRevolver;
                dataObject = physObj as SingleActionRevolver;
            }
            else if (physObj is StingerLauncher)
            {
                updateFunc = UpdateStingerLauncher;
                updateGivenFunc = UpdateGivenStingerLauncher;
                dataObject = physObj as StingerLauncher;
            }
            else if (physObj is MF2_RL)
            {
                updateFunc = UpdateMF2_RL;
                updateGivenFunc = UpdateGivenMF2_RL;
                dataObject = physObj as MF2_RL;
            }
            */
        }

        public bool UpdateItemData(byte[] newData = null)
        {
            if(dataObject != null)
            {
                if(newData != null)
                {
                    return updateGivenFunc(newData);
                }
                else
                {
                    return updateFunc();
                }
            }

            return false;
        }

        #region Type Updates
        private bool UpdateFlintlockWeapon()
        {
            FlintlockWeapon asFLW = physicalObject as FlintlockWeapon;
            
            bool modified = false;

            if (data.data == null)
            {
                data.data = new byte[6];
                modified = true;
            }

            byte preval = data.data[0];

            // Write hammer state
            data.data[0] = (byte)asFLW.HammerState;

            modified |= preval != data.data[0];

            preval = data.data[1];

            // Write has flint
            data.data[1] = asFLW.HasFlint() ? (byte)1 : (byte)0;

            modified |= preval != data.data[1];

            preval = data.data[2];

            // Write flint state
            data.data[2] = (byte)asFLW.FState;

            modified |= preval != data.data[2];

            byte preval0 = data.data[3];
            byte preval1 = data.data[4];
            byte preval2 = data.data[5];

            // Write flint uses
            Vector3 uses = (Vector3)Mod.FlintlockWeapon_m_flintUses.GetValue(asFLW);
            data.data[3] = (byte)(int)uses.x;
            data.data[4] = (byte)(int)uses.y;
            data.data[5] = (byte)(int)uses.z;

            modified |= (preval0 != data.data[3] || preval1 != data.data[4] || preval2 != data.data[5]);

            return modified;
        }

        private bool UpdateGivenFlintlockWeapon(byte[] newData)
        {
            bool modified = false;
            FlintlockWeapon asFLW = physicalObject as FlintlockWeapon;

            // Set hammer state
            FlintlockWeapon.HState preVal = asFLW.HammerState;

            asFLW.HammerState = (FlintlockWeapon.HState)newData[0];

            modified |= preVal != asFLW.HammerState;

            // Set hasFlint
            bool preVal0 = asFLW.HasFlint();

            Mod.FlintlockWeapon_m_hasFlint.SetValue(asFLW, newData[1] == 1);

            modified |= preVal0 ^ asFLW.HasFlint();

            // Set flint state
            FlintlockWeapon.FlintState preVal1 = asFLW.FState;

            asFLW.FState = (FlintlockWeapon.FlintState)newData[2];

            modified |= preVal1 != asFLW.FState;

            Vector3 preUses = (Vector3)Mod.FlintlockWeapon_m_flintUses.GetValue(asFLW);

            // Write flint uses
            Vector3 uses = Vector3.zero;
            uses.x = newData[3];
            uses.y = newData[4];
            uses.z = newData[5];
            Mod.FlintlockWeapon_m_flintUses.SetValue(asFLW, uses);

            modified |= !preUses.Equals(uses);

            data.data = newData;

            return modified;
        }

        private bool UpdateFlaregun()
        {
            Flaregun asFG = dataObject as Flaregun;
            bool modified = false;

            if (data.data == null)
            {
                data.data = new byte[3];
                modified = true;
            }

            byte preval = data.data[0];

            // Write hammer state
            data.data[0] = (bool)Mod.Flaregun_m_isHammerCocked.GetValue(asFG) ? (byte)1: (byte)0;

            modified |= preval != data.data[0];

            preval = data.data[1];
            byte preval0 = data.data[2];

            // Write chambered round class
            if (asFG.Chamber.GetRound() == null)
            {
                BitConverter.GetBytes((short)-1).CopyTo(data.data, 1);
            }
            else
            {
                BitConverter.GetBytes((short)asFG.Chamber.GetRound().RoundClass).CopyTo(data.data, 1);
            }

            modified |= (preval != data.data[1] || preval0 != data.data[2]);

            return modified;
        }

        private bool UpdateGivenFlaregun(byte[] newData)
        {
            bool modified = false;
            Flaregun asFG = dataObject as Flaregun;

            // Set hammer state
            bool preVal = (bool)Mod.Flaregun_m_isHammerCocked.GetValue(asFG);

            asFG.SetHammerCocked(newData[0] == 1);

            modified |= preVal ^ (newData[0] == 1);

            // Set chamber
            short chamberClassIndex = BitConverter.ToInt16(newData, 1);
            if (chamberClassIndex == -1) // We don't want round in chamber
            {
                if (asFG.Chamber.GetRound() != null)
                {
                    asFG.Chamber.SetRound(null, false);
                    modified = true;
                }
            }
            else // We want a round in the chamber
            {
                FireArmRoundClass roundClass = (FireArmRoundClass)chamberClassIndex;
                if (asFG.Chamber.GetRound() == null || asFG.Chamber.GetRound().RoundClass != roundClass)
                {
                    asFG.Chamber.SetRound(roundClass, asFG.Chamber.transform.position, asFG.Chamber.transform.rotation);
                    modified = true;
                }
            }

            data.data = newData;

            return modified;
        }

        private bool UpdateFlameThrower()
        {
            FlameThrower asFT = dataObject as FlameThrower;
            bool modified = false;

            if (data.data == null)
            {
                data.data = new byte[1];
                modified = true;
            }

            // Write firing
            byte preval = data.data[0];

            data.data[0] = (bool)Mod.FlameThrower_m_isFiring.GetValue(asFT) ? (byte)1 : (byte)0;

            modified |= preval != data.data[0];

            return modified;
        }

        private bool UpdateGivenFlameThrower(byte[] newData)
        {
            bool modified = false;
            FlameThrower asFT = dataObject as FlameThrower;

            // Set firing
            bool currentFiring = (bool)Mod.FlameThrower_m_isFiring.GetValue(asFT);
            if (currentFiring && newData[0] == 0)
            {
                // Stop firing
                Mod.FlameThrower_StopFiring.Invoke(asFT, null);
                modified = true;
            }
            else if(!currentFiring && newData[0] == 1)
            {
                // Start firing
                Mod.FlameThrower_m_hasFiredStartSound.SetValue(asFT, true);
                SM.PlayCoreSound(FVRPooledAudioType.GenericClose, asFT.AudEvent_Ignite, asFT.GetMuzzle().position);
                asFT.AudSource_FireLoop.volume = 0.4f;
                float vlerp;
                if (asFT.UsesValve)
                {
                    vlerp = asFT.Valve.ValvePos;
                }
                else if (asFT.UsesMF2Valve)
                {
                    vlerp = asFT.MF2Valve.Lerp;
                }
                else
                {
                    vlerp = 0.5f;
                }
                asFT.AudSource_FireLoop.pitch = Mathf.Lerp(asFT.AudioPitchRange.x, asFT.AudioPitchRange.y, vlerp);
                if (!asFT.AudSource_FireLoop.isPlaying)
                {
                    asFT.AudSource_FireLoop.Play();
                }
                modified = true;
            }

            data.data = newData;

            return modified;
        }

        private bool UpdateLeverActionFirearm()
        {
            LeverActionFirearm asLAF = dataObject as LeverActionFirearm;
            bool modified = false;

            if (data.data == null)
            {
                data.data = new byte[6];
                modified = true;
            }

            // Write chamber round
            byte preval0 = data.data[0];
            byte preval1 = data.data[1];

            if (asLAF.Chamber.GetRound() == null)
            {
                BitConverter.GetBytes((short)-1).CopyTo(data.data, 0);
            }
            else
            {
                BitConverter.GetBytes((short)asLAF.Chamber.GetRound().RoundClass).CopyTo(data.data, 0);
            }

            modified |= (preval0 != data.data[0] || preval1 != data.data[1]);

            // Write hammer state
            preval0 = data.data[2];

            data.data[2] = asLAF.IsHammerCocked ? (byte)1 : (byte)0;

            modified |= preval0 != data.data[2];

            if (asLAF.UsesSecondChamber)
            {
                // Write chamber2 round
                preval0 = data.data[3];
                preval1 = data.data[4];

                if (asLAF.Chamber2.GetRound() == null)
                {
                    BitConverter.GetBytes((short)-1).CopyTo(data.data, 3);
                }
                else
                {
                    BitConverter.GetBytes((short)asLAF.Chamber2.GetRound().RoundClass).CopyTo(data.data, 3);
                }

                modified |= (preval0 != data.data[3] || preval1 != data.data[4]);

                // Write hammer2 state
                preval0 = data.data[5];

                data.data[5] = ((bool)Mod.LeverActionFirearm_m_isHammerCocked2.GetValue(asLAF)) ? (byte)1 : (byte)0;

                modified |= preval0 != data.data[5];
            }

            return modified;
        }

        private bool UpdateGivenLeverActionFirearm(byte[] newData)
        {
            bool modified = false;
            LeverActionFirearm asLAF = dataObject as LeverActionFirearm;

            // Set chamber round
            short chamberClassIndex = BitConverter.ToInt16(newData, 0);
            if (chamberClassIndex == -1) // We don't want round in chamber
            {
                if (asLAF.Chamber.GetRound() != null)
                {
                    asLAF.Chamber.SetRound(null, false);
                    modified = true;
                }
            }
            else // We want a round in the chamber
            {
                FireArmRoundClass roundClass = (FireArmRoundClass)chamberClassIndex;
                if (asLAF.Chamber.GetRound() == null || asLAF.Chamber.GetRound().RoundClass != roundClass)
                {
                    asLAF.Chamber.SetRound(roundClass, asLAF.Chamber.transform.position, asLAF.Chamber.transform.rotation);
                    modified = true;
                }
            }

            // Set hammer state
            Mod.LeverActionFirearm_m_isHammerCocked.SetValue(asLAF, newData[0] == 1);

            // Set chamber2 round
            chamberClassIndex = BitConverter.ToInt16(newData, 3);
            if (chamberClassIndex == -1) // We don't want round in chamber
            {
                if (asLAF.Chamber2.GetRound() != null)
                {
                    asLAF.Chamber2.SetRound(null, false);
                    modified = true;
                }
            }
            else // We want a round in the chamber
            {
                FireArmRoundClass roundClass = (FireArmRoundClass)chamberClassIndex;
                if (asLAF.Chamber2.GetRound() == null || asLAF.Chamber2.GetRound().RoundClass != roundClass)
                {
                    asLAF.Chamber2.SetRound(roundClass, asLAF.Chamber2.transform.position, asLAF.Chamber2.transform.rotation);
                    modified = true;
                }
            }

            // Set hammer2 state
            Mod.LeverActionFirearm_m_isHammerCocked2.SetValue(asLAF, newData[5] == 1);

            data.data = newData;

            return modified;
        }

        private bool UpdateDerringer()
        {
            Derringer asDerringer = dataObject as Derringer;
            bool modified = false;

            int necessarySize = asDerringer.Barrels.Count * 2 + 1;

            if (data.data == null)
            {
                data.data = new byte[necessarySize];
                modified = true;
            }

            // Write hammer state
            byte preval0 = data.data[0];

            data.data[0] = asDerringer.IsExternalHammerCocked() ? (byte)1 : (byte)0;

            modified |= preval0 != data.data[0];

            // Write chambered rounds
            byte preval1;
            for (int i = 0; i < asDerringer.Barrels.Count; ++i)
            {
                // Write chambered round
                int firstIndex = i * 2 + 1;
                preval0 = data.data[firstIndex];
                preval1 = data.data[firstIndex + 1];

                if (asDerringer.Barrels[i].Chamber.GetRound() == null)
                {
                    BitConverter.GetBytes((short)-1).CopyTo(data.data, firstIndex);
                }
                else
                {
                    BitConverter.GetBytes((short)asDerringer.Barrels[i].Chamber.GetRound().RoundClass).CopyTo(data.data, firstIndex);
                }

                modified |= (preval0 != data.data[firstIndex] || preval1 != data.data[firstIndex + 1]);
            }

            return modified;
        }

        private bool UpdateGivenDerringer(byte[] newData)
        {
            bool modified = false;
            Derringer asDerringer = dataObject as Derringer;

            if (data.data == null)
            {
                modified = true;

                // Set hammer state
                if (newData[0] == 1 && !asDerringer.IsExternalHammerCocked())
                {
                    Mod.Derringer_CockHammer.Invoke(asDerringer, null);
                }
                else if(newData[0] == 0 && asDerringer.IsExternalHammerCocked())
                {
                    Mod.Derringer_m_isExternalHammerCocked.SetValue(asDerringer, false);
                }
            }
            else
            {
                if (data.data[0] != newData[0])
                {
                    // Set hammer state
                    if (newData[0] == 1 && !asDerringer.IsExternalHammerCocked())
                    {
                        Mod.Derringer_CockHammer.Invoke(asDerringer, null);
                    }
                    else if (newData[0] == 0 && asDerringer.IsExternalHammerCocked())
                    {
                        Mod.Derringer_m_isExternalHammerCocked.SetValue(asDerringer, false);
                    }
                    modified = true;
                }
            }

            // Set barrels
            for (int i = 0; i < asDerringer.Barrels.Count; ++i)
            {
                int firstIndex = i * 2 + 1;
                short chamberClassIndex = BitConverter.ToInt16(newData, firstIndex);
                if (chamberClassIndex == -1) // We don't want round in chamber
                {
                    if (asDerringer.Barrels[i].Chamber.GetRound() != null)
                    {
                        asDerringer.Barrels[i].Chamber.SetRound(null, false);
                        modified = true;
                    }
                }
                else // We want a round in the chamber
                {
                    FireArmRoundClass roundClass = (FireArmRoundClass)chamberClassIndex;
                    if (asDerringer.Barrels[i].Chamber.GetRound() == null || asDerringer.Barrels[i].Chamber.GetRound().RoundClass != roundClass)
                    {
                        asDerringer.Barrels[i].Chamber.SetRound(roundClass, asDerringer.Barrels[i].Chamber.transform.position, asDerringer.Barrels[i].Chamber.transform.rotation);
                        modified = true;
                    }
                }
            }

            data.data = newData;

            return modified;
        }

        private bool UpdateBreakActionWeapon()
        {
            BreakActionWeapon asBreakActionWeapon = dataObject as BreakActionWeapon;
            bool modified = false;

            int necessarySize = asBreakActionWeapon.Barrels.Length * 3;

            if (data.data == null)
            {
                data.data = new byte[necessarySize];
                modified = true;
            }

            // Write chambered rounds
            byte preval0;
            byte preval1;
            for (int i = 0; i < asBreakActionWeapon.Barrels.Length; ++i)
            {
                // Write chambered round
                int firstIndex = i * 3;
                preval0 = data.data[firstIndex];
                preval1 = data.data[firstIndex + 1];

                if (asBreakActionWeapon.Barrels[i].Chamber.GetRound() == null)
                {
                    BitConverter.GetBytes((short)-1).CopyTo(data.data, firstIndex);
                }
                else
                {
                    BitConverter.GetBytes((short)asBreakActionWeapon.Barrels[i].Chamber.GetRound().RoundClass).CopyTo(data.data, firstIndex);
                }

                modified |= (preval0 != data.data[firstIndex] || preval1 != data.data[firstIndex + 1]);

                // Write hammer state
                preval0 = data.data[firstIndex + 2];

                data.data[firstIndex + 2] = asBreakActionWeapon.Barrels[i].m_isHammerCocked ? (byte)1 : (byte)0;

                modified |= preval0 != data.data[firstIndex + 2];
            }

            return modified;
        }

        private bool UpdateGivenBreakActionWeapon(byte[] newData)
        {
            bool modified = false;
            BreakActionWeapon asBreakActionWeapon = dataObject as BreakActionWeapon;

            // Set barrels
            for (int i = 0; i < asBreakActionWeapon.Barrels.Length; ++i)
            {
                int firstIndex = i * 3;
                short chamberClassIndex = BitConverter.ToInt16(newData, firstIndex);
                if (chamberClassIndex == -1) // We don't want round in chamber
                {
                    if (asBreakActionWeapon.Barrels[i].Chamber.GetRound() != null)
                    {
                        asBreakActionWeapon.Barrels[i].Chamber.SetRound(null, false);
                        modified = true;
                    }
                }
                else // We want a round in the chamber
                {
                    FireArmRoundClass roundClass = (FireArmRoundClass)chamberClassIndex;
                    if (asBreakActionWeapon.Barrels[i].Chamber.GetRound() == null || asBreakActionWeapon.Barrels[i].Chamber.GetRound().RoundClass != roundClass)
                    {
                        asBreakActionWeapon.Barrels[i].Chamber.SetRound(roundClass, asBreakActionWeapon.Barrels[i].Chamber.transform.position, asBreakActionWeapon.Barrels[i].Chamber.transform.rotation);
                        modified = true;
                    }
                }

                asBreakActionWeapon.Barrels[i].m_isHammerCocked = newData[firstIndex + 2] == 1;
            }

            data.data = newData;

            return modified;
        }

        private bool UpdateBAP()
        {
            BAP asBAP = dataObject as BAP;
            bool modified = false;

            if (data.data == null)
            {
                data.data = new byte[4];
                modified = true;
            }

            byte preval = data.data[0];

            // Write fire mode index
            data.data[0] = (byte)Mod.BAP_m_fireSelectorMode.GetValue(asBAP);

            modified |= preval != data.data[0];

            preval = data.data[1];

            // Write hammer state
            data.data[1] = BitConverter.GetBytes((bool)Mod.BAP_m_isHammerCocked.GetValue(asBAP))[0];

            modified |= preval != data.data[1];

            preval = data.data[2];
            byte preval0 = data.data[3];

            // Write chambered round class
            if (asBAP.Chamber.GetRound() == null)
            {
                BitConverter.GetBytes((short)-1).CopyTo(data.data, 2);
            }
            else
            {
                BitConverter.GetBytes((short)asBAP.Chamber.GetRound().RoundClass).CopyTo(data.data, 2);
            }

            modified |= (preval != data.data[2] || preval0 != data.data[3]);

            return modified;
        }

        private bool UpdateGivenBAP(byte[] newData)
        {
            bool modified = false;
            BAP asBAP = dataObject as BAP;

            if (data.data == null)
            {
                modified = true;

                // Set fire select mode
                Mod.BAP_m_fireSelectorMode.SetValue(asBAP, (int)newData[0]);
            }
            else
            {
                if (data.data[0] != newData[0])
                {
                    // Set fire select mode
                    Mod.BAP_m_fireSelectorMode.SetValue(asBAP, (int)newData[0]);
                    modified = true;
                }
            }

            // Set hammer state
            if (newData[1] == 0)
            {
                if ((bool)Mod.BAP_m_isHammerCocked.GetValue(asBAP))
                {
                    Mod.BAP_m_isHammerCocked.SetValue(asBAP, false);
                    modified = true;
                }
            }
            else // Hammer should be cocked
            {
                if (!(bool)Mod.BAP_m_isHammerCocked.GetValue(asBAP))
                {
                    asBAP.CockHammer();
                    modified = true;
                }
            }

            // Set chamber
            short chamberClassIndex = BitConverter.ToInt16(newData, 2);
            if (chamberClassIndex == -1) // We don't want round in chamber
            {
                if (asBAP.Chamber.GetRound() != null)
                {
                    asBAP.Chamber.SetRound(null, false);
                    modified = true;
                }
            }
            else // We want a round in the chamber
            {
                FireArmRoundClass roundClass = (FireArmRoundClass)chamberClassIndex;
                if (asBAP.Chamber.GetRound() == null || asBAP.Chamber.GetRound().RoundClass != roundClass)
                {
                    asBAP.Chamber.SetRound(roundClass, asBAP.Chamber.transform.position, asBAP.Chamber.transform.rotation);
                    modified = true;
                }
            }

            data.data = newData;

            return modified;
        }

        private void SetBAPUpdateOverride(FireArmRoundClass roundClass)
        {
            BAP asBAP = dataObject as BAP;

            asBAP.Chamber.SetRound(roundClass, asBAP.Chamber.transform.position, asBAP.Chamber.transform.rotation);
        }

        private bool UpdateRevolvingShotgun()
        {
            RevolvingShotgun asRS = dataObject as RevolvingShotgun;
            bool modified = false;

            int necessarySize = asRS.Chambers.Length * 2 + 2;

            if (data.data == null)
            {
                data.data = new byte[necessarySize];
                modified = true;
            }

            byte preval0 = data.data[0];

            // Write cur chamber
            data.data[0] = (byte)asRS.CurChamber;

            modified |= preval0 != data.data[0];

            preval0 = data.data[1];

            // Write cylLoaded
            data.data[1] = asRS.CylinderLoaded ? (byte)1 : (byte)0;

            modified |= preval0 != data.data[1];

            // Write chambered rounds
            byte preval1;
            for (int i = 0; i < asRS.Chambers.Length; ++i)
            {
                int firstIndex = i * 2 + 2;
                preval0 = data.data[firstIndex];
                preval1 = data.data[firstIndex + 1];

                if (asRS.Chambers[i].GetRound() == null)
                {
                    BitConverter.GetBytes((short)-1).CopyTo(data.data, firstIndex);
                }
                else
                {
                    BitConverter.GetBytes((short)asRS.Chambers[i].GetRound().RoundClass).CopyTo(data.data, firstIndex);
                }

                modified |= (preval0 != data.data[firstIndex] || preval1 != data.data[firstIndex + 1]);
            }

            return modified;
        }

        private bool UpdateGivenRevolvingShotgun(byte[] newData)
        {
            bool modified = false;
            RevolvingShotgun asRS = dataObject as RevolvingShotgun;

            if (data.data == null)
            {
                modified = true;

                // Set cur chamber
                asRS.CurChamber = newData[0];

                // Set cyl loaded
                bool newCylLoaded = newData[1] == 1;
                if(newCylLoaded && !asRS.CylinderLoaded)
                {
                    // Load cylinder, chambers will be updated separately
                    asRS.ProxyCylinder.gameObject.SetActive(true);
                    asRS.PlayAudioEvent(FirearmAudioEventType.MagazineIn);
                    asRS.CurChamber = 0;
                    asRS.ProxyCylinder.localRotation = asRS.GetLocalRotationFromCylinder(0);
                }
                else if(!newCylLoaded && asRS.CylinderLoaded)
                {
                    // Eject cylinder, chambers will be updated separately, handling the spawn of a physical cylinder will also be handled separately
                    asRS.PlayAudioEvent(FirearmAudioEventType.MagazineOut, 1f);
                    asRS.EjectDelay = 0.4f;
                    asRS.CylinderLoaded = false;
                    asRS.ProxyCylinder.gameObject.SetActive(false);
                }
                asRS.CylinderLoaded = newCylLoaded;
            }
            else
            {
                if (data.data[0] != newData[0])
                {
                    // Set cur chamber
                    asRS.CurChamber = newData[0];
                    modified = true;
                }
                if (data.data[1] != newData[1])
                {
                    // Set cyl loaded
                    bool newCylLoaded = newData[1] == 1;
                    if (newCylLoaded && !asRS.CylinderLoaded)
                    {
                        // Load cylinder, chambers will be updated separately
                        asRS.ProxyCylinder.gameObject.SetActive(true);
                        asRS.PlayAudioEvent(FirearmAudioEventType.MagazineIn);
                        asRS.CurChamber = 0;
                        asRS.ProxyCylinder.localRotation = asRS.GetLocalRotationFromCylinder(0);
                    }
                    else if (!newCylLoaded && asRS.CylinderLoaded)
                    {
                        // Eject cylinder, chambers will be updated separately, handling the spawn of a physical cylinder will also be handled separately
                        asRS.PlayAudioEvent(FirearmAudioEventType.MagazineOut, 1f);
                        asRS.EjectDelay = 0.4f;
                        asRS.CylinderLoaded = false;
                        asRS.ProxyCylinder.gameObject.SetActive(false);
                    }
                    asRS.CylinderLoaded = newCylLoaded;
                }
            }

            // Set chambers
            for (int i = 0; i < asRS.Chambers.Length; ++i)
            {
                int firstIndex = i * 2 + 2;
                short chamberClassIndex = BitConverter.ToInt16(newData, firstIndex);
                if (chamberClassIndex == -1) // We don't want round in chamber
                {
                    if (asRS.Chambers[i].GetRound() != null)
                    {
                        asRS.Chambers[i].SetRound(null, false);
                        modified = true;
                    }
                }
                else // We want a round in the chamber
                {
                    FireArmRoundClass roundClass = (FireArmRoundClass)chamberClassIndex;
                    if (asRS.Chambers[i].GetRound() == null || asRS.Chambers[i].GetRound().RoundClass != roundClass)
                    {
                        asRS.Chambers[i].SetRound(roundClass, asRS.Chambers[i].transform.position, asRS.Chambers[i].transform.rotation);
                        modified = true;
                    }
                }
            }

            data.data = newData;

            return modified;
        }

        private bool UpdateRevolver()
        {
            Revolver asRevolver = dataObject as Revolver;
            bool modified = false;

            int necessarySize = asRevolver.Cylinder.numChambers * 2 + 1;

            if (data.data == null)
            {
                data.data = new byte[necessarySize];
                modified = true;
            }

            byte preval0 = data.data[0];

            // Write cur chamber
            data.data[0] = (byte)asRevolver.CurChamber;

            modified |= preval0 != data.data[0];

            // Write chambered rounds
            byte preval1;
            for (int i = 0; i < asRevolver.Chambers.Length; ++i)
            {
                int firstIndex = i * 2 + 1;
                preval0 = data.data[firstIndex];
                preval1 = data.data[firstIndex + 1];

                if (asRevolver.Chambers[i].GetRound() == null)
                {
                    BitConverter.GetBytes((short)-1).CopyTo(data.data, firstIndex);
                }
                else
                {
                    BitConverter.GetBytes((short)asRevolver.Chambers[i].GetRound().RoundClass).CopyTo(data.data, firstIndex);
                }

                modified |= (preval0 != data.data[firstIndex] || preval1 != data.data[firstIndex + 1]);
            }

            return modified;
        }

        private bool UpdateGivenRevolver(byte[] newData)
        {
            bool modified = false;
            Revolver asRevolver = dataObject as Revolver;

            if (data.data == null)
            {
                modified = true;

                // Set cur chamber
                asRevolver.CurChamber = newData[0];
            }
            else
            {
                if (data.data[0] != newData[0])
                {
                    // Set cur chamber
                    asRevolver.CurChamber = newData[0];
                    modified = true;
                }
            }

            // Set chambers
            for (int i = 0; i < asRevolver.Chambers.Length; ++i)
            {
                int firstIndex = i * 2 + 1;
                short chamberClassIndex = BitConverter.ToInt16(newData, firstIndex);
                if (chamberClassIndex == -1) // We don't want round in chamber
                {
                    if (asRevolver.Chambers[i].GetRound() != null)
                    {
                        asRevolver.Chambers[i].SetRound(null, false);
                        modified = true;
                    }
                }
                else // We want a round in the chamber
                {
                    FireArmRoundClass roundClass = (FireArmRoundClass)chamberClassIndex;
                    if (asRevolver.Chambers[i].GetRound() == null || asRevolver.Chambers[i].GetRound().RoundClass != roundClass)
                    {
                        asRevolver.Chambers[i].SetRound(roundClass, asRevolver.Chambers[i].transform.position, asRevolver.Chambers[i].transform.rotation);
                        modified = true;
                    }
                }
            }

            data.data = newData;

            return modified;
        }

        private bool FireRevolver()
        {
            Revolver asRevolver = dataObject as Revolver;
            int num2 = data.data[0] + asRevolver.ChamberOffset;
            if (num2 >= asRevolver.Cylinder.numChambers)
            {
                num2 -= asRevolver.Cylinder.numChambers;
            }
            Mod.Revolver_Fire.Invoke(dataObject, null);
            if (GM.CurrentSceneSettings.IsAmmoInfinite || GM.CurrentPlayerBody.IsInfiniteAmmo)
            {
                asRevolver.Chambers[num2].IsSpent = false;
                asRevolver.Chambers[num2].UpdateProxyDisplay();
            }
            return true;
        }

        private void SetRevolverUpdateOverride(FireArmRoundClass roundClass)
        {
            Revolver asRevolver = dataObject as Revolver;
            int num2 = data.data[0] + asRevolver.ChamberOffset;
            if (num2 >= asRevolver.Cylinder.numChambers)
            {
                num2 -= asRevolver.Cylinder.numChambers;
            }
            asRevolver.Chambers[num2].SetRound(roundClass, asRevolver.Chambers[num2].transform.position, asRevolver.Chambers[num2].transform.rotation);
        }

        private bool UpdateM203()
        {
            M203 asM203 = dataObject as M203;
            bool modified = false;

            if (data.data == null)
            {
                data.data = new byte[2];
                modified = true;
            }

            byte preval = data.data[0];
            byte preval0 = data.data[1];

            // Write chambered round class
            if (asM203.Chamber.GetRound() == null)
            {
                BitConverter.GetBytes((short)-1).CopyTo(data.data, 0);
            }
            else
            {
                BitConverter.GetBytes((short)asM203.Chamber.GetRound().RoundClass).CopyTo(data.data, 0);
            }

            modified |= (preval != data.data[0] || preval0 != data.data[1]);

            return modified;
        }

        private bool UpdateGivenM203(byte[] newData)
        {
            bool modified = data.data == null;
            M203 asM203 = dataObject as M203;

            // Set chamber
            short chamberClassIndex = BitConverter.ToInt16(newData, 0);
            if (chamberClassIndex == -1) // We don't want round in chamber
            {
                if (asM203.Chamber.GetRound() != null)
                {
                    asM203.Chamber.SetRound(null, false);
                    modified = true;
                }
            }
            else // We want a round in the chamber
            {
                FireArmRoundClass roundClass = (FireArmRoundClass)chamberClassIndex;
                if (asM203.Chamber.GetRound() == null || asM203.Chamber.GetRound().RoundClass != roundClass)
                {
                    asM203.Chamber.SetRound(roundClass, asM203.Chamber.transform.position, asM203.Chamber.transform.rotation);
                    modified = true;
                }
            }

            data.data = newData;

            return modified;
        }

        private void M203ChamberRound(FireArmRoundClass roundClass)
        {
            M203 asM203 = dataObject as M203;
            asM203.Chamber.SetRound(roundClass, asM203.Chamber.transform.position, asM203.Chamber.transform.rotation);
        }

        private FVRFireArmChamber M203GetChamber()
        {
            M203 asM203 = dataObject as M203;
            return asM203.Chamber;
        }

        private bool UpdateGP25()
        {
            GP25 asGP25 = dataObject as GP25;
            bool modified = false;

            if (data.data == null)
            {
                data.data = new byte[3];
                modified = true;
            }

            byte preval = data.data[0];

            // Write safety
            data.data[0] = (byte)(asGP25.m_safetyEngaged ? 1:0);

            modified |= preval != data.data[0];

            preval = data.data[1];
            byte preval0 = data.data[2];

            // Write chambered round class
            if (asGP25.Chamber.GetRound() == null)
            {
                BitConverter.GetBytes((short)-1).CopyTo(data.data, 1);
            }
            else
            {
                BitConverter.GetBytes((short)asGP25.Chamber.GetRound().RoundClass).CopyTo(data.data, 1);
            }

            modified |= (preval != data.data[1] || preval0 != data.data[2]);

            return modified;
        }

        private bool UpdateGivenGP25(byte[] newData)
        {
            bool modified = false;
            GP25 asGP25 = dataObject as GP25;

            if (data.data == null)
            {
                modified = true;

                // Set safety
                asGP25.m_safetyEngaged = newData[0] == 1;
            }
            else
            {
                if (data.data[0] != newData[0])
                {
                    // Set safety
                    asGP25.m_safetyEngaged = newData[0] == 1;
                    modified = true;
                }
            }

            // Set chamber
            short chamberClassIndex = BitConverter.ToInt16(newData, 1);
            if (chamberClassIndex == -1) // We don't want round in chamber
            {
                if (asGP25.Chamber.GetRound() != null)
                {
                    asGP25.Chamber.SetRound(null, false);
                    modified = true;
                }
            }
            else // We want a round in the chamber
            {
                FireArmRoundClass roundClass = (FireArmRoundClass)chamberClassIndex;
                if (asGP25.Chamber.GetRound() == null || asGP25.Chamber.GetRound().RoundClass != roundClass)
                {
                    asGP25.Chamber.SetRound(roundClass, asGP25.Chamber.transform.position, asGP25.Chamber.transform.rotation);
                    modified = true;
                }
            }

            data.data = newData;

            return modified;
        }

        private void GP25ChamberRound(FireArmRoundClass roundClass)
        {
            GP25 asGP25 = dataObject as GP25;
            asGP25.Chamber.SetRound(roundClass, asGP25.Chamber.transform.position, asGP25.Chamber.transform.rotation);
        }

        private FVRFireArmChamber GP25GetChamber()
        {
            GP25 asGP25 = dataObject as GP25;
            return asGP25.Chamber;
        }

        private bool UpdateAttachableTubeFed()
        {
            AttachableTubeFed asATF = dataObject as AttachableTubeFed;
            bool modified = false;

            if (data.data == null)
            {
                data.data = new byte[6];
                modified = true;
            }

            byte preval = data.data[0];

            // Write fire mode index
            data.data[0] = (byte)(int)typeof(TubeFedShotgun).GetField("m_fireSelectorMode", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(asATF);

            modified |= preval != data.data[0];

            preval = data.data[1];

            // Write hammer state
            data.data[1] = BitConverter.GetBytes(asATF.IsHammerCocked)[0];

            modified |= preval != data.data[1];

            preval = data.data[2];
            byte preval0 = data.data[3];

            // Write chambered round class
            if (asATF.Chamber.GetRound() == null)
            {
                BitConverter.GetBytes((short)-1).CopyTo(data.data, 2);
            }
            else
            {
                BitConverter.GetBytes((short)asATF.Chamber.GetRound().RoundClass).CopyTo(data.data, 2);
            }

            modified |= (preval != data.data[3] || preval0 != data.data[4]);

            preval = data.data[4];

            // Write bolt handle pos
            data.data[4] = (byte)asATF.Bolt.CurPos;

            modified |= preval != data.data[4];

            if (asATF.HasHandle)
            {
                preval = data.data[5];

                // Write bolt handle pos
                data.data[5] = (byte)asATF.Handle.CurPos;

                modified |= preval != data.data[5];
            }

            return modified;
        }

        private bool UpdateGivenAttachableTubeFed(byte[] newData)
        {
            bool modified = false;
            AttachableTubeFed asATF = dataObject as AttachableTubeFed;

            if (data.data == null)
            {
                modified = true;

                // Set fire select mode
                typeof(TubeFedShotgun).GetField("m_fireSelectorMode", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(asATF, (int)newData[0]);

                // Set bolt pos
                asATF.Bolt.LastPos = asATF.Bolt.CurPos;
                asATF.Bolt.CurPos = (AttachableTubeFedBolt.BoltPos)newData[4];

                if (asATF.HasHandle)
                {
                    // Set handle pos
                    asATF.Handle.LastPos = asATF.Handle.CurPos;
                    asATF.Handle.CurPos = (AttachableTubeFedFore.BoltPos)newData[5];
                }
            }
            else
            {
                if (data.data[0] != newData[0])
                {
                    // Set fire select mode
                    typeof(TubeFedShotgun).GetField("m_fireSelectorMode", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(asATF, (int)newData[0]);
                    modified = true;
                }
                if (data.data[4] != newData[4])
                {
                    // Set bolt pos
                    asATF.Bolt.LastPos = asATF.Bolt.CurPos;
                    asATF.Bolt.CurPos = (AttachableTubeFedBolt.BoltPos)newData[4];
                }
                if (asATF.HasHandle && data.data[5] != newData[5])
                {
                    // Set handle pos
                    asATF.Handle.LastPos = asATF.Handle.CurPos;
                    asATF.Handle.CurPos = (AttachableTubeFedFore.BoltPos)newData[5];
                }
            }

            // Set hammer state
            if (newData[1] == 0)
            {
                if (asATF.IsHammerCocked)
                {
                    typeof(TubeFedShotgun).GetField("m_isHammerCocked", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(asATF, BitConverter.ToBoolean(newData, 1));
                    modified = true;
                }
            }
            else // Hammer should be cocked
            {
                if (!asATF.IsHammerCocked)
                {
                    asATF.CockHammer();
                    modified = true;
                }
            }

            // Set chamber
            short chamberClassIndex = BitConverter.ToInt16(newData, 2);
            if (chamberClassIndex == -1) // We don't want round in chamber
            {
                if (asATF.Chamber.GetRound() != null)
                {
                    asATF.Chamber.SetRound(null, false);
                    modified = true;
                }
            }
            else // We want a round in the chamber
            {
                FireArmRoundClass roundClass = (FireArmRoundClass)chamberClassIndex;
                if (asATF.Chamber.GetRound() == null || asATF.Chamber.GetRound().RoundClass != roundClass)
                {
                    asATF.Chamber.SetRound(roundClass, asATF.Chamber.transform.position, asATF.Chamber.transform.rotation);
                    modified = true;
                }
            }

            data.data = newData;

            return modified;
        }

        private void AttachableTubeFedChamberRound(FireArmRoundClass roundClass)
        {
            AttachableTubeFed asATF = dataObject as AttachableTubeFed;
            asATF.Chamber.SetRound(roundClass, asATF.Chamber.transform.position, asATF.Chamber.transform.rotation);
        }

        private FVRFireArmChamber AttachableTubeFedGetChamber()
        {
            AttachableTubeFed asATF = dataObject as AttachableTubeFed;
            return asATF.Chamber;
        }

        private bool UpdateAttachableClosedBoltWeapon()
        {
            AttachableClosedBoltWeapon asACBW = dataObject as AttachableClosedBoltWeapon;
            bool modified = false;

            if (data.data == null)
            {
                data.data = new byte[5];
                modified = true;
            }

            byte preval = data.data[0];

            // Write fire mode index
            data.data[0] = (byte)asACBW.FireSelectorModeIndex;

            modified |= preval != data.data[0];

            preval = data.data[1];

            // Write camBurst
            data.data[1] = (byte)(int)typeof(AttachableClosedBoltWeapon).GetField("m_CamBurst", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(asACBW);

            modified |= preval != data.data[1];

            preval = data.data[2];

            // Write hammer state
            data.data[2] = BitConverter.GetBytes(asACBW.IsHammerCocked)[0];

            modified |= preval != data.data[2];

            preval = data.data[3];
            byte preval0 = data.data[4];

            // Write chambered round class
            if (asACBW.Chamber.GetRound() == null)
            {
                BitConverter.GetBytes((short)-1).CopyTo(data.data, 3);
            }
            else
            {
                BitConverter.GetBytes((short)asACBW.Chamber.GetRound().RoundClass).CopyTo(data.data, 3);
            }

            modified |= (preval != data.data[3] || preval0 != data.data[4]);

            return modified;
        }

        private bool UpdateGivenAttachableClosedBoltWeapon(byte[] newData)
        {
            bool modified = false;
            AttachableClosedBoltWeapon asACBW = dataObject as AttachableClosedBoltWeapon;

            if (data.data == null)
            {
                modified = true;

                // Set fire select mode
                typeof(ClosedBoltWeapon).GetField("m_fireSelectorMode", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(asACBW, (int)newData[0]);

                // Set camBurst
                typeof(ClosedBoltWeapon).GetField("m_CamBurst", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(asACBW, (int)newData[1]);
            }
            else
            {
                if (data.data[0] != newData[0])
                {
                    // Set fire select mode
                    typeof(ClosedBoltWeapon).GetField("m_fireSelectorMode", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(asACBW, (int)newData[0]);
                    modified = true;
                }
                if (data.data[1] != newData[1])
                {
                    // Set camBurst
                    typeof(ClosedBoltWeapon).GetField("m_CamBurst", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(asACBW, (int)newData[1]);
                    modified = true;
                }
            }

            // Set hammer state
            if (newData[2] == 0)
            {
                if (asACBW.IsHammerCocked)
                {
                    typeof(ClosedBoltWeapon).GetField("m_isHammerCocked", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(asACBW, BitConverter.ToBoolean(newData, 2));
                    modified = true;
                }
            }
            else // Hammer should be cocked
            {
                if (!asACBW.IsHammerCocked)
                {
                    asACBW.CockHammer();
                    modified = true;
                }
            }

            // Set chamber
            short chamberClassIndex = BitConverter.ToInt16(newData, 3);
            if (chamberClassIndex == -1) // We don't want round in chamber
            {
                if (asACBW.Chamber.GetRound() != null)
                {
                    asACBW.Chamber.SetRound(null, false);
                    modified = true;
                }
            }
            else // We want a round in the chamber
            {
                FireArmRoundClass roundClass = (FireArmRoundClass)chamberClassIndex;
                if (asACBW.Chamber.GetRound() == null || asACBW.Chamber.GetRound().RoundClass != roundClass)
                {
                    asACBW.Chamber.SetRound(roundClass, asACBW.Chamber.transform.position, asACBW.Chamber.transform.rotation);
                    modified = true;
                }
            }

            data.data = newData;

            return modified;
        }

        private void AttachableClosedBoltWeaponChamberRound(FireArmRoundClass roundClass)
        {
            AttachableClosedBoltWeapon asACBW = dataObject as AttachableClosedBoltWeapon;
            asACBW.Chamber.SetRound(roundClass, asACBW.Chamber.transform.position, asACBW.Chamber.transform.rotation);
        }

        private FVRFireArmChamber AttachableClosedBoltWeaponGetChamber()
        {
            AttachableClosedBoltWeapon asACBW = dataObject as AttachableClosedBoltWeapon;
            return asACBW.Chamber;
        }

        private bool UpdateAttachableBreakActions()
        {
            AttachableBreakActions asABA = dataObject as AttachableBreakActions;
            bool modified = false;

            if (data.data == null)
            {
                data.data = new byte[1];
                modified = true;
            }

            byte preval = data.data[0];

            // Write breachOpen
            data.data[0] = ((bool)Mod.AttachableBreakActions_m_isBreachOpen.GetValue(asABA)) ? (byte)1 : (byte)0;

            modified |= preval != data.data[0];

            preval = data.data[1];
            byte preval0 = data.data[2];

            // Write chambered round class
            if (asABA.Chamber.GetRound() == null)
            {
                BitConverter.GetBytes((short)-1).CopyTo(data.data, 1);
            }
            else
            {
                BitConverter.GetBytes((short)asABA.Chamber.GetRound().RoundClass).CopyTo(data.data, 1);
            }

            modified |= (preval != data.data[1] || preval0 != data.data[2]);

            return modified;
        }

        private bool UpdateGivenAttachableBreakActions(byte[] newData)
        {
            bool modified = false;
            AttachableBreakActions asABA = dataObject as AttachableBreakActions;

            if (data.data == null)
            {
                modified = true;

                // Set breachOpen
                bool current = ((bool)Mod.AttachableBreakActions_m_isBreachOpen.GetValue(asABA));
                bool newVal = newData[0] == 1;
                if ((current && !newVal) || (!current && newVal))
                {
                    asABA.ToggleBreach();
                }
                Mod.AttachableBreakActions_m_isBreachOpen.SetValue(asABA, newVal);
            }
            else
            {
                if (data.data[0] != newData[0])
                {
                    // Set breachOpen
                    bool current = ((bool)Mod.AttachableBreakActions_m_isBreachOpen.GetValue(asABA));
                    bool newVal = newData[0] == 1;
                    if ((current && !newVal)||(!current && newVal))
                    {
                        asABA.ToggleBreach();
                    }
                    Mod.AttachableBreakActions_m_isBreachOpen.SetValue(asABA, newVal);
                    modified = true;
                }
            }

            // Set chamber
            short chamberClassIndex = BitConverter.ToInt16(newData, 1);
            if (chamberClassIndex == -1) // We don't want round in chamber
            {
                if (asABA.Chamber.GetRound() != null)
                {
                    asABA.Chamber.SetRound(null, false);
                    modified = true;
                }
            }
            else // We want a round in the chamber
            {
                FireArmRoundClass roundClass = (FireArmRoundClass)chamberClassIndex;
                if (asABA.Chamber.GetRound() == null || asABA.Chamber.GetRound().RoundClass != roundClass)
                {
                    asABA.Chamber.SetRound(roundClass, asABA.Chamber.transform.position, asABA.Chamber.transform.rotation);
                    modified = true;
                }
            }

            data.data = newData;

            return modified;
        }

        private void AttachableBreakActionsChamberRound(FireArmRoundClass roundClass)
        {
            AttachableBreakActions asABA = dataObject as AttachableBreakActions;
            asABA.Chamber.SetRound(roundClass, asABA.Chamber.transform.position, asABA.Chamber.transform.rotation);
        }

        private FVRFireArmChamber AttachableBreakActionsGetChamber()
        {
            AttachableBreakActions asABA = dataObject as AttachableBreakActions;
            return asABA.Chamber;
        }

        private void UpdateAttachableFirearmParent()
        {
            FVRFireArmAttachment asAttachment = (dataObject as AttachableFirearm).Attachment;

            if (currentMountIndex != 255) // We want to be attached to a mount
            {
                if (data.parent != -1) // We have parent
                {
                    // We could be on wrong mount (or none physically) if we got a new mount through update but the parent hadn't been updated yet

                    // Get the mount we are supposed to be mounted to
                    FVRFireArmAttachmentMount mount = null;
                    H3MP_TrackedItemData parentTrackedItemData = null;
                    if (H3MP_ThreadManager.host)
                    {
                        parentTrackedItemData = H3MP_Server.items[data.parent];
                    }
                    else
                    {
                        parentTrackedItemData = H3MP_Client.items[data.parent];
                    }

                    if (parentTrackedItemData != null && parentTrackedItemData.physicalItem)
                    {
                        mount = parentTrackedItemData.physicalItem.physicalObject.AttachmentMounts[currentMountIndex];
                    }

                    // If not yet physically mounted to anything, can right away mount to the proper mount
                    if (asAttachment.curMount == null)
                    {
                        ++data.ignoreParentChanged;
                        asAttachment.AttachToMount(mount, true);
                        --data.ignoreParentChanged;
                    }
                    else if (asAttachment.curMount != mount) // Already mounted, but not on the right one, need to unmount, then mount of right one
                    {
                        ++data.ignoreParentChanged;
                        if (asAttachment.curMount != null)
                        {
                            asAttachment.DetachFromMount();
                        }

                        asAttachment.AttachToMount(mount, true);
                        --data.ignoreParentChanged;
                    }
                }
                // else, if this happens it is because we received a parent update to null and just haven't gotten the up to date mount index of -1 yet
                //       This will be handled on update
            }
            // else, on update we will detach from any current mount if this is the case, no need to handle this here
        }

        private bool UpdateLAPD2019Battery()
        {
            LAPD2019Battery asLAPD2019Battery = dataObject as LAPD2019Battery;
            bool modified = false;

            if (data.data == null)
            {
                data.data = new byte[4];
                modified = true;
            }

            byte preval = data.data[0];
            byte preval0 = data.data[1];
            byte preval1 = data.data[2];
            byte preval2 = data.data[3];

            // Write energy
            BitConverter.GetBytes(asLAPD2019Battery.GetEnergy()).CopyTo(data.data, 0);

            modified |= (preval != data.data[0] || preval0 != data.data[1] || preval1 != data.data[2] || preval2 != data.data[3]);

            return modified;
        }

        private bool UpdateGivenLAPD2019Battery(byte[] newData)
        {
            bool modified = false;
            LAPD2019Battery asLAPD2019Battery = dataObject as LAPD2019Battery;

            if (data.data == null)
            {
                modified = true;

                // Set energy
                asLAPD2019Battery.SetEnergy(BitConverter.ToSingle(newData, 0));
            }
            else
            {
                if (data.data[11] != newData[11] || data.data[12] != newData[12] || data.data[13] != newData[13] || data.data[14] != newData[14])
                {
                    // Set energy
                    asLAPD2019Battery.SetEnergy(BitConverter.ToSingle(newData, 0));
                    modified = true;
                }
            }

            data.data = newData;

            return modified;
        }

        private bool UpdateLAPD2019()
        {
            LAPD2019 asLAPD2019 = dataObject as LAPD2019;
            bool modified = false;

            if (data.data == null)
            {
                data.data = new byte[15];
                modified = true;
            }

            byte preval = data.data[0];

            // Write curChamber
            data.data[0] = (byte)asLAPD2019.CurChamber;

            modified |= preval != data.data[0];

            byte preval0;

            // Write chambered round classes
            for(int i=0; i < 5; ++i)
            {
                int firstIndex = i * 2 + 1;
                preval = data.data[firstIndex];
                preval0 = data.data[firstIndex + 1];
                if (asLAPD2019.Chambers[i].GetRound() == null)
                {
                    BitConverter.GetBytes((short)-1).CopyTo(data.data, firstIndex);
                }
                else
                {
                    BitConverter.GetBytes((short)asLAPD2019.Chambers[i].GetRound().RoundClass).CopyTo(data.data, firstIndex);
                }

                modified |= (preval != data.data[firstIndex] || preval0 != data.data[firstIndex + 1]);
            }

            preval = data.data[11];
            preval0 = data.data[12];
            byte preval1 = data.data[13];
            byte preval2 = data.data[14];

            // Write capacitor charge
            BitConverter.GetBytes((float)Mod.LAPD2019_m_capacitorCharge.GetValue(asLAPD2019)).CopyTo(data.data, 11);

            modified |= (preval != data.data[11] || preval0 != data.data[12] || preval1 != data.data[13] || preval2 != data.data[14]);

            preval = data.data[15];

            // Write capacitor charged
            data.data[15] = (bool)Mod.LAPD2019_m_isCapacitorCharged.GetValue(asLAPD2019) ? (byte)1 : (byte)0;

            modified |= preval != data.data[15];

            return modified;
        }

        private bool UpdateGivenLAPD2019(byte[] newData)
        {
            bool modified = false;
            LAPD2019 asLAPD2019 = dataObject as LAPD2019;

            if (data.data == null)
            {
                modified = true;

                // Set curChamber
                asLAPD2019.CurChamber = newData[0];

                // Set capacitor charge
                Mod.LAPD2019_m_capacitorCharge.SetValue(asLAPD2019, BitConverter.ToSingle(newData, 11));

                // Set capacitor charged
                Mod.LAPD2019_m_capacitorCharge.SetValue(asLAPD2019, newData[15] == 1);
            }
            else
            {
                if (data.data[0] != newData[0])
                {
                    // Set curChamber
                    asLAPD2019.CurChamber = newData[0];
                    modified = true;
                }
                if (data.data[11] != newData[11] || data.data[12] != newData[12] || data.data[13] != newData[13] || data.data[14] != newData[14])
                {
                    // Set capacitor charge
                    Mod.LAPD2019_m_capacitorCharge.SetValue(asLAPD2019, BitConverter.ToSingle(newData, 11));
                    modified = true;
                }
                if (data.data[15] != newData[15])
                {
                    // Set capacitor charged
                    Mod.LAPD2019_m_capacitorCharge.SetValue(asLAPD2019, newData[15] == 1);
                    modified = true;
                }
            }

            // Set chambers
            for (int i = 0; i < 5; ++i)
            {
                short chamberClassIndex = BitConverter.ToInt16(newData, i * 2 + 1);
                if (chamberClassIndex == -1) // We don't want round in chamber
                {
                    if (asLAPD2019.Chambers[i].GetRound() != null)
                    {
                        asLAPD2019.Chambers[i].SetRound(null, false);
                        modified = true;
                    }
                }
                else // We want a round in the chamber
                {
                    FireArmRoundClass roundClass = (FireArmRoundClass)chamberClassIndex;
                    if (asLAPD2019.Chambers[i].GetRound() == null || asLAPD2019.Chambers[i].GetRound().RoundClass != roundClass)
                    {
                        asLAPD2019.Chambers[i].SetRound(roundClass, asLAPD2019.Chambers[i].transform.position, asLAPD2019.Chambers[i].transform.rotation);
                        modified = true;
                    }
                }
            }

            data.data = newData;

            return modified;
        }

        private bool UpdateSosigWeaponInterface()
        {
            SosigWeaponPlayerInterface asInterface = dataObject as SosigWeaponPlayerInterface;
            bool modified = false;

            if (data.data == null)
            {
                data.data = new byte[3];
                modified = true;
            }

            byte preval = data.data[0];
            byte preval0 = data.data[1];

            // Write shots left
            BitConverter.GetBytes((short)(int)typeof(SosigWeapon).GetField("m_shotsLeft", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(asInterface.W)).CopyTo(data.data, 0);

            modified |= (preval != data.data[0] || preval0 != data.data[1]);

            preval = data.data[2];

            // Write MechaState
            data.data[2] = (byte)asInterface.W.MechaState;

            modified |= preval != data.data[2];

            return modified;
        }

        private bool UpdateGivenSosigWeaponInterface(byte[] newData)
        {
            bool modified = false;
            SosigWeaponPlayerInterface asInterface = dataObject as SosigWeaponPlayerInterface;

            if (data.data == null)
            {
                modified = true;

                // Set shots left
                typeof(SosigWeapon).GetField("m_shotsLeft", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(asInterface.W, BitConverter.ToInt16(newData, 0));

                // Set MechaState
                asInterface.W.MechaState = (SosigWeapon.SosigWeaponMechaState)newData[2];
            }
            else 
            {
                if (data.data[0] != newData[0] || data.data[1] != newData[1])
                {
                    // Set shots left
                    typeof(SosigWeapon).GetField("m_shotsLeft", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(asInterface.W, BitConverter.ToInt16(newData, 0));
                    modified = true;
                }
                if (data.data[2] != newData[2])
                {
                    // Set MechaState
                    asInterface.W.MechaState = (SosigWeapon.SosigWeaponMechaState)newData[2];
                    modified = true;
                }
            }

            data.data = newData;

            return modified;
        }

        private bool UpdateClosedBoltWeapon()
        {
            ClosedBoltWeapon asCBW = dataObject as ClosedBoltWeapon;
            bool modified = false;

            if (data.data == null)
            {
                data.data = new byte[5];
                modified = true;
            }

            byte preval = data.data[0];

            // Write fire mode index
            data.data[0] = (byte)asCBW.FireSelectorModeIndex;

            modified |= preval != data.data[0];

            preval = data.data[1];

            // Write camBurst
            data.data[1] = (byte)(int)typeof(ClosedBoltWeapon).GetField("m_CamBurst", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(asCBW);

            modified |= preval != data.data[1];

            preval = data.data[2];

            // Write hammer state
            data.data[2] = BitConverter.GetBytes(asCBW.IsHammerCocked)[0];

            modified |= preval != data.data[2];

            preval = data.data[3];
            byte preval0 = data.data[4];

            // Write chambered round class
            if(asCBW.Chamber.GetRound() == null)
            {
                BitConverter.GetBytes((short)-1).CopyTo(data.data, 3);
            }
            else
            {
                BitConverter.GetBytes((short)asCBW.Chamber.GetRound().RoundClass).CopyTo(data.data, 3);
            }

            modified |= (preval != data.data[3] || preval0 != data.data[4]);

            return modified;
        }

        private bool UpdateGivenClosedBoltWeapon(byte[] newData)
        {
            bool modified = false;
            ClosedBoltWeapon asCBW = dataObject as ClosedBoltWeapon;

            if(data.data == null)
            {
                modified = true;

                // Set fire select mode
                typeof(ClosedBoltWeapon).GetField("m_fireSelectorMode", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(asCBW, (int)newData[0]);

                // Set camBurst
                typeof(ClosedBoltWeapon).GetField("m_CamBurst", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(asCBW, (int)newData[1]);
            }
            else 
            {
                if (data.data[0] != newData[0])
                {
                    // Set fire select mode
                    typeof(ClosedBoltWeapon).GetField("m_fireSelectorMode", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(asCBW, (int)newData[0]);
                    modified = true;
                }
                if (data.data[1] != newData[1])
                {
                    // Set camBurst
                    typeof(ClosedBoltWeapon).GetField("m_CamBurst", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(asCBW, (int)newData[1]);
                    modified = true;
                }
            }

            // Set hammer state
            if (newData[2] == 0)
            {
                if (asCBW.IsHammerCocked)
                {
                    typeof(ClosedBoltWeapon).GetField("m_isHammerCocked", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(asCBW, BitConverter.ToBoolean(newData, 2));
                    modified = true;
                }
            }
            else // Hammer should be cocked
            {
                if (!asCBW.IsHammerCocked)
                {
                    asCBW.CockHammer();
                    modified = true;
                }
            }

            // Set chamber
            short chamberClassIndex = BitConverter.ToInt16(newData, 3);
            if(chamberClassIndex == -1) // We don't want round in chamber
            {
                if(asCBW.Chamber.GetRound() != null)
                {
                    asCBW.Chamber.SetRound(null, false);
                    modified = true;
                }
            }
            else // We want a round in the chamber
            {
                FireArmRoundClass roundClass = (FireArmRoundClass) chamberClassIndex;
                if (asCBW.Chamber.GetRound() == null || asCBW.Chamber.GetRound().RoundClass != roundClass)
                {
                    asCBW.Chamber.SetRound(roundClass, asCBW.Chamber.transform.position, asCBW.Chamber.transform.rotation);
                    modified = true;
                }
            }

            data.data = newData;

            return modified;
        }

        private void SetCBWUpdateOverride(FireArmRoundClass roundClass)
        {
            ClosedBoltWeapon asCBW = (ClosedBoltWeapon)dataObject;

            asCBW.Chamber.SetRound(roundClass, asCBW.Chamber.transform.position, asCBW.Chamber.transform.rotation);
        }

        private bool UpdateHandgun()
        {
            Handgun asHandgun = dataObject as Handgun;
            bool modified = false;

            if (data.data == null)
            {
                data.data = new byte[5];
                modified = true;
            }

            byte preval = data.data[0];

            // Write fire mode index
            data.data[0] = (byte)asHandgun.FireSelectorModeIndex;

            modified |= preval != data.data[0];

            preval = data.data[1];

            // Write camBurst
            data.data[1] = (byte)(int)typeof(Handgun).GetField("m_CamBurst", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(asHandgun);

            modified |= preval != data.data[1];

            preval = data.data[2];

            // Write hammer state
            data.data[2] = BitConverter.GetBytes((bool)typeof(Handgun).GetField("m_isHammerCocked", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(asHandgun))[0];

            modified |= preval != data.data[2];

            preval = data.data[3];
            byte preval0 = data.data[4];

            // Write chambered round class
            if (asHandgun.Chamber.GetRound() == null)
            {
                BitConverter.GetBytes((short)-1).CopyTo(data.data, 3);
            }
            else
            {
                BitConverter.GetBytes((short)asHandgun.Chamber.GetRound().RoundClass).CopyTo(data.data, 3);
            }

            modified |= (preval != data.data[3] || preval0 != data.data[4]);

            return modified;
        }

        private bool UpdateGivenHandgun(byte[] newData)
        {
            bool modified = false;
            Handgun asHandgun = dataObject as Handgun;

            if(data.data == null)
            {
                modified = true;

                // Set fire select mode
                typeof(Handgun).GetField("m_fireSelectorMode", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(asHandgun, (int)newData[0]);

                // Set camBurst
                typeof(Handgun).GetField("m_CamBurst", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(asHandgun, (int)newData[1]);
            }
            else 
            {
                if (data.data[0] != newData[0])
                {
                    // Set fire select mode
                    typeof(Handgun).GetField("m_fireSelectorMode", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(asHandgun, (int)newData[0]);
                    modified = true;
                }
                if (data.data[1] != newData[1])
                {
                    // Set camBurst
                    typeof(Handgun).GetField("m_CamBurst", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(asHandgun, (int)newData[1]);
                    modified = true;
                }
            }

            FieldInfo hammerCockedField = typeof(Handgun).GetField("m_isHammerCocked", BindingFlags.NonPublic | BindingFlags.Instance);
            bool isHammerCocked = (bool)hammerCockedField.GetValue(asHandgun);

            // Set hammer state
            if (newData[2] == 0)
            {
                if (isHammerCocked)
                {
                    hammerCockedField.SetValue(asHandgun, BitConverter.ToBoolean(newData, 2));
                    modified = true;
                }
            }
            else // Hammer should be cocked
            {
                if (!isHammerCocked)
                {
                    asHandgun.CockHammer(false);
                    modified = true;
                }
            }

            // Set chamber
            short chamberClassIndex = BitConverter.ToInt16(newData, 3);
            if(chamberClassIndex == -1) // We don't want round in chamber
            {
                if(asHandgun.Chamber.GetRound() != null)
                {
                    asHandgun.Chamber.SetRound(null, false);
                    modified = true;
                }
            }
            else // We want a round in the chamber
            {
                FireArmRoundClass roundClass = (FireArmRoundClass) chamberClassIndex;
                if (asHandgun.Chamber.GetRound() == null || asHandgun.Chamber.GetRound().RoundClass != roundClass)
                {
                    asHandgun.Chamber.SetRound(roundClass, asHandgun.Chamber.transform.position, asHandgun.Chamber.transform.rotation);
                    modified = true;
                }
            }

            data.data = newData;

            return modified;
        }

        private void SetHandgunUpdateOverride(FireArmRoundClass roundClass)
        {
            Handgun asHandgun = dataObject as Handgun;

            asHandgun.Chamber.SetRound(roundClass, asHandgun.Chamber.transform.position, asHandgun.Chamber.transform.rotation);
        }

        private bool UpdateTubeFedShotgun()
        {
            TubeFedShotgun asTFS = dataObject as TubeFedShotgun;
            bool modified = false;

            if (data.data == null)
            {
                data.data = new byte[6];
                modified = true;
            }

            byte preval = data.data[0];

            // Write fire mode index
            data.data[0] = (byte)(int)typeof(TubeFedShotgun).GetField("m_fireSelectorMode", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(asTFS);

            modified |= preval != data.data[0];

            preval = data.data[1];

            // Write hammer state
            data.data[1] = BitConverter.GetBytes(asTFS.IsHammerCocked)[0];

            modified |= preval != data.data[1];

            preval = data.data[2];
            byte preval0 = data.data[3];

            // Write chambered round class
            if(asTFS.Chamber.GetRound() == null)
            {
                BitConverter.GetBytes((short)-1).CopyTo(data.data, 2);
            }
            else
            {
                BitConverter.GetBytes((short)asTFS.Chamber.GetRound().RoundClass).CopyTo(data.data, 2);
            }

            modified |= (preval != data.data[3] || preval0 != data.data[4]);

            preval = data.data[4];

            // Write bolt handle pos
            data.data[4] = (byte)asTFS.Bolt.CurPos;

            modified |= preval != data.data[4];

            if (asTFS.HasHandle)
            {
                preval = data.data[5];

                // Write bolt handle pos
                data.data[5] = (byte)asTFS.Handle.CurPos;

                modified |= preval != data.data[5];
            }

            return modified;
        }

        private bool UpdateGivenTubeFedShotgun(byte[] newData)
        {
            bool modified = false;
            TubeFedShotgun asTFS = dataObject as TubeFedShotgun;

            if (data.data == null)
            {
                modified = true;

                // Set fire select mode
                typeof(TubeFedShotgun).GetField("m_fireSelectorMode", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(asTFS, (int)newData[0]);

                // Set bolt pos
                asTFS.Bolt.LastPos = asTFS.Bolt.CurPos;
                asTFS.Bolt.CurPos = (TubeFedShotgunBolt.BoltPos)newData[4];

                if (asTFS.HasHandle)
                {
                    // Set handle pos
                    asTFS.Handle.LastPos = asTFS.Handle.CurPos;
                    asTFS.Handle.CurPos = (TubeFedShotgunHandle.BoltPos)newData[5];
                }
            }
            else 
            {
                if (data.data[0] != newData[0])
                {
                    // Set fire select mode
                    typeof(TubeFedShotgun).GetField("m_fireSelectorMode", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(asTFS, (int)newData[0]);
                    modified = true;
                }
                if (data.data[4] != newData[4])
                {
                    // Set bolt pos
                    asTFS.Bolt.LastPos = asTFS.Bolt.CurPos;
                    asTFS.Bolt.CurPos = (TubeFedShotgunBolt.BoltPos)newData[4];
                }
                if (asTFS.HasHandle && data.data[5] != newData[5])
                {
                    // Set handle pos
                    asTFS.Handle.LastPos = asTFS.Handle.CurPos;
                    asTFS.Handle.CurPos = (TubeFedShotgunHandle.BoltPos)newData[5];
                }
            }

            // Set hammer state
            if (newData[1] == 0)
            {
                if (asTFS.IsHammerCocked)
                {
                    typeof(TubeFedShotgun).GetField("m_isHammerCocked", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(asTFS, BitConverter.ToBoolean(newData, 1));
                    modified = true;
                }
            }
            else // Hammer should be cocked
            {
                if (!asTFS.IsHammerCocked)
                {
                    asTFS.CockHammer();
                    modified = true;
                }
            }
            
            // Set chamber
            short chamberClassIndex = BitConverter.ToInt16(newData, 2);
            if(chamberClassIndex == -1) // We don't want round in chamber
            {
                if(asTFS.Chamber.GetRound() != null)
                {
                    asTFS.Chamber.SetRound(null, false);
                    modified = true;
                }
            }
            else // We want a round in the chamber
            {
                FireArmRoundClass roundClass = (FireArmRoundClass) chamberClassIndex;
                if (asTFS.Chamber.GetRound() == null || asTFS.Chamber.GetRound().RoundClass != roundClass)
                {
                    asTFS.Chamber.SetRound(roundClass, asTFS.Chamber.transform.position, asTFS.Chamber.transform.rotation);
                    modified = true;
                }
            }

            data.data = newData;

            return modified;
        }

        private void SetTFSUpdateOverride(FireArmRoundClass roundClass)
        {
            TubeFedShotgun asTFS = dataObject as TubeFedShotgun;

            asTFS.Chamber.SetRound(roundClass, asTFS.Chamber.transform.position, asTFS.Chamber.transform.rotation);
        }

        private bool UpdateBoltActionRifle()
        {
            BoltActionRifle asBAR = dataObject as BoltActionRifle;
            bool modified = false;

            if (data.data == null)
            {
                data.data = new byte[6];
                modified = true;
            }

            byte preval = data.data[0];

            // Write fire mode index
            data.data[0] = (byte)(int)typeof(BoltActionRifle).GetField("m_fireSelectorMode", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(asBAR);

            modified |= preval != data.data[0];

            preval = data.data[1];

            // Write hammer state
            data.data[1] = BitConverter.GetBytes(asBAR.IsHammerCocked)[0];

            modified |= preval != data.data[1];

            preval = data.data[2];
            byte preval0 = data.data[3];

            // Write chambered round class
            if(asBAR.Chamber.GetRound() == null)
            {
                BitConverter.GetBytes((short)-1).CopyTo(data.data, 2);
            }
            else
            {
                BitConverter.GetBytes((short)asBAR.Chamber.GetRound().RoundClass).CopyTo(data.data, 2);
            }

            modified |= (preval != data.data[3] || preval0 != data.data[4]);

            preval = data.data[4];

            // Write bolt handle state
            data.data[4] = (byte)asBAR.CurBoltHandleState;

            modified |= preval != data.data[4];

            preval = data.data[5];

            // Write bolt handle rot
            data.data[5] = (byte)asBAR.BoltHandle.HandleRot;

            modified |= preval != data.data[5];

            return modified;
        }

        private bool UpdateGivenBoltActionRifle(byte[] newData)
        {
            bool modified = false;
            BoltActionRifle asBAR = dataObject as BoltActionRifle;

            if (data.data == null)
            {
                modified = true;

                // Set fire select mode
                typeof(BoltActionRifle).GetField("m_fireSelectorMode", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(asBAR, (int)newData[0]);

                // Set bolt handle state
                asBAR.LastBoltHandleState = asBAR.CurBoltHandleState;
                asBAR.CurBoltHandleState = (BoltActionRifle_Handle.BoltActionHandleState)newData[4];

                // Set bolt handle rot
                asBAR.BoltHandle.LastHandleRot = asBAR.BoltHandle.HandleRot;
                asBAR.BoltHandle.HandleRot = (BoltActionRifle_Handle.BoltActionHandleRot)newData[5];
            }
            else 
            {
                if (data.data[0] != newData[0])
                {
                    // Set fire select mode
                    typeof(BoltActionRifle).GetField("m_fireSelectorMode", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(asBAR, (int)newData[0]);
                    modified = true;
                }
                if (data.data[4] != newData[4])
                {
                    // Set bolt handle state
                    asBAR.LastBoltHandleState = asBAR.CurBoltHandleState;
                    asBAR.CurBoltHandleState = (BoltActionRifle_Handle.BoltActionHandleState)newData[4];
                }
                if (data.data[5] != newData[5])
                {
                    // Set bolt handle rot
                    asBAR.BoltHandle.LastHandleRot = asBAR.BoltHandle.HandleRot;
                    asBAR.BoltHandle.HandleRot = (BoltActionRifle_Handle.BoltActionHandleRot)newData[5];
                }
            }

            // Set hammer state
            if (newData[1] == 0)
            {
                if (asBAR.IsHammerCocked)
                {
                    typeof(BoltActionRifle).GetField("m_isHammerCocked", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(asBAR, BitConverter.ToBoolean(newData, 1));
                    modified = true;
                }
            }
            else // Hammer should be cocked
            {
                if (!asBAR.IsHammerCocked)
                {
                    asBAR.CockHammer();
                    modified = true;
                }
            }
            
            // Set chamber
            short chamberClassIndex = BitConverter.ToInt16(newData, 2);
            if(chamberClassIndex == -1) // We don't want round in chamber
            {
                if(asBAR.Chamber.GetRound() != null)
                {
                    asBAR.Chamber.SetRound(null, false);
                    modified = true;
                }
            }
            else // We want a round in the chamber
            {
                FireArmRoundClass roundClass = (FireArmRoundClass) chamberClassIndex;
                if (asBAR.Chamber.GetRound() == null || asBAR.Chamber.GetRound().RoundClass != roundClass)
                {
                    asBAR.Chamber.SetRound(roundClass, asBAR.Chamber.transform.position, asBAR.Chamber.transform.rotation);
                    modified = true;
                }
            }

            data.data = newData;

            return modified;
        }

        private void SetBARUpdateOverride(FireArmRoundClass roundClass)
        {
            BoltActionRifle asBar = dataObject as BoltActionRifle;

            asBar.Chamber.SetRound(roundClass, asBar.Chamber.transform.position, asBar.Chamber.transform.rotation);
        }

        private bool UpdateAttachment()
        {
            bool modified = false;
            FVRFireArmAttachment asAttachment = dataObject as FVRFireArmAttachment;

            if (data.data == null)
            {
                data.data = new byte[1];
                modified = true;
            }

            byte preIndex = data.data[0];

            // Write attached mount index
            if (asAttachment.curMount == null)
            {
                data.data[0] = 255;
            }
            else
            {
                // Find the mount and set it
                bool found = false;
                for(int i=0; i < asAttachment.curMount.Parent.AttachmentMounts.Count; ++i)
                {
                    if (asAttachment.curMount.Parent.AttachmentMounts[i] == asAttachment.curMount)
                    {
                        data.data[0] = (byte)i;
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    data.data[0] = 255;
                }
            }

            return modified || (preIndex != data.data[0]);
        }

        private bool UpdateGivenAttachment(byte[] newData)
        {
            bool modified = false;
            FVRFireArmAttachment asAttachment = dataObject as FVRFireArmAttachment;

            if (data.data == null || data.data.Length != newData.Length)
            {
                data.data = new byte[1];
                data.data[0] = 255;
                currentMountIndex = 255;
                modified = true;
            }

            // If mount doesn't actually change, just return now
            byte mountIndex = newData[0];
            if(currentMountIndex == mountIndex)
            {
                return modified;
            }
            data.data[0] = mountIndex;

            byte preMountIndex = currentMountIndex;
            if (mountIndex == 255)
            {
                // Should not be mounted, check if currently is
                if(asAttachment.curMount != null)
                {
                    ++data.ignoreParentChanged;
                    asAttachment.DetachFromMount();
                    --data.ignoreParentChanged;
                    currentMountIndex = 255;

                    // Detach from mount will recover rigidbody, set as kinematic if not controller
                    if (data.controller != H3MP_GameManager.ID)
                    {
                        Mod.SetKinematicRecursive(asAttachment.transform, true);
                    }
                }
            }
            else
            {
                // Find mount instance we want to be mounted to
                FVRFireArmAttachmentMount mount = null;
                H3MP_TrackedItemData parentTrackedItemData = null;
                if (H3MP_ThreadManager.host)
                {
                    parentTrackedItemData = H3MP_Server.items[data.parent];
                }
                else
                {
                    parentTrackedItemData = H3MP_Client.items[data.parent];
                }

                if (parentTrackedItemData != null && parentTrackedItemData.physicalItem)
                {
                    // We want to be mounted, we have a parent
                    if (parentTrackedItemData.physicalItem.physicalObject.AttachmentMounts.Count > mountIndex)
                    {
                        mount = parentTrackedItemData.physicalItem.physicalObject.AttachmentMounts[mountIndex];
                    }
                }

                // Mount could be null if the mount index corresponds to a parent we have yet a receive a change to
                if (mount != null)
                {
                    ++data.ignoreParentChanged;
                    if (asAttachment.curMount != null)
                    {
                        asAttachment.DetachFromMount();
                    }

                    asAttachment.AttachToMount(mount, true);
                    currentMountIndex = mountIndex;
                    --data.ignoreParentChanged;
                }
            }

            return modified || (preMountIndex != currentMountIndex);
        }

        private void UpdateAttachmentParent()
        {
            FVRFireArmAttachment asAttachment = dataObject as FVRFireArmAttachment;

            if(currentMountIndex != 255) // We want to be attached to a mount
            {
                if (data.parent != -1) // We have parent
                {
                    // We could be on wrong mount (or none physically) if we got a new mount through update but the parent hadn't been updated yet

                    // Get the mount we are supposed to be mounted to
                    FVRFireArmAttachmentMount mount = null;
                    H3MP_TrackedItemData parentTrackedItemData = null;
                    if (H3MP_ThreadManager.host)
                    {
                        parentTrackedItemData = H3MP_Server.items[data.parent];
                    }
                    else
                    {
                        parentTrackedItemData = H3MP_Client.items[data.parent];
                    }

                    if (parentTrackedItemData != null && parentTrackedItemData.physicalItem)
                    {
                        mount = parentTrackedItemData.physicalItem.physicalObject.AttachmentMounts[currentMountIndex];
                    }

                    // If not yet physically mounted to anything, can right away mount to the proper mount
                    if (asAttachment.curMount == null)
                    {
                        ++data.ignoreParentChanged;
                        asAttachment.AttachToMount(mount, true);
                        --data.ignoreParentChanged;
                    }
                    else if(asAttachment.curMount != mount) // Already mounted, but not on the right one, need to unmount, then mount of right one
                    {
                        ++data.ignoreParentChanged;
                        if (asAttachment.curMount != null)
                        {
                            asAttachment.DetachFromMount();
                        }

                        asAttachment.AttachToMount(mount, true);
                        --data.ignoreParentChanged;
                    }
                }
                // else, if this happens it is because we received a parent update to null and just haven't gotten the up to date mount index of -1 yet
                //       This will be handled on update
            }
            // else, on update we will detach from any current mount if this is the case, no need to handle this here
        }

        private bool UpdateMagazine()
        {
            bool modified = false;
            FVRFireArmMagazine asMag = dataObject as FVRFireArmMagazine;

            int necessarySize = asMag.m_numRounds * 2 + 10;

            if(data.data == null || data.data.Length < necessarySize)
            {
                data.data = new byte[necessarySize];
                modified = true;
            }

            byte preval0 = data.data[0];
            byte preval1 = data.data[1];

            // Write count of loaded rounds
            BitConverter.GetBytes((short)asMag.m_numRounds).CopyTo(data.data, 0);

            modified |= (preval0 != data.data[0] || preval1 != data.data[1]);

            // Write loaded round classes
            for (int i=0; i < asMag.m_numRounds; ++i)
            {
                preval0 = data.data[i * 2 + 2];
                preval1 = data.data[i * 2 + 3];

                BitConverter.GetBytes((short)asMag.LoadedRounds[i].LR_Class).CopyTo(data.data, i * 2 + 2);

                modified |= (preval0 != data.data[i * 2 + 2] || preval1 != data.data[i * 2 + 3]);
            }

            // Write loaded into firearm
            data.data[necessarySize - 8] = asMag.FireArm != null ? (byte)1 : (byte)0;

            // Write secondary slot index, TODO: Having to look through each secondary slot for equality every update is obviously not optimal
            // We might want to look into patching (Attachable)Firearm's LoadMagIntoSecondary and eject from secondary to keep track of this instead
            if(asMag.FireArm == null)
            {
                data.data[necessarySize - 7] = (byte)255;
            }
            else
            {
                for (int i = 0; i < asMag.FireArm.SecondaryMagazineSlots.Length; ++i)
                {
                    if (asMag.FireArm.SecondaryMagazineSlots[i].Magazine == asMag)
                    {
                        data.data[necessarySize - 7] = (byte)i;
                        break;
                    }
                }
            }

            // Write loaded into AttachableFirearm
            data.data[necessarySize - 6] = asMag.AttachableFireArm != null ? (byte)1 : (byte)0;

            // Write secondary slot index, TODO: Having to look through each secondary slot for equality every update is obviously not optimal
            // We might want to look into patching (Attachable)Firearm's LoadMagIntoSecondary and eject from secondary to keep track of this instead
            if (asMag.AttachableFireArm == null)
            {
                data.data[necessarySize - 5] = (byte)255;
            }
            else
            {
                for (int i = 0; i < asMag.AttachableFireArm.SecondaryMagazineSlots.Length; ++i)
                {
                    if (asMag.AttachableFireArm.SecondaryMagazineSlots[i].Magazine == asMag)
                    {
                        data.data[necessarySize - 5] = (byte)i;
                        break;
                    }
                }
            }

            // Write fuel amount left
            BitConverter.GetBytes(asMag.FuelAmountLeft).CopyTo(data.data, necessarySize - 4);

            return modified;
        }

        private bool UpdateGivenMagazine(byte[] newData)
        {
            bool modified = false;
            FVRFireArmMagazine asMag = dataObject as FVRFireArmMagazine;

            if (data.data == null || data.data.Length != newData.Length)
            {
                modified = true;
            }

            int preRoundCount = asMag.m_numRounds;
            asMag.m_numRounds = 0;
            short numRounds = BitConverter.ToInt16(newData, 0);

            // Load rounds
            for (int i = 0; i < numRounds; ++i)
            {
                int first = i * 2 + 2;
                FireArmRoundClass newClass = (FireArmRoundClass)BitConverter.ToInt16(newData, first);
                if(asMag.LoadedRounds.Length > i && asMag.LoadedRounds[i] != null && newClass == asMag.LoadedRounds[i].LR_Class)
                {
                    ++asMag.m_numRounds;
                }
                else
                {
                    asMag.AddRound(newClass, false, false);
                    modified = true;
                }
            }

            modified |= preRoundCount != asMag.m_numRounds;

            if (modified)
            {
                asMag.UpdateBulletDisplay();
            }

            // Load into firearm if necessary
            if (newData[newData.Length - 8] == 1)
            {
                if (data.parent != -1)
                {
                    H3MP_TrackedItemData parentTrackedItemData = null;
                    if (H3MP_ThreadManager.host)
                    {
                        parentTrackedItemData = H3MP_Server.items[data.parent];
                    }
                    else
                    {
                        parentTrackedItemData = H3MP_Client.items[data.parent];
                    }

                    if (parentTrackedItemData != null && parentTrackedItemData.physicalItem != null && parentTrackedItemData.physicalItem.dataObject is FVRFireArm)
                    {
                        // We want to be loaded in a firearm, we have a parent, it is a firearm
                        if (asMag.FireArm != null)
                        {
                            if (asMag.FireArm != parentTrackedItemData.physicalItem.dataObject)
                            {
                                // Unload from current, load into new firearm
                                if (asMag.FireArm.Magazine == asMag)
                                {
                                    asMag.FireArm.EjectMag(true);
                                }
                                else
                                {
                                    for(int i=0; i < asMag.FireArm.SecondaryMagazineSlots.Length; ++i)
                                    {
                                        if (asMag.FireArm.SecondaryMagazineSlots[i].Magazine == asMag)
                                        {
                                            asMag.FireArm.EjectSecondaryMagFromSlot(i, true);
                                            break;
                                        }
                                    }
                                }
                                if (newData[newData.Length - 7] == 255)
                                {
                                    asMag.Load(parentTrackedItemData.physicalItem.dataObject as FVRFireArm);
                                }
                                else
                                {
                                    asMag.LoadIntoSecondary(parentTrackedItemData.physicalItem.dataObject as FVRFireArm, newData[newData.Length - 7]);
                                }
                                modified = true;
                            }
                        }
                        else if(asMag.AttachableFireArm != null)
                        {
                            // Unload from current, load into new firearm
                            if (asMag.AttachableFireArm.Magazine == asMag)
                            {
                                asMag.AttachableFireArm.EjectMag(true);
                            }
                            else
                            {
                                for (int i = 0; i < asMag.AttachableFireArm.SecondaryMagazineSlots.Length; ++i)
                                {
                                    if (asMag.AttachableFireArm.SecondaryMagazineSlots[i].Magazine == asMag)
                                    {
                                        //TODO: When H3 adds support for secondary slots on attachable firearm uncomment the following:
                                        //asMag.AttachableFireArm.EjectSecondaryMagFromSlot(i, true);
                                        break;
                                    }
                                }
                            }
                            if (newData[newData.Length - 7] == 255)
                            {
                                asMag.Load(parentTrackedItemData.physicalItem.dataObject as FVRFireArm);
                            }
                            else
                            {
                                asMag.LoadIntoSecondary(parentTrackedItemData.physicalItem.dataObject as FVRFireArm, newData[newData.Length - 7]);
                            }
                            modified = true;
                        }
                        else
                        {
                            // Load into firearm
                            if (newData[newData.Length - 7] == 255)
                            {
                                asMag.Load(parentTrackedItemData.physicalItem.dataObject as FVRFireArm);
                            }
                            else
                            {
                                asMag.LoadIntoSecondary(parentTrackedItemData.physicalItem.dataObject as FVRFireArm, newData[newData.Length - 7]);
                            }
                            modified = true;
                        }
                    }
                }
            }
            else if (newData[newData.Length - 6] == 1)
            {
                if (data.parent != -1)
                {
                    H3MP_TrackedItemData parentTrackedItemData = null;
                    if (H3MP_ThreadManager.host)
                    {
                        parentTrackedItemData = H3MP_Server.items[data.parent];
                    }
                    else
                    {
                        parentTrackedItemData = H3MP_Client.items[data.parent];
                    }

                    if (parentTrackedItemData != null && parentTrackedItemData.physicalItem != null && parentTrackedItemData.physicalItem.dataObject is AttachableFirearmPhysicalObject)
                    {
                        // We want to be loaded in a AttachableFireArm, we have a parent, it is a AttachableFireArm
                        if (asMag.AttachableFireArm != null)
                        {
                            if (asMag.AttachableFireArm != (parentTrackedItemData.physicalItem.dataObject as AttachableFirearmPhysicalObject).FA)
                            {
                                // Unload from current, load into new AttachableFireArm
                                if (asMag.AttachableFireArm.Magazine == asMag)
                                {
                                    asMag.AttachableFireArm.EjectMag(true);
                                }
                                else
                                {
                                    for (int i = 0; i < asMag.AttachableFireArm.SecondaryMagazineSlots.Length; ++i)
                                    {
                                        if (asMag.AttachableFireArm.SecondaryMagazineSlots[i].Magazine == asMag)
                                        {
                                            //TODO: When H3 adds support for secondary slots on attachable firearm uncomment the following:
                                            //asMag.AttachableFireArm.EjectSecondaryMagFromSlot(i, true);
                                            break;
                                        }
                                    }
                                }
                                if (newData[newData.Length - 5] == 255)
                                {
                                    asMag.Load((parentTrackedItemData.physicalItem.dataObject as AttachableFirearmPhysicalObject).FA);
                                }
                                else
                                {
                                    //TODO: When H3 adds support for secondary slots on attachable firearm uncomment the following:
                                    //asMag.LoadIntoSecondary((parentTrackedItemData.physicalItem.dataObject as AttachableFirearmPhysicalObject).FA, newData[newData.Length - 1]);
                                }
                                modified = true;
                            }
                        }
                        else if (asMag.FireArm != null)
                        {
                            // Unload from current firearm, load into new AttachableFireArm
                            if (asMag.FireArm.Magazine == asMag)
                            {
                                asMag.FireArm.EjectMag(true);
                            }
                            else
                            {
                                for (int i = 0; i < asMag.FireArm.SecondaryMagazineSlots.Length; ++i)
                                {
                                    if (asMag.FireArm.SecondaryMagazineSlots[i].Magazine == asMag)
                                    {
                                        asMag.FireArm.EjectSecondaryMagFromSlot(i, true);
                                        break;
                                    }
                                }
                            }
                            if (newData[newData.Length - 5] == 255)
                            {
                                asMag.Load((parentTrackedItemData.physicalItem.dataObject as AttachableFirearmPhysicalObject).FA);
                            }
                            else
                            {
                                //TODO: When H3 adds support for secondary slots on attachable firearm uncomment the following:
                                //asMag.LoadIntoSecondary((parentTrackedItemData.physicalItem.dataObject as AttachableFirearmPhysicalObject).FA, newData[newData.Length - 1]);
                            }
                            modified = true;
                        }
                        else
                        {
                            // Load into AttachableFireArm
                            if (newData[newData.Length - 5] == 255)
                            {
                                asMag.Load((parentTrackedItemData.physicalItem.dataObject as AttachableFirearmPhysicalObject).FA);
                            }
                            else
                            {
                                //TODO: When H3 adds support for secondary slots on attachable firearm uncomment the following:
                                //asMag.LoadIntoSecondary((parentTrackedItemData.physicalItem.dataObject as AttachableFirearmPhysicalObject).FA, newData[newData.Length - 1]);
                            }
                            modified = true;
                        }
                    }
                }
            }
            else
            {
                if (asMag.FireArm != null)
                {
                    // Don't want to be loaded, but we are loaded, unload
                    if (asMag.FireArm.Magazine == asMag)
                    {
                        asMag.FireArm.EjectMag(true);
                    }
                    else
                    {
                        for (int i = 0; i < asMag.FireArm.SecondaryMagazineSlots.Length; ++i)
                        {
                            if (asMag.FireArm.SecondaryMagazineSlots[i].Magazine == asMag)
                            {
                                asMag.FireArm.EjectSecondaryMagFromSlot(i, true);
                                break;
                            }
                        }
                    }
                    modified = true;
                }
                else if(asMag.AttachableFireArm != null)
                {
                    if (asMag.AttachableFireArm.Magazine == asMag)
                    {
                        asMag.AttachableFireArm.EjectMag(true);
                    }
                    else
                    {
                        for (int i = 0; i < asMag.AttachableFireArm.SecondaryMagazineSlots.Length; ++i)
                        {
                            if (asMag.AttachableFireArm.SecondaryMagazineSlots[i].Magazine == asMag)
                            {
                                //TODO: When H3 adds support for secondary slots on attachable firearm uncomment the following:
                                //asMag.AttachableFireArm.EjectSecondaryMagFromSlot(i, true);
                                break;
                            }
                        }
                    }
                    modified = true;
                }
            }

            float preAmount = asMag.FuelAmountLeft;

            asMag.FuelAmountLeft = BitConverter.ToSingle(newData, newData.Length - 4);

            modified |= preAmount != asMag.FuelAmountLeft;

            data.data = newData;

            return modified;
        }

        private bool UpdateClip()
        {
            bool modified = false;
            FVRFireArmClip asClip = dataObject as FVRFireArmClip;

            int necessarySize = asClip.m_numRounds * 2 + 3;

            if (data.data == null || data.data.Length < necessarySize)
            {
                data.data = new byte[necessarySize];
                modified = true;
            }

            byte preval0 = data.data[0];
            byte preval1 = data.data[1];

            // Write count of loaded rounds
            BitConverter.GetBytes((short)asClip.m_numRounds).CopyTo(data.data, 0);

            modified |= (preval0 != data.data[0] || preval1 != data.data[1]);

            // Write loaded round classes
            for (int i = 0; i < asClip.m_numRounds; ++i)
            {
                preval0 = data.data[i * 2 + 2];
                preval1 = data.data[i * 2 + 3];

                BitConverter.GetBytes((short)asClip.LoadedRounds[i].LR_Class).CopyTo(data.data, i * 2 + 2);

                modified |= (preval0 != data.data[i * 2 + 2] || preval1 != data.data[i * 2 + 3]);
            }

            // Write loaded into firearm
            BitConverter.GetBytes(asClip.FireArm != null).CopyTo(data.data, necessarySize - 1);

            return modified;
        }

        private bool UpdateGivenClip(byte[] newData)
        {
            bool modified = false;
            FVRFireArmClip asClip = dataObject as FVRFireArmClip;

            if (data.data == null || data.data.Length != newData.Length)
            {
                modified = true;
            }

            int preRoundCount = asClip.m_numRounds;
            asClip.m_numRounds = 0;
            short numRounds = BitConverter.ToInt16(newData, 0);

            // Load rounds
            for (int i = 0; i < numRounds; ++i)
            {
                int first = i * 2 + 2;
                FireArmRoundClass newClass = (FireArmRoundClass)BitConverter.ToInt16(newData, first);
                if (asClip.LoadedRounds.Length > i && asClip.LoadedRounds[i] != null && newClass == asClip.LoadedRounds[i].LR_Class)
                {
                    ++asClip.m_numRounds;
                }
                else
                {
                    asClip.AddRound(newClass, false, false);
                    modified = true;
                }
            }

            modified |= preRoundCount != asClip.m_numRounds;

            if (modified)
            {
                asClip.UpdateBulletDisplay();
            }

            // Load into firearm if necessary
            if (BitConverter.ToBoolean(newData, newData.Length - 1))
            {
                if (data.parent != -1)
                {
                    H3MP_TrackedItemData parentTrackedItemData = null;
                    if (H3MP_ThreadManager.host)
                    {
                        parentTrackedItemData = H3MP_Server.items[data.parent];
                    }
                    else
                    {
                        parentTrackedItemData = H3MP_Client.items[data.parent];
                    }

                    if (parentTrackedItemData != null && parentTrackedItemData.physicalItem != null && parentTrackedItemData.physicalItem.dataObject is FVRFireArm)
                    {
                        // We want to be loaded in a firearm, we have a parent, it is a firearm
                        if (asClip.FireArm != null)
                        {
                            if (asClip.FireArm != parentTrackedItemData.physicalItem.dataObject)
                            {
                                // Unload from current, load into new firearm
                                asClip.FireArm.EjectClip();
                                asClip.Load(parentTrackedItemData.physicalItem.dataObject as FVRFireArm);
                                modified = true;
                            }
                        }
                        else
                        {
                            // Load into firearm
                            asClip.Load(parentTrackedItemData.physicalItem.dataObject as FVRFireArm);
                            modified = true;
                        }
                    }
                }
            }
            else if (asClip.FireArm != null)
            {
                // Don't want to be loaded, but we are loaded, unload
                asClip.FireArm.EjectClip();
                modified = true;
            }

            data.data = newData;

            return modified;
        }

        private bool UpdateSpeedloader()
        {
            bool modified = false;
            Speedloader asSpeedloader = dataObject as Speedloader;

            int necessarySize = asSpeedloader.Chambers.Count * 2 + 2;

            if (data.data == null || data.data.Length < necessarySize)
            {
                data.data = new byte[necessarySize];
                modified = true;
            }

            byte preval0;
            byte preval1;

            // Write loaded round classes (-1 for none)
            for (int i = 0; i < asSpeedloader.Chambers.Count; ++i)
            {
                preval0 = data.data[i * 2];
                preval1 = data.data[i * 2 + 1];

                if (asSpeedloader.Chambers[i].IsLoaded)
                {
                    BitConverter.GetBytes((short)asSpeedloader.Chambers[i].LoadedClass).CopyTo(data.data, i * 2);
                }
                else
                {
                    BitConverter.GetBytes((short)-1).CopyTo(data.data, i * 2);
                }

                modified |= (preval0 != data.data[i * 2] || preval1 != data.data[i * 2 + 1]);
            }

            return modified;
        }

        private bool UpdateGivenSpeedloader(byte[] newData)
        {
            bool modified = false;
            Speedloader asSpeedloader = dataObject as Speedloader;

            if (data.data == null || data.data.Length != newData.Length)
            {
                modified = true;
            }

            // Load rounds
            for (int i = 0; i < asSpeedloader.Chambers.Count; ++i)
            {
                int first = i * 2;
                short classIndex = BitConverter.ToInt16(newData, first);
                if (classIndex != -1 && (!asSpeedloader.Chambers[i].IsLoaded || (short)asSpeedloader.Chambers[i].LoadedClass != classIndex))
                {
                    FireArmRoundClass newClass = (FireArmRoundClass)classIndex;
                    asSpeedloader.Chambers[i].Load(newClass, false);
                }
                else if(classIndex == -1 && asSpeedloader.Chambers[i].IsLoaded)
                {
                    asSpeedloader.Chambers[i].Unload();
                }
            }

            data.data = newData;

            return modified;
        }
        #endregion

        private void FixedUpdate()
        {
            if (physicalObject != null && data.controller != H3MP_GameManager.ID && data.position != null && data.rotation != null)
            {
                if (data.previousPos != null && data.velocity.magnitude < 1f)
                {
                    if (data.parent == -1)
                    {
                        physicalObject.transform.position = Vector3.Lerp(physicalObject.transform.position, data.position + data.velocity, interpolationSpeed * Time.deltaTime);
                    }
                    else
                    {
                        physicalObject.transform.localPosition = Vector3.Lerp(physicalObject.transform.localPosition, data.position + data.velocity, interpolationSpeed * Time.deltaTime);
                    }
                }
                else
                {
                    if (data.parent == -1)
                    {
                        physicalObject.transform.position = data.position;
                    }
                    else
                    {
                        physicalObject.transform.localPosition = data.position;
                    }
                }
                if (data.parent == -1)
                {
                    physicalObject.transform.rotation = Quaternion.Lerp(physicalObject.transform.rotation, data.rotation, interpolationSpeed * Time.deltaTime);
                }
                else
                {
                    physicalObject.transform.localRotation = Quaternion.Lerp(physicalObject.transform.localRotation, data.rotation, interpolationSpeed * Time.deltaTime);
                }
            }
        }

        private void OnDestroy()
        {
            //tracked list so that when we get the tracked ID we can send the destruction to server and only then can we remove it from the list
            H3MP_GameManager.trackedItemByItem.Remove(physicalObject);
            if (physicalObject is SosigWeaponPlayerInterface)
            {
                H3MP_GameManager.trackedItemBySosigWeapon.Remove((physicalObject as SosigWeaponPlayerInterface).W);
            }

            if (H3MP_ThreadManager.host)
            {
                if (H3MP_GameManager.giveControlOfDestroyed)
                {
                    // We just want to give control of our items to another client (usually because leaving scene with other clients left inside)
                    if (data.controller == 0)
                    {
                        int otherPlayer = Mod.GetBestPotentialObjectHost(data.controller);

                        if (otherPlayer == -1)
                        {
                            // No one to give control of item to, destroy it
                            if (sendDestroy && skipDestroy == 0)
                            {
                                H3MP_ServerSend.DestroyItem(data.trackedID);
                            }
                            else if (!sendDestroy)
                            {
                                sendDestroy = true;
                            }

                            if (data.removeFromListOnDestroy && H3MP_Server.items[data.trackedID] != null)
                            {
                                H3MP_Server.items[data.trackedID] = null;
                                H3MP_Server.availableItemIndices.Add(data.trackedID);
                                H3MP_GameManager.itemsByInstanceByScene[data.scene][data.instance].Remove(data.trackedID);
                            }
                        }
                        else
                        {
                            H3MP_ServerSend.GiveControl(data.trackedID, otherPlayer);

                            // Also change controller locally
                            data.SetController(otherPlayer);
                        }
                    }
                }
                else
                {
                    if (sendDestroy && skipDestroy == 0)
                    {
                        H3MP_ServerSend.DestroyItem(data.trackedID);
                    }
                    else if (!sendDestroy)
                    {
                        sendDestroy = true;
                    }

                    if (data.removeFromListOnDestroy && H3MP_Server.items[data.trackedID] != null)
                    {
                        H3MP_Server.items[data.trackedID] = null;
                        H3MP_Server.availableItemIndices.Add(data.trackedID);
                        H3MP_GameManager.itemsByInstanceByScene[data.scene][data.instance].Remove(data.trackedID);
                    }
                }
                if (data.localTrackedID != -1)
                {
                    H3MP_GameManager.items[data.localTrackedID] = H3MP_GameManager.items[H3MP_GameManager.items.Count - 1];
                    H3MP_GameManager.items[data.localTrackedID].localTrackedID = data.localTrackedID;
                    H3MP_GameManager.items.RemoveAt(H3MP_GameManager.items.Count - 1);
                    data.localTrackedID = -1;
                }
            }
            else
            {
                bool removeFromLocal = true;
                if (H3MP_GameManager.giveControlOfDestroyed)
                {
                    if (data.controller == H3MP_Client.singleton.ID)
                    {
                        int otherPlayer = Mod.GetBestPotentialObjectHost(data.controller);

                        if (otherPlayer == -1)
                        {
                            if (sendDestroy && skipDestroy == 0)
                            {
                                if (data.trackedID == -1)
                                {
                                    if (!unknownDestroyTrackedIDs.Contains(data.localTrackedID))
                                    {
                                        unknownDestroyTrackedIDs.Add(data.localTrackedID);
                                    }

                                    // We want to keep it in local until we give destruction order
                                    removeFromLocal = false;
                                }
                                else
                                {
                                    H3MP_ClientSend.DestroyItem(data.trackedID);

                                    H3MP_Client.items[data.trackedID] = null;
                                    H3MP_GameManager.itemsByInstanceByScene[data.scene][data.instance].Remove(data.trackedID);
                                }
                            }
                            else if (!sendDestroy)
                            {
                                sendDestroy = true;
                            }

                            if (data.removeFromListOnDestroy && data.trackedID != -1)
                            {
                                H3MP_Client.items[data.trackedID] = null;
                                H3MP_GameManager.itemsByInstanceByScene[data.scene][data.instance].Remove(data.trackedID);
                            }
                        }
                        else
                        {
                            if (data.trackedID == -1)
                            {
                                if (unknownControlTrackedIDs.ContainsKey(data.localTrackedID))
                                {
                                    unknownControlTrackedIDs[data.localTrackedID] = otherPlayer;
                                }
                                else
                                {
                                    unknownControlTrackedIDs.Add(data.localTrackedID, otherPlayer);
                                }

                                // We want to keep it in local until we give control
                                removeFromLocal = false;
                            }
                            else
                            {
                                H3MP_ClientSend.GiveControl(data.trackedID, otherPlayer);

                                // Also change controller locally
                                data.SetController(otherPlayer);
                            }
                        }
                    }
                }
                else
                {
                    if (sendDestroy && skipDestroy == 0)
                    {
                        if (data.trackedID == -1)
                        {
                            if (!unknownDestroyTrackedIDs.Contains(data.localTrackedID))
                            {
                                unknownDestroyTrackedIDs.Add(data.localTrackedID);
                            }

                            // We want to keep it in local until we give destruction order
                            removeFromLocal = false;
                        }
                        else
                        {
                            H3MP_ClientSend.DestroyItem(data.trackedID);

                            if (data.removeFromListOnDestroy)
                            {
                                H3MP_Client.items[data.trackedID] = null;
                                H3MP_GameManager.itemsByInstanceByScene[data.scene][data.instance].Remove(data.trackedID);
                            }
                        }
                    }
                    else if (!sendDestroy)
                    {
                        sendDestroy = true;
                    }

                    if (data.removeFromListOnDestroy && data.trackedID != -1)
                    {
                        H3MP_Client.items[data.trackedID] = null;
                        H3MP_GameManager.itemsByInstanceByScene[data.scene][data.instance].Remove(data.trackedID);
                    }
                }
                if (removeFromLocal && data.localTrackedID != -1)
                {
                    data.RemoveFromLocal();
                }
            }

            data.removeFromListOnDestroy = true;
        }

        private void OnTransformParentChanged()
        {
            if (data.ignoreParentChanged > 0)
            {
                return;
            }

            if (data.controller == H3MP_GameManager.ID)
            {
                Transform currentParent = transform.parent;
                H3MP_TrackedItem parentTrackedItem = null;
                while (currentParent != null)
                {
                    parentTrackedItem = currentParent.GetComponent<H3MP_TrackedItem>();
                    if (parentTrackedItem != null)
                    {
                        break;
                    }
                    currentParent = currentParent.parent;
                }
                if (parentTrackedItem != null)
                {
                    // Handle case of unknown tracked IDs
                    //      If ours is not yet known, put our local tracked ID in a wait dict with value as parent's LOCAL tracked ID if it is under our control
                    //      and the actual tracked ID if not, when we receive the tracked ID we set the parent
                    //          Note that if the parent is under our control, we need to store the local tracked ID because we might not have its tracked ID yet either
                    //          If it is not under our control then we have guarantee that is has a tracked ID
                    //      If the parent's tracked ID is not yet known, put it in a wait dict where key is the local tracked ID of the parent,
                    //      and the value is a list of all children that must be attached to this parent once we know the parent's tracked ID
                    //          Note that if we do not know the parent's tracked ID, it is because it is under our control
                    bool haveParentID = parentTrackedItem.data.trackedID != -1;
                    if (data.trackedID == -1)
                    {
                        KeyValuePair<int, bool> parentIDPair = new KeyValuePair<int, bool>(haveParentID ? parentTrackedItem.data.trackedID : parentTrackedItem.data.localTrackedID, haveParentID);
                        if (unknownTrackedIDs.ContainsKey(data.localTrackedID))
                        {
                            unknownTrackedIDs[data.localTrackedID] = parentIDPair;
                        }
                        else
                        {
                            unknownTrackedIDs.Add(data.localTrackedID, parentIDPair);
                        }
                    }
                    else
                    {
                        if(haveParentID)
                        {
                            if (parentTrackedItem.data.trackedID != data.parent)
                            {
                                // We have a parent trackedItem and it is new
                                // Update other clients
                                if (H3MP_ThreadManager.host)
                                {
                                    H3MP_ServerSend.ItemParent(data.trackedID, parentTrackedItem.data.trackedID);
                                }
                                else
                                {
                                    H3MP_ClientSend.ItemParent(data.trackedID, parentTrackedItem.data.trackedID);
                                }

                                // Update local
                                data.SetParent(parentTrackedItem.data, false);
                            }
                        }
                        else
                        {
                            if (unknownParentTrackedIDs.ContainsKey(parentTrackedItem.data.localTrackedID))
                            {
                                unknownParentTrackedIDs[parentTrackedItem.data.localTrackedID].Add(data.trackedID);
                            }
                            else
                            {
                                unknownParentTrackedIDs.Add(parentTrackedItem.data.localTrackedID, new List<int>() { data.trackedID });
                            }
                        }
                    }
                }
                else if (data.parent != -1)
                {
                    if (data.trackedID == -1)
                    {
                        KeyValuePair<int, bool> parentIDPair = new KeyValuePair<int, bool>(-1, false);
                        if (unknownTrackedIDs.ContainsKey(data.localTrackedID))
                        {
                            unknownTrackedIDs[data.localTrackedID] = parentIDPair;
                        }
                        else
                        {
                            unknownTrackedIDs.Add(data.localTrackedID, parentIDPair);
                        }
                    }
                    else
                    {
                        // We were detached from current parent
                        // Update other clients
                        if (H3MP_ThreadManager.host)
                        {
                            H3MP_ServerSend.ItemParent(data.trackedID, -1);
                        }
                        else
                        {
                            H3MP_ClientSend.ItemParent(data.trackedID, -1);
                        }

                        // Update locally
                        data.SetParent(null, false);
                    }
                }
            }
        }
    }
}
