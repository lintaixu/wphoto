import SwiftUI
import UniformTypeIdentifiers

/// 照片模式：從「檔案」選資料夾 → 縮圖牆 → 點開檢視
struct PhotoModeView: View {
    @StateObject private var session = FolderSession(kind: .photo)
    @State private var showPicker = false
    @State private var typeFilter: String? = nil
    @State private var selected: MediaFile?

    private let columns = [GridItem(.adaptive(minimum: 110), spacing: 3)]

    private var availableTypes: [String] {
        Array(Set(session.files.map(\.typeLabel))).sorted()
    }

    private var visibleFiles: [MediaFile] {
        guard let t = typeFilter else { return session.files }
        return session.files.filter { $0.typeLabel == t }
    }

    var body: some View {
        ZStack {
            Color.wpBackground.ignoresSafeArea()
            if session.folderURL == nil {
                EmptyFolderView(hint: "PlaceholderPhoto") { showPicker = true }
            } else if session.isScanning {
                ProgressView("Scanning")
            } else if visibleFiles.isEmpty {
                Text("NoPhotos").foregroundStyle(.secondary)
            } else {
                ScrollView {
                    LazyVGrid(columns: columns, spacing: 3) {
                        ForEach(visibleFiles) { file in
                            ThumbnailCell(file: file)
                                .onTapGesture { selected = file }
                        }
                    }
                    .padding(3)
                }
            }
        }
        .navigationTitle(session.folderURL == nil ? String(localized: "PhotoMode") : session.folderDisplayName)
        .navigationBarTitleDisplayMode(.inline)
        .toolbar {
            ToolbarItemGroup(placement: .topBarTrailing) {
                if !availableTypes.isEmpty {
                    Menu {
                        Picker("Type", selection: $typeFilter) {
                            Text("AllTypes").tag(String?.none)
                            ForEach(availableTypes, id: \.self) { t in
                                Text(t).tag(String?.some(t))
                            }
                        }
                    } label: {
                        Label(typeFilter ?? String(localized: "AllTypes"), systemImage: "line.3.horizontal.decrease.circle")
                    }
                }
                Button { showPicker = true } label: {
                    Image(systemName: "folder")
                }
            }
            ToolbarItem(placement: .bottomBar) {
                if !session.files.isEmpty {
                    Text(String(format: String(localized: "PhotoCount"), visibleFiles.count, visibleFiles.filter(\.isRaw).count))
                        .font(.footnote)
                        .foregroundStyle(.secondary)
                }
            }
        }
        .fileImporter(isPresented: $showPicker, allowedContentTypes: [.folder]) { result in
            if case .success(let url) = result {
                typeFilter = nil
                session.open(url)
            }
        }
        .fullScreenCover(item: $selected) { file in
            PhotoDetailView(files: visibleFiles, current: file)
        }
        .alert("Error", isPresented: Binding(get: { session.errorMessage != nil },
                                             set: { if !$0 { session.errorMessage = nil } })) {
            Button("OK") {}
        } message: {
            Text(session.errorMessage ?? "")
        }
        .onAppear { session.restoreLastFolder() }
    }
}

/// 縮圖格（LazyVGrid 只會載入看得到的格子）
struct ThumbnailCell: View {
    let file: MediaFile
    @State private var image: UIImage?

    var body: some View {
        ZStack(alignment: .topLeading) {
            Rectangle().fill(Color.wpCard)
            if let image {
                Image(uiImage: image)
                    .resizable()
                    .scaledToFill()
            } else {
                Image(systemName: file.isVideo ? "film" : "photo")
                    .foregroundStyle(.secondary)
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
            }
            if file.isRaw {
                Text("RAW")
                    .font(.system(size: 9, weight: .bold))
                    .padding(.horizontal, 5).padding(.vertical, 2)
                    .background(Color(red: 0.56, green: 0.35, blue: 0.17))
                    .clipShape(RoundedRectangle(cornerRadius: 4))
                    .padding(5)
            }
        }
        .aspectRatio(1, contentMode: .fill)
        .clipped()
        .contentShape(Rectangle())
        .task(id: file.url) {
            image = ThumbnailCache.shared.cached(file.url)
            if image == nil {
                image = await ThumbnailCache.shared.thumbnail(for: file)
            }
        }
    }
}

struct EmptyFolderView: View {
    let hint: LocalizedStringKey
    let action: () -> Void

    var body: some View {
        VStack(spacing: 18) {
            Image(systemName: "folder.badge.plus")
                .font(.system(size: 52))
                .foregroundStyle(Color.wpAccent)
            Text(hint)
                .foregroundStyle(.secondary)
            Button(action: action) {
                Text("ChooseFolder")
                    .fontWeight(.semibold)
                    .padding(.horizontal, 22).padding(.vertical, 10)
            }
            .buttonStyle(.borderedProminent)
            Text("FilesHint")
                .font(.footnote)
                .foregroundStyle(.tertiary)
                .multilineTextAlignment(.center)
                .padding(.horizontal, 40)
        }
    }
}
