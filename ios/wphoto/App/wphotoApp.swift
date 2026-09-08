import SwiftUI
import AVFoundation

@main
struct WphotoApp: App {
    init() {
        // 靜音開關打開時影片仍有聲音（看劇模式）
        try? AVAudioSession.sharedInstance().setCategory(.playback, mode: .moviePlayback)
    }

    var body: some Scene {
        WindowGroup {
            ModeSelectView()
                .preferredColorScheme(.dark)
        }
    }
}
