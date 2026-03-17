using Android.Content;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using androidcontentIntent = Android.Content.Intent;
using AndroidNet = Android.Net;

namespace SolanaWMAUnityMAUIIntegration.Platforms.Android
{
    public class WalletLauncher
    {
        public static void Launch(string uri)
        {
            var associationToken = Guid.NewGuid().ToString();
            // Same port range Unity SDK uses
            var port = new Random().Next(WebSocketsTransportContract.WebsocketsLocalPortMin, WebSocketsTransportContract.WebsocketsLocalPortMax + 1);            
            System.Diagnostics.Debug.WriteLine($"Launching wallet association={associationToken} port={port}");
            var server = new WalletSocketServer();
            server?.Start(port);
            var activity = Platform.CurrentActivity;
            var intent = CreateAssociationIntent(associationToken, port);
            activity.StartActivityForResult(intent, 0);
        }/*
        public Task<Response<object>> StartAndExecute(List<Action<IAdapterOperations>> actions)
        {
            if (actions == null || actions.Count == 0)
                throw new ArgumentException("Actions must be non-null and non-empty");
            _actions = new Queue<Action<IAdapterOperations>>(actions);
            var intent = LocalAssociationIntentCreator.CreateAssociationIntent(
                _session.AssociationToken,
                _port);
            _currentActivity.Call("startActivityForResult", intent, 0);
            _currentActivity.Call("runOnUiThread", new AndroidJavaRunnable(TryConnectWs));
            _startAssociationTaskCompletionSource = new TaskCompletionSource<Response<object>>();
            return _startAssociationTaskCompletionSource.Task;
        }

        private async void TryConnectWs()
        {
            var timeout = _clientTimeoutMs;
            while (_webSocket.State != WebSocketState.Open && !_didConnect && timeout.TotalSeconds > 0)
            {
                await _webSocket.Connect(awaitConnection: false);
                var timeDelta = TimeSpan.FromMilliseconds(500);
                timeout -= timeDelta;
                await Task.Delay(timeDelta);
            }
            if (_webSocket.State != WebSocketState.Open)
            {
                Debug.Log("Error: timeout");
            }
        }*/
        public static androidcontentIntent CreateAssociationIntent(string associationToken, int port)
        {
            var intent = new androidcontentIntent();
            intent.SetAction(androidcontentIntent.ActionView);
            intent.AddCategory(androidcontentIntent.CategoryBrowsable);
            
            var url = $"{SchemeMobileWalletAdapter}:/" +
                      $"{LocalPathSuffix}?association={associationToken}&port={port}";

            intent.SetData(AndroidNet.Uri.Parse(url));
            
            //intent.Call<AndroidJavaObject>("addFlags", 0x14000000);
            return intent;
        }
        public const string SchemeMobileWalletAdapter = "solana-wallet";
        public const string LocalPathSuffix = "v1/associate/local";
    }
    public class WalletSocketServer
    {
        bool _didConnect = false;
        private ClientWebSocket _webSocket;
        public async Task Start(int port)
        {
            var webSocketUri = WebSocketsTransportContract.WebsocketsLocalScheme + "://" + WebSocketsTransportContract.WebsocketsLocalHost + ":" + port + WebSocketsTransportContract.WebsocketsLocalPath;
            _webSocket = new ClientWebSocket();
            _webSocket.Options.AddSubProtocol(WebSocketsTransportContract.WebsocketsProtocol);
            var uri = new Uri(webSocketUri);
           
            try
            {
                var delay = TimeSpan.FromSeconds(5);
               
                await Task.Delay(delay);
                await _webSocket.ConnectAsync(uri, CancellationToken.None);
                Console.WriteLine("Connected to wallet");

                // ===== ON OPEN =====
                if (!_didConnect)
                {
                    _didConnect = true;

                  /*  var helloReq = _session.CreateHelloReq();

                    await _webSocket.SendAsync(
                        new ArraySegment<byte>(helloReq),
                        WebSocketMessageType.Binary,
                        true,
                        CancellationToken.None);*/

                 //   ListenKeyExchange();
                }

             //   await ReceiveLoop();
            }
            catch (Exception e)
            {
                // ===== ON ERROR =====
                Console.WriteLine("WebSocket Error: " + e.Message);
            }
            finally
            {
                // ===== ON CLOSE =====
                if (_didConnect)
                {
                  //  await TryReconnect(uri);
                }
            }


            //_webSocket.OnMessage += ReceivePublicKeyHandler;
        }
    }
    public static class WebSocketsTransportContract
    {
        public const string WebsocketsLocalScheme = "ws";
        public const string WebsocketsLocalHost = "127.0.0.1";
        public const string WebsocketsLocalPath = "/solana-wallet";
        public const int WebsocketsLocalPortMin = 49152;
        public const int WebsocketsLocalPortMax = 65535;
        public const string WebsocketsRelectorScheme = "wss";
        public const string WebsocketsProtocol = "com.solana.mobilewalletadapter.v1";
    }
}
