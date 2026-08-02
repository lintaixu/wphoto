using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace PhotoViewer;

/// <summary>取得 Windows Shell 的檔案縮圖（與檔案總管同一套快取，影片封面即來自於此）</summary>
public static class ShellThumb
{
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
    interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage(SIZE size, int flags, out IntPtr phbm);
    }

    [StructLayout(LayoutKind.Sequential)]
    struct SIZE { public int cx, cy; }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    static extern void SHCreateItemFromParsingName(string path, IntPtr pbc, ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory factory);

    [DllImport("gdi32.dll")]
    static extern bool DeleteObject(IntPtr hObject);

    const int SIIGBF_BIGGERSIZEOK = 0x1;
    const int SIIGBF_THUMBNAILONLY = 0x8;
    const int SIIGBF_INCACHEONLY = 0x10;

    /// <summary>回傳影片/檔案縮圖，取不到回傳 null。cacheOnly=true 時只撈系統快取（瞬間），不現場產生</summary>
    public static BitmapSource? Get(string path, int size, bool cacheOnly = false)
    {
        try
        {
            Guid iid = typeof(IShellItemImageFactory).GUID;
            SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out var factory);
            int flags = SIIGBF_THUMBNAILONLY | SIIGBF_BIGGERSIZEOK;
            if (cacheOnly)
                flags |= SIIGBF_INCACHEONLY;
            int hr = factory.GetImage(new SIZE { cx = size, cy = size }, flags, out IntPtr hbmp);
            if (hr != 0 || hbmp == IntPtr.Zero)
                return null;
            try
            {
                var bmp = Imaging.CreateBitmapSourceFromHBitmap(hbmp, IntPtr.Zero,
                    Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                bmp.Freeze();
                return bmp;
            }
            finally
            {
                DeleteObject(hbmp);
            }
        }
        catch
        {
            return null;
        }
    }
}
