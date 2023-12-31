using KSP.Sim.impl;
using Simpit.Providers;
using Simpit;
using System.Runtime.InteropServices;
using static KSP.Api.UIDataPropertyStrings.View;
using SpaceWarp.API.Game;
using KSP.Sim.Definitions;
using KSP.Game;

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

            VesselComponent simVessel = null;
            try { simVessel = Vehicle.ActiveSimVessel; } catch { }
            if (simVessel == null) return false;

            CelestialBodyComponent body = simVessel.mainBody;
            if (body == null) return false;

            if (body._celestialDataProvider.HasAtmosphere.GetValue())
            {
                message.atmoCharacteristics |= AtmoConditionsBits.hasAtmosphere;
                if (body.atmosphereContainsOxygen) message.atmoCharacteristics |= AtmoConditionsBits.hasOxygen;
                if (body.atmosphereDepth >= simVessel.AltitudeFromSeaLevel) message.atmoCharacteristics |= AtmoConditionsBits.isVesselInAtmosphere;

                message.temperature = (float)body.GetTemperature(simVessel.AltitudeFromSeaLevel);
                message.pressure = (float)body.GetPressure(simVessel.AltitudeFromSeaLevel);
                message.airDensity = (float)body.GetDensity(body.GetPressure(simVessel.AltitudeFromSeaLevel), body.GetTemperature(simVessel.AltitudeFromSeaLevel));
            }

            //KSP1 What does this do??? FlightGlobals.ActiveVessel.mainBody.GetFullTemperature(FlightGlobals.ActiveVessel.CoMD);

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
            try { simVessel = Vehicle.ActiveSimVessel; } catch { }
            TimeWarp tw = GameManager.Instance.Game.ViewController.TimeWarp;
            if (simVessel == null || tw == null) return false;

            myFlightStatus.flightStatusFlags = 0;
            if (simVessel.IsVesselInFlight()) myFlightStatus.flightStatusFlags += FlightStatusBits.isInFlight;
            if (simVessel.IsKerbalEVA) myFlightStatus.flightStatusFlags += FlightStatusBits.isEva;
            if (simVessel.IsVesselRecoverable) myFlightStatus.flightStatusFlags += FlightStatusBits.isRecoverable;
            //if (tw.Mode == TimeWarp.Modes.LOW) myFlightStatus.flightStatusFlags += FlightStatusBits.isInAtmoTW;
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
            /*
            myFlightStatus.crewCount = (byte)Math.Min(Byte.MaxValue, simVessel.GetCrewCount());

            if (simVessel.connection == null)
            {
                myFlightStatus.commNetSignalStrenghPercentage = 0;
            }
            else
            {
                myFlightStatus.commNetSignalStrenghPercentage = (byte)Math.Round(100 * FlightGlobals.ActiveVessel.connection.SignalStrength);
            }

            myFlightStatus.currentStage = (byte)Math.Min(255, simVessel.currentStage);
            myFlightStatus.vesselType = (byte)simVessel.vesselType;
            */
            return false;
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