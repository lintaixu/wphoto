using System;
using System.Collections.Generic;
using System.Linq;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;

namespace PhotoViewer;

public record InfoRow(string Label, string Value);

public static class ExifService
{
    /// <summary>讀取主要拍攝參數</summary>
    public static List<InfoRow> Read(string path)
    {
        var key = new List<InfoRow>();

        IReadOnlyList<MetadataExtractor.Directory> dirs;
        try
        {
            dirs = ImageMetadataReader.ReadMetadata(path);
        }
        catch
        {
            return key;
        }

        var exifDirs = dirs.OfType<ExifDirectoryBase>().ToList();

        string? Desc(int tag)
        {
            foreach (var d in exifDirs)
            {
                var v = d.GetDescription(tag);
                if (!string.IsNullOrWhiteSpace(v))
                    return v;
            }
            return null;
        }

        void Add(string label, int tag)
        {
            var v = Desc(tag);
            if (v != null)
                key.Add(new InfoRow(label, v));
        }

        Add("相機", ExifDirectoryBase.TagModel);
        Add("廠牌", ExifDirectoryBase.TagMake);
        Add("鏡頭", ExifDirectoryBase.TagLensModel);
        Add("拍攝時間", ExifDirectoryBase.TagDateTimeOriginal);
        Add("ISO", ExifDirectoryBase.TagIsoEquivalent);
        Add("快門", ExifDirectoryBase.TagExposureTime);
        Add("光圈", ExifDirectoryBase.TagFNumber);
        Add("焦距", ExifDirectoryBase.TagFocalLength);
        Add("等效焦距(35mm)", ExifDirectoryBase.Tag35MMFilmEquivFocalLength);
        Add("曝光補償", ExifDirectoryBase.TagExposureBias);
        Add("曝光模式", ExifDirectoryBase.TagExposureProgram);
        Add("測光模式", ExifDirectoryBase.TagMeteringMode);
        Add("白平衡", ExifDirectoryBase.TagWhiteBalanceMode);
        Add("閃光燈", ExifDirectoryBase.TagFlash);
        Add("色彩空間", ExifDirectoryBase.TagColorSpace);
        Add("軟體", ExifDirectoryBase.TagSoftware);

        var w = Desc(ExifDirectoryBase.TagExifImageWidth);
        var h = Desc(ExifDirectoryBase.TagExifImageHeight);
        if (w != null && h != null)
            key.Add(new InfoRow("影像尺寸", $"{w} x {h}"));

        // GPS
        var gps = dirs.OfType<GpsDirectory>().FirstOrDefault();
        var loc = gps?.GetGeoLocation();
        if (loc is { IsZero: false } g)
            key.Add(new InfoRow("GPS", $"{g.Latitude:F6}, {g.Longitude:F6}"));

        return key;
    }

    /// <summary>讀 EXIF Orientation（1~8，讀不到回傳 1）</summary>
    public static int GetOrientation(string path)
    {
        try
        {
            var dirs = ImageMetadataReader.ReadMetadata(path);
            foreach (var d in dirs.OfType<ExifIfd0Directory>())
                if (d.TryGetInt32(ExifDirectoryBase.TagOrientation, out int o))
                    return o;
        }
        catch { }
        return 1;
    }
}
