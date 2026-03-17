using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Solnet.Rpc;
using Solnet.Rpc.Builders;
using Solnet.Programs;
using Solnet.Rpc.Models;
using Solnet.Wallet;

namespace SolanaWMAUnityMAUIIntegration.SolanaWallet
{
    public class WalletConnection
    {
        private MobileWalletConnection _connection = new();
        private IRpcClient _rpcClient = ClientFactory.GetClient(Cluster.MainNet);
        private string _clusterName = "mainnet-beta";

        public void SetNetwork(bool isMainnet)
        {
            _rpcClient = ClientFactory.GetClient(isMainnet ? Cluster.MainNet : Cluster.DevNet);
            _clusterName = isMainnet ? "mainnet-beta" : "devnet";
            Console.WriteLine($"[WMA] Network switched to: {_clusterName}");
            
            // Clear session when switching networks to force fresh authorize
            AuthToken = null;
            Accounts = new();
            SolBalance = 0;
            TokenBalances = new();
        }
        
        // Session state
        public string? AuthToken { get; private set; }
        public List<AccountDetails> Accounts { get; private set; } = new();
        public byte[]? MainAddress => Accounts.FirstOrDefault()?.PublicKey;
        public string? MainAddressBase58 => Accounts.FirstOrDefault()?.DisplayAddress;

        public double SolBalance { get; private set; }
        public List<TokenBalance> TokenBalances { get; private set; } = new();

        private async Task<bool> EnsureConnected()
        {
            return await _connection.Connect();
        }

        private async Task<AuthorizationResult?> AuthorizeOrReauthorize()
        {
            if (!await EnsureConnected()) return null;

            AuthorizationResult? result;
            if (string.IsNullOrEmpty(AuthToken))
            {
                result = await _connection.Client!.Authorize(
                    new Uri("https://solana.unity-sdk.gg/"),
                    new Uri("favicon.ico", UriKind.Relative),
                    "Solana MAUI App",
                    _clusterName
                );
            }
            else
            {
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
                await RefreshBalances();
            }
            return result;
        }

        public async Task RefreshBalances()
        {
            if (MainAddressBase58 == null) return;

            try
            {
                // Fetch SOL Balance
                var balanceResult = await _rpcClient.GetBalanceAsync(MainAddressBase58);
                if (balanceResult.WasSuccessful)
                {
                    SolBalance = balanceResult.Result.Value / 1000000000.0;
                }

                // Fetch Token Balances
                var tokensResult = await _rpcClient.GetTokenAccountsByOwnerAsync(MainAddressBase58, tokenProgramId: Solnet.Programs.TokenProgram.ProgramIdKey);
                if (tokensResult.WasSuccessful)
                {
                    var newList = new List<TokenBalance>();
                    foreach (var acc in tokensResult.Result.Value)
                    {
                        var info = acc.Account.Data.Parsed.Info;
                        var amount = info.TokenAmount.AmountDecimal;
                        if (amount > 0)
                        {
                            newList.Add(new TokenBalance
                            {
                                Mint = info.Mint,
                                Amount = amount,
                                Decimals = info.TokenAmount.Decimals,
                                Symbol = "Token"
                            });
                        }
                    }
                    TokenBalances = newList;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[WMA] Error refreshing balances: " + ex.Message);
            }
        }

        public async Task ConnectWallet()
        {
            try
            {
                var auth = await AuthorizeOrReauthorize();
                if (auth != null)
                {
                    WalletCallbackService.HandleCallback("Connected");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[WMA] Connection Error: " + ex.Message);
            }
        }

        public async Task SendSol(string recipientBase58, ulong lamports)
        {
            try
            {
                if (MainAddress == null) return;

                var blockhashResult = await _rpcClient.GetRecentBlockHashAsync();
                if (!blockhashResult.WasSuccessful) throw new Exception("Failed to get recent blockhash");

                var feePayer = new PublicKey(MainAddress);
                var txBuilder = new TransactionBuilder()
                    .SetRecentBlockHash(blockhashResult.Result.Value.Blockhash)
                    .SetFeePayer(feePayer)
                    .AddInstruction(SystemProgram.Transfer(feePayer, new PublicKey(recipientBase58), lamports));
                
                var txBytes = txBuilder.Build(new List<Account>());

                if (!await EnsureConnected()) return;
                var auth = await AuthorizeOrReauthorize();
                if (auth == null) return;

                var signResult = await _connection.Client!.SignTransactions(new List<byte[]> { txBytes });
                
                if (signResult.SignedPayloads.Any())
                {
                    var txSignature = await _rpcClient.SendTransactionAsync(signResult.SignedPayloads[0]);
                    if (txSignature.WasSuccessful)
                    {
                        WalletCallbackService.HandleCallback("Sent: " + txSignature.Result);
                    }
                    else
                    {
                        throw new Exception(txSignature.Reason);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[WMA] SendSol Error: " + ex.Message);
                WalletCallbackService.HandleCallback("Error: " + ex.Message);
            }
        }

        public async Task SignTestMessage()
        {
            try
            {
                if (MainAddress == null) return;

                var message = Encoding.UTF8.GetBytes("Sign this message to prove ownership.");
                
                if (!await EnsureConnected()) return;
                var auth = await AuthorizeOrReauthorize();
                if (auth == null) return;

                var signResult = await _connection.Client!.SignMessages(
                    new List<byte[]> { message },
                    new List<byte[]> { MainAddress }
                );

                if (signResult.SignedPayloads.Any())
                {
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
