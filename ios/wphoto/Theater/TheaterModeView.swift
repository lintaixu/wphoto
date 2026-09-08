import SwiftUI
import UniformTypeIdentifiers

/// 看劇模式：從「檔案」選資料夾（含 NAS）→ 封面選集 → 播放
struct TheaterModeView: View {
    @StateObject private var session = FolderSession(kind: .video)
    @State private var showPicker = false
    @State private var playing: MediaFile?

    private let columns = [GridItem(.adaptive(minimum: 160), spacing: 12)]

    var body: some View {
        ZStack {
            Color.wpBackground.ignoresSafeArea()
            if session.folderURL == nil {
                EmptyFolderView(hint: "PlaceholderVideo") { showPicker = true }
            } else if session.isScanning {
                ProgressView("Scanning")
            } else if session.files.isEmpty {
                VStack(spacing: 8) {
                    Text("NoVideos").foregroundStyle(.secondary)
                    Text("VideoFormatNote").font(.footnote).foregroundStyle(.tertiary)
                        .multilineTextAlignment(.center).padding(.horizontal, 32)
                }
            } else {
                ScrollView {
                    LazyVGrid(columns: columns, spacing: 16) {
                        ForEach(session.files) { file in
                            EpisodeCard(file: file)
                                .onTapGesture { playing = file }
                        }
                    }
                    .padding(12)
                }
            }
        }
        .navigationTitle(session.folderURL == nil ? String(localized: "VideoMode") : session.folderDisplayName)
        .navigationBarTitleDisplayMode(.inline)
        .toolbar {
            ToolbarItem(placement: .topBarTrailing) {
                Button { showPicker = true } label: {
                    Image(systemName: "folder")
                }
            }
            ToolbarItem(placement: .bottomBar) {
                if !session.files.isEmpty {
                    Text(String(format: String(localized: "VideoCount"), session.files.count))
                        .font(.footnote)
                        .foregroundStyle(.secondary)
                }
            }
        }
        .fileImporter(isPresented: $showPicker, allowedContentTypes: [.folder]) { result in
            if case .success(let url) = result {
                session.open(url)
            }
        }
        .fullScreenCover(item: $playing) { file in
            PlayerScreen(file: file)
        }
        .onAppear { session.restoreLastFolder() }
    }
}

/// 封面卡片：縮圖（影片第 1 秒畫面）＋檔名＋時長
struct EpisodeCard: View {
    let file: MediaFile
    @State private var image: UIImage?
    @State private var duration: String = ""

    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            ZStack(alignment: .bottomTrailing) {
                Rectangle().fill(Color.wpCard)
                if let image {
                    Image(uiImage: image).resizable().scaledToFill()
                } else {
                    Image(systemName: "film")
                        .font(.title)
                        .foregroundStyle(.secondary)
                        .frame(maxWidth: .infinity, maxHeight: .infinity)
                }
                if !duration.isEmpty {
                    Text(duration)
                        .font(.caption2.weight(.semibold))
                        .padding(.horizontal, 6).padding(.vertical, 3)
                        .background(.black.opacity(0.7))
                        .clipShape(RoundedRectangle(cornerRadius: 5))
                        .padding(6)
                }
            }
            .aspectRatio(16 / 9, contentMode: .fill)
            .clipShape(RoundedRectangle(cornerRadius: 10, style: .continuous))
            Text(file.relativeName)
                .font(.caption)
                .lineLimit(2)
                .foregroundStyle(.primary)
        }
        .contentShape(Rectangle())
        .task(id: file.url) {
            image = ThumbnailCache.shared.cached(file.url)
            if image == nil {
                image = await ThumbnailCache.shared.thumbnail(for: file, maxPixel: 640)
            }
            if let d = await VideoThumbnailer.duration(url: file.url) {
                let total = Int(d.rounded())
                let h = total / 3600, m = (total % 3600) / 60, s = total % 60
                duration = h > 0 ? String(format: "%d:%02d:%02d", h, m, s) : String(format: "%d:%02d", m, s)
            }
        }
    }
}
