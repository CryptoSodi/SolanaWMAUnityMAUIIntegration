using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using SolanaWMAUnityMAUIIntegration.SolanaWallet;

namespace SolanaWMAUnityMAUIIntegration
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    [IntentFilter(new[] { Intent.ActionView }, Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable }, DataScheme = "ludocities")]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnNewIntent(Intent intent)
        {
            base.OnNewIntent(intent);

            var data = intent?.Data;

            if (data != null)
            {
                var url = data.ToString();

                System.Diagnostics.Debug.WriteLine("Wallet callback: " + url);

                WalletCallbackService.HandleCallback(url);
            }
        }
    }
}
