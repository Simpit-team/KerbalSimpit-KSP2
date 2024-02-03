using KSP.Game;
using KSP.Sim;
using KSP.Sim.impl;
using SpaceWarp.API.Game;
using SpaceWarp.API.Game.Extensions;
using System.Collections.Concurrent;
using UnityEngine;

namespace Simpit.Providers
{
    public class KerbalSimpitActionProvider : MonoBehaviour
    {
        // Inbound messages
        private EventDataObsolete<byte, object> AGActivateChannel, AGDeactivateChannel,
            AGToggleChannel, AGSetSingleChannel;

        // Outbound messages
        private EventDataObsolete<byte, object> AGStateChannel, AdvancedAGStateChannel;

        // TODO: Only using a single byte buffer for each of these is
        // technically unsafe. It's not impossible that multiple controllers
        // will attempt to send new packets between each Update(), and only
        // the last one will be affected. But it is unlikely, which is why
        // I'm not addressing it now.
        private volatile byte activateBuffer, deactivateBuffer,
            toggleBuffer, currentStateBuffer;
        //Using a queue here in case the controller rapidly sets multiple action groups in succession
        //to avoid the buffer being overwritten before it can be applied to the game
        private volatile ConcurrentQueue<byte> setSingleBuffer = new ConcurrentQueue<byte>();
        private volatile UInt32 currentAdvancedStateBuffer;

        // If set to true, the state should be sent at the next update even if no changes
        // are detected (for instance to initialise it after a new registration).
        private bool resendState, resendAdvancedState = false;

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
            AGSetSingleChannel = GameEvents.FindEvent<EventDataObsolete<byte, object>>("onSerialReceived" + +InboundPackets.SetSingleActionGroup);
            if (AGSetSingleChannel != null) AGSetSingleChannel.Add(actionSetSingleCallback);

            AGStateChannel = GameEvents.FindEvent<EventDataObsolete<byte, object>>("toSerial" + OutboundPackets.ActionGroups);
            GameEvents.FindEvent<EventDataObsolete<byte, object>>("onSerialChannelForceSend" + OutboundPackets.ActionGroups).Add(resendActionGroup);
            AdvancedAGStateChannel = GameEvents.FindEvent<EventDataObsolete<byte, object>>("toSerial" + OutboundPackets.AdvancedActionGroups);
            GameEvents.FindEvent<EventDataObsolete<byte, object>>("onSerialChannelForceSend" + OutboundPackets.AdvancedActionGroups).Add(resendAdvancedActionGroup);
        }

        public void OnDestroy()
        {
            if (AGActivateChannel != null) AGActivateChannel.Remove(actionActivateCallback);
            if (AGDeactivateChannel != null) AGDeactivateChannel.Remove(actionDeactivateCallback);
            if (AGToggleChannel != null) AGToggleChannel.Remove(actionToggleCallback);
            if (AGSetSingleChannel != null) AGSetSingleChannel.Remove(actionSetSingleCallback);
        }

        public void resendActionGroup(byte ID, object Data)
        {
            resendState = true;
        }

        public void resendAdvancedActionGroup(byte ID, object Data)
        {
            resendAdvancedState = true;
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

            byte actionGroupAndSetting;
            while (!setSingleBuffer.IsEmpty && setSingleBuffer.TryDequeue(out actionGroupAndSetting))
            {
                setSingleGroup(actionGroupAndSetting);
            }

            updateCurrentState();
            updateAdvancedCurrentState();
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

        public void actionSetSingleCallback(byte ID, object Data)
        {
            byte[] payload = (byte[])Data;
            setSingleBuffer.Enqueue(payload[0]);
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

        private bool updateAdvancedCurrentState()
        {
            UInt32 newState = getAdvancedGroups();
            if (newState != currentAdvancedStateBuffer || resendAdvancedState)
            {
                resendAdvancedState = false;
                if (AdvancedAGStateChannel != null)
                {
                    AdvancedAGStateChannel.Fire(OutboundPackets.AdvancedActionGroups, newState);
                    currentAdvancedStateBuffer = newState;
                }
                return true;
            }
            else
            {
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
                if (SimpitPlugin.Instance.config_verbose) SimpitPlugin.Instance.loggingQueueInfo.Enqueue("Activating stage");
                currentVessel.SetStage(true);
            }
            if ((groups & ActionGroupBits.GearBit) != 0)
            {
                if (SimpitPlugin.Instance.config_verbose) SimpitPlugin.Instance.loggingQueueInfo.Enqueue("Activating gear");
                //currentVessel.SetGroup(KSPActionGroup.Gear, true); //This sets the Gear state, but it can't be read. Probably a KSP2 bug. Use VesselComponent.SetActionGroup() instead
                simVessel.SetActionGroup(KSPActionGroup.Gear, true);
            }
            if ((groups & ActionGroupBits.LightBit) != 0)
            {
                if (SimpitPlugin.Instance.config_verbose) SimpitPlugin.Instance.loggingQueueInfo.Enqueue("Activating light");
                simVessel.SetActionGroup(KSPActionGroup.Lights, true);
            }
            if ((groups & ActionGroupBits.RCSBit) != 0)
            {
                if (SimpitPlugin.Instance.config_verbose) SimpitPlugin.Instance.loggingQueueInfo.Enqueue("Activating RCS");
                simVessel.SetActionGroup(KSPActionGroup.RCS, true);
                //simVessel.SetRCSEnabled(true);
            }
            if ((groups & ActionGroupBits.SASBit) != 0)
            {
                if (SimpitPlugin.Instance.config_verbose) SimpitPlugin.Instance.loggingQueueInfo.Enqueue("Activating SAS");
                simVessel.SetAutopilotEnableDisable(true);
                //simVessel.SetActionGroup(KSPActionGroup.SAS, true);
            }
            if ((groups & ActionGroupBits.BrakesBit) != 0)
            {
                //Wheel brakes only gets set in VesselVehicle.SetGroup and not in VesselComponent like all the other action groups
                if (SimpitPlugin.Instance.config_verbose) SimpitPlugin.Instance.loggingQueueInfo.Enqueue("Activating brakes");
                currentVessel.SetGroup(KSPActionGroup.Brakes, true);
            }
            if ((groups & ActionGroupBits.AbortBit) != 0)
            {
                if (SimpitPlugin.Instance.config_verbose) SimpitPlugin.Instance.loggingQueueInfo.Enqueue("Activating abort");
                simVessel.SetActionGroup(KSPActionGroup.Abort, true);
            }
            /* Moved to single action group message
            if ((groups & ActionGroupBits.SolarPanelsBit) != 0)
            {
                if (SimpitPlugin.Instance.config_verbose) SimpitPlugin.Instance.loggingQueueInfo.Enqueue("Activating solar panels");
                simVessel.SetActionGroup(KSPActionGroup.SolarPanels, true);
            }
            */
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
                if (SimpitPlugin.Instance.config_verbose) SimpitPlugin.Instance.loggingQueueInfo.Enqueue("Deactivating stage");
                currentVessel.SetStage(false);
            }
            if ((groups & ActionGroupBits.GearBit) != 0)
            {
                if (SimpitPlugin.Instance.config_verbose) SimpitPlugin.Instance.loggingQueueInfo.Enqueue("Deactivating gear");
                //currentVessel.SetGroup(KSPActionGroup.Gear, false);
                simVessel.SetActionGroup(KSPActionGroup.Gear, false);
            }
            if ((groups & ActionGroupBits.LightBit) != 0)
            {
                if (SimpitPlugin.Instance.config_verbose) SimpitPlugin.Instance.loggingQueueInfo.Enqueue("Deactivating light");
                simVessel.SetActionGroup(KSPActionGroup.Lights, false);
            }
            if ((groups & ActionGroupBits.RCSBit) != 0)
            {
                if (SimpitPlugin.Instance.config_verbose) SimpitPlugin.Instance.loggingQueueInfo.Enqueue("Deactivating RCS");
                simVessel.SetActionGroup(KSPActionGroup.RCS, false);
                //simVessel.SetRCSEnabled(false);
            }
            if ((groups & ActionGroupBits.SASBit) != 0)
            {
                if (SimpitPlugin.Instance.config_verbose) SimpitPlugin.Instance.loggingQueueInfo.Enqueue("Deactivating SAS");
                simVessel.SetAutopilotEnableDisable(false);
                //simVessel.SetActionGroup(KSPActionGroup.SAS, false);
            }
            if ((groups & ActionGroupBits.BrakesBit) != 0)
            {
                //Wheel brakes only gets set in VesselVehicle.SetGroup and not in VesselComponent like all the other action groups
                if (SimpitPlugin.Instance.config_verbose) SimpitPlugin.Instance.loggingQueueInfo.Enqueue("Deactivating brakes");
                currentVessel.SetGroup(KSPActionGroup.Brakes, false);
            }
            if ((groups & ActionGroupBits.AbortBit) != 0)
            {
                if (SimpitPlugin.Instance.config_verbose) SimpitPlugin.Instance.loggingQueueInfo.Enqueue("Deactivating abort");
                simVessel.SetActionGroup(KSPActionGroup.Abort, false);
            }
            /* Moved to single action group message
            if ((groups & ActionGroupBits.SolarPanelsBit) != 0)
            {
                if (SimpitPlugin.Instance.config_verbose) SimpitPlugin.Instance.loggingQueueInfo.Enqueue("Deactivating solar panels");
                simVessel.SetActionGroup(KSPActionGroup.SolarPanels, false);
            }
            */
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
                if (SimpitPlugin.Instance.config_verbose) SimpitPlugin.Instance.loggingQueueInfo.Enqueue("Toggling (activating) stage");
                currentVessel.SetStage(true);
            }
            if ((groups & ActionGroupBits.GearBit) != 0)
            {
                if (SimpitPlugin.Instance.config_verbose) SimpitPlugin.Instance.loggingQueueInfo.Enqueue("Toggling gear");
                simVessel.TriggerActionGroup(KSPActionGroup.Gear);
                //currentVessel.TriggerGroup(KSPActionGroup.Gear);
            }
            if ((groups & ActionGroupBits.LightBit) != 0)
            {
                if (SimpitPlugin.Instance.config_verbose) SimpitPlugin.Instance.loggingQueueInfo.Enqueue("Toggling light");
                simVessel.TriggerActionGroup(KSPActionGroup.Lights);
            }
            if ((groups & ActionGroupBits.RCSBit) != 0)
            {
                if (SimpitPlugin.Instance.config_verbose) SimpitPlugin.Instance.loggingQueueInfo.Enqueue("Toggling RCS");
                //simVessel.SetActionGroup(KSPActionGroup.RCS, simVessel.GetActionGroupState(KSPActionGroup.RCS) == KSPActionGroupState.False);
                simVessel.TriggerActionGroup(KSPActionGroup.RCS);
                //simVessel.SetRCSEnabled(!simVessel.IsRCSEnabled);
            }
            if ((groups & ActionGroupBits.SASBit) != 0)
            {
                if (SimpitPlugin.Instance.config_verbose) SimpitPlugin.Instance.loggingQueueInfo.Enqueue("Toggling SAS");
                simVessel.SetAutopilotEnableDisable(!simVessel.AutopilotStatus.IsEnabled);
                //simVessel.TriggerActionGroup(KSPActionGroup.SAS);
            }
            if ((groups & ActionGroupBits.BrakesBit) != 0)
            {
                //Wheel brakes only gets set in VesselVehicle.SetGroup and not in VesselComponent like all the other action groups
                if (SimpitPlugin.Instance.config_verbose) SimpitPlugin.Instance.loggingQueueInfo.Enqueue("Toggling brakes");
                currentVessel.TriggerGroup(KSPActionGroup.Brakes);
            }
            if ((groups & ActionGroupBits.AbortBit) != 0)
            {
                if (SimpitPlugin.Instance.config_verbose) SimpitPlugin.Instance.loggingQueueInfo.Enqueue("Toggling abort");
                simVessel.TriggerActionGroup(KSPActionGroup.Abort);
            }
            /* Moved to single action group message
            if ((groups & ActionGroupBits.SolarPanelsBit) != 0)
            {
                if (SimpitPlugin.Instance.config_verbose) SimpitPlugin.Instance.loggingQueueInfo.Enqueue("Toggling solar panels");
                simVessel.TriggerActionGroup(KSPActionGroup.SolarPanels);
            }
            */
        }

        
        private void setSingleGroup(byte actionGroupAndSetting)
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

            //Bitmask the first six bits as which action group to use and the last two bits as what to do with said action group
            int actionGroupIndex = (actionGroupAndSetting & 0b11111100) >> 2;
            byte actionSetting = (byte)(actionGroupAndSetting & 0b00000011);

            bool activate = (actionSetting == ActionGroupSettings.activate);
            bool deactivate = (actionSetting == ActionGroupSettings.deactivate);
            bool toggle = (actionSetting == ActionGroupSettings.toggle);

            string debugString;
            if (activate) debugString = "Activate action group ";
            else if (deactivate) debugString = "Deactivate action group ";
            else debugString = "Toggle action group ";

            switch (actionGroupIndex)
            {
                case AdvancedActionGroupIndexes.advancedStageAction:
                    debugString += "stage.";
                    if (activate || toggle) currentVessel.SetStage(true);
                    else currentVessel.SetStage(false);
                    break;
                case AdvancedActionGroupIndexes.advancedGearAction:
                    debugString += "gear.";
                    if (activate) simVessel.SetActionGroup(KSPActionGroup.Gear, true);
                    else if(deactivate) simVessel.SetActionGroup(KSPActionGroup.Gear, false);
                    else simVessel.TriggerActionGroup(KSPActionGroup.Gear);
                    break;
                case AdvancedActionGroupIndexes.advancedLightAction:
                    debugString += "lights.";
                    if (activate) simVessel.SetActionGroup(KSPActionGroup.Lights, true);
                    else if(deactivate) simVessel.SetActionGroup(KSPActionGroup.Lights, false);
                    else simVessel.TriggerActionGroup(KSPActionGroup.Lights);
                    break;
                case AdvancedActionGroupIndexes.advancedRcsAction:
                    debugString += "RCS.";
                    if (activate) simVessel.SetActionGroup(KSPActionGroup.RCS, true);// SetRCSEnabled(true);
                    else if (deactivate) simVessel.SetActionGroup(KSPActionGroup.RCS, false);// SetRCSEnabled(false);
                    else simVessel.TriggerActionGroup(KSPActionGroup.RCS);// SetRCSEnabled(!simVessel.IsRCSEnabled);
                    break;
                case AdvancedActionGroupIndexes.advancedSasAction:
                    debugString += "SAS.";
                    if (activate) simVessel.SetAutopilotEnableDisable(true);
                    else if (deactivate) simVessel.SetAutopilotEnableDisable(false);
                    else simVessel.SetAutopilotEnableDisable(!simVessel.AutopilotStatus.IsEnabled);
                    break;
                case AdvancedActionGroupIndexes.advancedBrakesAction:
                    debugString += "brakes.";
                    if (activate) currentVessel.SetGroup(KSPActionGroup.Brakes, true);
                    else if (deactivate) currentVessel.SetGroup(KSPActionGroup.Brakes, false);
                    else currentVessel.TriggerGroup(KSPActionGroup.Brakes);
                    break;
                case AdvancedActionGroupIndexes.advancedAbortAction:
                    debugString += "abort.";
                    if (activate) simVessel.SetActionGroup(KSPActionGroup.Abort, true);
                    else if (deactivate) simVessel.SetActionGroup(KSPActionGroup.Abort, false);
                    else simVessel.TriggerActionGroup(KSPActionGroup.Abort);
                    break;
                case AdvancedActionGroupIndexes.advancedSolarAction:
                    debugString += "solar.";
                    if (activate) simVessel.SetActionGroup(KSPActionGroup.SolarPanels, true);
                    else if (deactivate) simVessel.SetActionGroup(KSPActionGroup.SolarPanels, false);
                    else simVessel.TriggerActionGroup(KSPActionGroup.SolarPanels);
                    break;
                case AdvancedActionGroupIndexes.advancedRadiatorAction:
                    debugString += "radiator.";
                    if (activate) simVessel.SetActionGroup(KSPActionGroup.RadiatorPanels, true);
                    else if (deactivate) simVessel.SetActionGroup(KSPActionGroup.RadiatorPanels, false);
                    else simVessel.TriggerActionGroup(KSPActionGroup.RadiatorPanels);
                    break;
                case AdvancedActionGroupIndexes.advancedScienceAction:
                    debugString += "science.";
                    if (activate) simVessel.SetActionGroup(KSPActionGroup.Science, true);
                    else if (deactivate) simVessel.SetActionGroup(KSPActionGroup.Science, false);
                    else simVessel.TriggerActionGroup(KSPActionGroup.Science);
                    break;
                default:
                    break;
            }

            if (SimpitPlugin.Instance.config_verbose) SimpitPlugin.Instance.loggingQueueInfo.Enqueue(debugString);
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
            //if (currentVessel.GetGroup(KSPActionGroup.Brakes))
            if (simVessel.GetActionGroupState(KSPActionGroup.Brakes) == KSPActionGroupState.True)
            {
                groups = (byte)(groups | ActionGroupBits.BrakesBit);
            }
            if (simVessel.GetActionGroupState(KSPActionGroup.Abort) == KSPActionGroupState.True)
            {
                groups = (byte)(groups | ActionGroupBits.AbortBit);
            }
            /* Moved to single action group message
            if (simVessel.GetActionGroupState(KSPActionGroup.SolarPanels) == KSPActionGroupState.True)
            {
                groups = (byte)(groups | ActionGroupBits.SolarPanelsBit);
            }
            */
            return groups;
        }

        private UInt32 getAdvancedGroups()
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

            UInt32 advancedGroups = 0;

            //Move the state of each action group to it's according place in the byte array

            //None = 0,
            //True = 1,
            //False = 2,
            //Mixed = 3
            UInt32 state = (UInt32)simVessel.GetActionGroupState(KSPActionGroup.Gear);
            int moveBy = AdvancedActionGroupIndexes.advancedGearAction * 2;
            advancedGroups |= state << moveBy;

            state = (UInt32)simVessel.GetActionGroupState(KSPActionGroup.Lights);
            moveBy = AdvancedActionGroupIndexes.advancedLightAction * 2;
            advancedGroups |= state << moveBy;

            state = (UInt32)KSPActionGroupState.False;
            if (simVessel.IsRCSEnabled) state = (UInt32)KSPActionGroupState.True;
            moveBy = AdvancedActionGroupIndexes.advancedRcsAction * 2;
            advancedGroups |= state << moveBy;

            state = (UInt32)KSPActionGroupState.False;
            if (simVessel.AutopilotStatus.IsEnabled) state = (UInt32)KSPActionGroupState.True;
            moveBy = AdvancedActionGroupIndexes.advancedSasAction * 2;
            advancedGroups |= state << moveBy;

            state = (UInt32)simVessel.GetActionGroupState(KSPActionGroup.Brakes);
            moveBy = AdvancedActionGroupIndexes.advancedBrakesAction * 2;
            advancedGroups |= state << moveBy;

            state = (UInt32)simVessel.GetActionGroupState(KSPActionGroup.Abort);
            moveBy = AdvancedActionGroupIndexes.advancedAbortAction * 2;
            advancedGroups |= state << moveBy;

            state = (UInt32)simVessel.GetActionGroupState(KSPActionGroup.SolarPanels);
            moveBy = AdvancedActionGroupIndexes.advancedSolarAction * 2;
            advancedGroups |= state << moveBy;

            state = (UInt32)simVessel.GetActionGroupState(KSPActionGroup.RadiatorPanels);
            moveBy = AdvancedActionGroupIndexes.advancedRadiatorAction * 2;
            advancedGroups |= state << moveBy;

            //state = (UInt32)simVessel.GetActionGroupState(KSPActionGroup.Science);
            //The Science Action Group uses bitmasks to display if science experiments are available and if science experiments are running.
            state = 0;
            VesselDataProvider vesselData = GameManager.Instance.Game.ViewController.DataProvider.VesselDataProvider;
            if (vesselData != null && vesselData.GetScienceStatusIndicatorOpportunityAvailable()) state |= 1;
            if (vesselData != null && vesselData.GetScienceStatusIndicatorExperimentInProgress()) state |= 2;
            moveBy = AdvancedActionGroupIndexes.advancedScienceAction * 2;
            advancedGroups |= state << moveBy;

            return advancedGroups;
        }
    }
}
