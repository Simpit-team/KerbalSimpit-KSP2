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
            try
            {
                ResourceDefinitionDatabase resourceDatabase = GameManager.Instance.Game.ResourceDefinitionDatabase;
                if (!resourceDatabase.IsDefinitionDataFrozen)
                {
                    SimpitPlugin.Instance.loggingQueueDebug.Enqueue("The resource database isn't ready yet. Try again later.");
                    return;
                }
                foreach (ResourceDefinitionID resourceId in resourceDatabase.GetAllResourceIDs())
                {
                    SimpitPlugin.Instance.loggingQueueDebug.Enqueue("Registering resource \"" + resourceDatabase.GetResourceNameFromID(resourceId) + "\" with id \"" + resourceId + "\".");
                }
            }
            catch { }
        }
    }
}
