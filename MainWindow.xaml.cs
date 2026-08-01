using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LibVLCSharp.Shared;
using Microsoft.Win32;

namespace PhotoViewer;

public class FileItem
{
    public required string Path { get; init; }
    public required string Root { get; init; }
    /// <summary>清單顯示用：相對於所選資料夾的路徑（子資料夾的檔案會顯示「子資料夾\檔名」）</summary>
    public string Name => System.IO.Path.GetRelativePath(Root, Path);
    public bool IsRaw => ImageLoader.IsRaw(Path);
    public bool IsVideo => ImageLoader.IsVideo(Path);
    public Visibility RawBadgeVisibility => IsRaw ? Visibility.Visible : Visibility.Collapsed;
    public Visibility VideoBadgeVisibility => IsVideo ? Visibility.Visible : Visibility.Collapsed;
}

public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
{
    readonly ObservableCollection<FileItem> _files = new();
    int _loadToken;
    LibVLC? _libVLC;
    MediaPlayer? _player;
    Media? _currentMedia;

    public MainWindow()
    {
        InitializeComponent();
        FileList.ItemsSource = _files;
        InitVideoPlayer();

        var args = Environment.GetCommandLineArgs();
        if (args.Length > 1 && System.IO.Directory.Exists(args[1]))
            OpenFolder(args[1]);
    }

    void InitVideoPlayer()
    {
        try
        {
            // 單一檔案發行時 libvlc 資料夾在 exe 旁邊，明確指定路徑最保險
            string exeDir = System.IO.Path.GetDirectoryName(Environment.ProcessPath!)!;
            string libvlcDir = System.IO.Path.Combine(exeDir, "libvlc", Environment.Is64BitProcess ? "win-x64" : "win-x86");
            if (System.IO.Directory.Exists(libvlcDir))
                Core.Initialize(libvlcDir);
            else
                Core.Initialize();

            _libVLC = new LibVLC();
            _player = new MediaPlayer(_libVLC);
            VideoViewControl.MediaPlayer = _player;
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"⚠ 影片播放引擎初始化失敗：{ex.Message}（照片瀏覽不受影響）";
        }
    }

    void StopVideo()
    {
        if (_player is { IsPlaying: true } || _player?.Media != null)
            _player.Stop();
        _currentMedia?.Dispose();
        _currentMedia = null;
        VideoViewControl.Visibility = Visibility.Collapsed;
    }

    // ---------- 縮放與平移 ----------
    double _zoom = 1.0;
    System.Windows.Point _dragStart;
    bool _dragging;

    void ResetZoom()
    {
        _zoom = 1.0;
        ImgScale.ScaleX = ImgScale.ScaleY = 1.0;
        ImgPan.X = ImgPan.Y = 0;
    }

    void ZoomHost_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (MainImage.Source == null || VideoViewControl.Visibility == Visibility.Visible)
            return;

        double factor = e.Delta > 0 ? 1.25 : 0.8;
        double newZoom = Math.Clamp(_zoom * factor, 1.0, 10.0);
        if (Math.Abs(newZoom - _zoom) < 0.001)
            return;

        // 以游標位置為中心縮放
        var pos = e.GetPosition(ZoomHost);
        double ratio = newZoom / _zoom;
        ImgPan.X = pos.X - (pos.X - ImgPan.X) * ratio;
        ImgPan.Y = pos.Y - (pos.Y - ImgPan.Y) * ratio;
        _zoom = newZoom;
        ImgScale.ScaleX = ImgScale.ScaleY = newZoom;

        if (_zoom <= 1.001)
            ResetZoom();
        StatusLabel.Text = _zoom > 1 ? $"縮放 {_zoom * 100:F0}%（雙擊還原）" : "縮放 100%";
        e.Handled = true;
    }

    void ZoomHost_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ResetZoom();
            StatusLabel.Text = "縮放 100%";
            return;
        }
        if (_zoom > 1 && MainImage.Source != null)
        {
            _dragging = true;
            _dragStart = e.GetPosition(ZoomHost);
            ZoomHost.CaptureMouse();
            ZoomHost.Cursor = Cursors.SizeAll;
        }
    }

    void ZoomHost_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _dragging = false;
        ZoomHost.ReleaseMouseCapture();
        ZoomHost.Cursor = Cursors.Arrow;
    }

    void ZoomHost_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging)
            return;
        var pos = e.GetPosition(ZoomHost);
        ImgPan.X += pos.X - _dragStart.X;
        ImgPan.Y += pos.Y - _dragStart.Y;
        _dragStart = pos;
    }

    void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // 空白鍵：影片播放/暫停
        if (e.Key == Key.Space && VideoViewControl.Visibility == Visibility.Visible && _player != null)
        {
            _player.Pause(); // 呼叫一次即在播放/暫停間切換
            e.Handled = true;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        StopVideo();
        _player?.Dispose();
        _libVLC?.Dispose();
        base.OnClosed(e);
    }

    void ChooseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "選擇照片資料夾" };
        if (dlg.ShowDialog() == true)
            OpenFolder(dlg.FolderName);
    }

    void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths && paths.Length > 0)
        {
            string p = paths[0];
            if (System.IO.Directory.Exists(p))
                OpenFolder(p);
            else if (File.Exists(p))
                OpenFolder(System.IO.Path.GetDirectoryName(p)!, selectFile: p);
        }
    }

    int _scanToken;

    async void OpenFolder(string folder, string? selectFile = null)
    {
        int scanToken = ++_scanToken;
        FolderLabel.Text = folder;
        StatusLabel.Text = "掃描資料夾中…";
        _files.Clear();

        var paths = await Task.Run(() =>
        {
            var result = new List<string>();
            ScanFolder(folder, result);
            result.Sort(StringComparer.OrdinalIgnoreCase);
            return result;
        });

        if (scanToken != _scanToken) return; // 已改開別的資料夾

        _allPaths = paths;
        _rootFolder = folder;

        // 類型下拉選單：只列出資料夾裡實際存在的類型
        var types = paths.Select(TypeLabel).Distinct().OrderBy(t => t, StringComparer.Ordinal).ToList();
        _suppressFilterEvent = true;
        TypeFilter.ItemsSource = types.Count > 0 ? new[] { FilterAll }.Concat(types).ToList() : new List<string>();
        TypeFilter.SelectedIndex = types.Count > 0 ? 0 : -1;
        _suppressFilterEvent = false;

        ApplyFilter(selectFile);
    }

    List<string> _allPaths = new();
    string _rootFolder = "";
    bool _suppressFilterEvent;

    const string FilterAll = "全部類型";

    /// <summary>副檔名 → 類型名稱（JPG/JPEG 等同義副檔名合併顯示）</summary>
    static string TypeLabel(string path)
    {
        string ext = System.IO.Path.GetExtension(path).TrimStart('.').ToUpperInvariant();
        return ext switch
        {
            "JPG" or "JPEG" => "JPEG",
            "TIF" or "TIFF" => "TIFF",
            "HEIF" or "HIF" or "HEIC" => "HEIF",
            "M2TS" => "MTS",
            _ => ext,
        };
    }

    void TypeFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_suppressFilterEvent)
            ApplyFilter(null);
    }

    void ApplyFilter(string? selectFile)
    {
        string? filter = TypeFilter.SelectedItem as string;
        if (filter == FilterAll)
            filter = null;

        _files.Clear();
        foreach (var p in _allPaths)
            if (filter == null || TypeLabel(p) == filter)
                _files.Add(new FileItem { Path = p, Root = _rootFolder });

        int rawCount = _files.Count(f => f.IsRaw);
        int videoCount = _files.Count(f => f.IsVideo);
        StatusLabel.Text = $"共 {_files.Count} 個檔案（RAW {rawCount}、影片 {videoCount}）";

        if (_files.Count > 0)
        {
            var target = selectFile != null
                ? _files.FirstOrDefault(f => string.Equals(f.Path, selectFile, StringComparison.OrdinalIgnoreCase)) ?? _files[0]
                : _files[0];
            FileList.SelectedItem = target;
            FileList.ScrollIntoView(target);
            FileList.Focus();
        }
        else
        {
            StopVideo();
            MainImage.Source = null;
            MainImage.Visibility = Visibility.Visible;
            PlaceholderText.Visibility = Visibility.Visible;
            PlaceholderText.Text = "這個資料夾裡沒有支援的照片或影片格式";
            ClearInfo();
        }
    }

    /// <summary>遞迴掃描資料夾，跳過沒有權限的目錄</summary>
    static void ScanFolder(string dir, List<string> result)
    {
        try
        {
            foreach (var f in System.IO.Directory.EnumerateFiles(dir))
                if (ImageLoader.IsSupported(f))
                    result.Add(f);
            foreach (var d in System.IO.Directory.EnumerateDirectories(dir))
                ScanFolder(d, result);
        }
        catch (UnauthorizedAccessException) { }
        catch (IOException) { }
    }

    async void FileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FileList.SelectedItem is not FileItem item)
            return;

        int token = ++_loadToken;
        string path = item.Path;
        StatusLabel.Text = $"讀取中… {item.Name}";

        if (item.IsVideo)
        {
            await ShowVideoAsync(item, token);
            return;
        }

        StopVideo();
        MainImage.Visibility = Visibility.Visible;

        try
        {
            var result = await Task.Run(() =>
            {
                var img = ImageLoader.Load(path);
                var key = ExifService.Read(path);
                return (img, key);
            });

            if (token != _loadToken) return; // 使用者已切到別張

            PlaceholderText.Visibility = Visibility.Collapsed;
            ResetZoom();
            MainImage.Source = result.img;
            FillInfo(path, result.img.PixelWidth, result.img.PixelHeight, result.key);
            StatusLabel.Text = $"{item.Name} — {result.img.PixelWidth} x {result.img.PixelHeight}";
        }
        catch (Exception ex)
        {
            if (token != _loadToken) return;
            MainImage.Source = null;
            PlaceholderText.Visibility = Visibility.Visible;
            PlaceholderText.Text = "⚠ 無法讀取這張照片";
            StatusLabel.Text = $"⚠ 讀取失敗：{ex.Message}";
            FillInfo(path, 0, 0, SafeReadExif(path));
        }
    }

    async Task ShowVideoAsync(FileItem item, int token)
    {
        StopVideo();
        if (_libVLC == null || _player == null)
        {
            StatusLabel.Text = "⚠ 影片播放引擎未初始化，無法播放";
            return;
        }

        var media = new Media(_libVLC, new Uri(item.Path));
        await media.Parse(MediaParseOptions.ParseLocal);

        if (token != _loadToken)
        {
            media.Dispose();
            return;
        }

        // 影片資訊
        var rows = new List<InfoRow>();
        if (media.Duration > 0)
            rows.Add(new InfoRow("時長", TimeSpan.FromMilliseconds(media.Duration).ToString(@"hh\:mm\:ss")));
        foreach (var t in media.Tracks)
        {
            if (t.TrackType == TrackType.Video)
            {
                rows.Add(new InfoRow("解析度", $"{t.Data.Video.Width} x {t.Data.Video.Height}"));
                if (t.Data.Video.FrameRateDen > 0)
                    rows.Add(new InfoRow("影格率", $"{t.Data.Video.FrameRateNum / (double)t.Data.Video.FrameRateDen:F2} fps"));
                rows.Add(new InfoRow("視訊編碼", FourCC(t.Codec)));
            }
            else if (t.TrackType == TrackType.Audio)
            {
                rows.Add(new InfoRow("音訊編碼", FourCC(t.Codec)));
                rows.Add(new InfoRow("音訊", $"{t.Data.Audio.Channels} 聲道 / {t.Data.Audio.Rate} Hz"));
            }
        }
        // 拍攝時間等 metadata（MP4/MOV 可讀）
        rows.AddRange(SafeReadExif(item.Path));

        ResetZoom();
        MainImage.Source = null;
        MainImage.Visibility = Visibility.Collapsed;
        PlaceholderText.Visibility = Visibility.Collapsed;
        VideoViewControl.Visibility = Visibility.Visible;

        _currentMedia = media;
        _player.Play(media);

        FillInfo(item.Path, 0, 0, rows);
        StatusLabel.Text = $"{item.Name} — 播放中（空白鍵：播放/暫停）";
    }

    static string FourCC(uint codec)
    {
        Span<char> c = stackalloc char[4];
        for (int i = 0; i < 4; i++)
        {
            char ch = (char)((codec >> (8 * i)) & 0xFF);
            c[i] = char.IsLetterOrDigit(ch) ? ch : ' ';
        }
        return new string(c).Trim().ToUpperInvariant();
    }

    static List<InfoRow> SafeReadExif(string path)
    {
        try { return ExifService.Read(path); }
        catch { return new List<InfoRow>(); }
    }

    void FillInfo(string path, int w, int h, List<InfoRow> key)
    {
        var fi = new FileInfo(path);
        var basic = new List<InfoRow>
        {
            new("檔名", fi.Name),
            new("檔案大小", HumanSize(fi.Length)),
        };
        if (w > 0)
            basic.Add(new InfoRow("顯示尺寸", $"{w} x {h}"));

        FileInfoList.ItemsSource = basic;
        KeyInfoList.ItemsSource = key;
        var vis = key.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        KeyTitle.Visibility = vis;
        KeyCard.Visibility = vis;
    }

    void ClearInfo()
    {
        FileInfoList.ItemsSource = null;
        KeyInfoList.ItemsSource = null;
        KeyTitle.Visibility = Visibility.Collapsed;
        KeyCard.Visibility = Visibility.Collapsed;
    }

    static string HumanSize(long n) => n switch
    {
        < 1024 => $"{n} B",
        < 1024 * 1024 => $"{n / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{n / 1024.0 / 1024:F1} MB",
        _ => $"{n / 1024.0 / 1024 / 1024:F2} GB",
    };
}
