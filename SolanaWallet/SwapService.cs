using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Text;

namespace SolanaWMAUnityMAUIIntegration.SolanaWallet
{
    public class JupiterQuoteResponse
    {
        [JsonProperty("inputMint")]
        public string InputMint { get; set; } = string.Empty;

        [JsonProperty("outputMint")]
        public string OutputMint { get; set; } = string.Empty;

        [JsonProperty("inAmount")]
        public string InAmount { get; set; } = string.Empty;

        [JsonProperty("outAmount")]
        public string OutAmount { get; set; } = string.Empty;

        [JsonProperty("otherAmountThreshold")]
        public string OtherAmountThreshold { get; set; } = string.Empty;

        [JsonProperty("priceImpactPct")]
        public string PriceImpactPct { get; set; } = string.Empty;
    }

    public class JupiterSwapResponse
    {
        [JsonProperty("swapTransaction")]
        public string SwapTransaction { get; set; } = string.Empty;
    }

    public static class SwapService
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private const string JUPITER_API_MAINNET = "https://quote-api.jup.ag/v6";
        private const string JUPITER_API_DEVNET = "https://devnet.jup.ag/v6";

        private static string GetBaseUrl(bool isMainnet) => isMainnet ? JUPITER_API_MAINNET : JUPITER_API_DEVNET;

        public static async Task<JupiterQuoteResponse?> GetQuote(string inputMint, string outputMint, ulong amount, bool isMainnet = true)
        {
            try
            {
                var baseUrl = GetBaseUrl(isMainnet);
                var url = $"{baseUrl}/quote?inputMint={inputMint}&outputMint={outputMint}&amount={amount}&slippageBps=50";
                Console.WriteLine($"[Jupiter] Fetching quote ({ (isMainnet ? "Mainnet" : "Devnet") }): {url}");
                var response = await _httpClient.GetStringAsync(url);
                return JsonConvert.DeserializeObject<JupiterQuoteResponse>(response);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Jupiter] GetQuote Error: {ex.Message}");
                return null;
            }
        }

        public static async Task<string?> GetSwapTransaction(string userPublicKey, JupiterQuoteResponse quote, bool isMainnet = true)
        {
            try
            {
                var baseUrl = GetBaseUrl(isMainnet);
                var url = $"{baseUrl}/swap";

                var body = new
                {
                    userPublicKey = userPublicKey,
                    quoteResponse = quote,
                    wrapAndUnwrapSol = true
                };

                Console.WriteLine($"[Jupiter] Requesting swap transaction for {userPublicKey}...");
                var response = await _httpClient.PostAsJsonAsync(url, body);
                var json = await response.Content.ReadAsStringAsync();
                
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[Jupiter] Swap API Error: {json}");
                    return null;
                }

                var swapRes = JsonConvert.DeserializeObject<JupiterSwapResponse>(json);
                return swapRes?.SwapTransaction;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Jupiter] GetSwapTransaction Error: {ex.Message}");
                return null;
            }
        }
    }
}
