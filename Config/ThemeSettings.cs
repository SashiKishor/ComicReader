namespace ComicReader.Config;

/// <summary>
/// Theme colors and styling variables for Netflix-inspired dark UI.
/// </summary>
public sealed class ThemeSettings
{
    public string PrimaryBackground { get; set; } = "#141414";
    public string SecondaryBackground { get; set; } = "#181818";
    public string CardBackground { get; set; } = "#232323";
    public string AccentColor { get; set; } = "#E50914";
    public string AccentHoverColor { get; set; } = "#F40612";
    public string TextPrimary { get; set; } = "#FFFFFF";
    public string TextSecondary { get; set; } = "#AAAAAA";
    public string SurfaceBorder { get; set; } = "#333333";
}

