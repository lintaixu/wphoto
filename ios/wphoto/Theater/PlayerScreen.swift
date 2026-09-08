import SwiftUI
import AVKit

/// 全螢幕播放：AVPlayerViewController 內建進度條、倍速（0.5x–2x）、字幕／音軌選單、
/// 子母畫面與 AirPlay；HDR / Dolby Vision / Atmos 由 iOS 原生處理。
struct PlayerScreen: View {
    let file: MediaFile
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        ZStack(alignment: .topLeading) {
            Color.black.ignoresSafeArea()
            PlayerView(url: file.url)
                .ignoresSafeArea()
            Button { dismiss() } label: {
                Image(systemName: "chevron.down")
                    .font(.headline)
                    .padding(10)
                    .background(.ultraThinMaterial, in: Circle())
            }
            .padding(.leading, 16)
            .padding(.top, 8)
        }
    }
}

struct PlayerView: UIViewControllerRepresentable {
    let url: URL

    func makeUIViewController(context: Context) -> AVPlayerViewController {
        let vc = AVPlayerViewController()
        vc.player = AVPlayer(url: url)
        vc.allowsPictureInPicturePlayback = true
        vc.canStartPictureInPictureAutomaticallyFromInline = true
        vc.player?.play()
        return vc
    }

    func updateUIViewController(_ vc: AVPlayerViewController, context: Context) {
        if (vc.player?.currentItem?.asset as? AVURLAsset)?.url != url {
            vc.player = AVPlayer(url: url)
            vc.player?.play()
        }
    }

    static func dismantleUIViewController(_ vc: AVPlayerViewController, coordinator: ()) {
        vc.player?.pause()
        vc.player = nil
    }
}
