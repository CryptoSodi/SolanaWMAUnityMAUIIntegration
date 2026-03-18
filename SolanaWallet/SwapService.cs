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

        [JsonProperty("routePlan")]
        public List<object> RoutePlan { get; set; } = new();

        // Store the original JSON to pass back to the /swap endpoint
        [JsonIgnore]
        public string OriginalJson { get; set; } = string.Empty;
    }

    public class JupiterSwapResponse
    {
        [JsonProperty("swapTransaction")]
        public string SwapTransaction { get; set; } = string.Empty;

        [JsonProperty("prioritizationFeeLamports")]
        public ulong PrioritizationFeeLamports { get; set; }

        [JsonProperty("computeUnitLimit")]
        public uint ComputeUnitLimit { get; set; }
    }

    public class JupiterTokenData
    {
        [JsonProperty("address")]
        public string Address { get; set; } = string.Empty;
        [JsonProperty("symbol")]
        public string Symbol { get; set; } = string.Empty;
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;
        [JsonProperty("decimals")]
        public int Decimals { get; set; }
        [JsonProperty("logoURI")]
        public string LogoURI { get; set; } = string.Empty;
    }

    public static class SwapService
    {
        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        private const string JUPITER_API_MAINNET = "https://lite-api.jup.ag/swap/v1";
        private const string JUPITER_API_DEVNET = "https://lite-api.jup.ag/swap/v1";
        private const string JUPITER_TOKEN_LIST_STRICT = "https://token.jup.ag/strict";
        private const string JUPITER_TOKEN_LIST_ALL = "https://token.jup.ag/all";

        /// <summary>
        /// Set your Jupiter API key here if you have one.
        /// </summary>
        public static string? ApiKey { get; set; } = "d0d4939e-01f5-4fe3-8f1e-f1df08afeaa2";

        private static string GetBaseUrl(bool isMainnet) => isMainnet ? JUPITER_API_MAINNET : JUPITER_API_DEVNET;

        private static void ApplyHeaders()
        {
            _httpClient.DefaultRequestHeaders.Clear();
            if (!string.IsNullOrEmpty(ApiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("x-api-key", ApiKey);
            }
        }

        public static async Task<List<JupiterTokenData>> GetTokens(bool strict = true)
        {
            try
            {
                ApplyHeaders();
                string url = strict ? JUPITER_TOKEN_LIST_STRICT : JUPITER_TOKEN_LIST_ALL;
                Console.WriteLine($"[Jupiter] Fetching token list (strict: {strict})...");
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return new List<JupiterTokenData>();
                
                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<List<JupiterTokenData>>(content) ?? new List<JupiterTokenData>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Jupiter] GetTokens Error: {ex.Message}");
                return new List<JupiterTokenData>();
            }
        }

        public static async Task<JupiterQuoteResponse?> GetQuote(string inputMint, string outputMint, ulong amount, bool isMainnet = true)
        {
            try
            {
                ApplyHeaders();
                var baseUrl = GetBaseUrl(isMainnet);
                // restrictIntermediateTokens=true ensures stable routing
                // slippageBps=100 is 1.0%
                var url = $"{baseUrl}/quote?inputMint={inputMint}&outputMint={outputMint}&amount={amount}&slippageBps=100&restrictIntermediateTokens=true&onlyDirectRoutes=false";
                Console.WriteLine($"[Jupiter] Fetching quote ({(isMainnet ? "Mainnet" : "Devnet")}): {url}");
                
                var response = await _httpClient.GetAsync(url);
                var content = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[Jupiter] Quote Response: {content}");

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[Jupiter] GetQuote Error ({(int)response.StatusCode}): {content}");
                    return null;
                }

                var quote = JsonConvert.DeserializeObject<JupiterQuoteResponse>(content);
                if (quote != null)
                {
                    quote.OriginalJson = content;
                }
                return quote;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Jupiter] GetQuote Exception: {ex.Message}");
                return null;
            }
        }

        public static async Task<JupiterSwapResponse?> GetSwapTransaction(string userPublicKey, JupiterQuoteResponse quote, bool isMainnet = true)
        {
            try
            {
                ApplyHeaders();
                var baseUrl = GetBaseUrl(isMainnet);
                var url = $"{baseUrl}/swap";

                var quoteObj = JsonConvert.DeserializeObject<object>(quote.OriginalJson);

                var body = new
                {
                    userPublicKey = userPublicKey,
                    quoteResponse = quoteObj,
                    wrapAndUnwrapSol = true,
                    dynamicComputeUnitLimit = true,
                    prioritizationFeeLamports = "auto" 
                };

                Console.WriteLine($"[Jupiter] Requesting swap transaction for {userPublicKey} via {url}...");
                var response = await _httpClient.PostAsJsonAsync(url, body);
                var json = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[Jupiter] Swap Response: {json}");
                
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[Jupiter] Swap API Error: {json}");
                    return null;
                }

                return JsonConvert.DeserializeObject<JupiterSwapResponse>(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Jupiter] GetSwapTransaction Error: {ex.Message}");
                return null;
            }
        }
    }
}
