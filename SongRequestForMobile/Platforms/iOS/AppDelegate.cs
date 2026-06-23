using AVFoundation;
using Foundation;

namespace SongRequestForMobile
{
    [Register("AppDelegate")]
    public class AppDelegate : MauiUIApplicationDelegate
    {
        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

        public override bool FinishedLaunching(UIKit.UIApplication application, Foundation.NSDictionary launchOptions)
        {
            // Configure audio session for background playback
            var session = AVAudioSession.SharedInstance();
            session.SetCategory(AVAudioSessionCategory.Playback, AVAudioSessionCategoryOptions.MixWithOthers);
            session.SetActive(true);

            return base.FinishedLaunching(application, launchOptions);
        }
    }
}
