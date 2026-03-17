using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SolanaWMAUnityMAUIIntegration.SolanaWallet
{
    public class WalletConnection
    {
        public async Task ConnectWallet()
        {
            var associationToken = Guid.NewGuid().ToString();
            var port = 8900;

            var uri =
    $"solana-wallet:/v1/associate" +
    $"?association_token={Uri.EscapeDataString(associationToken)}" +
    $"&port={port}" +
    $"&identity_uri={Uri.EscapeDataString("https://ludocities.com")}" +
    $"&icon_uri={Uri.EscapeDataString("https://ludocities.com/favicon.ico")}" +
    $"&identity_name={Uri.EscapeDataString("Ludo Cities")}";

            System.Diagnostics.Debug.WriteLine("Launching wallet: " + uri);

#if ANDROID
            Platforms.Android.WalletLauncher.Launch(uri);
#endif
        }
    }
}
