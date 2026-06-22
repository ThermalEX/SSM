namespace SSM.Core.Models;

public class ThemeConfig
{
    public string Name { get; set; } = "Default";
    public string Version { get; set; } = "1.0";
    public string Background { get; set; } = "#0D1117";
    public string AccentColor { get; set; } = "#58A6FF";
    public string TextColor { get; set; } = "#E6EDF3";
    public string SecondaryTextColor { get; set; } = "#8B949E";
    public string WarningColor { get; set; } = "#D29922";
    public string DangerColor { get; set; } = "#F85149";
    public string FontFamily { get; set; } = "Segoe UI";
    public int FontSize { get; set; } = 14;
    public List<ComponentConfig> Components { get; set; } = [];
}

public class ComponentConfig
{
    public string Type        { get; set; } = string.Empty;
    public double X           { get; set; }
    public double Y           { get; set; }
    public double Width       { get; set; } = 200;
    public double Height      { get; set; } = 40;
    public string DataBinding { get; set; } = string.Empty;
    public string Label       { get; set; } = string.Empty;  // 编辑器左侧列表名
    public string Text        { get; set; } = string.Empty;  // 静态文字内容
    public double FontSize    { get; set; } = 14;
    public string FontColor   { get; set; } = "#FFFFFF";
    public double Opacity     { get; set; } = 1.0;
}
