using SolanaWMAUnityMAUIIntegration.SolanaWallet;

namespace SolanaWMAUnityMAUIIntegration
{
    public partial class MainPage : ContentPage
    {
        WalletConnection wallet = new WalletConnection();

        public MainPage()
        {
            InitializeComponent();
            WalletCallbackService.OnWalletConnected += OnWalletConnected;
        }

        private void OnWalletConnected(string message)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                StatusLabel.Text = "Status: " + message;
                UpdateUI();
                if (message.StartsWith("Error"))
                {
                    await DisplayAlert("Wallet Error", message, "OK");
                }
            });
        }

        private void UpdateUI()
        {
            AddressLabel.Text = wallet.MainAddressBase58 ?? "Not Connected";
            BalanceLabel.Text = $"{wallet.SolBalance:N2} SOL";
            TokensCollectionView.ItemsSource = wallet.TokenBalances;
            
            if (wallet.MainAddress != null)
            {
                ConnectBtn.Text = "Reconnect / Refresh";
                ConnectBtn.BackgroundColor = Color.FromArgb("#2a2a2a");
                ConnectBtn.TextColor = Colors.White;
            }
        }

        private async void ConnectWalletClicked(object sender, EventArgs e)
        {
            StatusLabel.Text = "Status: Connecting...";
            await wallet.ConnectWallet();
        }

        private void NetworkSwitch_Toggled(object sender, ToggledEventArgs e)
        {
            wallet.SetNetwork(e.Value); // True = Mainnet, False = Devnet
            UpdateUI();
        }

        private void SendClicked(object sender, EventArgs e)
        {
            if (wallet.MainAddress == null)
            {
                DisplayAlert("Wallet", "Please connect your wallet first", "OK");
                return;
            }
            SendForm.IsVisible = true;
        }

        private async void ConfirmSendClicked(object sender, EventArgs e)
        {
            var recipient = RecipientEntry.Text;
            if (string.IsNullOrEmpty(recipient)) return;

            if (!double.TryParse(AmountEntry.Text, out double solAmount)) return;

            StatusLabel.Text = $"Status: Sending {solAmount} SOL...";
            SendForm.IsVisible = false;

            ulong lamports = (ulong)(solAmount * 1000000000);
            await wallet.SendSol(recipient, lamports);
        }

        private void CancelSendClicked(object sender, EventArgs e)
        {
            SendForm.IsVisible = false;
        }

        private async void SwapClicked(object sender, EventArgs e)
        {
            await DisplayAlert("Swap", "Swap functionality coming soon! (Awaiting token address)", "OK");
        }
    }
}
