﻿using KSP.Sim.impl;
using KSP.Sim.State;
using KSP.Sim;
using System.Runtime.InteropServices;
using UnityEngine;
using Simpit.Utilities;
using SpaceWarp.API.Game;
using KSP.Game;
using KSP.Messages;
using HarmonyLib;
using KSP;

namespace Simpit.Providers
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    [Serializable]
    public struct RotationalStruct
    {
        public short pitch;
        public short roll;
        public short yaw;
        public byte mask;
    }
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    [Serializable]
    public struct TranslationalStruct
    {
        public short X;
        public short Y;
        public short Z;
        public byte mask;
    }
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    [Serializable]
    public struct WheelStruct
    {
        public short steer;
        public short throttle;
        public byte mask;
    }
    /*
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    [Serializable]
    public struct CustomAxixStruct
    {
        public short custom1;
        public short custom2;
        public short custom3;
        public short custom4;
        public byte mask;
    }
    */
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    [Serializable]
    public struct ThrottleStruct
    {
        public short throttle;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    [Serializable]
    public struct SASModeInfoStruct
    {
        public byte currentSASMode;
        public ushort SASModeAvailability;
    }

    public class KerbalSimpitAxisController : MonoBehaviour
    {
        // Inbound messages
        private EventDataObsolete<byte, object> RotationChannel, TranslationChannel,
            WheelChannel, ThrottleChannel, SASInfoChannel, AutopilotChannel; // , CustomAxisChannel

        private RotationalStruct myRotation, newRotation;

        private TranslationalStruct myTranslation, newTranslation;

        private WheelStruct myWheel, newWheel;

        /*
        private CustomAxixStruct myCustomAxis, newCustomAxis;
        */

        private SASModeInfoStruct mySASInfo, newSASInfo;
        private bool _forceSendingSASInfo = false;

        private short myThrottle;
        private bool lastThrottleSentIsZero = true;

        private AutopilotMode mySASMode;

        private VesselComponent lastActiveVessel;
        
        public FlightCtrlState lastFlightCtrlState = new FlightCtrlState();

        public void Start()
        {
            RotationChannel = GameEvents.FindEvent<EventDataObsolete<byte, object>>("onSerialReceived" + InboundPackets.VesselRotation);
            if (RotationChannel != null) RotationChannel.Add(vesselRotationCallback);
            TranslationChannel = GameEvents.FindEvent<EventDataObsolete<byte, object>>("onSerialReceived" + InboundPackets.VesselTranslation);
            if (TranslationChannel != null) TranslationChannel.Add(vesselTranslationCallback);
            WheelChannel = GameEvents.FindEvent<EventDataObsolete<byte, object>>("onSerialReceived" + InboundPackets.WheelControl);
            if (WheelChannel != null) WheelChannel.Add(wheelCallback);
            ThrottleChannel = GameEvents.FindEvent<EventDataObsolete<byte, object>>("onSerialReceived" + InboundPackets.VesselThrottle);
            if (ThrottleChannel != null) ThrottleChannel.Add(throttleCallback);
            /*
            CustomAxisChannel = GameEvents.FindEvent<EventDataObsolete<byte, object>>("onSerialReceived" + InboundPackets.VesselCustomAxis);
            if (CustomAxisChannel != null) CustomAxisChannel.Add(customAxisCallback);
            */
            AutopilotChannel = GameEvents.FindEvent<EventDataObsolete<byte, object>>("onSerialReceived" + InboundPackets.AutopilotMode);
            if (AutopilotChannel != null) AutopilotChannel.Add(autopilotModeCallback);

            SASInfoChannel = GameEvents.FindEvent<EventDataObsolete<byte, object>>("toSerial" + OutboundPackets.SASInfo);
            GameEvents.FindEvent<EventDataObsolete<byte, object>>("onSerialChannelForceSend" + OutboundPackets.SASInfo).Add(forceSendingSASInfo);

            SimpitPlugin.AddToDeviceHandler(SASInfoProvider);

            mySASInfo.currentSASMode = 255; // value for not enabled
            mySASInfo.SASModeAvailability = 0;

            try { lastActiveVessel = Vehicle.ActiveSimVessel; } catch { }
            
            if(lastActiveVessel != null) lastActiveVessel.Autopilot._vesselView.OnAutopilotUpdate += AutopilotUpdater;
            GameManager.Instance.Game.Messages.Subscribe<VesselChangedMessage>(new Action<MessageCenterMessage>(OnVesselChange));
        }
        public void OnDestroy()
        {
            if (RotationChannel != null) RotationChannel.Remove(vesselRotationCallback);
            if (TranslationChannel != null) TranslationChannel.Remove(vesselTranslationCallback);
            if (WheelChannel != null) WheelChannel.Remove(wheelCallback);
            if (ThrottleChannel != null) ThrottleChannel.Remove(throttleCallback);

            /*
            if (CustomAxisChannel != null) CustomAxisChannel.Remove(customAxisCallback);
            */
            if (AutopilotChannel != null) AutopilotChannel.Remove(autopilotModeCallback);
            SimpitPlugin.RemoveToDeviceHandler(SASInfoProvider);

            if (lastActiveVessel != null && lastActiveVessel.Autopilot != null && lastActiveVessel.Autopilot._vesselView != null) lastActiveVessel.Autopilot._vesselView.OnAutopilotUpdate -= AutopilotUpdater;
            GameManager.Instance.Game.Messages.Unsubscribe<VesselChangedMessage>(OnVesselChange);
        }

        public void OnVesselChange(MessageCenterMessage msg)
        {
            if (!(msg is VesselChangedMessage vesselMessage))
                return;

            if (lastActiveVessel != null && lastActiveVessel.Autopilot != null && lastActiveVessel.Autopilot._vesselView != null) lastActiveVessel.Autopilot._vesselView.OnAutopilotUpdate -= AutopilotUpdater;
            try { lastActiveVessel = Vehicle.ActiveSimVessel; } catch { }
            if (lastActiveVessel != null) lastActiveVessel.Autopilot._vesselView.OnAutopilotUpdate += AutopilotUpdater;
        }
        
        public void vesselRotationCallback(byte ID, object Data)
        {

            newRotation = KerbalSimpitUtils.ByteArrayToStructure<RotationalStruct>((byte[])Data);
            // Bit fields:
            // pitch = 1
            // roll = 2
            // yaw = 4
            if ((newRotation.mask & (byte)1) > 0) myRotation.pitch = newRotation.pitch;
            if ((newRotation.mask & (byte)2) > 0) myRotation.roll = newRotation.roll;
            if ((newRotation.mask & (byte)4) > 0) myRotation.yaw = newRotation.yaw;
        }

        public void vesselTranslationCallback(byte ID, object Data)
        {
            newTranslation = KerbalSimpitUtils.ByteArrayToStructure<TranslationalStruct>((byte[])Data);
            // Bit fields:
            // X = 1
            // Y = 2
            // Z = 4
            if ((newTranslation.mask & (byte)1) > 0) myTranslation.X = newTranslation.X; 
            if ((newTranslation.mask & (byte)2) > 0) myTranslation.Y = newTranslation.Y;
            if ((newTranslation.mask & (byte)4) > 0) myTranslation.Z = newTranslation.Z;
        }

        public void wheelCallback(byte ID, object Data)
        {
            newWheel = KerbalSimpitUtils.ByteArrayToStructure<WheelStruct>((byte[])Data);
            // Bit fields
            // steer = 1
            // throttle = 2
            if ((newWheel.mask & (byte)1) > 0) myWheel.steer = newWheel.steer;
            if ((newWheel.mask & (byte)2) > 0) myWheel.throttle = newWheel.throttle;
        }

        /*
        public void customAxisCallback(byte ID, object Data)
        {
            newCustomAxis = KerbalSimpitUtils.ByteArrayToStructure<CustomAxixStruct>((byte[])Data);

            if ((newCustomAxis.mask & (byte)1) > 0) myCustomAxis.custom1 = newCustomAxis.custom1;
            if ((newCustomAxis.mask & (byte)2) > 0) myCustomAxis.custom2 = newCustomAxis.custom2;
            if ((newCustomAxis.mask & (byte)4) > 0) myCustomAxis.custom3 = newCustomAxis.custom3;
            if ((newCustomAxis.mask & (byte)8) > 0) myCustomAxis.custom4 = newCustomAxis.custom4;
        }
        */

        public void throttleCallback(byte ID, object Data)
        {
            myThrottle = BitConverter.ToInt16((byte[])Data, 0);
        }

        public void autopilotModeCallback(byte ID, object Data)
        {
            byte[] payload = (byte[])Data;
            
            VesselComponent simVessel = null;
            try { simVessel = Vehicle.ActiveSimVessel; }
            catch { }
            if (simVessel == null) return;

            VesselAutopilot autopilot = simVessel.Autopilot;

            if (autopilot == null)
            {
                SimpitPlugin.Instance.loggingQueueInfo.Enqueue("Ignoring a SAS MODE Message since I could not find the autopilot");
                return;
            }

            mySASMode = (AutopilotMode)(payload[0]);

            if (autopilot.CanSetMode(mySASMode))
            {
                autopilot.SetMode(mySASMode);
                if (SimpitPlugin.Instance.config_verbose) 
                {
                    SimpitPlugin.Instance.loggingQueueInfo.Enqueue(String.Format("SAS mode is {0} (payload was {1})", autopilot.AutopilotMode.ToString(), (int)payload[0]));
                }
            }
            else
            {
                SimpitPlugin.Instance.loggingQueueInfo.Enqueue(String.Format("Unable to set SAS mode to {0}", mySASMode.ToString()));
            }
        }

        public void AutopilotUpdater(ref FlightCtrlState fcs, float deltaTime)
        {
            if (myRotation.pitch != 0) fcs.pitch = (float)myRotation.pitch / Int16.MaxValue;
            if (myRotation.roll != 0) fcs.roll = (float)myRotation.roll / Int16.MaxValue;
            if (myRotation.yaw != 0) fcs.yaw = (float)myRotation.yaw / Int16.MaxValue;
            
            if (myTranslation.X != 0) fcs.X = ((float)myTranslation.X / Int16.MaxValue);
            if (myTranslation.Y != 0) fcs.Y = ((float)myTranslation.Y / Int16.MaxValue);
            if (myTranslation.Z != 0) fcs.Z = ((float)myTranslation.Z / Int16.MaxValue);
            
            if (myWheel.steer != 0) fcs.wheelSteer = (float)myWheel.steer / Int16.MaxValue;
            if (myWheel.throttle != 0) fcs.wheelThrottle = (float)myWheel.throttle / Int16.MaxValue;
            
            if (myThrottle != 0 || !lastThrottleSentIsZero)
            {
                // Throttle seems to be handled differently than the other axis since when no value is set, a zero is assumed. For throttle, no value set mean the last one is used.
                // So we need to send a 0 first before stopping to send values.
                fcs.mainThrottle = (float)myThrottle / Int16.MaxValue;
                lastThrottleSentIsZero = (myThrottle == 0);
            }
            /*
            if (myCustomAxis.custom1 != 0) axisGroupModule.UpdateAxisGroup(KSPAxisGroup.Custom01, (float)myCustomAxis.custom1 / Int16.MaxValue);
            if (myCustomAxis.custom2 != 0) axisGroupModule.UpdateAxisGroup(KSPAxisGroup.Custom02, (float)myCustomAxis.custom2 / Int16.MaxValue);
            if (myCustomAxis.custom3 != 0) axisGroupModule.UpdateAxisGroup(KSPAxisGroup.Custom03, (float)myCustomAxis.custom3 / Int16.MaxValue);
            if (myCustomAxis.custom4 != 0) axisGroupModule.UpdateAxisGroup(KSPAxisGroup.Custom04, (float)myCustomAxis.custom4 / Int16.MaxValue);
            */
            // Store the last flight command to send them in the dedicated channels
            lastFlightCtrlState = new FlightCtrlState(fcs);
            //currentVessel.AtomicSet(fcsi);
            //simVessel.ApplyFlightCtrlState(fcsi);
        }
        
        [HarmonyPatch(typeof(EVAInputHandler), "FixedUpdate")]
        public class EVAInputHandlerPatch
        {
            public static KerbalSimpitAxisController simpitAxisController;
            static bool Prefix()
            {
                //SimpitPlugin.Instance.loggingQueueDebug.Enqueue("EVAInputHandlerPatch.FixedUpdate");

                bool runOriginalFixedUpdate = true;
                bool translationHasInput = false;
                bool rotationHasInput = false;
                if (GameManager.Instance.Game.ViewController == null) return runOriginalFixedUpdate;
                EVAInputHandler thisEvaInputHandler = GameManager.Instance.Game.ViewController.evaInputHandler;
                if (thisEvaInputHandler == null) return runOriginalFixedUpdate;

                if (simpitAxisController.myRotation.pitch != 0) rotationHasInput = true;
                if (simpitAxisController.myRotation.roll != 0) rotationHasInput = true;
                if (simpitAxisController.myRotation.yaw != 0) rotationHasInput = true;
                if (simpitAxisController.myTranslation.X != 0) translationHasInput = true;
                if (simpitAxisController.myTranslation.Y != 0) translationHasInput = true;
                if (simpitAxisController.myTranslation.Z != 0) translationHasInput = true;
                
                if(!translationHasInput && !rotationHasInput) return runOriginalFixedUpdate; //No inputs from Simpit -> Do original FixedUpdate
                runOriginalFixedUpdate = false; 

                FlightCtrlState oldfcs = thisEvaInputHandler._flightCtrlState;
                byte playerId = GameManager.Instance.Game.LocalPlayer.PlayerId;
                ViewController viewController = thisEvaInputHandler.Game.ViewController;
                IVehicle activeVehicle;
                if (viewController == null || !viewController.TryGetActiveVehicle(out activeVehicle, true) || !activeVehicle.CanPlayerControl(playerId))
                    return runOriginalFixedUpdate;
                VesselComponent simVessel = activeVehicle.GetSimVessel();
                KerbalComponent kerbal = simVessel.SimulationObject.Kerbal;
                KerbalBehavior behaviorIfLoaded = thisEvaInputHandler.Game.ViewController.GetBehaviorIfLoaded(kerbal);
                if (!simVessel.IsKerbalEVA || kerbal == null || !(behaviorIfLoaded != null))
                    return runOriginalFixedUpdate;
                bool flag = !viewController.TimeWarp.IsWarping || viewController.TimeWarp.IsPhysicsTimeWarp;
                if ((!(!viewController.IsPaused & flag) ? 0 : (behaviorIfLoaded.IsInputAllowed() ? 1 : 0)) != 0)
                {
                    //Match the ship translation axes (that's why y and z are swapped here)
                    if (translationHasInput)
                    {
                        thisEvaInputHandler._flightCtrlState.X = ((float)simpitAxisController.myTranslation.X / Int16.MaxValue);
                        thisEvaInputHandler._flightCtrlState.Y = ((float)simpitAxisController.myTranslation.Z / Int16.MaxValue);
                        thisEvaInputHandler._flightCtrlState.Z = (-(float)simpitAxisController.myTranslation.Y / Int16.MaxValue);
                    }
                    thisEvaInputHandler._flightCtrlState.inputYaw = thisEvaInputHandler._moveStrafeLeftRight;
                    thisEvaInputHandler._flightCtrlState.inputPitch = 0.0f;
                    thisEvaInputHandler._flightCtrlState.inputRoll = 0.0f;
                    if (rotationHasInput)
                    {
                        thisEvaInputHandler._flightCtrlState.pitch = (float)simpitAxisController.myRotation.pitch / Int16.MaxValue;
                        thisEvaInputHandler._flightCtrlState.roll  = (float)simpitAxisController.myRotation.roll  / Int16.MaxValue;
                        thisEvaInputHandler._flightCtrlState.yaw   = (float)simpitAxisController.myRotation.yaw   / Int16.MaxValue;
                    }
                    thisEvaInputHandler._flightCtrlState.gearUp = thisEvaInputHandler._run;
                    thisEvaInputHandler._flightCtrlState.stage = thisEvaInputHandler._jump;
                    /* Original content in the FixedUpdate
                    thisEvaInputHandler._flightCtrlState.X = thisEvaInputHandler._moveLeftRight;
                    thisEvaInputHandler._flightCtrlState.Y = thisEvaInputHandler._moveUpDown;
                    thisEvaInputHandler._flightCtrlState.Z = thisEvaInputHandler._moveFrontBack;
                    thisEvaInputHandler._flightCtrlState.inputYaw = thisEvaInputHandler._moveStrafeLeftRight;
                    thisEvaInputHandler._flightCtrlState.inputPitch = 0.0f;
                    thisEvaInputHandler._flightCtrlState.inputRoll = 0.0f;
                    thisEvaInputHandler._flightCtrlState.yaw = thisEvaInputHandler._rotateYaw;
                    thisEvaInputHandler._flightCtrlState.pitch = thisEvaInputHandler._rotatePitch;
                    thisEvaInputHandler._flightCtrlState.roll = thisEvaInputHandler._rotateRoll;
                    thisEvaInputHandler._flightCtrlState.gearUp = thisEvaInputHandler._run;
                    thisEvaInputHandler._flightCtrlState.stage = thisEvaInputHandler._jump;
                    */
                }
                else
                    thisEvaInputHandler.ResetInput((VesselComponent)null);
                if (thisEvaInputHandler._flightCtrlState != oldfcs)
                {
                    simVessel.SetFlightControlState(thisEvaInputHandler._flightCtrlState);
                    // Store the last flight command to send them in the dedicated channels
                    simpitAxisController.lastFlightCtrlState = new FlightCtrlState(thisEvaInputHandler._flightCtrlState);
                }
                else
                    thisEvaInputHandler._flightCtrlState = simVessel.flightCtrlState;

                return runOriginalFixedUpdate;
            }
        }

        public void forceSendingSASInfo(byte ID, object Data)
        {
            _forceSendingSASInfo = true;
        }

        public void SASInfoProvider()
        {
            VesselComponent simVessel = null;
            try
            {
                simVessel = Vehicle.ActiveSimVessel;
            }
            catch { }
            if (simVessel == null) return;

            VesselAutopilot autopilot = simVessel.Autopilot;

            if (autopilot == null) return;

            if (autopilot.Enabled)
            {
                newSASInfo.currentSASMode = (byte)autopilot.AutopilotMode;
            }
            else
            {
                newSASInfo.currentSASMode = 255; //special value to indicate a disabled SAS
            }

            newSASInfo.SASModeAvailability = 0;
            foreach (AutopilotMode i in Enum.GetValues(typeof(AutopilotMode)))
            {
                if (autopilot.CanSetMode(i))
                {
                    newSASInfo.SASModeAvailability = (ushort)(newSASInfo.SASModeAvailability | (1 << (byte)i));
                }
            }

            if (mySASInfo.currentSASMode != newSASInfo.currentSASMode || mySASInfo.SASModeAvailability != newSASInfo.SASModeAvailability || _forceSendingSASInfo)
            {
                if (SASInfoChannel != null)
                {
                    mySASInfo = newSASInfo;
                    SASInfoChannel.Fire(OutboundPackets.SASInfo, mySASInfo);
                }
                _forceSendingSASInfo = false;
            }
        }
    }

    public class RotationCommandProvider : GenericProvider<RotationalStruct>
    {
        private KerbalSimpitAxisController controller = null;
        RotationCommandProvider() : base(OutboundPackets.RotationCmd) { }

        public override void Start()
        {
            base.Start();
            controller = (KerbalSimpitAxisController)FindObjectOfType(typeof(KerbalSimpitAxisController));
        }

        protected override bool updateMessage(ref RotationalStruct message)
        {
            if (controller != null)
            {
                message.pitch = (short)(controller.lastFlightCtrlState.pitch * Int16.MaxValue);
                message.yaw = (short)(controller.lastFlightCtrlState.yaw * Int16.MaxValue);
                message.roll = (short)(controller.lastFlightCtrlState.roll * Int16.MaxValue);
            }
            else
            {
                SimpitPlugin.Instance.loggingQueueInfo.Enqueue("KerbalSimpitAxisController is not found");
            }

            return false;
        }
    }

    public class TranslationCommandProvider : GenericProvider<TranslationalStruct>
    {
        private KerbalSimpitAxisController controller = null;
        TranslationCommandProvider() : base(OutboundPackets.TranslationCmd) { }

        public override void Start()
        {
            base.Start();
            controller = (KerbalSimpitAxisController)FindObjectOfType(typeof(KerbalSimpitAxisController));
        }

        protected override bool updateMessage(ref TranslationalStruct message)
        {
            if (controller != null)
            {
                message.X = (short)(controller.lastFlightCtrlState.X * Int16.MaxValue);
                message.Y = (short)(controller.lastFlightCtrlState.Y * Int16.MaxValue);
                message.Z = (short)(controller.lastFlightCtrlState.Z * Int16.MaxValue);
            }
            else
            {
                SimpitPlugin.Instance.loggingQueueInfo.Enqueue("KerbalSimpitAxisController is not found");
            }

            return false;
        }
    }

    public class WheelCommandProvider : GenericProvider<WheelStruct>
    {
        private KerbalSimpitAxisController controller = null;
        WheelCommandProvider() : base(OutboundPackets.WheelCmd) { }

        public override void Start()
        {
            base.Start();
            controller = (KerbalSimpitAxisController)FindObjectOfType(typeof(KerbalSimpitAxisController));
        }

        protected override bool updateMessage(ref WheelStruct message)
        {
            if (controller != null)
            {
                message.steer = (short)(controller.lastFlightCtrlState.wheelSteer * Int16.MaxValue);
                message.throttle = (short)(controller.lastFlightCtrlState.wheelThrottle * Int16.MaxValue);
            }
            else
            {
                SimpitPlugin.Instance.loggingQueueInfo.Enqueue("KerbalSimpitAxisController is not found");
            }

            return false;
        }
    }

    public class ThrottleCommandProvider : GenericProvider<ThrottleStruct>
    {
        private KerbalSimpitAxisController controller = null;
        ThrottleCommandProvider() : base(OutboundPackets.ThrottleCmd) { }

        public override void Start()
        {
            base.Start();
            controller = (KerbalSimpitAxisController)FindObjectOfType(typeof(KerbalSimpitAxisController));
        }

        protected override bool updateMessage(ref ThrottleStruct message)
        {
            if (controller != null)
            {
                message.throttle = (short)(controller.lastFlightCtrlState.mainThrottle * Int16.MaxValue);
            }
            else
            {
                SimpitPlugin.Instance.loggingQueueInfo.Enqueue("KerbalSimpitAxisController is not found");
            }

            return false;
        }
    }
}
