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
            var url = isMainnet ? "https://api.mainnet-beta.solana.com" : "https://api.devnet.solana.com";
            _rpcClient = ClientFactory.GetClient(url);
            _clusterName = isMainnet ? "mainnet-beta" : "devnet";
            Console.WriteLine($"[WMA] Network switched to: {_clusterName} ({url})");
            
            AuthToken = null;
            Accounts = new();
            SolBalance = 0;
            TokenBalances = new();
        }
        
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

            AuthorizationResult? result = null;
            
            if (!string.IsNullOrEmpty(AuthToken))
            {
                try
                {
                    Console.WriteLine($"[WMA] Attempting Reauthorize with token: {AuthToken.Substring(0, 10)}...");
                    result = await _connection.Client!.Reauthorize(
                        new Uri("https://solana.unity-sdk.gg/"),
                        new Uri("favicon.ico", UriKind.Relative),
                        "Solana MAUI App",
                        AuthToken
                    );
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WMA] Reauthorize failed: {ex.Message}. Falling back to Authorize.");
                    AuthToken = null;
                }
            }

            if (result == null)
            {
                Console.WriteLine("[WMA] Authorizing new session...");
                result = await _connection.Client!.Authorize(
                    new Uri("https://solana.unity-sdk.gg/"),
                    new Uri("favicon.ico", UriKind.Relative),
                    "Solana MAUI App",
                    _clusterName
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
                
                var balanceResult = await _rpcClient.GetBalanceAsync(MainAddressBase58);
                if (balanceResult.WasSuccessful)
                {
                    SolBalance = balanceResult.Result.Value / 1000000000.0;
                    Console.WriteLine($"[WMA] SOL Balance: {SolBalance}");
                }

                var newList = new List<TokenBalance>();

                var tokensResult = await _rpcClient.GetTokenAccountsByOwnerAsync(MainAddressBase58, tokenProgramId: Solnet.Programs.TokenProgram.ProgramIdKey);
                if (tokensResult.WasSuccessful)
                {
                    Console.WriteLine($"[WMA] Found {tokensResult.Result.Value.Count} TokenProgram accounts");
                    ProcessTokenAccounts(tokensResult.Result.Value, newList);
                }

                var tokens2022Result = await _rpcClient.GetTokenAccountsByOwnerAsync(MainAddressBase58, tokenProgramId: TokenService.TOKEN_2022_PROGRAM_ID.Key);
                if (tokens2022Result.WasSuccessful)
                {
                    Console.WriteLine($"[WMA] Found {tokens2022Result.Result.Value.Count} Token-2022 accounts");
                    ProcessTokenAccounts(tokens2022Result.Result.Value, newList);
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
                    if (acc.Account.Data.Parsed == null) continue;

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
                catch { }
            }
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
                Console.WriteLine("[WMA] Connection Error: " + ex.Message);
            }
        }

        public async Task SendToken(string recipientBase58, ulong amount, string mintAddress, int decimals)
        {
            try
            {
                if (MainAddress == null) return;

                Console.WriteLine("[WMA] Fetching blockhash...");
                var blockhashResult = await _rpcClient.GetLatestBlockHashAsync();
                if (!blockhashResult.WasSuccessful || blockhashResult.Result == null) 
                {
                    throw new Exception($"Failed to get latest blockhash: {blockhashResult.Reason}");
                }

                var feePayer = new PublicKey(MainAddress);
                var mint = new PublicKey(mintAddress);
                var recipient = new PublicKey(recipientBase58);

                var senderAta = TokenService.FindAssociatedTokenAddress(feePayer, mint);
                var recipientAta = TokenService.FindAssociatedTokenAddress(recipient, mint);

                Console.WriteLine($"[WMA] Sending {amount} tokens. Sender ATA: {senderAta}, Recipient ATA: {recipientAta}");

                var txBuilder = new TransactionBuilder()
                    .SetRecentBlockHash(blockhashResult.Result.Value.Blockhash)
                    .SetFeePayer(feePayer);

                var recipientAtaInfo = await _rpcClient.GetAccountInfoAsync(recipientAta.Key);
                if (!recipientAtaInfo.WasSuccessful || recipientAtaInfo.Result.Value == null)
                {
                    Console.WriteLine("[WMA] Recipient ATA not found. Adding creation instruction...");
                    txBuilder.AddInstruction(TokenService.CreateAssociatedTokenAccountInstruction(feePayer, recipient, mint));
                }

                txBuilder.AddInstruction(TokenService.CreateTransferCheckedInstruction(
                    senderAta, mint, recipientAta, feePayer, amount, (byte)decimals
                ));
                
                // Build the transaction message
                var msgBytes = txBuilder.CompileMessage();
                
                // Manually construct the Transaction wire format for WMA:
                // signature_count (1) + signature (64 zero bytes) + message
                var txBytes = new byte[1 + 64 + msgBytes.Length];
                txBytes[0] = 1; // 1 signature slot
                Array.Copy(msgBytes, 0, txBytes, 65, msgBytes.Length);

                var auth = await AuthorizeOrReauthorize();
                if (auth == null) return;

                Console.WriteLine("[WMA] Requesting signature from wallet...");
                var signResult = await _connection.Client!.SignTransactions(new List<byte[]> { txBytes });
                
                Console.WriteLine($"[WMA] Received sign result. Payloads count: {signResult?.SignedPayloads?.Count ?? 0}");

                if (signResult != null && signResult.SignedPayloads.Any())
                {
                    Console.WriteLine("[WMA] Broadcasting signed transaction...");
                    try 
                    {
                        var txSignature = await _rpcClient.SendTransactionAsync(signResult.SignedPayloads[0]);
                        if (txSignature.WasSuccessful)
                        {
                            Console.WriteLine($"[WMA] Success! Signature: {txSignature.Result}");
                            WalletCallbackService.HandleCallback("Token Sent: " + txSignature.Result);
                        }
                        else
                        {
                            Console.WriteLine($"[WMA] RPC Broadcast Error: {txSignature.Reason}");
                            WalletCallbackService.HandleCallback("Broadcast Error: " + txSignature.Reason);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[WMA] Exception during broadcast: {ex.Message}");
                        WalletCallbackService.HandleCallback("Broadcast Exception: " + ex.Message);
                    }
                }
                else
                {
                    Console.WriteLine("[WMA] No signed payloads returned from wallet.");
                    WalletCallbackService.HandleCallback("Error: No signature received");
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

                Console.WriteLine("[WMA] Fetching blockhash...");
                var blockhashResult = await _rpcClient.GetLatestBlockHashAsync();
                if (!blockhashResult.WasSuccessful || blockhashResult.Result == null) 
                {
                    throw new Exception($"Failed to get latest blockhash: {blockhashResult.Reason}");
                }

                var feePayer = new PublicKey(MainAddress);
                var txBuilder = new TransactionBuilder()
                    .SetRecentBlockHash(blockhashResult.Result.Value.Blockhash)
                    .SetFeePayer(feePayer)
                    .AddInstruction(SystemProgram.Transfer(feePayer, new PublicKey(recipientBase58), lamports));
                
                var msgBytes = txBuilder.CompileMessage();
                var txBytes = new byte[1 + 64 + msgBytes.Length];
                txBytes[0] = 1;
                Array.Copy(msgBytes, 0, txBytes, 65, msgBytes.Length);

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
                        throw new Exception($"Broadcast failed: {txSignature.Reason}");
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
