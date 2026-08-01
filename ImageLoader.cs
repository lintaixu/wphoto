using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Sdcb.LibRaw;

namespace PhotoViewer;

public static class ImageLoader
{
    public static readonly HashSet<string> RawExts = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cr2", ".cr3", ".nef", ".nrw", ".arw", ".srf", ".sr2", ".dng",
        ".raf", ".orf", ".rw2", ".pef", ".srw", ".raw", ".rwl", ".3fr",
        ".fff", ".iiq", ".x3f", ".erf", ".mrw", ".kdc", ".dcr"
    };

    public static readonly HashSet<string> StandardExts = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".tif", ".tiff", ".bmp", ".webp", ".gif",
        ".heic", ".heif", ".hif"   // Fuji/Sony HEIF（Magick.NET 解碼，不依賴系統擴充）
    };

    // Fuji: MOV/MP4；Sony: XAVC S/HS (MP4)、AVCHD (MTS/M2TS)、專業機 MXF
    public static readonly HashSet<string> VideoExts = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mov", ".m4v", ".mts", ".m2ts", ".avi", ".mxf"
    };

    public static bool IsRaw(string path) => RawExts.Contains(Path.GetExtension(path));

    public static bool IsVideo(string path) => VideoExts.Contains(Path.GetExtension(path));

    public static bool IsSupported(string path) =>
        IsRaw(path) || IsVideo(path) || StandardExts.Contains(Path.GetExtension(path));

    /// <summary>讀取影像（背景執行緒安全，回傳前已 Freeze）</summary>
    public static BitmapSource Load(string path)
    {
        BitmapSource result = IsRaw(path) ? LoadRaw(path) : LoadStandard(path);
        if (!result.IsFrozen && result.CanFreeze)
            result.Freeze();
        return result;
    }

    static BitmapSource LoadStandard(string path)
    {
        try
        {
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.UriSource = new Uri(path);
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.Rotation = OrientationToRotation(ExifService.GetOrientation(path));
            bi.EndInit();
            bi.Freeze();
            return bi;
        }
        catch
        {
            // WPF 內建解碼器不支援（HEIF/HIF、少見的 TIFF 變體）→ 改用 Magick.NET
            return LoadWithMagick(path);
        }
    }

    static BitmapSource LoadWithMagick(string path)
    {
        using var mi = new ImageMagick.MagickImage(path);
        mi.AutoOrient();
        byte[] bmp = mi.ToByteArray(ImageMagick.MagickFormat.Bmp);
        return FromJpegBytes(bmp, Rotation.Rotate0);
    }

    /// <summary>內嵌預覽至少要有這麼多像素才拿來當主圖，否則走完整解碼</summary>
    const int MinPreviewPixels = 2_000_000;

    static BitmapSource LoadRaw(string path)
    {
        using RawContext ctx = RawContext.OpenFile(path);

        // RAW 通常內嵌多張 JPEG 預覽（例如富士 RAF：160x120 小縮圖 + 全尺寸預覽），
        // 逐一檢查挑最大的那張，比完整解碼快非常多
        byte[]? bestJpeg = null;
        int bestPixels = 0;
        for (int i = 0; i < 4; i++)
        {
            try
            {
                ctx.UnpackThumbnail(i);
                using ProcessedImage t = ctx.MakeDcrawMemoryThumbnail();
                if (t.ImageType == ProcessedImageType.Jpeg && t.Width * t.Height > bestPixels)
                {
                    bestPixels = t.Width * t.Height;
                    bestJpeg = t.AsSpan<byte>().ToArray();
                }
            }
            catch
            {
                break; // 沒有更多縮圖
            }
        }
        if (bestJpeg != null && bestPixels >= MinPreviewPixels)
            return FromJpegBytes(bestJpeg, OrientationToRotation(ExifService.GetOrientation(path)));

        // 沒有夠大的預覽 → 完整解碼（半尺寸就夠螢幕看，速度快 4 倍；
        // LibRaw 會自動套用相機白平衡與旋轉）
        ctx.Unpack();
        ctx.DcrawProcess(c => c.HalfSize = true);
        using ProcessedImage img = ctx.MakeDcrawMemoryImage();
        int stride = img.Width * 3;
        var bmp = BitmapSource.Create(img.Width, img.Height, 96, 96,
            PixelFormats.Rgb24, null, img.DataPointer, img.Height * stride, stride);
        bmp.Freeze();
        return bmp;
    }

    static BitmapSource FromJpegBytes(byte[] data, Rotation rotation)
    {
        var bi = new BitmapImage();
        bi.BeginInit();
        bi.StreamSource = new MemoryStream(data);
        bi.CacheOption = BitmapCacheOption.OnLoad;
        bi.Rotation = rotation;
        bi.EndInit();
        bi.Freeze();
        return bi;
    }

    static Rotation OrientationToRotation(int orientation) => orientation switch
    {
        6 => Rotation.Rotate90,
        3 => Rotation.Rotate180,
        8 => Rotation.Rotate270,
        _ => Rotation.Rotate0,
    };
}
