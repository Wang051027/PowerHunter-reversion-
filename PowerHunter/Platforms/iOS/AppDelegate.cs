using Foundation;
using UIKit;

namespace PowerHunter;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
    {
        // Enable battery monitoring on iOS
        UIDevice.CurrentDevice.BatteryMonitoringEnabled = true;

        return base.FinishedLaunching(application, launchOptions);
    }
}
