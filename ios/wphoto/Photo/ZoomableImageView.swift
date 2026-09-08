import SwiftUI
import UIKit

/// 捏合縮放、拖曳平移、雙擊放大／還原（UIScrollView 實作，手感與相簿一致）
struct ZoomableImageView: UIViewRepresentable {
    let image: UIImage

    func makeUIView(context: Context) -> ZoomScrollView {
        let sv = ZoomScrollView()
        sv.delegate = context.coordinator
        sv.imageView.image = image
        let dbl = UITapGestureRecognizer(target: context.coordinator, action: #selector(Coordinator.doubleTap(_:)))
        dbl.numberOfTapsRequired = 2
        sv.addGestureRecognizer(dbl)
        return sv
    }

    func updateUIView(_ sv: ZoomScrollView, context: Context) {
        if sv.imageView.image !== image {
            sv.imageView.image = image
            sv.setZoomScale(1, animated: false)
            sv.setNeedsLayout()
        }
    }

    func makeCoordinator() -> Coordinator { Coordinator() }

    final class Coordinator: NSObject, UIScrollViewDelegate {
        func viewForZooming(in scrollView: UIScrollView) -> UIView? {
            (scrollView as? ZoomScrollView)?.imageView
        }

        func scrollViewDidZoom(_ scrollView: UIScrollView) {
            (scrollView as? ZoomScrollView)?.centerImage()
        }

        @objc func doubleTap(_ g: UITapGestureRecognizer) {
            guard let sv = g.view as? ZoomScrollView else { return }
            if sv.zoomScale > 1 {
                sv.setZoomScale(1, animated: true)
            } else {
                let p = g.location(in: sv.imageView)
                let w = sv.bounds.width / 3, h = sv.bounds.height / 3
                sv.zoom(to: CGRect(x: p.x - w / 2, y: p.y - h / 2, width: w, height: h), animated: true)
            }
        }
    }
}

final class ZoomScrollView: UIScrollView {
    let imageView = UIImageView()

    override init(frame: CGRect) {
        super.init(frame: frame)
        minimumZoomScale = 1
        maximumZoomScale = 10
        bouncesZoom = true
        showsVerticalScrollIndicator = false
        showsHorizontalScrollIndicator = false
        contentInsetAdjustmentBehavior = .never
        backgroundColor = .black
        imageView.contentMode = .scaleAspectFit
        addSubview(imageView)
    }

    required init?(coder: NSCoder) { fatalError("init(coder:) has not been implemented") }

    override func layoutSubviews() {
        super.layoutSubviews()
        if zoomScale == 1 {
            imageView.frame = bounds
            contentSize = bounds.size
        }
        centerImage()
    }

    func centerImage() {
        var f = imageView.frame
        f.origin.x = f.width < bounds.width ? (bounds.width - f.width) / 2 : 0
        f.origin.y = f.height < bounds.height ? (bounds.height - f.height) / 2 : 0
        imageView.frame = f
    }
}
