using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace SSM.Core.Helpers;

public static class SensorpanelImporter
{
    private static readonly Regex ImgFilRx =
        new(@"<IMGFIL>([^<]+)</IMGFIL>", RegexOptions.Compiled);
    private static readonly Regex ImgDatRx =
        new(@"<IMGDAT>([0-9A-Fa-f]+)</IMGDAT>", RegexOptions.Compiled);

    public static string Import(string sensorpanelPath, string themesBaseDir)
    {
        var themeName = Path.GetFileNameWithoutExtension(sensorpanelPath);
        var destDir   = Path.Combine(themesBaseDir, themeName);
        Directory.CreateDirectory(destDir);

        var enc  = DetectEncoding(sensorpanelPath);
        var raw  = File.ReadAllText(sensorpanelPath, enc);

        var outLines   = new List<string>();
        string lastFil = "";

        foreach (var line in raw.Split('\n'))
        {
            var filMatch = ImgFilRx.Match(line);
            if (filMatch.Success)
                lastFil = filMatch.Groups[1].Value.Trim();

            var datMatch = ImgDatRx.Match(line);
            if (datMatch.Success)
            {
                var origFil  = filMatch.Success ? filMatch.Groups[1].Value.Trim() : lastFil;
                var baseName = string.IsNullOrEmpty(origFil) ? null : Path.GetFileName(origFil);

                if (!string.IsNullOrEmpty(baseName))
                {
                    try
                    {
                        var bytes = Convert.FromHexString(datMatch.Groups[1].Value.Trim());
                        File.WriteAllBytes(Path.Combine(destDir, baseName), bytes);
                    }
                    catch { /* skip corrupt data */ }
                }

                // Strip IMGDAT; normalize IMGFIL to base filename
                var cleaned = ImgDatRx.Replace(line, "").TrimEnd('\r');
                if (!string.IsNullOrEmpty(baseName) && baseName != origFil)
                    cleaned = ImgFilRx.Replace(cleaned, $"<IMGFIL>{baseName}</IMGFIL>");
                if (cleaned.Trim().Length > 0)
                    outLines.Add(cleaned);
            }
            else
            {
                // Normalize any IMGFIL paths on non-IMGDAT lines
                var processedLine = line.TrimEnd('\r');
                if (filMatch.Success)
                {
                    var origFil  = filMatch.Groups[1].Value.Trim();
                    var baseName = Path.GetFileName(origFil);
                    if (baseName != origFil)
                        processedLine = ImgFilRx.Replace(processedLine, $"<IMGFIL>{baseName}</IMGFIL>");
                }
                outLines.Add(processedLine);
            }
        }

        // Write cleaned content as UTF-16 LE .sp2 (our standard format)
        var sp2Path = Path.Combine(destDir, themeName + ".sp2");
        File.WriteAllText(sp2Path, string.Join('\n', outLines), Encoding.Unicode);
        return sp2Path;
    }

    // Detect whether the file is UTF-16 LE or UTF-8 by inspecting the first bytes.
    // AIDA64 .sp2 files are UTF-16 LE (first char is '<' = 3C 00).
    // Some .sensorpanel exports are plain UTF-8 (first byte is 0x3C '<').
    private static Encoding DetectEncoding(string path)
    {
        Span<byte> header = stackalloc byte[4];
        using var fs = File.OpenRead(path);
        int n = fs.Read(header);
        if (n >= 2 && header[0] == 0xFF && header[1] == 0xFE) return Encoding.Unicode;   // UTF-16 LE BOM
        if (n >= 2 && header[0] == 0x3C && header[1] == 0x00) return Encoding.Unicode;   // UTF-16 LE, no BOM
        if (n >= 3 && header[0] == 0xEF && header[1] == 0xBB && header[2] == 0xBF) return Encoding.UTF8; // UTF-8 BOM
        // 无 BOM 时用 GBK（AIDA64 中文版导出格式），需要先注册 CodePagesEncodingProvider
        try { return Encoding.GetEncoding(936); }
        catch { return Encoding.UTF8; }
    }
}
