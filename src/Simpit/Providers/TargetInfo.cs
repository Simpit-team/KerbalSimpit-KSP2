using UnityEngine;
using KSP.Sim.impl;
using SpaceWarp.API.Game;
using KSP.Sim;
using KSP.Game;

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
                
                OrbitTargeter targeter = simVessel.Orbiter.OrbitTargeter;
                PatchedConicsOrbit orbiterPatchForTarget = GetOrbiterPatchForTarget(targeter);
                PatchedConicsOrbit targetPatch = GetTargetPatch(targeter, orbiterPatchForTarget);
                UpdateIntersects(orbiterPatchForTarget, targetPatch, targeter._targetObject.IsVessel);
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

        //Based on KSP.Sim.impl.OrbitTargeter.GetOrbiterPatchForTarget()
        private PatchedConicsOrbit GetOrbiterPatchForTarget(OrbitTargeter targeter)
        {
            List<PatchedConicsOrbit> patchList;
            int numPatchesAhead = targeter._orbiter.PatchedConicSolver.PatchesAhead;
            int patchIndex = 0;
            patchList = targeter._orbiter.PatchedConicSolver.CurrentTrajectory;
            
            PatchedConicsOrbit orbiterPatchForTarget;
            if (targeter._targetObject.CelestialBody != null)
            {
                if (targeter._targetObject.CelestialBody == patchList[patchIndex].referenceBody)
                {
                    for (int index = patchIndex; index < numPatchesAhead; ++index)
                    {
                        if (targeter._targetObject.CelestialBody != patchList[index].referenceBody)
                        {
                            patchIndex = index;
                            break;
                        }
                    }
                }
                if (patchIndex > 0 && targeter._targetObject.CelestialBody == patchList[patchIndex - 1].referenceBody)
                {
                    for (int index = patchIndex - 1; index >= 0; --index)
                    {
                        if (targeter._targetObject.CelestialBody != patchList[index].referenceBody)
                        {
                            numPatchesAhead = index;
                            break;
                        }
                    }
                }
                while (numPatchesAhead > patchIndex && targeter._targetObject.CelestialBody == patchList[numPatchesAhead].referenceBody)
                    --numPatchesAhead;
                orbiterPatchForTarget = targeter.FindPatch(patchList, numPatchesAhead, patchIndex, targeter._targetObject.Orbiter.PatchedConicsOrbit.referenceBody, targeter._targetObject.CelestialBody);
            }
            else
                orbiterPatchForTarget = targeter._targetObject.Vessel == null ? patchList[numPatchesAhead] : targeter.FindPatch(patchList, numPatchesAhead, patchIndex, targeter._targetObject.Orbiter.PatchedConicsOrbit.referenceBody);
            return orbiterPatchForTarget;
        }

        //Based on KSP.Sim.impl.OrbitTargeter.GetTargetPatch()
        private PatchedConicsOrbit GetTargetPatch(OrbitTargeter targeter, PatchedConicsOrbit orbiterPatch)
        {
            if (targeter._targetObject.Vessel != null && targeter._targetObject.Vessel.SimulationObject.Orbiter.PatchedConicSolver != null)
            {
                for (int index = 0; index <= targeter._targetObject.Vessel.SimulationObject.Orbiter.PatchedConicSolver.PatchesAhead; ++index)
                {
                    PatchedConicsOrbit targetPatch = targeter._targetObject.Vessel.SimulationObject.Orbiter.PatchedConicSolver.CurrentTrajectory[index];
                    if (targetPatch.referenceBody == orbiterPatch.referenceBody)
                        return targetPatch;
                }
            }
            return targeter._targetObject.Orbiter.PatchedConicsOrbit;
        }

        //Based on KSP.Sim.impl.OrbitTargeter.UpdateIntersectNodes()
        private void UpdateIntersects(PatchedConicsOrbit orbiterPatch, PatchedConicsOrbit targetPatch, bool targetObjectIsVessel)
        {
            if (orbiterPatch.referenceBody == targetPatch.referenceBody && orbiterPatch.PeApIntersects(targetPatch, 10000.0))
            {
                double CD = 0.0;
                double CCD = 0.0;
                double trueAnomaly1 = 0.0;
                double num1 = 0.0;
                double trueAnomaly2 = 0.0;
                double num2 = 0.0;
                int iterations = 0;
                int closestPoints = orbiterPatch.FindClosestPoints(targetPatch, ref CD, ref CCD, ref trueAnomaly1, ref num1, ref trueAnomaly2, ref num2, 0.0001, 20, ref iterations);
                double utforTrueAnomaly1 = orbiterPatch.GetUTforTrueAnomaly(trueAnomaly1, 0.0);
                double utforTrueAnomaly2 = orbiterPatch.GetUTforTrueAnomaly(trueAnomaly2, 0.0);
                if (utforTrueAnomaly1 > utforTrueAnomaly2)
                {
                    UtilMath.SwapValues(ref utforTrueAnomaly1, ref utforTrueAnomaly2);
                    UtilMath.SwapValues(ref trueAnomaly1, ref trueAnomaly2);
                    UtilMath.SwapValues(ref num1, ref num2);
                }
                double num3 = orbiterPatch.period * (double)(int)((GameManager.Instance.Game.UniverseModel.UniverseTime - utforTrueAnomaly1) / orbiterPatch.period + 1.0);
                double universalTime1 = utforTrueAnomaly1 + num3;
                double universalTime2 = utforTrueAnomaly2 + num3;
                if (PatchedConics.IsUniversalTimeWithinPatchBounds(universalTime1, orbiterPatch) && (!targetObjectIsVessel || PatchedConics.IsUniversalTimeWithinPatchBounds(universalTime1, targetPatch)))
                {
                    double separationDistance = OrbitTargeter.GetSeparationDistance(orbiterPatch, targetPatch, universalTime1);
                    double relativeSpeed = OrbitTargeter.GetRelativeSpeed(orbiterPatch, targetPatch, universalTime1);
                    intersect1.Set(universalTime1, null, separationDistance, relativeSpeed, true, null);
                }
                else
                {
                    intersect1.SetInvalid();
                }
                if (closestPoints > 1)
                {
                    if (PatchedConics.IsUniversalTimeWithinPatchBounds(universalTime2, orbiterPatch) && (!targetObjectIsVessel || PatchedConics.IsUniversalTimeWithinPatchBounds(universalTime2, targetPatch)))
                    {
                        double separationDistance = OrbitTargeter.GetSeparationDistance(orbiterPatch, targetPatch, universalTime2);
                        double relativeSpeed = OrbitTargeter.GetRelativeSpeed(orbiterPatch, targetPatch, universalTime2);
                        intersect2.Set(universalTime2, null, separationDistance, relativeSpeed, true, null);
                    }
                    else
                    {
                        intersect2.SetInvalid();
                    }
                }
                else
                {
                    intersect2.SetInvalid();
                }
            }
            else
            {
                intersect1.SetInvalid();
                intersect2.SetInvalid();
            }
        }

        void ForceSendingIntersects(byte ID, object Data)
        {
            resendIntersects = true;
        }
    }
}