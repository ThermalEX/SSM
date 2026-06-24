using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Interop;

namespace SSM.Core.Helpers;

/// <summary>
/// Enables Win32 WM_DROPFILES on any Window, bypassing both UIPI (admin elevation)
/// and the AllowsTransparency OLE-drag restriction.
/// </summary>
public static class Win32FileDrop
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint DragQueryFile(IntPtr hDrop, uint iFile, StringBuilder? lpszFile, uint cch);

    [DllImport("shell32.dll")]
    private static extern void DragAcceptFiles(IntPtr hwnd, bool fAccept);

    [DllImport("shell32.dll")]
    private static extern void DragFinish(IntPtr hDrop);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ChangeWindowMessageFilterEx(
        IntPtr hwnd, uint message, uint action, IntPtr changeInfo);

    private const int  WM_DROPFILES   = 0x0233;
    private const int  WM_COPYDATA    = 0x004A;
    private const int  WM_COPYGLOBALDATA = 0x0049;
    private const uint MSGFLT_ALLOW   = 1;

    /// <summary>
    /// Call from OnSourceInitialized. Returns the HwndSource hook delegate —
    /// caller must keep a reference to prevent GC.
    /// </summary>
    public static HwndSourceHook Install(IntPtr hwnd, Action<string[]> onFilesDropped)
    {
        // Allow these messages to cross the UIPI privilege boundary
        ChangeWindowMessageFilterEx(hwnd, WM_DROPFILES,      MSGFLT_ALLOW, IntPtr.Zero);
        ChangeWindowMessageFilterEx(hwnd, WM_COPYDATA,       MSGFLT_ALLOW, IntPtr.Zero);
        ChangeWindowMessageFilterEx(hwnd, WM_COPYGLOBALDATA, MSGFLT_ALLOW, IntPtr.Zero);

        DragAcceptFiles(hwnd, true);

        return (_, msg, wParam, _, ref handled) =>
        {
            if (msg != WM_DROPFILES) return IntPtr.Zero;
            var files = QueryFiles(wParam);
            DragFinish(wParam);
            handled = true;
            if (files.Length > 0)
                onFilesDropped(files);
            return IntPtr.Zero;
        };
    }

    private static string[] QueryFiles(IntPtr hDrop)
    {
        uint count = DragQueryFile(hDrop, 0xFFFFFFFF, null, 0);
        var result = new string[count];
        for (uint i = 0; i < count; i++)
        {
            var sb = new StringBuilder(260);
            DragQueryFile(hDrop, i, sb, 260);
            result[i] = sb.ToString();
        }
        return result;
    }
}
