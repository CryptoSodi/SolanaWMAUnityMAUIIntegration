using SolanaWMAUnityMAUIIntegration.SolanaWallet;

namespace SolanaWMAUnityMAUIIntegration
{
    public partial class MainPage : ContentPage
    {
        WalletConnection wallet = new WalletConnection();
        private bool _isSwapping = false;

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
            CopyBtn.IsVisible = wallet.MainAddressBase58 != null;
            
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

        private async void CopyAddressClicked(object sender, EventArgs e)
        {
            if (wallet.MainAddressBase58 != null)
            {
                await Clipboard.Default.SetTextAsync(wallet.MainAddressBase58);
                StatusLabel.Text = "Status: Address copied!";
                await Task.Delay(2000);
                StatusLabel.Text = "Status: Idle";
            }
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
            _isSwapping = false;
            SendForm.IsVisible = true;
            AmountEntry.Placeholder = "Amount (SOL)";
        }

        private void SwapClicked(object sender, EventArgs e)
        {
            if (wallet.MainAddress == null)
            {
                DisplayAlert("Wallet", "Please connect your wallet first", "OK");
                return;
            }
            _isSwapping = true;
            SendForm.IsVisible = true;
            AmountEntry.Placeholder = "Amount (LUDC)";
        }

        private async void ConfirmSendClicked(object sender, EventArgs e)
        {
            var recipient = RecipientEntry.Text;
            if (string.IsNullOrEmpty(recipient)) return;

            if (!double.TryParse(AmountEntry.Text, out double amount)) return;

            SendForm.IsVisible = false;

            if (_isSwapping)
            {
                // Hardcoded LUDC mint for demo: 8Abr4aSqHbqUNK1ubRVfcdnAhS3RjmYRPDf11dt7pcfW (Decimals: 9)
                string mint = "8Abr4aSqHbqUNK1ubRVfcdnAhS3RjmYRPDf11dt7pcfW";
                int decimals = 9;
                ulong tokenAmount = (ulong)(amount * Math.Pow(10, decimals));
                
                StatusLabel.Text = $"Status: Sending {amount} LUDC...";
                await wallet.SendToken(recipient, tokenAmount, mint, decimals);
            }
            else
            {
                StatusLabel.Text = $"Status: Sending {amount} SOL...";
                ulong lamports = (ulong)(amount * 1000000000);
                await wallet.SendSol(recipient, lamports);
            }
        }

        private void CancelSendClicked(object sender, EventArgs e)
        {
            SendForm.IsVisible = false;
        }
    }
}
