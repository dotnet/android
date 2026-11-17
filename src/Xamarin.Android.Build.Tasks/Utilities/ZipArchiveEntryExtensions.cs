#nullable enable
public static class ZipArchiveEntryExtensions {
    public static bool IsDirectory (this ZipArchiveEntry entry)
    {
        return entry.Length == 0 && entry.FullName.EndsWith ("/", StringComparison.Ordinal);
    }
}