using KSP.Sim;
using KSP.Sim.impl;
using SpaceWarp.API.Game;
using SpaceWarp.API.Game.Extensions;
using UnityEngine;

namespace Simpit.Providers
{
    public class KerbalSimpitActionProvider : MonoBehaviour
    {
        // Inbound messages
        private EventDataObsolete<byte, object> AGActivateChannel, AGDeactivateChannel,
            AGToggleChannel;

        // Outbound messages
        private EventDataObsolete<byte, object> AGStateChannel;

        // TODO: Only using a single byte buffer for each of these is
        // technically unsafe. It's not impossible that multiple controllers
        // will attempt to send new packets between each Update(), and only
        // the last one will be affected. But it is unlikely, which is why
        // I'm not addressing it now.
        private volatile byte activateBuffer, deactivateBuffer,
            toggleBuffer, currentStateBuffer;

        // If set to true, the state should be sent at the next update even if no changes
        // are detected (for instance to initialise it after a new registration).
        private bool resendState = false;

        public void Start()
        {
            activateBuffer = 0;
            deactivateBuffer = 0;
            toggleBuffer = 0;
            currentStateBuffer = 0;

            AGActivateChannel = GameEvents.FindEvent<EventDataObsolete<byte, object>>("onSerialReceived" + InboundPackets.ActionGroupActivate);
            if (AGActivateChannel != null) AGActivateChannel.Add(actionActivateCallback);
            AGDeactivateChannel = GameEvents.FindEvent<EventDataObsolete<byte, object>>("onSerialReceived" + InboundPackets.ActionGroupDeactivate);
            if (AGDeactivateChannel != null) AGDeactivateChannel.Add(actionDeactivateCallback);
            AGToggleChannel = GameEvents.FindEvent<EventDataObsolete<byte, object>>("onSerialReceived" + +InboundPackets.ActionGroupToggle);
            if (AGToggleChannel != null) AGToggleChannel.Add(actionToggleCallback);

            AGStateChannel = GameEvents.FindEvent<EventDataObsolete<byte, object>>("toSerial" + OutboundPackets.ActionGroups);
            GameEvents.FindEvent<EventDataObsolete<byte, object>>("onSerialChannelForceSend" + OutboundPackets.ActionGroups).Add(resendActionGroup);
        }

        public void OnDestroy()
        {
            if (AGActivateChannel != null) AGActivateChannel.Remove(actionActivateCallback);
            if (AGDeactivateChannel != null) AGDeactivateChannel.Remove(actionDeactivateCallback);
            if (AGToggleChannel != null) AGToggleChannel.Remove(actionToggleCallback);
        }

        public void resendActionGroup(byte ID, object Data)
        {
            resendState = true;
        }

        public void Update()
        {
            if (activateBuffer > 0)
            {
                activateGroups(activateBuffer);
                activateBuffer = 0;
            }
            if (deactivateBuffer > 0)
            {
                deactivateGroups(deactivateBuffer);
                deactivateBuffer = 0;
            }
            if (toggleBuffer > 0)
            {
                toggleGroups(toggleBuffer);
                toggleBuffer = 0;
            }

            updateCurrentState();
        }

        public void actionActivateCallback(byte ID, object Data)
        {
            byte[] payload = (byte[])Data;
            activateBuffer = payload[0];
        }

        public void actionDeactivateCallback(byte ID, object Data)
        {
            byte[] payload = (byte[])Data;
            deactivateBuffer = payload[0];
        }

        public void actionToggleCallback(byte ID, object Data)
        {
            byte[] payload = (byte[])Data;
            toggleBuffer = payload[0];
        }

        private bool updateCurrentState()
        {
            byte newState = getGroups();
            if (newState != currentStateBuffer || resendState)
            {
                resendState = false;
                if (AGStateChannel != null) {
                    AGStateChannel.Fire(OutboundPackets.ActionGroups, newState);
                    currentStateBuffer = newState;
                }
                return true;
            } else {
                return false;
            }
        }

        private void activateGroups(byte groups)
        {
            VesselVehicle currentVessel = null;
            VesselComponent simVessel = null;
            try
            {
                currentVessel = Vehicle.ActiveVesselVehicle;
                simVessel = Vehicle.ActiveSimVessel;
            }
            catch { }
            if (currentVessel == null || simVessel == null) return;

            if ((groups & ActionGroupBits.StageBit) != 0)
            {
                if (SimpitPlugin.Instance.config_verbose) SimpitPlugin.Instance.Logger.LogInfo("Activating stage");
                currentVessel.SetStage(true);
            }
            if ((groups & ActionGroupBits.GearBit) != 0)
            {
                if (SimpitPlugin.Instance.config_verbose) SimpitPlugin.Instance.Logger.LogInfo("Activating gear");
                //currentVessel.SetGroup(KSPActionGroup.Gear, true); //This sets the Gear state, but it can't be read. Probably a KSP2 bug. Use VesselComponent.SetActionGroup() instead
                simVessel.SetActionGroup(KSPActionGroup.Gear, true);
            }
            if ((groups & ActionGroupBits.LightBit) != 0)
            {
                if (SimpitPlugin.Instance.config_verbose) SimpitPlugin.Instance.Logger.LogInfo("Activating light");
                simVessel.SetActionGroup(KSPActionGroup.Lights, true);
            }
            if ((groups & ActionGroupBits.RCSBit) != 0)
            {
                if (SimpitPlugin.Instance.config_verbose) SimpitPlugin.Instance.Logger.LogInfo("Activating RCS");
                //simVessel.SetActionGroup(KSPActionGroup.RCS, true);
                simVessel.SetRCSEnabled(true);
            }
            if ((groups & ActionGroupBits.SASBit) != 0)
            {
                if (SimpitPlugin.Instance.config_verbose) SimpitPlugin.Instance.Logger.LogInfo("Activating SAS");
                simVessel.SetAutopilotEnableDisable(true);
                //simVessel.SetActionGroup(KSPActionGroup.SAS, true);
            }
            if ((groups & ActionGroupBits.BrakesBit) != 0)
            {
                //Wheel brakes only gets set in VesselVehicle.SetGroup and not in VesselComponent like all the other action groups
                if (SimpitPlugin.Instance.config_verbose) SimpitPlugin.Instance.Logger.LogInfo("Activating brakes");
                currentVessel.SetGroup(KSPActionGroup.Brakes, true);
            }
            if ((groups & ActionGroupBits.AbortBit) != 0)
            {
                if (SimpitPlugin.Instance.config_verbose) SimpitPlugin.Instance.Logger.LogInfo("Activating abort");
                simVessel.SetActionGroup(KSPActionGroup.Abort, true);
            }
            if ((groups & ActionGroupBits.SolarPanelsBit) != 0)
            {
                if (SimpitPlugin.Instance.config_verbose) SimpitPlugin.Instance.Logger.LogInfo("Activating solar panels");
                simVessel.SetActionGroup(KSPActionGroup.SolarPanels, true);
            }
        }

        private void deactivateGroups(byte groups)
        {
            VesselVehicle currentVessel = null;
            VesselComponent simVessel = null;
            try
            {
                currentVessel = Vehicle.ActiveVesselVehicle;
                simVessel = Vehicle.ActiveSimVessel;
            }
            catch { }
            if (currentVessel == null || simVessel == null) return;

            if ((groups & ActionGroupBits.StageBit) != 0)
            {
                if (SimpitPlugin.Instance.config_verbose) SimpitPlugin.Instance.Logger.LogInfo("Deactivating stage");
                currentVessel.SetStage(false);
            }
            if ((groups & ActionGroupBits.GearBit) != 0)
            {
                if (SimpitPlugin.Instance.config_verbose) SimpitPlugin.Instance.Logger.LogInfo("Deactivating gear");
                //currentVessel.SetGroup(KSPActionGroup.Gear, false);
                simVessel.SetActionGroup(KSPActionGroup.Gear, false);
            }
            if ((groups & ActionGroupBits.LightBit) != 0)
            {
                if (SimpitPlugin.Instance.config_verbose) SimpitPlugin.Instance.Logger.LogInfo("Deactivating light");
                simVessel.SetActionGroup(KSPActionGroup.Lights, false);
            }
            if ((groups & ActionGroupBits.RCSBit) != 0)
            {
                if (SimpitPlugin.Instance.config_verbose) SimpitPlugin.Instance.Logger.LogInfo("Deactivating RCS");
                //simVessel.SetActionGroup(KSPActionGroup.RCS, false);
                simVessel.SetRCSEnabled(false);
            }
            if ((groups & ActionGroupBits.SASBit) != 0)
            {
                if (SimpitPlugin.Instance.config_verbose) SimpitPlugin.Instance.Logger.LogInfo("Deactivating SAS");
                simVessel.SetAutopilotEnableDisable(false);
                //simVessel.SetActionGroup(KSPActionGroup.SAS, false);
            }
            if ((groups & ActionGroupBits.BrakesBit) != 0)
            {
                //Wheel brakes only gets set in VesselVehicle.SetGroup and not in VesselComponent like all the other action groups
                if (SimpitPlugin.Instance.config_verbose) SimpitPlugin.Instance.Logger.LogInfo("Deactivating brakes");
                currentVessel.SetGroup(KSPActionGroup.Brakes, false);
            }
            if ((groups & ActionGroupBits.AbortBit) != 0)
            {
                if (SimpitPlugin.Instance.config_verbose) SimpitPlugin.Instance.Logger.LogInfo("Deactivating abort");
                simVessel.SetActionGroup(KSPActionGroup.Abort, false);
            }
            if ((groups & ActionGroupBits.SolarPanelsBit) != 0)
            {
                if (SimpitPlugin.Instance.config_verbose) SimpitPlugin.Instance.Logger.LogInfo("Deactivating solar panels");
                simVessel.SetActionGroup(KSPActionGroup.SolarPanels, false);
            }
        }

        private void toggleGroups(byte groups)
        {
            VesselVehicle currentVessel = null;
            VesselComponent simVessel = null;
            try
            {
                currentVessel = Vehicle.ActiveVesselVehicle;
                simVessel = Vehicle.ActiveSimVessel;
            }
            catch { }
            if (currentVessel == null || simVessel == null) return;

            if ((groups & ActionGroupBits.StageBit) != 0)
            {
                if (SimpitPlugin.Instance.config_verbose) SimpitPlugin.Instance.Logger.LogInfo("Toggling (activating) stage");
                currentVessel.SetStage(true);
            }
            if ((groups & ActionGroupBits.GearBit) != 0)
            {
                if (SimpitPlugin.Instance.config_verbose) SimpitPlugin.Instance.Logger.LogInfo("Toggling gear");
                simVessel.TriggerActionGroup(KSPActionGroup.Gear);
                //currentVessel.TriggerGroup(KSPActionGroup.Gear);
            }
            if ((groups & ActionGroupBits.LightBit) != 0)
            {
                if (SimpitPlugin.Instance.config_verbose) SimpitPlugin.Instance.Logger.LogInfo("Toggling light");
                simVessel.TriggerActionGroup(KSPActionGroup.Lights);
            }
            if ((groups & ActionGroupBits.RCSBit) != 0)
            {
                if (SimpitPlugin.Instance.config_verbose) SimpitPlugin.Instance.Logger.LogInfo("Toggling RCS");
                //simVessel.SetActionGroup(KSPActionGroup.RCS, simVessel.GetActionGroupState(KSPActionGroup.RCS) == KSPActionGroupState.False);
                //simVessel.TriggerActionGroup(KSPActionGroup.RCS);
                simVessel.SetRCSEnabled(!simVessel.IsRCSEnabled);
            }
            if ((groups & ActionGroupBits.SASBit) != 0)
            {
                if (SimpitPlugin.Instance.config_verbose) SimpitPlugin.Instance.Logger.LogInfo("Toggling SAS");
                simVessel.SetAutopilotEnableDisable(!simVessel.AutopilotStatus.IsEnabled);
                //simVessel.TriggerActionGroup(KSPActionGroup.SAS);
            }
            if ((groups & ActionGroupBits.BrakesBit) != 0)
            {
                //Wheel brakes only gets set in VesselVehicle.SetGroup and not in VesselComponent like all the other action groups
                if (SimpitPlugin.Instance.config_verbose) SimpitPlugin.Instance.Logger.LogInfo("Toggling brakes");
                currentVessel.TriggerGroup(KSPActionGroup.Brakes);
            }
            if ((groups & ActionGroupBits.AbortBit) != 0)
            {
                if (SimpitPlugin.Instance.config_verbose) SimpitPlugin.Instance.Logger.LogInfo("Toggling abort");
                simVessel.TriggerActionGroup(KSPActionGroup.Abort);
            }
            if ((groups & ActionGroupBits.SolarPanelsBit) != 0)
            {
                if (SimpitPlugin.Instance.config_verbose) SimpitPlugin.Instance.Logger.LogInfo("Toggling solar panels");
                simVessel.TriggerActionGroup(KSPActionGroup.SolarPanels);
            }
        }

        private byte getGroups()
        {
            VesselVehicle currentVessel = null;
            VesselComponent simVessel = null;
            try
            {
                currentVessel = Vehicle.ActiveVesselVehicle; 
                simVessel = Vehicle.ActiveSimVessel; 
            }
            catch { return 0; }
            if (currentVessel == null || simVessel == null) return 0;

            byte groups = 0;
            /*
            Theresa: I couldn't find a state for the stage in KSP2
            if (simVessel.GetActionGroupState(KSPActionGroup.Stage) == KSPActionGroupState.True)
            {
                groups = (byte)(groups | ActionGroupBits.StageBit);
            }
            */
            //if (Vehicle.ActiveVesselVehicle.GetGroup(KSPActionGroup.Gear)) //This always returns false. Is probably a bug in KSP2.
            if (simVessel.GetActionGroupState(KSPActionGroup.Gear) == KSPActionGroupState.True)
            {
                groups = (byte)(groups | ActionGroupBits.GearBit);
            }
            if (simVessel.GetActionGroupState(KSPActionGroup.Lights) == KSPActionGroupState.True)
            {
                groups = (byte)(groups | ActionGroupBits.LightBit);
            }
            //if (simVessel.GetActionGroupState(KSPActionGroup.RCS) == KSPActionGroupState.True)
            if (simVessel.IsRCSEnabled)
            {
                groups = (byte)(groups | ActionGroupBits.RCSBit);
            }
            //if (simVessel.GetActionGroupState(KSPActionGroup.SAS) == KSPActionGroupState.True) This doesn't work... Use VesselComponent.AutopilotStatus.IsEnabled instead
            if (simVessel.AutopilotStatus.IsEnabled)
            {
                groups = (byte)(groups | ActionGroupBits.SASBit);
            }
            if (currentVessel.GetGroup(KSPActionGroup.Brakes))
            {
                groups = (byte)(groups | ActionGroupBits.BrakesBit);
            }
            if (simVessel.GetActionGroupState(KSPActionGroup.Abort) == KSPActionGroupState.True)
            {
                groups = (byte)(groups | ActionGroupBits.AbortBit);
            }
            if (simVessel.GetActionGroupState(KSPActionGroup.SolarPanels) == KSPActionGroupState.True)
            {
                groups = (byte)(groups | ActionGroupBits.SolarPanelsBit);
            }
            return groups;
        }
    }
}
