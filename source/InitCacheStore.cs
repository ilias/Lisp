namespace Lisp;

internal static class InitCacheStore
{
    private sealed record Snapshot(string Path, DateTime StampUtc, IReadOnlyList<InitEntry> Entries);

    private static Snapshot? _snapshot;

    public static IReadOnlyList<InitEntry>? TryGet(string path, DateTime stampUtc)
    {
        var snapshot = _snapshot;
        return snapshot != null && snapshot.Path == path && snapshot.StampUtc == stampUtc
            ? snapshot.Entries
            : null;
    }

    public static void Save(string path, DateTime stampUtc, IReadOnlyList<InitEntry> entries)
    {
        _snapshot = new Snapshot(path, stampUtc, [.. entries]);
    }
}