using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SolanaWMAUnityMAUIIntegration.SolanaWallet
{
    public static class WalletCallbackService
    {
        public static event Action<string>? OnWalletConnected;

        public static void HandleCallback(string url)
        {
            OnWalletConnected?.Invoke(url);
        }
    }
}
