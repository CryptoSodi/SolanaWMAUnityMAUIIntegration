# 🚀 Solana WMA SDK for .NET MAUI

The **first-ever Solana Mobile Wallet Adapter (WMA) SDK for .NET MAUI**.

This project brings native **Solana Mobile Stack** support to **C# MAUI developers**, enabling direct communication with mobile wallets like **Phantom** and **Solflare** on Android — without relying on Unity or heavy workarounds.

---

## ✨ Features

- ✅ Native **Solana Mobile Wallet Adapter (WMA)** integration for .NET MAUI  
- ✅ Direct wallet connection (Phantom, Solflare, etc.)  
- ✅ No Unity dependency — pure MAUI implementation  
- ✅ Optimized for performance and low overhead  
- ✅ Clean C#-friendly API design  
- ✅ Android-first implementation  

---

## 🧠 Why This Matters

Until now, developers building Solana mobile apps in C# had limited options:
- Use Unity SDK (heavy and unnecessary for non-game apps)
- Build custom native bridges (complex and time-consuming)

This SDK solves that by:
- Providing a **native MAUI bridge**
- Enabling **true mobile Web3 apps in C#**
- Opening Solana to the **.NET ecosystem**

---

## 📦 Installation

### 1. Clone the Repository

```bash
git clone https://github.com/CryptoSodi/SolanaWMAUnityMAUIIntegration.git
cd SolanaWMAUnityMAUIIntegration
```

---

### 2. Add to Your MAUI Project

- Include the binding / wrapper project into your solution
- Reference it in your MAUI app

---

### 3. Configure Android Manifest

Make sure your `AndroidManifest.xml` includes:

```xml
<uses-permission android:name="android.permission.INTERNET" />
<uses-permission android:name="android.permission.ACCESS_NETWORK_STATE" />
```

---

### 4. Minimum Requirements

- .NET MAUI  
- Android API Level 24+  
- Physical Android device (required for wallet interaction)

---

## 🔌 Usage

### Initialize Wallet Connection

```csharp
var walletClient = new MobileWalletAdapterClient();
await walletClient.ConnectAsync();
```

---

### Request Wallet Address

```csharp
var account = await walletClient.GetAccountAsync();
Console.WriteLine($"Wallet Address: {account.PublicKey}");
```

---

### Sign Transaction

```csharp
var signedTx = await walletClient.SignTransactionAsync(transactionBytes);
```

---

## 🧩 Architecture Overview

```
MAUI App (C#)
     ↓
WMA Bridge Layer (This SDK)
     ↓
Android Native Layer (Kotlin/Java Interop)
     ↓
Solana Mobile Wallet Adapter (Socket-based protocol)
     ↓
Wallet Apps (Phantom / Solflare)
```

---

## ⚙️ How It Works

- Implements the **Solana Mobile Wallet Adapter protocol**
- Uses **local socket communication** between app and wallet
- Handles:
  - Session creation
  - Authorization
  - Transaction signing
  - Message signing

---

## 📱 Supported Wallets

- Phantom (Mobile)  
- Solflare (Mobile)  
- Any wallet supporting **Solana Mobile Stack**

---

## 🚧 Current Status

- ✅ Core connection flow implemented  
- ✅ Wallet authorization working  
- ✅ Transaction signing supported  
- ⚠️ iOS support — *planned*  
- ⚠️ Advanced session management — *in progress*  

---

## 🛣️ Roadmap

- [ ] iOS support (if/when Solana Mobile expands)  
- [ ] NuGet package release  
- [ ] Simplified high-level API  
- [ ] Sample production-ready app  
- [ ] Deep link fallback support  

---

## 🤝 Contributing

Contributions are welcome!

1. Fork the repo  
2. Create a feature branch  
3. Submit a PR  

---

## 📄 License

MIT License — feel free to use in commercial and personal projects.

---

## 🙌 Acknowledgements

- Solana Mobile team for the WMA protocol  
- Unity SDK reference implementation  
- MAUI community  

---

## 🌍 Vision

This project aims to make **Solana mobile development accessible to every .NET developer**.

No Unity.  
No hacks.  
Just clean, native Web3 in C#.

---

## ⭐ Support

If you find this useful:

- ⭐ Star the repo  
- 🐛 Report issues  
- 📢 Share with the community  
