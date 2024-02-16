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

        public struct IntersectStruct
        {
            public float distanceAtIntersect1;
            public Int32 timeToIntersect1;
            public float velocityAtIntersect1;
            public float distanceAtIntersect2;
            public Int32 timeToIntersect2;
            public float velocityAtIntersect2;

            public IntersectStruct(bool negative)
            {
                if(negative)
                {
                    distanceAtIntersect1 = -1;
                    timeToIntersect1 = -1;
                    velocityAtIntersect1 = -1;
                    distanceAtIntersect2 = -1;
                    timeToIntersect2 = -1;
                    velocityAtIntersect2 = -1;
                }
            }
        }

        private IntersectMarker intersect1 = new IntersectMarker();
        private IntersectMarker intersect2 = new IntersectMarker();
        private TargetStruct myTargetInfo;
        private IntersectStruct myIntersectInfo = new IntersectStruct();

        private EventDataObsolete<byte, object> targetChannel;
        private EventDataObsolete<byte, object> intersectChannel;

        bool resendIntersects;

        private bool ProviderActive;

        public void Start()
        {
            ProviderActive = false;

            SimpitPlugin.AddToDeviceHandler(TargetProvider);
            SimpitPlugin.AddToDeviceHandler(IntersectProvider);
            targetChannel = GameEvents.FindEvent<EventDataObsolete<byte, object>>("toSerial" + OutboundPackets.TargetInfo);
            intersectChannel = GameEvents.FindEvent<EventDataObsolete<byte, object>>("toSerial" + OutboundPackets.Intersects);
            GameEvents.FindEvent<EventDataObsolete<byte, object>>("onSerialChannelForceSend" + OutboundPackets.Intersects).Add(ForceSendingIntersects);
        }

        public void Update()
        {
            // We only need to register as a device handler if
            // there's an active target. So we keep a watch on
            // targets and add/remove ourselves as required.
            // Calling the provider once on removing the handler
            // to ensure that a message with default data gets sent.
            VesselComponent simVessel = null;
            try { simVessel = Vehicle.ActiveSimVessel; } catch { }
            if (simVessel == null) return;

            if (simVessel.TargetObject != null)
            {
                if (!ProviderActive)
                {
                    SimpitPlugin.AddToDeviceHandler(TargetProvider);
                    SimpitPlugin.AddToDeviceHandler(IntersectProvider);
                    ProviderActive = true;
                }
            }
            else
            {
                if (ProviderActive)
                {
                    SimpitPlugin.RemoveToDeviceHandler(TargetProvider);
                    TargetProvider();
                    SimpitPlugin.RemoveToDeviceHandler(IntersectProvider);
                    IntersectProvider();
                    ProviderActive = false;
                }
            }
        }

        public void OnDestroy()
        {
            SimpitPlugin.RemoveToDeviceHandler(TargetProvider);
            SimpitPlugin.RemoveToDeviceHandler(IntersectProvider);
        }

        public void TargetProvider()
        {
            VesselComponent simVessel = null;
            try { simVessel = Vehicle.ActiveSimVessel; } catch { }
            if (simVessel == null) return;

            TargetStruct oldTargetStruct = myTargetInfo;
            myTargetInfo = new TargetStruct();

            if (simVessel.TargetObject != null)
            {
                //myTargetInfo.distance = (float)Position.Distance(simVessel.TargetObject.Position, simVessel.CenterOfMass);
                myTargetInfo.distance = (float)Position.Distance(simVessel.TargetObject.transform.Position, simVessel.transform.Position);
                myTargetInfo.velocity = (float)simVessel.TargetVelocity.magnitude;

                myTargetInfo.heading = 0;
                myTargetInfo.pitch = 0;
                myTargetInfo.velocityHeading = 0;
                myTargetInfo.velocityPitch = 0;

                //The following code was adapted from the KSP1 Simpit, but it doesn't work properly with KSP2
                /*
                Vector3d targetDirection = Position.Delta(simVessel.TargetObject.transform.Position, simVessel.transform.Position).vector;
                KerbalSimpitTelemetryProvider.WorldVecToNavHeading(simVessel, targetDirection, out myTargetInfo.heading, out myTargetInfo.pitch);

                KerbalSimpitTelemetryProvider.WorldVecToNavHeading(simVessel, simVessel.TargetVelocity.vector, out myTargetInfo.velocityHeading, out myTargetInfo.velocityPitch);
                */
                
            }
            if (!myTargetInfo.Equals(oldTargetStruct))
            {
                if (targetChannel != null) targetChannel.Fire(OutboundPackets.TargetInfo, myTargetInfo);
            }
        }

        public void IntersectProvider()
        {
            try
            {
                VesselComponent simVessel = null;
                try { simVessel = Vehicle.ActiveSimVessel; } catch { }
                if (simVessel == null) return;

                IntersectStruct oldIntersectStruct = myIntersectInfo;
                myIntersectInfo = new IntersectStruct(true);
                if (simVessel.TargetObject != null)
                {
                    OrbitTargeter targeter = simVessel.Orbiter.OrbitTargeter;

                    intersect1 = targeter.Intersect1Orbiter;
                    intersect2 = targeter.Intersect2Orbiter;

                    if (intersect1.IsValid)
                    {
                        myIntersectInfo.distanceAtIntersect1 = (float)intersect1.RelativeDistance;
                        myIntersectInfo.velocityAtIntersect1 = (float)intersect1.RelativeSpeed;
                        myIntersectInfo.timeToIntersect1 = (Int32)(intersect1.UniversalTime - simVessel._universeModel.UniverseTime);
                    }
                    if (intersect2.IsValid)
                    {
                        myIntersectInfo.distanceAtIntersect2 = (float)intersect2.RelativeDistance;
                        myIntersectInfo.velocityAtIntersect2 = (float)intersect2.RelativeSpeed;
                        myIntersectInfo.timeToIntersect2 = (Int32)(intersect2.UniversalTime - simVessel._universeModel.UniverseTime);
                    }
                }

                if (!myIntersectInfo.Equals(oldIntersectStruct) || resendIntersects)
                {
                    resendIntersects = false;
                    if (intersectChannel != null) intersectChannel.Fire(OutboundPackets.Intersects, myIntersectInfo);
                    //SimpitPlugin.Instance.loggingQueueInfo.Enqueue(String.Format("d1 {0:0}, v1 {1:0}, t1 {2:0}     d2 {3:0}, v2 {4:0}, t2 {5:0}", myIntersectInfo.distanceAtIntersect1, myIntersectInfo.velocityAtIntersect1, myIntersectInfo.timeToIntersect1, myIntersectInfo.distanceAtIntersect2, myIntersectInfo.velocityAtIntersect2, myIntersectInfo.timeToIntersect2));
                }
                else
                {
                    //SimpitPlugin.Instance.loggingQueueInfo.Enqueue("No update to intersects");
                }
            }
            catch (Exception e) { SimpitPlugin.Instance.loggingQueueError.Enqueue("Exception updating intersects: " + e.Message + "\n" + e.StackTrace); }
        }

        void ForceSendingIntersects(byte ID, object Data)
        {
            resendIntersects = true;
        }
    }
}