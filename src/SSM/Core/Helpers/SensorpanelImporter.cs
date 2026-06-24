using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace SSM.Core.Helpers;

/// <summary>
/// Imports AIDA64 .sensorpanel files.
/// A .sensorpanel is sp2-compatible XML (UTF-16 LE) where image files are embedded
/// inline as hex strings in &lt;IMGDAT&gt; tags. This class extracts those images
/// and writes a standard .sp2 file alongside them so OverlayCanvasRenderer can load it.
/// </summary>
public static class SensorpanelImporter
{
    private static readonly Regex ImgFilRx =
        new(@"<IMGFIL>([^<]+)</IMGFIL>", RegexOptions.Compiled);
    private static readonly Regex ImgDatRx =
        new(@"<IMGDAT>([0-9A-Fa-f]+)</IMGDAT>", RegexOptions.Compiled);

    /// <summary>
    /// Imports a .sensorpanel file into <paramref name="themesBaseDir"/>.
    /// Returns the path of the created .sp2 file, or throws on error.
    /// </summary>
    public static string Import(string sensorpanelPath, string themesBaseDir)
    {
        var themeName = Path.GetFileNameWithoutExtension(sensorpanelPath);
        var destDir   = Path.Combine(themesBaseDir, themeName);
        Directory.CreateDirectory(destDir);

        // .sensorpanel is UTF-16 LE (same as .sp2); fall back to UTF-8
        string raw;
        try   { raw = File.ReadAllText(sensorpanelPath, Encoding.Unicode); }
        catch { raw = File.ReadAllText(sensorpanelPath, Encoding.UTF8); }

        var outLines  = new List<string>();
        string lastFil = "";

        foreach (var line in raw.Split('\n'))
        {
            // Track the most recent IMGFIL on this line
            var filMatch = ImgFilRx.Match(line);
            if (filMatch.Success)
                lastFil = filMatch.Groups[1].Value.Trim();

            var datMatch = ImgDatRx.Match(line);
            if (datMatch.Success)
            {
                // Save the embedded image
                var targetFil = filMatch.Success
                    ? filMatch.Groups[1].Value.Trim()
                    : lastFil;

                if (!string.IsNullOrEmpty(targetFil))
                {
                    try
                    {
                        var bytes = Convert.FromHexString(datMatch.Groups[1].Value);
                        File.WriteAllBytes(Path.Combine(destDir, targetFil), bytes);
                    }
                    catch { /* skip corrupt data */ }
                }

                // Strip IMGDAT from the line; keep the rest (config tags)
                var cleaned = ImgDatRx.Replace(line, "").TrimEnd('\r');
                if (cleaned.Length > 0)
                    outLines.Add(cleaned);
            }
            else
            {
                outLines.Add(line.TrimEnd('\r'));
            }
        }

        // Write cleaned content as UTF-16 LE .sp2
        var sp2Path = Path.Combine(destDir, themeName + ".sp2");
        File.WriteAllText(sp2Path, string.Join('\n', outLines), Encoding.Unicode);
        return sp2Path;
    }
}
