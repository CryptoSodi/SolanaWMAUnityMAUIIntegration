using Android.Content;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using androidcontentIntent = Android.Content.Intent;
using AndroidNet = Android.Net;

namespace SolanaWMAUnityMAUIIntegration.Platforms.Android
{
    public class WalletLauncher
    {
        public static void Launch(string associationToken, int port)
        {
            var activity = Platform.CurrentActivity;
            if (activity == null) return;
            
            var intent = new androidcontentIntent();
            intent.SetAction(androidcontentIntent.ActionView);
            intent.AddCategory(androidcontentIntent.CategoryBrowsable);
            
            var url = $"{SchemeMobileWalletAdapter}:/" +
                      $"{LocalPathSuffix}?association={associationToken}&port={port}";

            intent.SetData(AndroidNet.Uri.Parse(url));
            activity.StartActivity(intent);
        }

        public const string SchemeMobileWalletAdapter = "solana-wallet";
        public const string LocalPathSuffix = "v1/associate/local";
    }

    public static class WebSocketsTransportContract
    {
        public const string WebsocketsLocalScheme = "ws";
        public const string WebsocketsLocalHost = "127.0.0.1";
        public const string WebsocketsLocalPath = "/solana-wallet";
        public const int WebsocketsLocalPortMin = 49152;
        public const int WebsocketsLocalPortMax = 65535;
        public const string WebsocketsProtocol = "com.solana.mobilewalletadapter.v1";
    }
}
