import Foundation
import UIKit
import AVFoundation

/// 縮圖快取：NSCache 在記憶體吃緊時會自動釋放
final class ThumbnailCache {
    static let shared = ThumbnailCache()

    private let cache: NSCache<NSURL, UIImage> = {
        let c = NSCache<NSURL, UIImage>()
        c.countLimit = 400
        return c
    }()

    func cached(_ url: URL) -> UIImage? {
        cache.object(forKey: url as NSURL)
    }

    /// 取得縮圖（照片：內嵌預覽優先；影片：擷取第 1 秒畫面）
    func thumbnail(for file: MediaFile, maxPixel: Int = 320) async -> UIImage? {
        if let c = cached(file.url) { return c }
        let image: UIImage?
        if file.isVideo {
            image = await VideoThumbnailer.frame(url: file.url, maxPixel: maxPixel)
        } else {
            let url = file.url
            image = await Task.detached(priority: .utility) {
                ImageLoading.thumbnail(url: url, maxPixel: maxPixel)
            }.value
        }
        if let image {
            cache.setObject(image, forKey: file.url as NSURL)
        }
        return image
    }
}

enum VideoThumbnailer {
    static func frame(url: URL, maxPixel: Int) async -> UIImage? {
        let asset = AVURLAsset(url: url)
        let gen = AVAssetImageGenerator(asset: asset)
        gen.appliesPreferredTrackTransform = true
        gen.maximumSize = CGSize(width: maxPixel, height: maxPixel)
        do {
            let (cg, _) = try await gen.image(at: CMTime(seconds: 1, preferredTimescale: 600))
            return UIImage(cgImage: cg)
        } catch {
            return nil
        }
    }

    /// 時長（秒）
    static func duration(url: URL) async -> Double? {
        let asset = AVURLAsset(url: url)
        guard let d = try? await asset.load(.duration) else { return nil }
        let s = CMTimeGetSeconds(d)
        return s.isFinite ? s : nil
    }
}
