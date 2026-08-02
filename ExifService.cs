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

        Add(Loc.T("Camera"), ExifDirectoryBase.TagModel);
        Add(Loc.T("Make"), ExifDirectoryBase.TagMake);
        Add(Loc.T("Lens"), ExifDirectoryBase.TagLensModel);
        Add(Loc.T("DateTaken"), ExifDirectoryBase.TagDateTimeOriginal);
        Add(Loc.T("ISO"), ExifDirectoryBase.TagIsoEquivalent);
        Add(Loc.T("Shutter"), ExifDirectoryBase.TagExposureTime);
        Add(Loc.T("Aperture"), ExifDirectoryBase.TagFNumber);
        Add(Loc.T("FocalLength"), ExifDirectoryBase.TagFocalLength);
        Add(Loc.T("FocalLength35"), ExifDirectoryBase.Tag35MMFilmEquivFocalLength);
        Add(Loc.T("ExposureComp"), ExifDirectoryBase.TagExposureBias);
        Add(Loc.T("ExposureMode"), ExifDirectoryBase.TagExposureProgram);
        Add(Loc.T("Metering"), ExifDirectoryBase.TagMeteringMode);
        Add(Loc.T("WhiteBalance"), ExifDirectoryBase.TagWhiteBalanceMode);
        Add(Loc.T("Flash"), ExifDirectoryBase.TagFlash);
        Add(Loc.T("ColorSpace"), ExifDirectoryBase.TagColorSpace);
        Add(Loc.T("Software"), ExifDirectoryBase.TagSoftware);

        var w = Desc(ExifDirectoryBase.TagExifImageWidth);
        var h = Desc(ExifDirectoryBase.TagExifImageHeight);
        if (w != null && h != null)
            key.Add(new InfoRow(Loc.T("ImageSize"), $"{w} x {h}"));

        // GPS
        var gps = dirs.OfType<GpsDirectory>().FirstOrDefault();
        var loc = gps?.GetGeoLocation();
        if (loc is { IsZero: false } g)
            key.Add(new InfoRow(Loc.T("GPS"), $"{g.Latitude:F6}, {g.Longitude:F6}"));

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
