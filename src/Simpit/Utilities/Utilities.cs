using KSP.Game;
using KSP.Sim.ResourceSystem;
using System;
using System.Runtime.InteropServices;

namespace Simpit.Utilities
{
    public class KerbalSimpitUtils
    {
        // https://stackoverflow.com/questions/2871/reading-a-c-c-data-structure-in-c-sharp-from-a-byte-array/41836532#41836532
        public static T ByteArrayToStructure<T>(byte[] bytes) where T : struct
        {
            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                return (T) Marshal.PtrToStructure(handle.AddrOfPinnedObject(),
                                                   typeof(T));
            }
            finally
            {
                handle.Free();
            }
        }

        public static void PrintAllAvailableResources()
        {
            ResourceDefinitionDatabase resourceDatabase = GameManager.Instance.Game.ResourceDefinitionDatabase;
            Dictionary<ResourceDefinitionID, double> resources = new Dictionary<ResourceDefinitionID, double>();
            foreach (ResourceDefinitionID resourceId in resourceDatabase.GetAllResourceIDs())
            {
                SimpitPlugin.Instance.Logger.LogInfo(String.Format("Found resource \"{0}\" with ID \"{1}\"", resourceDatabase.GetDefinitionData(resourceId).name, resourceId));
            }
        }
    }
}
