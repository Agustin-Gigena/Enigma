using Android.App;
using Android.Runtime;

namespace Enigma.Maui.Platforms.Android;

[Application(Label = "Enigma")]
public class MainApplication(IntPtr handle, JniHandleOwnership ownership)
    : MauiApplication(handle, ownership)
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
