using System.Windows;

namespace PhotoViewer;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Loc.Init(); // 依系統語言或使用者偏好載入 UI 字串（繁中／簡中／英文）
    }
}
