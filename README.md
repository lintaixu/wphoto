# wphoto

Windows 桌面照片瀏覽器，支援 RAW 與影片。

## 功能

- 支援 20+ 種 RAW 格式（RAF、ARW、CR2/CR3、NEF、DNG…），優先讀取內嵌預覽，秒開大檔
- 支援 Fuji / Sony 全部拍攝格式：JPEG、HEIF (.HIF)、MOV、MP4 (XAVC)、MTS/M2TS (AVCHD)
- 影片播放內建 LibVLC，10-bit H.265 也能播，不依賴系統解碼器
- 右側面板顯示拍攝資訊：相機、鏡頭、ISO、快門、光圈、焦距、GPS 等
- 遞迴掃描子資料夾、依檔案類型篩選
- 深色極簡介面

## 開發

```
dotnet build PhotoViewer
```

發行：

```
dotnet publish PhotoViewer -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

發行後將 `PhotoViewer.exe` 與 `libvlc` 資料夾一起壓縮散佈。

## 技術

- .NET 9 / WPF + WPF-UI (Fluent)
- Sdcb.LibRaw（RAW 解碼）
- MetadataExtractor（EXIF）
- LibVLCSharp（影片播放）
- Magick.NET（HEIF 解碼）
