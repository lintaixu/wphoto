import Foundation
import Combine

/// 透過「檔案」App 選取的資料夾：iCloud Drive、本機、或在「檔案」中連接的 NAS (SMB) 都適用。
/// 使用 security-scoped bookmark 保存，下次開啟 App 自動續用。
@MainActor
final class FolderSession: ObservableObject {
    enum Kind: String { case photo, video }

    @Published private(set) var folderURL: URL?
    @Published private(set) var files: [MediaFile] = []
    @Published private(set) var isScanning = false
    @Published var errorMessage: String?

    private let kind: Kind
    private var accessing = false

    init(kind: Kind) {
        self.kind = kind
    }

    private var bookmarkKey: String { "folderBookmark.\(kind.rawValue)" }

    var folderDisplayName: String {
        folderURL?.lastPathComponent ?? ""
    }

    // MARK: - 開啟資料夾

    /// 使用者剛從檔案選取器選到的資料夾
    func open(_ url: URL) {
        stopAccess()
        guard url.startAccessingSecurityScopedResource() else {
            errorMessage = String(localized: "AccessDenied")
            return
        }
        accessing = true
        folderURL = url
        saveBookmark(url)
        Task { await scan() }
    }

    /// App 啟動時嘗試還原上次的資料夾
    func restoreLastFolder() {
        guard folderURL == nil,
              let data = UserDefaults.standard.data(forKey: bookmarkKey) else { return }
        var stale = false
        guard let url = try? URL(resolvingBookmarkData: data, options: [], relativeTo: nil, bookmarkDataIsStale: &stale)
        else { return }
        if stale { saveBookmark(url) }
        open(url)
    }

    private func saveBookmark(_ url: URL) {
        if let data = try? url.bookmarkData(options: [], includingResourceValuesForKeys: nil, relativeTo: nil) {
            UserDefaults.standard.set(data, forKey: bookmarkKey)
        }
    }

    private func stopAccess() {
        if accessing, let url = folderURL {
            url.stopAccessingSecurityScopedResource()
        }
        accessing = false
        files = []
    }

    // MARK: - 掃描（遞迴子資料夾）

    private func scan() async {
        guard let root = folderURL else { return }
        isScanning = true
        errorMessage = nil
        let wantVideo = kind == .video
        let result: [MediaFile] = await Task.detached(priority: .userInitiated) {
            Self.enumerate(root: root, video: wantVideo)
        }.value
        files = result
        isScanning = false
    }

    nonisolated private static func enumerate(root: URL, video: Bool) -> [MediaFile] {
        let keys: [URLResourceKey] = [.isRegularFileKey, .isDirectoryKey, .nameKey]
        guard let en = FileManager.default.enumerator(
            at: root, includingPropertiesForKeys: keys, options: [.skipsHiddenFiles, .skipsPackageDescendants]
        ) else { return [] }

        let rootPath = root.standardizedFileURL.path
        var out: [MediaFile] = []
        for case let url as URL in en {
            guard let v = try? url.resourceValues(forKeys: Set(keys)), v.isRegularFile == true else { continue }
            let ok = video ? MediaTypes.isVideo(url) : MediaTypes.isPhoto(url)
            guard ok else { continue }
            let full = url.standardizedFileURL.path
            var rel = full.hasPrefix(rootPath) ? String(full.dropFirst(rootPath.count)) : url.lastPathComponent
            if rel.hasPrefix("/") { rel.removeFirst() }
            out.append(MediaFile(url: url, relativeName: rel))
        }
        return out.sorted { $0.relativeName.localizedStandardCompare($1.relativeName) == .orderedAscending }
    }
}
