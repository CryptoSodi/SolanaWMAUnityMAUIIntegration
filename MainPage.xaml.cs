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
            Console.WriteLine("Wallet Event: " + message);

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                StatusLabel.Text = "Status: " + message;
                await DisplayAlert("Wallet Response", message, "OK");
            });
        }

        private async void ConnectWalletClicked(object sender, EventArgs e)
        {
            StatusLabel.Text = "Status: Connecting...";
            await wallet.ConnectWallet();
        }

        private async void SignTransactionClicked(object sender, EventArgs e)
        {
            StatusLabel.Text = "Status: Signing Transaction...";
            await wallet.SignTestTransaction();
        }

        private async void SignMessageClicked(object sender, EventArgs e)
        {
            StatusLabel.Text = "Status: Signing Message...";
            await wallet.SignTestMessage();
        }
    }
}
