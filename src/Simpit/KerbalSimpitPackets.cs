﻿namespace Simpit
{
    public static class CommonPackets
    {
        public static byte Synchronisation = 0;
        public static byte EchoRequest = 1;
        public static byte EchoResponse = 2;
    }

    public static class OutboundPackets
    {
        // Propulsion Resources
        public static byte LiquidFuel = 10;
        public static byte LiquidFuelStage = 11;
        public static byte Methane = 10;
        public static byte MethaneStage = 11;
        public static byte Oxidizer = 12;
        public static byte OxidizerStage = 13;
        public static byte SolidFuel = 14;
        public static byte SolidFuelStage = 15;
        public static byte XenonGas = 28;
        public static byte XenonGasStage = 29;
        public static byte MonoPropellant = 16;
        public static byte EvaPropellant = 18;

        //Propulsion Resources on KSP2
        public static byte IntakeAir = 52;
        //public static byte TestRocks = xx; //Available in KSP2 but seems to be unused
        public static byte Hydrogen = 53;
        public static byte HydrogenStage = 54;
        //public static byte Methalox = xx; //Available in KSP2 but seems to be unused
        //public static byte MethaloxStage = xx; //Available in KSP2 but seems to be unused
        //public static byte MethaneAir = xx; //Available in KSP2 but seems to be unused
        //public static byte MethaneAirStage = xx; //Available in KSP2 but seems to be unused
        public static byte Uranium = 55;
        //public static byte XenonEC = xx; //Available in KSP2 but seems to be unused
        //public static byte XenonECStage = xx; //Available in KSP2 but seems to be unused

        // Vessel Resources
        public static byte ElectricCharge = 17;
        public static byte Ore = 19;
        public static byte Ablator = 20;
        public static byte AblatorStage = 21;
        public static byte TACLSResource = 30;
        public static byte TACLSWaste = 31;
        public static byte CustomResource1 = 32;
        public static byte CustomResource2 = 33;

        // Vessel Movement/Postion
        public static byte Altitude = 8;
        public static byte Velocities = 22;
        public static byte Airspeed = 27;
        public static byte Apsides = 9;
        public static byte ApsidesTime = 24;
        public static byte ManeuverData = 34;
        public static byte SASInfo = 35;
        public static byte OrbitInfo = 36;
        public static byte RotationData = 45;

        // Vessel commands
        public static byte RotationCmd = 47;
        public static byte TranslationCmd = 48;
        public static byte WheelCmd = 49;
        public static byte ThrottleCmd = 50;

        // Vessel Details
        public static byte ActionGroups = 37;
        public static byte DeltaV = 38;
        public static byte DeltaVEnv = 39;
        public static byte BurnTime = 40;
        public static byte CustomActionGroups = 41;
        public static byte TempLimit = 42;

        // External Environment
        public static byte TargetInfo = 25;
        public static byte SoIName = 26;
        public static byte SceneChange = 3;
        public static byte FlightStatus = 43;
        public static byte AtmoCondition = 44;
        public static byte VesselName = 46;
        public static byte VesselChange = 51;
    }

    public static class InboundPackets
    {
        public static byte CloseSerialPort = 7;
        public static byte RegisterHandler = 8;
        public static byte DeregisterHandler = 9;
        public static byte RequestMessage = 29;
        public static byte CAGEnable = 10;
        public static byte CAGDisable = 11;
        public static byte CAGToggle = 12;
        public static byte ActionGroupActivate = 13;
        public static byte ActionGroupDeactivate = 14;
        public static byte ActionGroupToggle = 15;
        public static byte VesselRotation = 16;
        public static byte VesselTranslation = 17;
        public static byte WheelControl = 18;
        public static byte VesselThrottle = 19;
        public static byte AutopilotMode = 20;
        public static byte CameraMode = 21;
        public static byte CameraRotation = 22;
        public static byte CameraTranslation = 23;
        public static byte WarpChange = 24;
        public static byte TimewarpTo = 30;
        public static byte CustomLog = 25;
        public static byte KeyboardEmulator = 26;
        public static byte VesselCustomAxis = 27;
        public static byte NavballMode = 28;
    }

    public static class HandshakeValues
    {
        public static byte KerbalSpaceProgram1 = 0x37;
        public static byte KerbalSpaceProgram2 = 0xC8;
    }

    public static class CameraControlBits
    {
        // Flight Camera Modes
        public const byte FlightMode = 1;
        // Order from: https://kerbalspaceprogram.com/api/class_flight_camera.html#ae90b21deb28ca1978c229f0511116c1a
        public const byte Auto = 2;
        public const byte Free = 3;
        public const byte Orbital = 4;
        public const byte Chase = 5;
        public const byte Locked = 6;

        public const byte IVAMode = 10;
        public const byte PlanetaryMode = 20;

        public const byte NextCamera = 50;
        public const byte PreviousCamera = 51;
        public const byte NextCameraModeState = 52;
        public const byte PreviousCameraModeState = 53;
        // Cycle Camera Modes
        // public static byte NextBit = 6;
        //public static byte PreviousBit = 7;

        //public static short 

    }

    public static class ActionGroupBits
    {
        // This is the same order given in
        // https://kerbalspaceprogram.com/api/_base_action_8cs.html
        public static byte StageBit = 1;
        public static byte GearBit = 2;
        public static byte LightBit = 4;
        public static byte RCSBit = 8;
        public static byte SASBit = 16;
        public static byte BrakesBit = 32;
        public static byte AbortBit = 64;
    }

    public static class WarpControlValues
    {
        public const byte warpRate1 = 0;
        public const byte warpRate2 = 1;
        public const byte warpRate3 = 2;
        public const byte warpRate4 = 3;
        public const byte warpRate5 = 4;
        public const byte warpRate6 = 5;
        public const byte warpRate7 = 6;
        public const byte warpRate8 = 7;
        public const byte warpRatePhys1 = 8;
        public const byte warpRatePhys2 = 9;
        public const byte warpRatePhys3 = 10;
        public const byte warpRatePhys4 = 11;
        public const byte warpRateUp = 12;
        public const byte warpRateDown = 13;
        public const byte warpCancelAutoWarp = 255;
    }

    public static class TimewarpToValues
    {
        public const byte timewarpToNow = 0;
        public const byte timewarpToManeuver = 1;
        public const byte timewarpToBurn = 2;
        public const byte timewarpToNextSOI = 3;
        public const byte timewarpToApoapsis = 4;
        public const byte timewarpToPeriapsis = 5;
        public const byte timewarpToNextMorning = 6;
    }

    public static class CustomLogBits
    {
        public static byte Verbose = 1;
        public static byte PrintToScreen = 2;
        public static byte NoHeader = 4;
    }

    public static class FlightStatusBits
    {
        public static byte isInFlight = 1;
        public static byte isEva = 2;
        public static byte isRecoverable = 4;
        public static byte isInAtmoTW = 8;
        public static byte comnetControlLevel0 = 16; // Code the control level on 2 bits (4 possible values)
        public static byte comnetControlLevel1 = 32; // Code the control level on 2 bits (4 possible values)
        public static byte hasTargetSet = 64;
    }

    public static class AtmoConditionsBits
    {
        public static byte hasAtmosphere = 1;
        public static byte hasOxygen = 2;
        public static byte isVesselInAtmosphere = 4;
    }

    public static class KeyboardEmulatorModifier
    {
        public static byte SHIFT_MOD = 1;
        public static byte CTRL_MOD = 2;
        public static byte ALT_MOD = 4;
        public static byte KEY_DOWN_MOD = 8;
        public static byte KEY_UP_MOD = 16;
    };

    public static class VesselChangeValues
    {
        public static byte switching = 1;
        public static byte docking = 2;
        public static byte undocking = 3;
    }
}