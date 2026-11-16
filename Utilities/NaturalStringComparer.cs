using System.Runtime.InteropServices;

namespace ComicReader.Utilities;

/// <summary>
/// Natural sort comparer using Windows native StrCmpLogicalW for intuitive page/filename ordering (e.g. Page 2 before Page 10).
/// </summary>
public sealed class NaturalStringComparer : IComparer<string>
{
    public static readonly NaturalStringComparer Instance = new();

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int StrCmpLogicalW(string psz1, string psz2);

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return -1;
        if (y is null) return 1;

        try
        {
            return StrCmpLogicalW(x, y);
        }
        catch
        {
            return string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
        }
    }
}

