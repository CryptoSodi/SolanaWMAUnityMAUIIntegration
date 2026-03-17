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
            // Use specific endpoints instead of default enum to ensure reliability
            var url = isMainnet ? "https://api.mainnet-beta.solana.com" : "https://api.devnet.solana.com";
            _rpcClient = ClientFactory.GetClient(url);
            _clusterName = isMainnet ? "mainnet-beta" : "devnet";
            Console.WriteLine($"[WMA] Network switched to: {_clusterName} ({url})");
            
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
                Console.WriteLine($"[WMA] Refreshing balances for {MainAddressBase58}...");
                
                // Fetch SOL Balance
                var balanceResult = await _rpcClient.GetBalanceAsync(MainAddressBase58);
                if (balanceResult.WasSuccessful)
                {
                    SolBalance = balanceResult.Result.Value / 1000000000.0;
                    Console.WriteLine($"[WMA] SOL Balance: {SolBalance}");
                }

                var newList = new List<TokenBalance>();

                // Fetch Original Token Program Balances
                var tokensResult = await _rpcClient.GetTokenAccountsByOwnerAsync(MainAddressBase58, tokenProgramId: Solnet.Programs.TokenProgram.ProgramIdKey);
                if (tokensResult.WasSuccessful)
                {
                    Console.WriteLine($"[WMA] Found {tokensResult.Result.Value.Count} TokenProgram accounts");
                    ProcessTokenAccounts(tokensResult.Result.Value, newList);
                }
                else
                {
                    Console.WriteLine($"[WMA] TokenProgram fetch failed: {tokensResult.Reason}");
                }

                // Fetch Token-2022 Program Balances
                var tokens2022Result = await _rpcClient.GetTokenAccountsByOwnerAsync(MainAddressBase58, tokenProgramId: "TokenzQdBNbLqP5VEhdkAS6EPFLC1PHnBqCXEpPxuEb");
                if (tokens2022Result.WasSuccessful)
                {
                    Console.WriteLine($"[WMA] Found {tokens2022Result.Result.Value.Count} Token-2022 accounts");
                    ProcessTokenAccounts(tokens2022Result.Result.Value, newList);
                }
                else
                {
                    Console.WriteLine($"[WMA] Token-2022 fetch failed (likely unsupported by RPC): {tokens2022Result.Reason}");
                }

                TokenBalances = newList;
                Console.WriteLine($"[WMA] Total non-zero token balances displayed: {TokenBalances.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine("[WMA] Error refreshing balances: " + ex.Message);
            }
        }

        private void ProcessTokenAccounts(List<Solnet.Rpc.Models.TokenAccount> accounts, List<TokenBalance> newList)
        {
            foreach (var acc in accounts)
            {
                try
                {
                    if (acc.Account.Data.Parsed == null)
                    {
                        Console.WriteLine($"[WMA] Warning: Account {acc.PublicKey} has no parsed data. Skipping.");
                        continue;
                    }

                    var info = acc.Account.Data.Parsed.Info;
                    var amount = info.TokenAmount.AmountDecimal;
                    
                    Console.WriteLine($"[WMA] Token Account: {acc.PublicKey}, Mint: {info.Mint}, Balance: {amount}");

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
                catch (Exception ex)
                {
                    Console.WriteLine($"[WMA] Error processing token account {acc.PublicKey}: {ex.Message}");
                }
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

        public async Task SendToken(string recipientBase58, ulong amount, string mintAddress, int decimals)
        {
            try
            {
                if (MainAddress == null) return;

                var blockhashResult = await _rpcClient.GetRecentBlockHashAsync();
                if (!blockhashResult.WasSuccessful) throw new Exception("Failed to get recent blockhash");

                var feePayer = new PublicKey(MainAddress);
                var mint = new PublicKey(mintAddress);
                var recipient = new PublicKey(recipientBase58);

                // Derive ATAs
                var senderAta = TokenService.FindAssociatedTokenAddress(feePayer, mint);
                var recipientAta = TokenService.FindAssociatedTokenAddress(recipient, mint);

                var txBuilder = new TransactionBuilder()
                    .SetRecentBlockHash(blockhashResult.Result.Value.Blockhash)
                    .SetFeePayer(feePayer);

                // Check if recipient ATA exists
                var recipientAtaInfo = await _rpcClient.GetAccountInfoAsync(recipientAta.Key);
                if (!recipientAtaInfo.WasSuccessful || recipientAtaInfo.Result.Value == null)
                {
                    Console.WriteLine("[WMA] Creating recipient ATA...");
                    txBuilder.AddInstruction(TokenService.CreateAssociatedTokenAccountInstruction(feePayer, recipient, mint));
                }

                // Add TransferChecked instruction
                txBuilder.AddInstruction(TokenService.CreateTransferCheckedInstruction(
                    senderAta,
                    mint,
                    recipientAta,
                    feePayer,
                    amount,
                    (byte)decimals
                ));
                
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
                        WalletCallbackService.HandleCallback("Token Sent: " + txSignature.Result);
                    }
                    else
                    {
                        throw new Exception(txSignature.Reason);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[WMA] SendToken Error: " + ex.Message);
                WalletCallbackService.HandleCallback("Error: " + ex.Message);
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
