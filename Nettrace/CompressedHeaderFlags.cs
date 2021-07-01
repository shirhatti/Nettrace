namespace Nettrace
{
    public enum CompressedHeaderFlags
    {
        MetadataId = 1 << 0,
        CaptureThreadAndSequence = 1 << 1,
        ThreadId = 1 << 2,
        StackId = 1 << 3,
        ActivityId = 1 << 4,
        RelatedActivityId = 1 << 5,
        Sorted = 1 << 6,
        DataLength = 1 << 7
    }
}
