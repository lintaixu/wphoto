# wphoto for iOS

Native SwiftUI companion to the Windows app. Same two modes, same dark look — built on Apple's own decoders, so RAW (RAF/ARW/CR3/…), HEIF, HDR10 and Dolby Vision all render natively.

原生 SwiftUI 版本。照片／看劇兩種模式與 Windows 版一致；RAW、HEIF、HDR、杜比視界都走 Apple 原生解碼。

## Open folders from anywhere in Files · 從「檔案」開啟任何位置

Both modes pick a folder through the Files app — **iCloud Drive, On My iPhone, or a NAS you've connected in Files (SMB)**. The folder is remembered (security-scoped bookmark) so it reopens next launch.

兩種模式都透過「檔案」App 選資料夾——**iCloud Drive、我的 iPhone、或在「檔案」裡連接的 NAS (SMB)** 都可以，且會記住上次的資料夾。

> NAS note: iOS downloads a file the first time it is read, so a grid of 80 MB RAWs over Wi-Fi takes a while to fill. Thumbnails only load for cells on screen.

## Features · 功能

| Photo mode | Theater mode |
|---|---|
| RAW / JPEG / HEIF / TIFF via ImageIO (Apple RAW pipeline) | MP4 / MOV / M4V via AVPlayer |
| Thumbnail grid, type filter (only types present in the folder) | Episode grid with frame thumbnails and duration |
| Pinch-zoom, double-tap, swipe between photos | Native controls: scrubber, 0.5×–2× speed, subtitle/audio tracks, fullscreen, PiP, AirPlay |
| Shooting info sheet: camera, lens, ISO, shutter, aperture, focal length, GPS… | HDR10 / Dolby Vision / Atmos handled by iOS natively |

Not in this version: **MKV / MTS playback** (needs a VLC engine — planned via MobileVLCKit), external `.srt` files.

## Build · 建置

iOS apps can only be compiled by Xcode on a Mac (or a macOS CI runner). The project is defined with [XcodeGen](https://github.com/yonaskolb/XcodeGen):

```bash
brew install xcodegen
cd ios
xcodegen generate        # → wphoto.xcodeproj
open wphoto.xcodeproj    # Run on Simulator or your iPhone
```

Every push touching `ios/` is compiled on GitHub Actions (`.github/workflows/ios.yml`, macOS runner) as a build check.

To install on your own iPhone: open the project in Xcode, pick your Apple ID under Signing & Capabilities, and run. A free Apple ID allows 7-day sideloading; TestFlight / App Store distribution needs the Apple Developer Program.

## Languages · 語言

Follows the iPhone system language: English, 繁體中文, 简体中文 (`Resources/*.lproj/Localizable.strings`).
