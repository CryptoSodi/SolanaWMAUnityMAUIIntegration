using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Linq;

namespace SolanaWMAUnityMAUIIntegration.SolanaWallet
{
    public class JsonRequest
    {
        public class JsonRequestIdentity
        {
            [JsonProperty("uri", NullValueHandling = NullValueHandling.Ignore)]
            public Uri? Uri { get; set; }

            [JsonProperty("icon", NullValueHandling = NullValueHandling.Ignore)]
            public Uri? Icon { get; set; }

            [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
            public string? Name { get; set; }
        }

        public class JsonRequestParams
        {
            [JsonProperty("identity", NullValueHandling = NullValueHandling.Ignore)]
            public JsonRequestIdentity? Identity { get; set; }

            [JsonProperty("cluster", NullValueHandling = NullValueHandling.Ignore)]
            public string? Cluster { get; set; }

            [JsonProperty("auth_token", NullValueHandling = NullValueHandling.Ignore)]
            public string? AuthToken { get; set; }

            [JsonProperty("payloads", NullValueHandling = NullValueHandling.Ignore)]
            public List<string>? Payloads { get; set; }

            [JsonProperty("addresses", NullValueHandling = NullValueHandling.Ignore)]
            public List<string>? Addresses { get; set; }
        }

        [JsonProperty("jsonrpc")]
        public string JsonRpc { get; set; } = "2.0";

        [JsonProperty("method")]
        public string Method { get; set; } = string.Empty;

        [JsonProperty("params")]
        public JsonRequestParams Params { get; set; } = new();

        [JsonProperty("id")]
        public int Id { get; set; }
    }

    public class Response<T>
    {
        public class ResponseError
        {
            [JsonProperty("code")]
            public long Code { get; set; }

            [JsonProperty("message")]
            public string Message { get; set; } = string.Empty;
        }

        [JsonProperty("jsonrpc")]
        public string JsonRpc { get; set; } = string.Empty;

        [JsonProperty("result")]
        public T? Result { get; set; }

        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("error")]
        public ResponseError? Error { get; set; }

        public bool WasSuccessful => Error is null;
        public bool Failed => Error is not null;
    }

    public abstract class JsonRpc20Client
    {
        private readonly IMessageSender _messageSender;
        private readonly Dictionary<int, (TaskCompletionSource<object> tcs, Type resultType)> _pendingRequests = new();

        protected JsonRpc20Client(IMessageSender messageSender)
        {
            _messageSender = messageSender;
        }

        protected async Task<T> SendRequest<T>(JsonRequest jsonRequest)
        {
            var message = JsonConvert.SerializeObject(jsonRequest);
            var messageBytes = System.Text.Encoding.UTF8.GetBytes(message);
            
            var tcs = new TaskCompletionSource<object>();
            _pendingRequests[jsonRequest.Id] = (tcs, typeof(T));
            
            await _messageSender.Send(messageBytes);
            
            var result = await tcs.Task;
            return (T)result;
        }

        public void Receive(string message)
        {
            try
            {
                var response = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(message);
                if (response != null && response.TryGetValue("id", out var idToken))
                {
                    int id = (int)idToken;
                    Console.WriteLine($"[WMA] Processing RPC Response for ID: {id}");
                    if (_pendingRequests.TryGetValue(id, out var pending))
                    {
                        if (response.TryGetValue("error", out var errorToken) && errorToken.Type != Newtonsoft.Json.Linq.JTokenType.Null)
                        {
                            var error = errorToken.ToObject<Response<object>.ResponseError>();
                            Console.WriteLine($"[WMA] RPC Error: {error?.Message}");
                            pending.tcs.SetException(new Exception(error?.Message ?? "Unknown RPC error"));
                        }
                        else if (response.TryGetValue("result", out var resultToken))
                        {
                            Console.WriteLine($"[WMA] RPC Success. Deserializing result to {pending.resultType.Name}");
                            var result = resultToken.ToObject(pending.resultType);
                            pending.tcs.SetResult(result!);
                        }
                        else
                        {
                            Console.WriteLine("[WMA] Invalid RPC response: missing result and error");
                            pending.tcs.SetException(new Exception("Invalid RPC response: missing result and error"));
                        }
                        _pendingRequests.Remove(id);
                    }
                    else
                    {
                        Console.WriteLine($"[WMA] Warning: Received response for unknown request ID: {id}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WMA] Error processing RPC response: {ex.Message}");
            }
        }
    }
}
