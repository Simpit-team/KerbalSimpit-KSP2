using KSP.Game;
using KSP.Messages.PropertyWatchers;
using KSP.Sim.DeltaV;
using KSP.Sim.impl;
using KSP.Sim.Maneuver;
using SpaceWarp.API.Game;
using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace Simpit.Providers
{
    public class KerbalSimpitTelemetryProvider : MonoBehaviour
    {
        [StructLayout(LayoutKind.Sequential, Pack=1)][Serializable]
        public struct AltitudeStruct
        {
            public float alt;
            public float surfalt;
        }

        [StructLayout(LayoutKind.Sequential, Pack=1)][Serializable]
        public struct ApsidesStruct
        {
            public float periapsis;
            public float apoapsis;
        }

        [StructLayout(LayoutKind.Sequential, Pack=1)][Serializable]
        public struct ApsidesTimeStruct
        {
            public int periapsis;
            public int apoapsis;
        }

        [StructLayout(LayoutKind.Sequential, Pack=1)][Serializable]
        public struct VelocityStruct
        {
            public float orbital;
            public float surface;
            public float vertical;
        }

        [StructLayout(LayoutKind.Sequential, Pack=1)][Serializable]
        public struct RotationStruct
        {
            public float heading;
            public float pitch;
            public float roll;
            public float orbitalVelocityHeading;
            public float orbitalVelocityPitch;
            public float surfaceVelocityHeading;
            public float surfaceVelocityPitch;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        [Serializable]
        public struct OrbitInfoStruct
        {
            public float eccentricity;
            public float semiMajorAxis;
            public float inclination;
            public float longAscendingNode;
            public float argPeriapsis;
            public float trueAnomaly;
            public float meanAnomaly;
            public float period;
        }

        [StructLayout(LayoutKind.Sequential, Pack=1)][Serializable]
        public struct AirspeedStruct
        {
            public float IAS;
            public float MachNumber;
            public float gForces;
        }

        public struct ManeuverStruct
        {
            public float timeToNextManeuver;
            public float deltaVNextManeuver;
            public float durationNextManeuver;
            public float deltaVTotal;
            public float headingNextManeuver;
            public float pitchNextManeuver;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)][Serializable]
        public struct DeltaVStruct
        {
            public float stageDeltaV;
            public float totalDeltaV;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)][Serializable]
        public struct DeltaVEnvStruct
        {
            public float stageDeltaVASL;
            public float totalDeltaVASL;
            public float stageDeltaVVac;
            public float totalDeltaVVac;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)][Serializable]
        public struct BurnTimeStruct
        {
            public float stageBurnTime;
            public float totalBurnTime;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        [Serializable]
        public struct TempLimitStruct
        {
            public byte tempLimitPercentage;
            public byte skinTempLimitPercentage;
        }

        private AltitudeStruct myAlt;
        private ApsidesStruct myApsides;
        private ApsidesTimeStruct myApsidesTime;
        private VelocityStruct myVelocity;
        private RotationStruct myRotation;
        private AirspeedStruct myAirspeed;
        private ManeuverStruct myManeuver;
        private DeltaVStruct myDeltaVStruct;
        private DeltaVEnvStruct myDeltaVEnvStruct;
        private BurnTimeStruct myBurnTimeStruct;
        private OrbitInfoStruct myOrbitInfoStruct;
        private TempLimitStruct myTempLimitStruct;

        private EventDataObsolete<byte, object> altitudeChannel, apsidesChannel,
            apsidesTimeChannel, ortbitInfoChannel, velocityChannel, airspeedChannel,
            maneuverChannel, rotationChannel, deltaVChannel, deltaVEnvChannel, burnTimeChannel, tempLimitChannel;

        public void Start()
        {
            SimpitPlugin.AddToDeviceHandler(AltitudeProvider);
            altitudeChannel = GameEvents.FindEvent<EventDataObsolete<byte, object>>("toSerial" + OutboundPackets.Altitude);
            SimpitPlugin.AddToDeviceHandler(ApsidesProvider);
            apsidesChannel = GameEvents.FindEvent<EventDataObsolete<byte, object>>("toSerial" + OutboundPackets.Apsides);
            SimpitPlugin.AddToDeviceHandler(ApsidesTimeProvider);
            apsidesTimeChannel = GameEvents.FindEvent<EventDataObsolete<byte, object>>("toSerial" + OutboundPackets.ApsidesTime);
            SimpitPlugin.AddToDeviceHandler(OrbitInfoProvider);
            ortbitInfoChannel = GameEvents.FindEvent<EventDataObsolete<byte, object>>("toSerial" + OutboundPackets.OrbitInfo);
            SimpitPlugin.AddToDeviceHandler(VelocityProvider);
            maneuverChannel = GameEvents.FindEvent<EventDataObsolete<byte, object>>("toSerial" + OutboundPackets.ManeuverData);
            SimpitPlugin.AddToDeviceHandler(ManeuverProvider);
            velocityChannel = GameEvents.FindEvent<EventDataObsolete<byte, object>>("toSerial" + OutboundPackets.Velocities);
            SimpitPlugin.AddToDeviceHandler(RotationProvider);
            rotationChannel = GameEvents.FindEvent<EventDataObsolete<byte, object>>("toSerial" + OutboundPackets.RotationData);
            SimpitPlugin.AddToDeviceHandler(DeltaVProvider);
            deltaVChannel = GameEvents.FindEvent<EventDataObsolete<byte, object>>("toSerial" + OutboundPackets.DeltaV);
            SimpitPlugin.AddToDeviceHandler(DeltaVEnvProvider);
            deltaVEnvChannel = GameEvents.FindEvent<EventDataObsolete<byte, object>>("toSerial" + OutboundPackets.DeltaVEnv);
            SimpitPlugin.AddToDeviceHandler(BurnTimeProvider);
            burnTimeChannel = GameEvents.FindEvent<EventDataObsolete<byte, object>>("toSerial" + OutboundPackets.BurnTime);
            SimpitPlugin.AddToDeviceHandler(AirspeedProvider);
            airspeedChannel = GameEvents.FindEvent<EventDataObsolete<byte, object>>("toSerial" + OutboundPackets.Airspeed);
            SimpitPlugin.AddToDeviceHandler(TempLimitProvider);
            tempLimitChannel = GameEvents.FindEvent<EventDataObsolete<byte, object>>("toSerial" + OutboundPackets.TempLimit);
        }

        public void OnDestroy()
        {
            SimpitPlugin.RemoveToDeviceHandler(AltitudeProvider);
            SimpitPlugin.RemoveToDeviceHandler(ApsidesProvider);
            SimpitPlugin.RemoveToDeviceHandler(ApsidesTimeProvider);
            SimpitPlugin.RemoveToDeviceHandler(OrbitInfoProvider);
            SimpitPlugin.RemoveToDeviceHandler(VelocityProvider);
            SimpitPlugin.RemoveToDeviceHandler(RotationProvider);
            SimpitPlugin.RemoveToDeviceHandler(AirspeedProvider);
            SimpitPlugin.RemoveToDeviceHandler(ManeuverProvider);
            SimpitPlugin.RemoveToDeviceHandler(DeltaVProvider);
            SimpitPlugin.RemoveToDeviceHandler(DeltaVEnvProvider);
            SimpitPlugin.RemoveToDeviceHandler(BurnTimeProvider);
            SimpitPlugin.RemoveToDeviceHandler(TempLimitProvider);
        }

        public void AltitudeProvider()
        {
            VesselComponent simVessel = null;
            try { simVessel = Vehicle.ActiveSimVessel; } catch { }
            if (simVessel == null) return;

            myAlt.alt = (float)simVessel.AltitudeFromSeaLevel;
            myAlt.surfalt = (float)simVessel.AltitudeFromSurface;
            if (altitudeChannel != null) altitudeChannel.Fire(OutboundPackets.Altitude, myAlt);
        }

        public void ApsidesProvider()
        {
            VesselComponent simVessel = null;
            try { simVessel = Vehicle.ActiveSimVessel; } catch { }
            if (simVessel == null) return;

            myApsides.apoapsis = (float)(simVessel.Orbit.Apoapsis - simVessel.mainBody.radius);
            myApsides.periapsis = (float)(simVessel.Orbit.Periapsis - simVessel.mainBody.radius);
            if (apsidesChannel != null) apsidesChannel.Fire(OutboundPackets.Apsides, myApsides);
        }

        public void ApsidesTimeProvider()
        {
            VesselComponent simVessel = null;
            try { simVessel = Vehicle.ActiveSimVessel; } catch { }
            if (simVessel == null) return;

            myApsidesTime.apoapsis = (int)simVessel.Orbit.TimeToAp;
            myApsidesTime.periapsis = (int)simVessel.Orbit.TimeToPe;
            if (apsidesTimeChannel != null) apsidesTimeChannel.Fire(OutboundPackets.ApsidesTime, myApsidesTime);
        }

        public void VelocityProvider()
        {
            VesselComponent simVessel = null;
            try { simVessel = Vehicle.ActiveSimVessel; } catch { }
            if (simVessel == null) return;

            myVelocity.orbital = (float)simVessel.OrbitalSpeed;
            myVelocity.surface = (float)simVessel.SrfSpeedMagnitude;
            myVelocity.vertical = (float)simVessel.VerticalSrfSpeed;
            if (velocityChannel != null) velocityChannel.Fire(OutboundPackets.Velocities, myVelocity);
        }

        // Convert a direction given in world space v into a heading and a pitch, relative to the vessel passed as a paramater
        public static void WorldVecToNavHeading(VesselComponent activeVessel, Vector3d v, out float heading, out float pitch)
        {
            KSP.Sim.Position CoM = activeVessel.CenterOfMass;
            Vector3d north, up, east;
            up = (CoM - activeVessel.mainBody.Position).vector.normalized;
            north = Vector3d.Exclude(up, ((activeVessel.mainBody.Position + activeVessel.mainBody.transform.up * (float)activeVessel.mainBody.radius) - CoM).vector).normalized;
            east = Vector3d.Cross(up, north);

            // Code from KSPIO to do angle conversions : https://github.com/zitron-git/KSPSerialIO/blob/062d97e892077ea14737f5e79268c0c4d067f5b6/KSPSerialIO/KSPIO.cs#L1301-L1313
            pitch = (float)-((Vector3d.Angle(up, v)) - 90.0f);
            Vector3d progradeFlat = Vector3d.Exclude(up, v);
            float NAngle = (float)Vector3d.Angle(north, progradeFlat);
            float EAngle = (float)Vector3d.Angle(east, progradeFlat);
            if (EAngle < 90)
                heading = NAngle;
            else
                heading = -NAngle + 360;
        }
        
        public void RotationProvider()
        {
            VesselComponent simVessel = null;
            try { simVessel = Vehicle.ActiveSimVessel; } catch { }
            if (simVessel == null) return;


            // Code from KSPIO to compute angles and velocities https://github.com/zitron-git/KSPSerialIO/blob/062d97e892077ea14737f5e79268c0c4d067f5b6/KSPSerialIO/KSPIO.cs#L929-L971
            KSP.Sim.Position CoM = simVessel.CenterOfMass;
            Vector3d north, up, east;
            up = (CoM - simVessel.mainBody.Position).vector.normalized;
            north = Vector3d.Exclude(up, ((simVessel.mainBody.Position + simVessel.mainBody.transform.up * (float)simVessel.mainBody.radius) - CoM).vector).normalized;
            east = Vector3d.Cross(up, north);

            Vector3d attitude = Quaternion.Inverse(Quaternion.Euler(90, 0, 0) * Quaternion.Inverse(Vehicle.ActiveVesselVehicle.Rotation.localRotation) * Quaternion.LookRotation(north, up)).eulerAngles;
            
            myRotation.roll = (float) ((attitude.z > 180) ? (attitude.z - 360.0) : attitude.z);
            myRotation.pitch = (float) ((attitude.x > 180) ? (360.0 - attitude.x) : -attitude.x);
            myRotation.heading = (float) attitude.y;

            WorldVecToNavHeading(simVessel, simVessel.SurfaceVelocity.vector.normalized, out myRotation.surfaceVelocityHeading, out myRotation.surfaceVelocityPitch);

            if (rotationChannel != null) rotationChannel.Fire(OutboundPackets.RotationData, myRotation);
        }

            public void OrbitInfoProvider()
        {
            VesselComponent simVessel = null;
            try { simVessel = Vehicle.ActiveSimVessel; } catch { }
            if (simVessel == null) return;


            PatchedConicsOrbit currentOrbit = simVessel.Orbit;
            myOrbitInfoStruct.eccentricity = (float) currentOrbit.eccentricity;
            myOrbitInfoStruct.semiMajorAxis = (float)currentOrbit.semiMajorAxis;
            myOrbitInfoStruct.inclination = (float)currentOrbit.inclination;
            myOrbitInfoStruct.longAscendingNode = (float)currentOrbit.longitudeOfAscendingNode;
            myOrbitInfoStruct.argPeriapsis = (float)currentOrbit.argumentOfPeriapsis;
            myOrbitInfoStruct.trueAnomaly = (float)currentOrbit.TrueAnomaly;
            myOrbitInfoStruct.meanAnomaly = (float)currentOrbit.MeanAnomaly;
            myOrbitInfoStruct.period = (float)currentOrbit.period;
            if (ortbitInfoChannel != null) ortbitInfoChannel.Fire(OutboundPackets.OrbitInfo, myOrbitInfoStruct);
        }

        public void AirspeedProvider()
        {
            VesselComponent simVessel = null;
            try { simVessel = Vehicle.ActiveSimVessel; } catch { }
            if (simVessel == null) return;

            myAirspeed.IAS = Mathf.Sqrt((2 * (float)simVessel.DynamicPressure_kPa * (10000f/9.80665f)) / (float)simVessel.mainBody.atmosphereDensityASL);
            SimpitGui.SetDebugText("IAS " + myAirspeed.IAS+ "\n    SFS " + simVessel.SurfaceVelocity.magnitude);    
            myAirspeed.MachNumber = (float)simVessel.MachNumber;
            myAirspeed.gForces = (float)simVessel.geeForce;
            if (airspeedChannel != null) airspeedChannel.Fire(OutboundPackets.Airspeed, myAirspeed);
        }

        public void TempLimitProvider()
        {
            VesselComponent simVessel = null;
            try { simVessel = Vehicle.ActiveSimVessel; } catch { }
            if (simVessel == null) return;

            double maxTempPercentage = 0.0;
            double maxSkinTempPercentage = 0.0;

            
            // Iterate on a copy ?
            foreach (PartComponent part in simVessel.SimulationObject.PartOwner.Parts)
            {
                maxTempPercentage = Math.Max(maxTempPercentage, 100.0 * part.Temperature / part.MaxTemp);
                maxSkinTempPercentage = 0; //I think there is no skin tempereature in KSP2, or is there?
            }

            //Prevent the byte to overflow in case of extremely hot vessel
            if (maxTempPercentage > 255) maxTempPercentage = 255;
            if (maxSkinTempPercentage > 255) maxSkinTempPercentage = 255;

            myTempLimitStruct.tempLimitPercentage = (byte)Math.Round(maxTempPercentage);
            myTempLimitStruct.skinTempLimitPercentage = (byte)Math.Round(maxSkinTempPercentage);

            if (tempLimitChannel != null) tempLimitChannel.Fire(OutboundPackets.TempLimit, myTempLimitStruct);
        }

        //Return the DeltaVStageInfo of the first stage to consider for deltaV and burn time computation
        //KSP1: Can return null when no deltaV is available (for instance in EVA).
        private DeltaVStageInfo getCurrentStageDeltaV()
        {
            VesselComponent simVessel = null;
            try { simVessel = Vehicle.ActiveSimVessel; } catch { }
            if (simVessel == null) return null;

            VesselDeltaVComponent vesselDeltaV = simVessel.VesselDeltaV;

            if (vesselDeltaV == null)
            {
                return null; // KSP1: This happen in EVA for instance.
            }
            DeltaVStageInfo currentStageInfo = null;

            try
            {
                if(DeltaVExtensions.CurrentStage(vesselDeltaV) == vesselDeltaV.StageInfo.Count) 
                // KSP1: if (FlightGlobals.ActiveVessel.currentStage == FlightGlobals.ActiveVessel.VesselDeltaV.OperatingStageInfo.Count)
                {
                    // KSP1: Rocket has not taken off, use first stage with deltaV (to avoid stage of only stabilizer)
                    for (int i = vesselDeltaV.StageInfo.Count - 1; i >= 0; i--)
                    {
                        currentStageInfo = vesselDeltaV.GetStage(i);
                        if (currentStageInfo.DeltaVActual > 0)
                        {
                            break;
                        }
                    }
                }
                else
                {
                    currentStageInfo = vesselDeltaV.GetStage(DeltaVExtensions.CurrentStage(vesselDeltaV));
                }
            }
            catch (NullReferenceException)
            {
                // KSP1: This happens when reverting a flight.
                // KSP1: FlightGlobals.ActiveVessel.VesselDeltaV.OperatingStageInfo is not null but using it produce a
                // KSP1: NullReferenceException in KSP code. This is probably due to the fact that the rocket is not fully initialized.
            }
            return currentStageInfo;
        }

        public void ManeuverProvider()
        {
            VesselComponent simVessel = null;
            try { simVessel = Vehicle.ActiveSimVessel; } catch { }
            if (simVessel == null) return;

            myManeuver.timeToNextManeuver = 0.0f;
            myManeuver.deltaVNextManeuver = 0.0f;
            myManeuver.durationNextManeuver = 0.0f;
            myManeuver.deltaVTotal = 0.0f;
            myManeuver.headingNextManeuver = 0.0f;
            myManeuver.pitchNextManeuver = 0.0f;

            ManeuverPlanSolver mps = simVessel.Orbiter.ManeuverPlanSolver;
            if(mps != null)
            //KSP1 if (FlightGlobals.ActiveVessel.patchedConicSolver != null)
            {
                List<ManeuverNodeData> maneuvers = simVessel.Orbiter.SimulationObject.ManeuverPlan.GetNodes();
                if (maneuvers != null)
                //KSP1 if (FlightGlobals.ActiveVessel.patchedConicSolver.maneuverNodes != null)
                {
                    if(maneuvers.Count > 0)
                    {
                        double ut = GameManager.Instance.Game.SpaceSimulation.UniverseModel.UniverseTime;
                        myManeuver.timeToNextManeuver = (float)(maneuvers[0].Time - ut);
                        myManeuver.deltaVNextManeuver = (float)maneuvers[0].BurnRequiredDV;

                        WorldVecToNavHeading(simVessel, maneuvers[0].BurnVector, out myManeuver.headingNextManeuver, out myManeuver.pitchNextManeuver);

                        DeltaVStageInfo currentStageInfo = getCurrentStageDeltaV();
                        if (currentStageInfo != null)
                        {
                            //KSP1 Old method, use a simple crossmultiplication to compute the estimated burn time based on the current stage only
                            //KSP1 myManeuver.durationNextManeuver = (float)(maneuvers[0].DeltaV.magnitude * currentStageInfo.stageBurnTime) / currentStageInfo.deltaVActual;

                            //KSP1 The estimation based on the startBurnIn seems to be more accurate than using the previous method of crossmultiplication
                            //KSP1 myManeuver.durationNextManeuver = (float)((maneuvers[0].Time - ut - maneuvers[0].startBurnIn) * 2);
                            myManeuver.durationNextManeuver = (float)maneuvers[0].BurnDuration;
                        }

                        foreach (ManeuverNodeData maneuver in maneuvers)
                        {
                            myManeuver.deltaVTotal += (float)maneuver.BurnRequiredDV;
                        }
                    }
                }
            }
            if (maneuverChannel != null) maneuverChannel.Fire(OutboundPackets.ManeuverData, myManeuver);
        }

        public void DeltaVProvider()
        {
            DeltaVStageInfo currentStageInfo = getCurrentStageDeltaV();
            
            if (currentStageInfo != null)
            {
                myDeltaVStruct.stageDeltaV = (float)currentStageInfo.DeltaVActual;
                myDeltaVStruct.totalDeltaV = (float)Vehicle.ActiveSimVessel.VesselDeltaV.TotalDeltaVActual;
            }
            else
            {
                myDeltaVStruct.stageDeltaV = 0;
                myDeltaVStruct.totalDeltaV = 0;
            }

            if (deltaVChannel != null) deltaVChannel.Fire(OutboundPackets.DeltaV, myDeltaVStruct);
        }

        public void DeltaVEnvProvider()
        {
            DeltaVStageInfo currentStageInfo = getCurrentStageDeltaV();

            if (currentStageInfo != null)
            {

                myDeltaVEnvStruct.stageDeltaVASL = (float)currentStageInfo.DeltaVatASL;
                myDeltaVEnvStruct.stageDeltaVVac = (float)currentStageInfo.DeltaVinVac;
                myDeltaVEnvStruct.totalDeltaVASL = (float)Vehicle.ActiveSimVessel.VesselDeltaV.TotalDeltaVASL;
                myDeltaVEnvStruct.totalDeltaVVac = (float)Vehicle.ActiveSimVessel.VesselDeltaV.TotalDeltaVVac;
            }
            else
            {
                myDeltaVEnvStruct.stageDeltaVASL = 0;
                myDeltaVEnvStruct.stageDeltaVVac = 0;
                myDeltaVEnvStruct.totalDeltaVASL = 0;
                myDeltaVEnvStruct.totalDeltaVVac = 0;
            }

            if (deltaVEnvChannel != null) deltaVEnvChannel.Fire(OutboundPackets.DeltaVEnv, myDeltaVEnvStruct);
        }

        public void BurnTimeProvider()
        {
            DeltaVStageInfo currentStageInfo = getCurrentStageDeltaV();

            if (currentStageInfo != null)
            {
                myBurnTimeStruct.stageBurnTime = (float)currentStageInfo.StageBurnTime;
                myBurnTimeStruct.totalBurnTime = (float)Vehicle.ActiveSimVessel.VesselDeltaV.TotalBurnTime;
            }
            else
            {
                myBurnTimeStruct.stageBurnTime = 0;
                myBurnTimeStruct.totalBurnTime = 0;
            }

            if (burnTimeChannel != null) burnTimeChannel.Fire(OutboundPackets.BurnTime, myBurnTimeStruct);
        }
    }
}
