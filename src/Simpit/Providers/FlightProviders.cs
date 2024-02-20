using KSP.Sim.impl;
using System.Runtime.InteropServices;
using SpaceWarp.API.Game;
using KSP.Sim.Definitions;
using KSP.Game;
using KSP.Map;
using KSP.Sim.DeltaV;

namespace Simpit.Providers
{
    #region AtmoCondition
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    [Serializable]
    struct atmoConditionStruct
    {
        public byte atmoCharacteristics; // has atmosphere, has oxygen in atmosphere, isVessel in atmosphere
        public float airDensity;
        public float temperature;
        public float pressure;
    }

    class AtmoConditionProvider : GenericProvider<atmoConditionStruct>
    {
        AtmoConditionProvider() : base(OutboundPackets.AtmoCondition) { }

        protected override bool updateMessage(ref atmoConditionStruct message)
        {
            message.atmoCharacteristics = 0;
            message.temperature = 0;
            message.pressure = 0;
            message.airDensity = 0;

            VesselComponent simVessel = null;
            try { simVessel = Vehicle.ActiveSimVessel; } catch { }
            if (simVessel == null) return false;

            CelestialBodyComponent body = simVessel.mainBody;
            if (body == null) return false;

            if (body.hasAtmosphere)
            {
                message.atmoCharacteristics |= AtmoConditionsBits.hasAtmosphere;
                if (body.atmosphereContainsOxygen) message.atmoCharacteristics |= AtmoConditionsBits.hasOxygen;
                if (simVessel._telemetryComponent.IsInAtmosphere) message.atmoCharacteristics |= AtmoConditionsBits.isVesselInAtmosphere;

                message.temperature = (float)simVessel._telemetryComponent.AtmosphericTemperature;
                message.pressure = (float)simVessel._telemetryComponent.StaticPressure_kPa;
                message.airDensity = (float)simVessel._telemetryComponent.AtmosphericDensity;
            }

            return false;
        }
    }
    #endregion

    #region FlightStatus
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    [Serializable]
    public struct FlightStatusStruct
    {
        public byte flightStatusFlags; // content defined with the FlightStatusBits
        public byte vesselSituation; // See Vessel.Situations for possible values
        public byte currentTWIndex;
        public byte crewCapacity;
        public byte crewCount;
        public byte commNetSignalStrenghPercentage;
        public byte currentStage;
        public byte vesselType;
    }

    class FlightStatusProvider : GenericProvider<FlightStatusStruct>
    {
        FlightStatusProvider() : base(OutboundPackets.FlightStatus) { }

        protected override bool updateMessage(ref FlightStatusStruct myFlightStatus)
        {
            VesselComponent simVessel = null;
            TimeWarp tw = null;
            try 
            { 
                simVessel = Vehicle.ActiveSimVessel; 
                tw = GameManager.Instance.Game.ViewController.TimeWarp;
            } catch { }
            if (simVessel == null || tw == null) return false;

            myFlightStatus.flightStatusFlags = 0;
            if (simVessel.IsFlying) myFlightStatus.flightStatusFlags += FlightStatusBits.isInFlight;
            if (simVessel.IsKerbalEVA) myFlightStatus.flightStatusFlags += FlightStatusBits.isEva;
            if (simVessel.IsVesselRecoverable) myFlightStatus.flightStatusFlags += FlightStatusBits.isRecoverable;
            if (tw.IsPhysicsTimeWarp) myFlightStatus.flightStatusFlags += FlightStatusBits.isInAtmoTW;
            switch (simVessel.ControlStatus)
            {
                case VesselControlState.NoControl:
                    break;
                case VesselControlState.NoCommNet:
                    myFlightStatus.flightStatusFlags += FlightStatusBits.comnetControlLevel0;
                    break;
                case VesselControlState.FullControlHibernation:
                    myFlightStatus.flightStatusFlags += FlightStatusBits.comnetControlLevel1;
                    break;
                case VesselControlState.FullControl:
                    myFlightStatus.flightStatusFlags += FlightStatusBits.comnetControlLevel0;
                    myFlightStatus.flightStatusFlags += FlightStatusBits.comnetControlLevel1;
                    break;
            }
            if (simVessel.HasTargetObject) myFlightStatus.flightStatusFlags += FlightStatusBits.hasTargetSet;

            myFlightStatus.vesselSituation = (byte)simVessel.Situation;
            myFlightStatus.currentTWIndex = (byte)tw.CurrentRateIndex;
            myFlightStatus.crewCapacity = (byte)Math.Min(Byte.MaxValue, simVessel.TotalCommandCrewCapacity);
            myFlightStatus.crewCount = (byte)Math.Min(Byte.MaxValue, GameManager.Instance.Game.SessionManager.KerbalRosterManager.GetAllKerbalsInVessel(simVessel.GlobalId).Count);

            //TODO is there even Data on KSP2 that can be used here? For now just use 0% and 100% depending on CommNet availability
            if (simVessel.ControlStatus == VesselControlState.NoControl || simVessel.ControlStatus == VesselControlState.NoCommNet) myFlightStatus.commNetSignalStrenghPercentage = 0;
            else myFlightStatus.commNetSignalStrenghPercentage = 100;
            /* 
            if (simVessel.connection == null)
            {
                myFlightStatus.commNetSignalStrenghPercentage = 0;
            }
            else
            {
                myFlightStatus.commNetSignalStrenghPercentage = (byte)Math.Round(100 * FlightGlobals.ActiveVessel.connection.SignalStrength);
            }
            */
            myFlightStatus.currentStage = (byte)Math.Min(255, DeltaVExtensions.CurrentStage(simVessel.VesselDeltaV));
            myFlightStatus.vesselType = (byte)ConstructMapItemFromVessel(simVessel);
            return false;
        }

        //Taken from MapCore.cs
        private MapItemType ConstructMapItemFromVessel(VesselComponent vessel)
        {
            MapItemType mapItemType = MapItemType.Vessel;
            if (vessel.ControlStatus == VesselControlState.NoControl && vessel.IsDebris())
                mapItemType = MapItemType.Debris;
            else if (vessel.IsKerbalEVA)
                mapItemType = MapItemType.Astronaut;
            return mapItemType;
        }
    }
    #endregion


    #region VesselName
    class VesselNameProvider : GenericProviderString
    {
        VesselNameProvider() : base(OutboundPackets.VesselName) { }

        protected override bool updateMessage(ref String myMsg)
        {
            VesselComponent simVessel = null;
            try { simVessel = Vehicle.ActiveSimVessel; } catch { }
            if (simVessel == null) return false;

            myMsg = simVessel.DisplayName;
            return false;
        }
    }
    #endregion

    #region SOIName
    class SOINameProvider : GenericProviderString
    {
        SOINameProvider() : base(OutboundPackets.SoIName) { }

        protected override bool updateMessage(ref String myMsg)
        {
            VesselComponent simVessel = null;
            try { simVessel = Vehicle.ActiveSimVessel; } catch { }
            if (simVessel == null || simVessel.Orbit == null || simVessel.Orbit.referenceBody == null) return false;

            myMsg = simVessel.Orbit.referenceBody.bodyName;
            return false;
        }
    }
    #endregion
}