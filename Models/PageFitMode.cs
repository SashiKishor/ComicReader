using System.ComponentModel;
using System.Text.Json.Serialization;

namespace ComicReader.Models;

/// <summary>
/// Display sizing mode for comic reader viewer.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PageFitMode
{
    [Description("Fit to Screen")]
    FitToScreen,

    [Description("Fit to Width")]
    FitToWidth,

    [Description("Fit to Height")]
    FitToHeight,

    [Description("Original Size (1:1)")]
    OriginalSize
}
