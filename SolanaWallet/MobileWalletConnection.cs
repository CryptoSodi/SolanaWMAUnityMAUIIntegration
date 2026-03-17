using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using Newtonsoft.Json;
using System.Linq;

namespace SolanaWMAUnityMAUIIntegration.SolanaWallet
{
    public class MobileWalletConnection : IMessageSender
    {
        private ClientWebSocket? _webSocket;
        private MobileWalletSession? _session;
        private MobileWalletAdapterClient? _client;
        private int _port;
        private string? _associationToken;
        private TaskCompletionSource<bool>? _handshakeTcs;

        public MobileWalletAdapterClient? Client => _client;

        private bool _isConnecting = false;

        public async Task<bool> Connect()
        {
            if (_isConnecting)
            {
                Console.WriteLine("[WMA] Connection already in progress, skipping...");
                return false;
            }

            _isConnecting = true;
            try
            {
                Console.WriteLine("[WMA] Initializing MobileWalletSession...");
                _session = new MobileWalletSession();
                _associationToken = _session.AssociationToken;
                _port = new Random().Next(49152, 65535);
                _client = null; // Reset client for new connection

                Console.WriteLine($"[WMA] Association Token: {_associationToken}");
                Console.WriteLine($"[WMA] Local Port: {_port}");

#if ANDROID
                Console.WriteLine("[WMA] Launching Wallet Intent...");
                Platforms.Android.WalletLauncher.Launch(_associationToken, _port);
                
                Console.WriteLine("[WMA] Waiting 2 seconds for wallet to warm up...");
                await Task.Delay(2000);
#endif

                var webSocketUri = $"ws://127.0.0.1:{_port}/solana-wallet";
                var timeout = TimeSpan.FromSeconds(10);
                var cts = new CancellationTokenSource(timeout);

                while (!cts.IsCancellationRequested)
                {
                    try
                    {
                        // Always dispose and recreate the socket on each attempt to be safe
                        if (_webSocket != null)
                        {
                            try { _webSocket.Dispose(); } catch { }
                            _webSocket = null;
                        }

                        Console.WriteLine("[WMA] Creating new WebSocket instance...");
                        _webSocket = new ClientWebSocket();
                        _webSocket.Options.AddSubProtocol("com.solana.mobilewalletadapter.v1");

                        await _webSocket.ConnectAsync(new Uri(webSocketUri), cts.Token);
                        if (_webSocket.State == WebSocketState.Open)
                        {
                            Console.WriteLine("[WMA] WebSocket Connected!");
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[WMA] Connection attempt failed: {ex.Message}. Retrying...");
                        await Task.Delay(500);
                    }
                }

                if (_webSocket?.State != WebSocketState.Open)
                {
                    Console.WriteLine("[WMA] Error: Failed to connect to wallet WebSocket within timeout.");
                    return false;
                }

                _handshakeTcs = new TaskCompletionSource<bool>();
                
                // Start Receive Loop
                _ = ReceiveLoop();

                // Send Hello
                Console.WriteLine("[WMA] Sending HELLO_REQ...");
                var helloReq = _session.CreateHelloReq();
                await _webSocket.SendAsync(new ArraySegment<byte>(helloReq), WebSocketMessageType.Binary, true, CancellationToken.None);

                var result = await _handshakeTcs.Task;
                Console.WriteLine($"[WMA] Handshake {(result ? "Succeeded" : "Failed")}");
                return result;
            }
            finally
            {
                _isConnecting = false;
            }
        }

        private async Task ReceiveLoop()
        {
            if (_webSocket == null || _session == null) return;

            var buffer = new byte[1024 * 32];
            while (_webSocket.State == WebSocketState.Open)
            {
                try
                {
                    var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Console.WriteLine("[WMA] WebSocket Closed by remote.");
                        await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                    }
                    else if (result.MessageType == WebSocketMessageType.Binary)
                    {
                        var data = buffer.Take(result.Count).ToArray();
                        
                        if (_client == null)
                        {
                            Console.WriteLine("[WMA] Received HELLO_RSP. Generating shared secret...");
                            // Handshake response (HELLO_RSP)
                            _session.GenerateSessionEcdhSecret(data);
                            _client = new MobileWalletAdapterClient(this);
                            _handshakeTcs?.SetResult(true);
                        }
                        else
                        {
                            Console.WriteLine($"[WMA] Received Encrypted Payload ({data.Length} bytes). Decrypting...");
                            // Encrypted payload
                            var decrypted = _session.DecryptSessionPayload(data);
                            var json = Encoding.UTF8.GetString(decrypted);
                            Console.WriteLine($"[WMA] Decrypted JSON: {json}");
                            _client.Receive(json);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WMA] WebSocket Receive Loop Error: {ex.Message}");
                    break;
                }
            }
        }

        async Task IMessageSender.Send(byte[] message)
        {
            if (_webSocket == null || _session == null) return;

            Console.WriteLine($"[WMA] Sending RPC Request: {Encoding.UTF8.GetString(message)}");
            var encrypted = _session.EncryptSessionPayload(message);
            Console.WriteLine($"[WMA] Encrypted Payload Size: {encrypted.Length} bytes");
            await _webSocket.SendAsync(new ArraySegment<byte>(encrypted), WebSocketMessageType.Binary, true, CancellationToken.None);
        }
    }
}
