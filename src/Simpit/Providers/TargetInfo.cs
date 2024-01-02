using System;
using KSP.IO;
using Simpit.Providers;
using Simpit;
using UnityEngine;
using KSP.Sim.impl;
using SpaceWarp.API.Game;
using KSP.Sim;

namespace Simpit.Providers
{
    public class KerbalSimpitTargetProvider : MonoBehaviour
    {
        public struct TargetStruct
        {
            public float distance;
            public float velocity;
            public float heading;
            public float pitch;
            public float velocityHeading;
            public float velocityPitch;
        }

        private TargetStruct myTargetInfo;

        private EventDataObsolete<byte, object> targetChannel;

        private bool ProviderActive;

        public void Start()
        {
            ProviderActive = false;

            SimpitPlugin.AddToDeviceHandler(TargetProvider);
            targetChannel = GameEvents.FindEvent<EventDataObsolete<byte, object>>("toSerial" + OutboundPackets.TargetInfo);
        }

        public void Update()
        {
            // We only need to register as a device handler if
            // there's an active target. So we keep a watch on
            // targets and add/remove ourselves as required.
            VesselComponent simVessel = null;
            try { simVessel = Vehicle.ActiveSimVessel; } catch { }
            if (simVessel == null) return;

            if (simVessel.TargetObject != null)
            {
                if (!ProviderActive)
                {
                    SimpitPlugin.AddToDeviceHandler(TargetProvider);
                    ProviderActive = true;
                }
            }
            else
            {
                if (ProviderActive)
                {
                    SimpitPlugin.RemoveToDeviceHandler(TargetProvider);
                    ProviderActive = false;
                }
            }
        }

        public void OnDestroy()
        {
            SimpitPlugin.RemoveToDeviceHandler(TargetProvider);
        }

        public void TargetProvider()
        {
            VesselComponent simVessel = null;
            try { simVessel = Vehicle.ActiveSimVessel; } catch { }
            if (simVessel == null) return;

            try
            {
                if (simVessel.TargetObject != null)
                {
                    //if (FlightGlobals.fetch.VesselTarget.GetTransform() != null && FlightGlobals.ActiveVessel.transform != null)
                    //{
                        myTargetInfo.distance = (float)KSP.Sim.Position.Distance(simVessel.TargetObject.Position, simVessel.CenterOfMass);
                        myTargetInfo.velocity = (float)simVessel.TargetVelocity.magnitude;

                        Vector3d targetDirection = (simVessel.TargetObject.Position - simVessel.CenterOfMass).vector;
                        KerbalSimpitTelemetryProvider.WorldVecToNavHeading(simVessel, targetDirection, out myTargetInfo.heading, out myTargetInfo.pitch);

                        KerbalSimpitTelemetryProvider.WorldVecToNavHeading(simVessel, simVessel.TargetVelocity.vector, out myTargetInfo.velocityHeading, out myTargetInfo.velocityPitch);
                        if (targetChannel != null) targetChannel.Fire(OutboundPackets.TargetInfo, myTargetInfo);
                    //}
                }
            }
            catch (NullReferenceException e)
            {
                // Several issues where caused when a target was not set or when switching vessels and some data is set but not all data needed.
                // This catch prevent the provider from stopping to work, but we should investigate if it is still triggered somehow
                SimpitPlugin.Instance.Logger.LogInfo("Exception raised in TargetProvider");
                SimpitPlugin.Instance.Logger.LogInfo(e.Message);
                SimpitPlugin.Instance.Logger.LogInfo(e.StackTrace);
            }
        }
    }
}