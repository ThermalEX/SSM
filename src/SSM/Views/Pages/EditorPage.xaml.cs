using System.IO;
using System.Windows.Controls;
using UserControl = System.Windows.Controls.UserControl;
using SSM.Core.Interfaces;

namespace SSM.Views.Pages;

public partial class EditorPage : UserControl
{
    public EditorPage()
    {
        InitializeComponent();
    }

    public void Initialize(IHardwareMonitorService monitor, string sp2Path)
    {
        Preview.Initialize(monitor, sp2Path);
        UpdateTemplateName(sp2Path);
    }

    public void ReloadTemplate(string sp2Path)
    {
        Preview.ReloadTemplate(sp2Path);
        UpdateTemplateName(sp2Path);
    }

    private void UpdateTemplateName(string sp2Path)
    {
        var dir  = Path.GetDirectoryName(sp2Path) ?? "";
        var name = Path.GetFileName(dir);
        TemplateNameText.Text = name;
    }

    public void Cleanup() => Preview.Cleanup();
}
