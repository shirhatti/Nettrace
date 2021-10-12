using System.Runtime.InteropServices;

namespace Nettrace
{
    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    internal struct MetadataEvent
    {
        public int MetaDataId;      // The Meta-Data ID that is being defined.
        public string ProviderName; // The 2 byte Unicode, null terminated string representing the Name of the Provider (e.g. EventSource)
        public int EventId;         // A small number that uniquely represents this Event within this provider.  
        public string EventName;    // The 2 byte Unicode, null terminated string representing the Name of the Event
        public long Keywords;       // 64 bit set of groups (keywords) that this event belongs to.
        public int Version;          // The version number for this event.
        public int Level;           // The verbosity (5 is verbose, 1 is only critical) for the event.
    }
}
