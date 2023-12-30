using KSP.Game;
using KSP.Navigation;
using KSP.Sim;
using KSP.Sim.impl;
using Simpit;
using SpaceWarp.API.Game;
using UnityEngine;

namespace Simpit.Providers
{
    public class KerbalSimpitNavBallProvider : MonoBehaviour
    {
        private EventDataObsolete<byte, object> navBallChannel;
        public void Start()
        {
            navBallChannel = GameEvents.FindEvent<EventDataObsolete<byte, object>>("onSerialReceived" + InboundPackets.NavballMode);
            if (navBallChannel != null) navBallChannel.Add(cycleNavBallModeCallback);
        }

        public void OnDestroy()
        {
            if (navBallChannel != null) navBallChannel.Remove(cycleNavBallModeCallback);
        }
        public void cycleNavBallModeCallback(byte ID, object Data)
        {
            VesselComponent simVessel = null;
            try { simVessel = Vehicle.ActiveSimVessel; } catch { }
            if (simVessel == null) return;

            SpeedDisplayMode nextSpeedDisplayMode = simVessel.speedMode.Next();
            Vehicle.ActiveVesselVehicle.SetSpeedDisplayMode(nextSpeedDisplayMode);
            //KSP1 UnityMainThreadDispatcher.Instance().Enqueue(() => FlightGlobals.CycleSpeedModes());
        }
    }
}