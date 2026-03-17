using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SolanaWMAUnityMAUIIntegration.SolanaWallet
{
    internal class MobileWalletAdapter
    {
        private ClientWebSocket _socket;
        private Uri _walletUri;

        public async Task Connect(string walletUrl)
        {
            _walletUri = new Uri(walletUrl);

            _socket = new ClientWebSocket();

            await _socket.ConnectAsync(
                new Uri("ws://localhost:8900"),
                CancellationToken.None
            );
        }

        public async Task<string> SendRequest(object request)
        {
            var json = JsonSerializer.Serialize(request);

            var buffer = Encoding.UTF8.GetBytes(json);

            await _socket.SendAsync(
                new ArraySegment<byte>(buffer),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None
            );

            var receiveBuffer = new byte[4096];

            var result = await _socket.ReceiveAsync(
                new ArraySegment<byte>(receiveBuffer),
                CancellationToken.None
            );

            return Encoding.UTF8.GetString(receiveBuffer, 0, result.Count);
        }
    }
}
