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
        void OnWalletConnected(string url)
        {
            Console.WriteLine("Wallet connected: " + url);

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await DisplayAlert("Wallet Response", url, "OK");
            });
        }
        async void ConnectWalletClicked(object sender, EventArgs e)
        {
            await wallet.ConnectWallet();
        }
    }
}
