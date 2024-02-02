using UnityEngine;
using System.Runtime.InteropServices;
using KSP.Sim.impl;
using Simpit.Utilities;
using KSP.Game;
using KSP.Sim.Maneuver;
using SpaceWarp.API.Game;
using KSP.Sim;

namespace Simpit.Providers
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    [Serializable]
    struct TimewarpToStruct
    {
        public byte instant; // In the TimewarpToValues enum
        public float delay; // negative for warping before the instant
    }

    class KerbalSimpitWarpControl : MonoBehaviour
    {
        // Inbound messages
        private EventDataObsolete<byte, object> WarpChannel, TimewarpToChannel;

        private const bool USE_INSTANT_WARP = false;

        public void Start()
        {
            WarpChannel = GameEvents.FindEvent<EventDataObsolete<byte, object>>("onSerialReceived" + InboundPackets.WarpChange);
            if (WarpChannel != null) WarpChannel.Add(WarpCommandCallback);
            TimewarpToChannel = GameEvents.FindEvent<EventDataObsolete<byte, object>>("onSerialReceived" + InboundPackets.TimewarpTo);
            if (TimewarpToChannel != null) TimewarpToChannel.Add(TimewarpToChannelCommandCallback);
        }

        public void OnDestroy()
        {
            if (WarpChannel != null) WarpChannel.Remove(WarpCommandCallback);
            if (TimewarpToChannel != null) WarpChannel.Remove(TimewarpToChannelCommandCallback);
        }

        public void WarpCommandCallback(byte ID, object Data)
        {
            byte[] payload = (byte[])Data;
            byte command = payload[0];
            ProcessWarpCommand(command);
        }

        public void TimewarpToChannelCommandCallback(byte ID, object Data)
        {
            TimewarpToStruct command = KerbalSimpitUtils.ByteArrayToStructure<TimewarpToStruct>((byte[])Data);
            ProcessTimewarpToCommand(command);
        }

        public void SetWarpRate(int rateIndex)
        {
            UniverseDataProvider udp = GameManager.Instance.Game.ViewController.DataProvider.UniverseDataProvider;
            if (rateIndex <= udp.MaxTimeRateIndex.GetValue())
            {
                udp.SetTimeRateIndex(rateIndex);
            }
            else
            {
                SimpitPlugin.Instance.loggingQueueInfo.Enqueue("Cannot find a warp speed at index: " + rateIndex);
            }
        }

        public void ProcessWarpCommand(byte command)
        {
            if (SimpitPlugin.Instance.config_verbose) SimpitPlugin.Instance.loggingQueueInfo.Enqueue("Receveid TW command " + command);
            //UniverseDataProvider udp = GameManager.Instance.Game.ViewController.DataProvider.UniverseDataProvider;
            //int currentRate = udp.GetTimeRateIndex();
            TimeWarp tw = GameManager.Instance.Game.ViewController.TimeWarp;
            switch (command)
            {
                case WarpControlValuesKsp2.warpRate1:
                case WarpControlValuesKsp2.warpRate2:
                case WarpControlValuesKsp2.warpRate3:
                case WarpControlValuesKsp2.warpRate4:
                case WarpControlValuesKsp2.warpRate5:
                case WarpControlValuesKsp2.warpRate6:
                case WarpControlValuesKsp2.warpRate7:
                case WarpControlValuesKsp2.warpRate8:
                case WarpControlValuesKsp2.warpRate9:
                case WarpControlValuesKsp2.warpRate10:
                case WarpControlValuesKsp2.warpRate11:
                    tw.SetRateIndex(command, USE_INSTANT_WARP);
                    break;
                case WarpControlValuesKsp2.warpRateUp:
                    tw.IncreaseTimeWarp();
                    break;
                case WarpControlValuesKsp2.warpRateDown:
                    tw.DecreaseTimeWarp();
                    break;
                case WarpControlValuesKsp2.warpCancelAutoWarp:
                    tw.StopTimeWarp();
                    break;
                default:
                    SimpitPlugin.Instance.loggingQueueInfo.Enqueue("Received an unrecognized Warp control command : " + command + ". Ignoring it.");
                    break;
            }
        }

        public void ProcessTimewarpToCommand(TimewarpToStruct command)
        {
            // In those cases, we need to timewarp to a given time. Let's compute this time (UT)
            double timeToWarpTo = double.MinValue;

            VesselComponent simVessel = null;
            try { simVessel = Vehicle.ActiveSimVessel; } catch { }
            if (simVessel == null) return;

            double ut = GameManager.Instance.Game.SpaceSimulation.UniverseModel.UniverseTime;
            PatchTransitionType orbitType = simVessel.Orbit.PatchEndTransition;
            double timeOfSOIChange = double.NaN;
            if (orbitType == PatchTransitionType.Encounter || orbitType == PatchTransitionType.Escape)
            {
                timeOfSOIChange = simVessel.Orbit.EndUT;
            }

            switch (command.instant)
            {
                case TimewarpToValues.timewarpToNow:
                    timeToWarpTo = ut;
                    break;
                //In KSP2 the maneuver time isn't in the middle of the maneuver any more, but at the start of the maneuver, so maneuver time and start burn time are the same
                case TimewarpToValues.timewarpToManeuver:
                case TimewarpToValues.timewarpToBurn:
                    List<ManeuverNodeData> maneuvers = simVessel.Orbiter.SimulationObject.ManeuverPlan.GetNodes();
                    if (maneuvers != null && maneuvers.Count > 0 && maneuvers[0] != null)
                    {
                        timeToWarpTo = maneuvers[0].Time;
                    }
                    else
                    {
                        SimpitPlugin.Instance.loggingQueueInfo.Enqueue("There is no maneuver to warp to.");
                        return;
                    }
                    break;
                case TimewarpToValues.timewarpToNextSOI:
                    if (timeOfSOIChange != double.NaN)
                    {
                        timeToWarpTo = timeOfSOIChange;
                    }
                    else
                    {
                        SimpitPlugin.Instance.loggingQueueInfo.Enqueue("There is no SOI change to warp to. Orbit type : " + orbitType); 
                        return;
                    }
                    break;
                case TimewarpToValues.timewarpToApoapsis:
                    
                    double timeToApoapsis = simVessel.Orbit.TimeToAp;
                    if (Double.IsNaN(timeToApoapsis) || Double.IsInfinity(timeToApoapsis))
                    {
                        //This can happen in an escape trajectory for instance
                        SimpitPlugin.Instance.loggingQueueInfo.Enqueue("Cannot TW to apoasis since there is no apoapsis");
                        return;
                    }
                    else if (timeOfSOIChange != double.NaN && ut + timeToApoapsis > timeOfSOIChange)
                    {
                        //This can happen in an escape trajectory for instance
                        SimpitPlugin.Instance.loggingQueueInfo.Enqueue("Cannot TW to apoasis since there is an SOI change before that");
                        return;
                    }
                    else
                    {
                        timeToWarpTo = ut + timeToApoapsis;
                    }
                    break;
                case TimewarpToValues.timewarpToPeriapsis:
                    double timeToPeriapsis = simVessel.Orbit.TimeToPe;
                    if (Double.IsNaN(timeToPeriapsis) || Double.IsInfinity(timeToPeriapsis))
                    {
                        //Can this happen ?
                        SimpitPlugin.Instance.loggingQueueInfo.Enqueue("Cannot TW to periapsis since there is no periapsis");
                        return;
                    }
                    else if (timeOfSOIChange != double.NaN && ut + timeToPeriapsis > timeOfSOIChange)
                    {
                        //This can happen in an escape trajectory for instance
                        SimpitPlugin.Instance.loggingQueueInfo.Enqueue("Cannot TW to periapsis since there is an SOI change before that");
                        return;
                    }
                    else
                    {
                        timeToWarpTo = ut + timeToPeriapsis;
                    }
                    break;
                case TimewarpToValues.timewarpToNextMorning:
                    if (simVessel.Situation == VesselSituations.Landed ||
                        simVessel.Situation == VesselSituations.Splashed ||
                        simVessel.Situation == VesselSituations.PreLaunch)
                    {
                        SimpitPlugin.Instance.loggingQueueInfo.Enqueue("TimeToDaylight calculations are not correctly implemented yet. Cannot warp to next morning.");
                        return;

                        double timeToMorning = OrbitalComputations.TimeToDaylight(simVessel.Latitude, simVessel.Longitude, simVessel.mainBody);
                        timeToWarpTo = ut + timeToMorning;
                    }
                    else
                    {
                        SimpitPlugin.Instance.loggingQueueInfo.Enqueue("Cannot warp to next morning if not landed or splashed");
                        return;
                    }
                    break;
                default:
                    SimpitPlugin.Instance.loggingQueueInfo.Enqueue("Received an unrecognized WarpTo command : " + command + ". Ignoring it.");
                    return;
                    break;
            }

            timeToWarpTo = timeToWarpTo + command.delay;
            
            if (timeToWarpTo < 0)
            {
                SimpitPlugin.Instance.loggingQueueInfo.Enqueue("Cannot compute the time to timewarp to. Ignoring TW command " + command);
                return;
            }
            if (timeToWarpTo < ut)
            {
                SimpitPlugin.Instance.loggingQueueInfo.Enqueue("Cannot warp in the past. Ignoring TW command " + command);
                return;
            }

            if (SimpitPlugin.Instance.config_verbose) SimpitPlugin.Instance.loggingQueueInfo.Enqueue("TW to UT " + timeToWarpTo + ". Which is " + (timeToWarpTo - ut) + "s away");
            safeWarpTo(timeToWarpTo);
        }

        private void safeWarpTo(double UT)
        {
            GameManager.Instance.Game.ViewController.TimeWarp.WarpTo(UT);
        }
    }
}