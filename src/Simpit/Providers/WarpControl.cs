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
        private const bool DISPLAY_MESSAGE = false; //When true, each call to Timewarp.SetRate crashes KSP on my computer

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
            if (rateIndex <= udp.GetMaxTimeRateIndex())
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
            if (SimpitPlugin.Instance.config_verbose) SimpitPlugin.Instance.loggingQueueInfo.Enqueue("receveid TW command " + command);
            //UniverseDataProvider udp = GameManager.Instance.Game.ViewController.DataProvider.UniverseDataProvider;
            //int currentRate = udp.GetTimeRateIndex();
            TimeWarp tw = GameManager.Instance.Game.ViewController.TimeWarp;
            switch (command)
            {
                case WarpControlValues.warpRate1:
                    tw.SetRateIndex(command, USE_INSTANT_WARP);
                    //KSP2 SetWarpRate(command);
                    //KSP1 TimeWarp.SetRate(0, USE_INSTANT_WARP, DISPLAY_MESSAGE);
                    break;
                case WarpControlValues.warpRate2:
                case WarpControlValues.warpRate3:
                case WarpControlValues.warpRate4:
                case WarpControlValues.warpRate5:
                case WarpControlValues.warpRate6:
                case WarpControlValues.warpRate7:
                case WarpControlValues.warpRate8:
                    tw.SetRateIndex(command, USE_INSTANT_WARP);
                    //SetWarpRate(command);
                    break;
                case WarpControlValues.warpRatePhys1:
                    tw.SetRateIndex(0, USE_INSTANT_WARP);
                    //SetWarpRate(0);
                    break;
                case WarpControlValues.warpRatePhys2:
                    tw.SetRateIndex(1, USE_INSTANT_WARP);
                    //SetWarpRate(1);
                    break;
                case WarpControlValues.warpRatePhys3:
                    tw.SetRateIndex(1, USE_INSTANT_WARP);
                    //SetWarpRate(2);
                    break;
                case WarpControlValues.warpRatePhys4:
                    //SetWarpRate(command - WarpControlValues.warpRatePhys1);
                    //SetWarpRate(2);
                    tw.SetRateIndex(2, USE_INSTANT_WARP);
                    break;
                case WarpControlValues.warpRateUp:
                    tw.IncreaseTimeWarp();
                    /*
                    int MaxRateIndex = udp.GetMaxTimeRateIndex();
                    if (currentRate < MaxRateIndex)
                    {
                        SetWarpRate(currentRate + 1);
                        // KSP1 UnityMainThreadDispatcher.Instance().Enqueue(() => TimeWarp.SetRate(currentRate + 1, USE_INSTANT_WARP, DISPLAY_MESSAGE));
                    }
                    else
                    {
                        SimpitPlugin.Instance.loggingQueueInfo.Enqueue("Already at max warp rate.");
                    }
                    */
                    break;
                case WarpControlValues.warpRateDown:
                    tw.DecreaseTimeWarp();
                    /*
                    if (currentRate > 0)
                    {
                        SetWarpRate(currentRate - 1);
                    }
                    else
                    {
                        SimpitPlugin.Instance.loggingQueueInfo.Enqueue("Already at min warp rate.");
                    }
                    */
                    break;
                case WarpControlValues.warpCancelAutoWarp:
                    tw.StopTimeWarp();
                    //udp.SetTimeRateIndex(0);
                    //KSP1 TimeWarp.fetch.CancelAutoWarp();
                    //KSP1 TimeWarp.SetRate(0, USE_INSTANT_WARP, DISPLAY_MESSAGE);
                    break;
                default:
                    SimpitPlugin.Instance.loggingQueueInfo.Enqueue("received an unrecognized Warp control command : " + command + ". Ignoring it.");
                    break;
            }
        }

        public void ProcessTimewarpToCommand(TimewarpToStruct command)
        {
            // In those cases, we need to timewarp to a given time. Let's compute this time (UT)
            double timeToWarp = -1;

            VesselComponent simVessel = null;
            try { simVessel = Vehicle.ActiveSimVessel; } catch { }
            if (simVessel == null) return;

            double ut = GameManager.Instance.Game.SpaceSimulation.UniverseModel.UniverseTime;

            switch (command.instant)
            {
                case TimewarpToValues.timewarpToNow:
                    timeToWarp = ut;
                    break;
                //In KSP2 the maneuver time isn't in the middle of the maneuver any more, but at the start of the maneuver, so maneuver time and start burn time are the same
                case TimewarpToValues.timewarpToManeuver:
                case TimewarpToValues.timewarpToBurn:
                    List<ManeuverNodeData> maneuvers = simVessel.Orbiter.SimulationObject.ManeuverPlan.GetNodes();
                    if (maneuvers != null && maneuvers.Count > 0 && maneuvers[0] != null)
                    {
                        timeToWarp = maneuvers[0].Time;
                    }
                    else
                    {
                        SimpitPlugin.Instance.loggingQueueInfo.Enqueue("There is no maneuver to warp to.");
                    }
                    break;
                case TimewarpToValues.timewarpToNextSOI:
                    PatchTransitionType orbitType = simVessel.Orbit.PatchEndTransition;

                    if (orbitType == PatchTransitionType.Encounter ||
                        orbitType == PatchTransitionType.Escape)
                    {
                        timeToWarp = simVessel.Orbit.EndUT;
                    }
                    else
                    {
                        SimpitPlugin.Instance.loggingQueueInfo.Enqueue("There is no SOI change to warp to. Orbit type : " + orbitType);
                    }
                    break;
                case TimewarpToValues.timewarpToApoapsis:
                    double timeToApoapsis = simVessel.Orbit.TimeToAp;
                    if (Double.IsNaN(timeToApoapsis) || Double.IsInfinity(timeToApoapsis))
                    {
                        //This can happen in an escape trajectory for instance
                        SimpitPlugin.Instance.loggingQueueInfo.Enqueue("Cannot TW to apoasis since there is no apoapsis");
                    }
                    else
                    {
                        timeToWarp = ut + timeToApoapsis;
                    }
                    break;
                case TimewarpToValues.timewarpToPeriapsis:
                    double timeToPeriapsis = simVessel.Orbit.TimeToPe;
                    if (Double.IsNaN(timeToPeriapsis) || Double.IsInfinity(timeToPeriapsis))
                    {
                        //Can this happen ?
                        SimpitPlugin.Instance.loggingQueueInfo.Enqueue("Cannot TW to periapsis since there is no periapsis");
                    }
                    else
                    {
                        timeToWarp = ut + timeToPeriapsis;
                    }
                    break;
                case TimewarpToValues.timewarpToNextMorning:

                    if (simVessel.Situation == VesselSituations.Landed ||
                        simVessel.Situation == VesselSituations.Splashed ||
                        simVessel.Situation == VesselSituations.PreLaunch)
                    {
                        double timeToMorning = OrbitalComputations.TimeToDaylight(simVessel.Latitude, simVessel.Longitude, simVessel.mainBody);
                        timeToWarp = ut + timeToMorning;
                    }
                    else
                    {
                        SimpitPlugin.Instance.loggingQueueInfo.Enqueue("Cannot warp to next morning if not landed or splashed");
                    }
                    break;
                default:
                    SimpitPlugin.Instance.loggingQueueInfo.Enqueue("received an unrecognized WarpTO command : " + command + ". Ignoring it.");
                    break;
            }

            timeToWarp = timeToWarp + command.delay;
            if (SimpitPlugin.Instance.config_verbose) SimpitPlugin.Instance.loggingQueueInfo.Enqueue("TW to UT " + timeToWarp + ". Which is " + (timeToWarp - ut) + "s away");

            if (timeToWarp < 0)
            {
                SimpitPlugin.Instance.loggingQueueInfo.Enqueue("cannot compute the time to timewarp to. Ignoring TW command " + command);
            }
            else if (timeToWarp < ut)
            {
                SimpitPlugin.Instance.loggingQueueInfo.Enqueue("cannot warp in the past. Ignoring TW command " + command);
            }
            else
            {
                safeWarpTo(timeToWarp);
            }
        }

        private void safeWarpTo(double UT)
        {
            GameManager.Instance.Game.ViewController.TimeWarp.WarpTo(UT);
            //KSP1 UnityMainThreadDispatcher.Instance().Enqueue(() => TimeWarp.fetch.WarpTo(UT));
        }
    }
}