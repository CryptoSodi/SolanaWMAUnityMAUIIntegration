using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace SolanaWMAUnityMAUIIntegration.SolanaWallet
{
    public interface IMessageSender
    {
        Task Send(byte[] message);
    }

    public interface IMessageReceiver
    {
        // This is usually implemented by the client to receive decrypted messages
    }
    
    public interface IAdapterOperations
    {
        // Define WMA operations here
        Task<AuthorizationResult> Authorize(Uri identityUri, Uri iconUri, string identityName, string cluster);
        Task<AuthorizationResult> Reauthorize(Uri identityUri, Uri iconUri, string identityName, string authToken);
        Task<SignedResult> SignTransactions(IEnumerable<byte[]> transactions);
        Task<SignedResult> SignMessages(IEnumerable<byte[]> messages, IEnumerable<byte[]> addresses);
    }

    public class AccountDetails
    {
        [JsonProperty("address")]
        public string Address { get; set; } = string.Empty;

        [JsonProperty("display_address")]
        public string DisplayAddress { get; set; } = string.Empty;

        [JsonProperty("display_address_format")]
        public string? DisplayAddressFormat { get; set; }

        [JsonProperty("label")]
        public string? Label { get; set; }

        // The 'address' field in WMA JSON is base64 encoded raw bytes
        public byte[] PublicKey => Convert.FromBase64String(Address);
    }

    public class AuthorizationResult
    {
        [JsonProperty("auth_token")]
        public string AuthToken { get; set; } = string.Empty;

        [JsonProperty("accounts")]
        public List<AccountDetails> Accounts { get; set; } = new();

        [JsonProperty("wallet_uri_base")]
        public string? WalletUriBase { get; set; }
    }

    public class TokenBalance
    {
        public string Mint { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int Decimals { get; set; }
        public string Symbol { get; set; } = "Unknown";
        public string DisplayAmount => Amount.ToString("N" + Math.Min(Decimals, 4));
    }

    public class SignedResult
    {
        public List<string> SignedPayloads { get; set; } = new();
        public List<byte[]> SignedPayloadsBytes => SignedPayloads.Select(Convert.FromBase64String).ToList();
    }
}
