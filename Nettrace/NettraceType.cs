namespace Nettrace
{
    internal struct NettraceType
    {
        public readonly int Version { get; init; }
        public readonly int MinimumReaderVersion { get; init; }
        public readonly string Name { get; init; }
    }
}
