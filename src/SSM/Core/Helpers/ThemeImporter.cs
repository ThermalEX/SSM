using System.IO;
using System.IO.Compression;

namespace SSM.Core.Helpers;

/// <summary>
/// Shared import logic for .sensorpanel / .spzip / .sp2 theme files.
/// Existing directories are silently overwritten.
/// </summary>
public static class ThemeImporter
{
    public static readonly string[] AcceptedExts = [".sensorpanel", ".spzip", ".sp2"];

    /// <summary>
    /// Imports the given file into <paramref name="themesDir"/> and returns
    /// the absolute path of the resulting .sp2 file.
    /// </summary>
    public static string Import(string path, string themesDir)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".sensorpanel" => SensorpanelImporter.Import(path, themesDir),
            ".spzip"       => ImportSpzip(path, themesDir),
            ".sp2"         => ImportSp2Dir(path, themesDir),
            _              => throw new NotSupportedException($"不支持的文件格式：{ext}")
        };
    }

    private static string ImportSpzip(string zipPath, string themesDir)
    {
        var destDir = Path.Combine(themesDir, Path.GetFileNameWithoutExtension(zipPath));
        if (Directory.Exists(destDir)) Directory.Delete(destDir, true);
        Directory.CreateDirectory(themesDir);
        ZipFile.ExtractToDirectory(zipPath, destDir);
        return Directory.GetFiles(destDir, "*.sp2").FirstOrDefault()
            ?? throw new InvalidDataException("压缩包中未找到 .sp2 文件");
    }

    private static string ImportSp2Dir(string sp2Path, string themesDir)
    {
        var srcDir  = Path.GetDirectoryName(sp2Path)!;
        var destDir = Path.Combine(themesDir, Path.GetFileName(srcDir));
        if (Directory.Exists(destDir)) Directory.Delete(destDir, true);
        CopyDirectory(srcDir, destDir);
        return Path.Combine(destDir, Path.GetFileName(sp2Path));
    }

    private static void CopyDirectory(string src, string dst)
    {
        Directory.CreateDirectory(dst);
        foreach (var file in Directory.GetFiles(src))
            File.Copy(file, Path.Combine(dst, Path.GetFileName(file)), overwrite: true);
        foreach (var dir in Directory.GetDirectories(src))
            CopyDirectory(dir, Path.Combine(dst, Path.GetFileName(dir)));
    }
}
