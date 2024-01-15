using System.Runtime.InteropServices;
using KSP.Sim.DeltaV;
using KSP.Game;
using KSP.Sim.ResourceSystem;
using KSP.Sim.impl;
using SpaceWarp.API.Game;
using KSP.Messages;
using System.Collections.Generic;

namespace Simpit.Providers
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    [Serializable]
    public struct ResourceStruct
    {
        public float Max;
        public float Available;
    }

    /// <summary>
    /// Generic provider for a resource message.
    /// This abstract class need only a default constructor to be usable, to define the channel ID,
    /// the resource name and if the computed is performed on the whole vessel or for the current stage only.
    /// </summary>
    abstract class GenericResourceProvider : GenericProvider<ResourceStruct>
    {
        private string _resourceName;
        private ResourceDefinitionID _resourceID;
        private bool _stageOnly;
        //public Dictionary<ResourceDefinitionID, ContainedResourceData> resources = new Dictionary<ResourceDefinitionID, ContainedResourceData>();
        
        public GenericResourceProvider(byte channelID, string resourceName, bool stageOnly) : base(channelID)
        {
            _resourceName = resourceName;
            _stageOnly = stageOnly;
        }

        public override void Start()
        {
            base.Start();
        }

        override protected bool updateMessage(ref ResourceStruct message)
        {
            //Try to get the resource ids as soon as the database is ready for it
            if(_resourceID.Value == ResourceDefinitionID.INVALID_ID_VALUE) 
            {
                try
                {
                    ResourceDefinitionDatabase resourceDatabase = GameManager.Instance.Game.ResourceDefinitionDatabase;
                    if (!resourceDatabase.IsDefinitionDataFrozen) return false; //Wait for the resource database to be frosen so definition data can be retrieved
                    ResourceDefinitionData resource = resourceDatabase.GetDefinitionData(resourceDatabase.GetResourceIDFromName(_resourceName));
                    _resourceID = resource.resourceDatabaseID;

                    SimpitPlugin.Instance.loggingQueueDebug.Enqueue("Registering resource \"" + _resourceName + "\" with id \"" + _resourceID + "\".");
                } 
                catch 
                {
                    return false;
                }
            }

            message.Max = 0;
            message.Available = 0;

            VesselComponent simVessel = null;
            try { simVessel = Vehicle.ActiveSimVessel; } catch { }
            if (simVessel == null) return false;

            ContainedResourceData containedResource;
            if (_stageOnly) containedResource = CalculateVesselStageResources(simVessel);
            else containedResource = CalculateVesselResources(simVessel);

            message.Max = (float)containedResource.CapacityUnits;
            message.Available = (float)containedResource.StoredUnits;

            /*
            string stageOrShip = "ship";
            if (_stageOnly) stageOrShip = "stage";
            SimpitPlugin.Instance.loggingQueueDebug.Enqueue(String.Format("Resource in {3}: \"{0}\" with \"{1}\" StoredUnits in \"{2}\" CapacityUnits.",
                GameManager.Instance.Game.ResourceDefinitionDatabase.GetResourceNameFromID(_resourceID),
                containedResource.StoredUnits,
                containedResource.CapacityUnits,
                stageOrShip));
            */

            return false;
        }

        public ContainedResourceData CalculateVesselResources(VesselComponent simVessel)
        {
            ContainedResourceData containedResource = new ContainedResourceData();
            if (simVessel.SimulationObject.objVesselBehavior.parts == null) return containedResource;

            foreach (PartComponent part in simVessel.SimulationObject.PartOwner.Parts)
            {
                if (part == null) break;

                foreach (ContainedResourceData resourceInPart in part.PartResourceContainer.GetAllResourcesContainedData())
                {
                    if (resourceInPart.ResourceID == _resourceID)
                    {
                        containedResource.CapacityUnits += resourceInPart.CapacityUnits;
                        containedResource.StoredUnits += Math.Abs(resourceInPart.StoredUnits);
                    }
                }
            }
            return containedResource;
        }

        public ContainedResourceData CalculateVesselStageResources(VesselComponent simVessel)
        {
            ContainedResourceData containedResource = new ContainedResourceData();
            if (simVessel.SimulationObject.objVesselBehavior == null || simVessel.SimulationObject.objVesselBehavior.parts == null) return containedResource;
            foreach (PartComponent part in simVessel.SimulationObject.PartOwner.Parts)
            {
                PartComponentModule_Engine module;
                if (part != null && part.TryGetModule<PartComponentModule_Engine>(out module) && module.EngineIgnited)
                {
                    foreach (ContainedResourceData resourceInPart in module.GetContainedResourceData())
                    {
                        if (resourceInPart.ResourceID == _resourceID)
                        {
                            containedResource.CapacityUnits += resourceInPart.CapacityUnits;
                            containedResource.StoredUnits += module.IsPropellantStarved ? 0.0 : Math.Abs(resourceInPart.StoredUnits);
                        }
                    }
                }
            }
            return containedResource;
        }
    }

    class MonoPropellantProvider : GenericResourceProvider { public MonoPropellantProvider() : base(OutboundPackets.MonoPropellant, "MonoPropellant", false) { } }
    class SolidFuelProvider : GenericResourceProvider { public SolidFuelProvider() : base(OutboundPackets.SolidFuel, "SolidFuel", false) { } }
    class SolidFuelStageProvider : GenericResourceProvider { public SolidFuelStageProvider() : base(OutboundPackets.SolidFuelStage, "SolidFuel", true) { } }
    class IntakeAirProvider : GenericResourceProvider { public IntakeAirProvider() : base(OutboundPackets.IntakeAir, "IntakeAir", false) { } }
    class EvaPropellantProvider : GenericResourceProvider { public EvaPropellantProvider() : base(OutboundPackets.EvaPropellant, "EVAPropellant", false) { } }
    class HydrogenProvider : GenericResourceProvider { public HydrogenProvider() : base(OutboundPackets.Hydrogen, "Hydrogen", false) { } }
    class HydrogenStageProvider : GenericResourceProvider { public HydrogenStageProvider() : base(OutboundPackets.HydrogenStage, "Hydrogen", true) { } }
    class LiquidFuelProvider : GenericResourceProvider { public LiquidFuelProvider() : base(OutboundPackets.Methane, "Methane", false) { } }
    class LiquidFuelStageProvider : GenericResourceProvider { public LiquidFuelStageProvider() : base(OutboundPackets.MethaneStage, "Methane", true) { } }
    class OxidizerProvider : GenericResourceProvider { public OxidizerProvider() : base(OutboundPackets.Oxidizer, "Oxidizer", false) { } }
    class OxidizerStageProvider : GenericResourceProvider { public OxidizerStageProvider() : base(OutboundPackets.OxidizerStage, "Oxidizer", true) { } }
    class UraniumProvider : GenericResourceProvider { public UraniumProvider() : base(OutboundPackets.Uranium, "Uranium", false) { } }
    class ElectricChargeProvider : GenericResourceProvider { public ElectricChargeProvider() : base(OutboundPackets.ElectricCharge, "ElectricCharge", false) { } }
    class XenonGasProvider : GenericResourceProvider { public XenonGasProvider() : base(OutboundPackets.XenonGas, "Xenon", false) { } }
    class XenonGasStageProvider : GenericResourceProvider { public XenonGasStageProvider() : base(OutboundPackets.XenonGasStage, "Xenon", true) { } }
    class AblatorProvider : GenericResourceProvider { public AblatorProvider() : base(OutboundPackets.Ablator, "Ablator", false) { } }
    class AblatorStageProvider : GenericResourceProvider { public AblatorStageProvider() : base(OutboundPackets.AblatorStage, "Ablator", true) { } }

    //doesn't exist any more in KSP2: class OreProvider : GenericResourceProvider { public OreProvider() : base(OutboundPackets.Ore, "Ore", false) { } }

    //The following resources exist in KSP2 but they don't seem to have any valuable info...
    //class TestRocksProvider : GenericResourceProvider { public TestRocksProvider() : base(OutboundPackets.TestRocks, "TestRocks", false) { } }
    //class MethaloxProvider : GenericResourceProvider { public MethaloxProvider() : base(OutboundPackets.Methalox, "Methalox", false) { } }
    //class MethaloxStageProvider : GenericResourceProvider { public MethaloxStageProvider() : base(OutboundPackets.MethaloxStage, "Methalox", true) { } }
    //class MethaneAirProvider : GenericResourceProvider { public MethaneAirProvider() : base(OutboundPackets.MethaneAir, "MethaneAir", false) { } }
    //class MethaneAirStageProvider : GenericResourceProvider { public MethaneAirStageProvider() : base(OutboundPackets.MethaneAirStage, "MethaneAir", true) { } }
    //class XenonECProvider : GenericResourceProvider { public XenonECProvider() : base(OutboundPackets.XenonEC, "XenonEC", false) { } }
    //class XenonECStageProvider : GenericResourceProvider { public XenonECStageProvider() : base(OutboundPackets.XenonECStage, "XenonEC", true) { } }
}