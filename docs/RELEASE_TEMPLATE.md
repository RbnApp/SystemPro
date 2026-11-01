# Release 0.1.3: Initial Launch & Core Features 🚀

We are excited to announce the first major release of **RbnSystemPro**! This tool brings powerful system optimization, driver management, and network utilities to your fingertips in a beautiful, modern interface.

### ✨ What's New

#### 🖥️ Windows Deployment
- **Debloat Script:** Runs Raphire/Win11Debloat with administrator rights; the source address is shown before running.
- **System Check:** Shows TPM, Secure Boot, UAC and Defender/firewall status.

#### 🎮 Advanced Driver & Hardware
- **GPU Detection:** Detects the installed GPU driver version; NVIDIA/AMD/Intel drivers are manual downloads from the vendor page.
- **Essential Drivers:** Detects missing Audio, LAN and Chipset drivers and verifies the result after installation.
- **Monitoring:** CPU/RAM/GPU usage on the performance page; temperatures are shown only when a sensor reports them (otherwise "-").

#### 🚀 PC Performance & Health
- **Deep Cleaner:** Removes temp files, browser caches (Chrome, Edge, Opera), and obsolete registry keys.
- **Latency Tuner:** Optimizes TCP/IP for gaming and streaming.

#### 🛡️ Windows Update & Security
- **Security Dashboard:** Check Activation, Defender, and Firewall status instantly.
- **Update Status:** Shows the last successful update date and opens Windows Update settings.

#### � Network Optimizer
- **Sticky WiFi Connection:** Always see your active connection at the top.
- **DNS Switcher:** Toggle between Google DNS (8.8.8.8) and Cloudflare DNS (1.1.1.1).
- **Network Reset:** Flush DNS, reset TCP/IP stack, and optimize latency.

#### 📦 Software Deployment
- **Bulk Install:** Deploy essential apps (Chrome, VS Code, Discord, etc.) silently using `winget`.
- **Categorized Lists:** Easily find Development tools, Browsers, and Essentials.

### 🛠️ Architecture & Dev Changes
- **MVVM Pattern:** Strict adherence to MVVM with Dependency Injection.
- **Service Layer:** Modular services for Network, Cleaner, and Drivers.
- **Logging System:** Dual-logging system (Technical Logs + User Activity Timeline).

### 🐛 Bug Fixes
- Fixed an issue where WiFi password input validation was too strict (< 8 chars).
- Resolved duplicate WiFi network listing when connected.
- Fixed layout issues on the Network Page causing scrollbar to hide.
- Corrected SSID parsing logic for Turkish system locales.

### 📦 Installation
1.  Download `RbnSystemPro_Setup_0.1.3.exe`.
2.  Run `RbnSystemPro_Setup_0.1.3.exe` as Administrator.
3.  Follow the installation wizard.
4.  Enjoy a faster, cleaner PC!
