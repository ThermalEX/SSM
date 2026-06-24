using System.IO;
using System.Text;
using WpfColor = System.Windows.Media.Color;

namespace SSM.Core.Helpers;

public static class Sp2Writer
{
    public static void Write(string filePath, Sp2Panel panel)
    {
        var lines = new List<string>();

        lines.Add("<SPVER>SPANEL0600</SPVER>");
        lines.Add(T("SPWIDTH", (int)panel.Width) +
                  T("SPHEIGHT", (int)panel.Height) +
                  T("SPBGCOLOR", ToRef(panel.Background)));

        foreach (var el in panel.Elements)
            lines.Add(SerializeElement(el));

        // UTF-16 LE with BOM — same encoding as the original sp2 files
        File.WriteAllLines(filePath, lines, Encoding.Unicode);
    }

    private static string SerializeElement(Sp2Element el)
    {
        var sb = new StringBuilder();
        sb.Append(T("ID", el.RawId));
        sb.Append(T("ITMX", (int)el.X));
        sb.Append(T("ITMY", (int)el.Y));
        if (el.Width  > 0) sb.Append(T("RESIZW", (int)el.Width));
        if (el.Height > 0) sb.Append(T("RESIZH", (int)el.Height));

        switch (el.ElementKind)
        {
            case "IMG":
                sb.Append(T("IMGFIL", el.ImageFile));
                sb.Append(T("BGIMG", el.IsBackground ? 1 : 0));
                break;

            case "LBL":
                sb.Append(T("LBL", el.Label));
                sb.Append(T("FNTNAM", el.FontName));
                sb.Append(T("TXTSIZ", el.FontSize));
                sb.Append(T("TXTCOL", ToRef(el.TextColor)));
                break;

            case "SIMPLE" or "BAR":
                AppendTextSensorFields(sb, el);
                break;

            case "GAUGE":
                sb.Append(T("MINVAL", el.MinVal));
                sb.Append(T("MAXVAL", el.MaxVal));
                if (el.GaugeFrames.Count > 0)
                    sb.Append(T("STAFLS", string.Join("|", el.GaugeFrames)));
                break;

            case "GRAPH":
                sb.Append(T("MINVAL", el.MinVal));
                sb.Append(T("MAXVAL", el.MaxVal));
                sb.Append(T("GPHCOL", ToRef(el.GraphColor)));
                sb.Append(T("BGCOL",  ToRef(el.BgColor)));
                break;
        }

        return sb.ToString();
    }

    private static void AppendTextSensorFields(StringBuilder sb, Sp2Element el)
    {
        if (el.Label.Length > 0) sb.Append(T("LBL", el.Label));
        if (el.Unit.Length  > 0) sb.Append(T("UNT", el.Unit));
        sb.Append(T("SHWLBL", el.ShowLabel ? 1 : 0));
        sb.Append(T("SHWVAL", el.ShowValue ? 1 : 0));
        sb.Append(T("SHWUNT", el.ShowUnit  ? 1 : 0));
        sb.Append(T("FNTNAM", el.FontName));
        sb.Append(T("TXTSIZ", el.FontSize));
        sb.Append(T("TXTCOL", ToRef(el.TextColor)));
        sb.Append(T("VALCOL", ToRef(el.ValueColor)));
        sb.Append(T("LBLCOL", ToRef(el.LabelColor)));
    }

    private static string T(string tag, object val) => $"<{tag}>{val}</{tag}>";

    private static long ToRef(WpfColor c) =>
        (long)c.R | ((long)c.G << 8) | ((long)c.B << 16);
}
