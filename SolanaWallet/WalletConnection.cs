using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Solnet.Rpc.Builders;
using Solnet.Programs;
using Solnet.Rpc.Models;
using Solnet.Wallet;

namespace SolanaWMAUnityMAUIIntegration.SolanaWallet
{
    public class WalletConnection
    {
        private MobileWalletConnection _connection = new();
        
        // Session state
        public string? AuthToken { get; private set; }
        public List<AccountDetails> Accounts { get; private set; } = new();
        public byte[]? MainAddress => Accounts.FirstOrDefault()?.PublicKey;

        private async Task<bool> EnsureConnected()
        {
            // Re-establish connection for each operation as WMA usually closes it after each response
            return await _connection.Connect();
        }

        private async Task<AuthorizationResult?> AuthorizeOrReauthorize()
        {
            if (!await EnsureConnected()) return null;

            AuthorizationResult? result;
            if (string.IsNullOrEmpty(AuthToken))
            {
                Console.WriteLine("[WMA] Authorizing new session...");
                result = await _connection.Client!.Authorize(
                    new Uri("https://solana.unity-sdk.gg/"),
                    new Uri("favicon.ico", UriKind.Relative),
                    "Solana MAUI App",
                    "mainnet-beta"
                );
            }
            else
            {
                Console.WriteLine("[WMA] Reauthorizing existing session...");
                result = await _connection.Client!.Reauthorize(
                    new Uri("https://solana.unity-sdk.gg/"),
                    new Uri("favicon.ico", UriKind.Relative),
                    "Solana MAUI App",
                    AuthToken
                );
            }

            if (result != null)
            {
                AuthToken = result.AuthToken;
                Accounts = result.Accounts;
                Console.WriteLine($"[WMA] Session active. Token: {AuthToken}, Accounts: {Accounts.Count}");
            }
            return result;
        }

        public async Task ConnectWallet()
        {
            try
            {
                var auth = await AuthorizeOrReauthorize();
                if (auth != null)
                {
                    var address = auth.Accounts.FirstOrDefault()?.DisplayAddress ?? "Unknown";
                    WalletCallbackService.HandleCallback("Connected: " + address);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[WMA] Connection/Authorization Error: " + ex.Message);
            }
        }

        public async Task SignTestTransaction()
        {
            try
            {
                if (MainAddress == null)
                {
                    await ConnectWallet();
                    if (MainAddress == null) return;
                }

                Console.WriteLine("[WMA] SignTestTransaction is currently disabled due to library conflict.");
                WalletCallbackService.HandleCallback("SignTransaction Disabled");
                
                /*
                // Create a simple memo transaction
                var blockhash = "11111111111111111111111111111111"; // Placeholder, wallet will usually fill or validate
                var feePayer = new PublicKey(MainAddress);
                var txBuilder = new TransactionBuilder()
                    .SetRecentBlockHash(blockhash)
                    .SetFeePayer(feePayer)
                    .AddInstruction(Solnet.Programs.MemoProgram.NewMemoInstruction("Hello from MAUI!"));
                
                var txBytes = txBuilder.Build(new List<Account>()); // No private keys, wallet signs it

                var auth = await AuthorizeOrReauthorize();
                if (auth == null) return;

                Console.WriteLine("[WMA] Requesting signature for transaction...");
                var signResult = await _connection.Client!.SignTransactions(new List<byte[]> { txBytes });
                
                if (signResult.SignedPayloads.Any())
                {
                    Console.WriteLine("[WMA] Transaction signed successfully!");
                    WalletCallbackService.HandleCallback("Transaction Signed!");
                }
                */
            }
            catch (Exception ex)
            {
                Console.WriteLine("[WMA] SignTransaction Error: " + ex.Message);
            }
        }

        public async Task SignTestMessage()
        {
            try
            {
                if (MainAddress == null)
                {
                    await ConnectWallet();
                    if (MainAddress == null) return;
                }

                var message = Encoding.UTF8.GetBytes("Sign this message to prove ownership.");
                
                var auth = await AuthorizeOrReauthorize();
                if (auth == null) return;

                Console.WriteLine("[WMA] Requesting signature for message...");
                var signResult = await _connection.Client!.SignMessages(
                    new List<byte[]> { message },
                    new List<byte[]> { MainAddress }
                );

                if (signResult.SignedPayloads.Any())
                {
                    Console.WriteLine("[WMA] Message signed successfully!");
                    WalletCallbackService.HandleCallback("Message Signed!");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[WMA] SignMessage Error: " + ex.Message);
            }
        }
    }
}
