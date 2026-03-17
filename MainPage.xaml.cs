using SolanaWMAUnityMAUIIntegration.SolanaWallet;

namespace SolanaWMAUnityMAUIIntegration
{
    public partial class MainPage : ContentPage
    {
        WalletConnection wallet = new WalletConnection();
        private bool _isSwapping = false;
        private TokenBalance? _selectedToken = null;

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
                
                if (message.StartsWith("Token Sent") || message.StartsWith("Sent:") || message.StartsWith("Swap Sent"))
                {
                    StatusLabel.Text = "Status: Transaction successful! Reloading...";
                    await wallet.RefreshBalances();
                    UpdateUI();
                }

                if (message.StartsWith("Error"))
                {
                    await DisplayAlert("Wallet Error", message, "OK");
                }
            });
        }

        private void UpdateUI()
        {
            MainThread.BeginInvokeOnMainThread(() => {
                AddressLabel.Text = wallet.MainAddressBase58 ?? "Not Connected";
                BalanceLabel.Text = $"{wallet.SolBalance:N2} SOL";
                
                TokensCollectionView.ItemsSource = null;
                TokensCollectionView.ItemsSource = wallet.TokenBalances;
                
                CopyBtn.IsVisible = wallet.MainAddressBase58 != null;
                
                if (wallet.MainAddress != null)
                {
                    ConnectBtn.Text = "Reconnect / Refresh";
                    ConnectBtn.BackgroundColor = Color.FromArgb("#2a2a2a");
                    ConnectBtn.TextColor = Colors.White;
                }
            });
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
            _selectedToken = null;
            SendForm.IsVisible = true;
            RecipientEntry.IsVisible = true;
            AmountEntry.Placeholder = "Amount (SOL)";
            TokensCollectionView.SelectedItem = null;
        }

        private void TokensCollectionView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.FirstOrDefault() is TokenBalance token)
            {
                if (wallet.MainAddress == null) return;

                _selectedToken = token;
                _isSwapping = false; // Row tap = Send
                SendForm.IsVisible = true;
                RecipientEntry.IsVisible = true;
                AmountEntry.Placeholder = $"Amount ({token.Symbol})";
                StatusLabel.Text = $"Status: Selected {token.Symbol} for Send";
                
                TokensCollectionView.SelectedItem = null; 
            }
        }

        private void TokenSwapClicked(object sender, EventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is TokenBalance token)
            {
                if (wallet.MainAddress == null)
                {
                    DisplayAlert("Wallet", "Please connect your wallet first", "OK");
                    return;
                }

                _selectedToken = token;
                _isSwapping = true;
                SendForm.IsVisible = true;
                RecipientEntry.IsVisible = false; // Recipient not needed for Swap
                AmountEntry.Placeholder = $"Amount ({token.Symbol}) to Swap for SOL";
                StatusLabel.Text = $"Status: Selected {token.Symbol} for Swap";
            }
        }

        private void SwapClicked(object sender, EventArgs e)
        {
            if (wallet.MainAddress == null)
            {
                DisplayAlert("Wallet", "Please connect your wallet first", "OK");
                return;
            }
            DisplayAlert("Select Token", "Tap the 'Swap' button next to a token in the list.", "OK");
        }

        private async void ConfirmSendClicked(object sender, EventArgs e)
        {
            if (!double.TryParse(AmountEntry.Text, out double amount)) return;

            SendForm.IsVisible = false;

            if (_isSwapping)
            {
                if (_selectedToken != null)
                {
                    StatusLabel.Text = $"Status: Swapping {amount} {_selectedToken.Symbol} for SOL...";
                    ulong tokenAmount = (ulong)(amount * Math.Pow(10, _selectedToken.Decimals));
                    await wallet.PerformJupiterSwap(_selectedToken.Mint, "So11111111111111111111111111111111111111112", tokenAmount);
                }
            }
            else
            {
                var recipient = RecipientEntry.Text;
                if (string.IsNullOrEmpty(recipient)) 
                {
                    await DisplayAlert("Error", "Recipient address is required for sending", "OK");
                    return;
                }

                if (_selectedToken != null)
                {
                    StatusLabel.Text = $"Status: Sending {amount} {_selectedToken.Symbol}...";
                    ulong tokenAmount = (ulong)(amount * Math.Pow(10, _selectedToken.Decimals));
                    await wallet.SendToken(recipient, tokenAmount, _selectedToken.Mint, _selectedToken.Decimals);
                }
                else
                {
                    StatusLabel.Text = $"Status: Sending {amount} SOL...";
                    ulong lamports = (ulong)(amount * 1000000000);
                    await wallet.SendSol(recipient, lamports);
                }
            }
        }

        private void CancelSendClicked(object sender, EventArgs e)
        {
            SendForm.IsVisible = false;
        }
    }
}
