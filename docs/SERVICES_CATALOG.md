# Service Catalog & Logic Patterns

This document describes the core services and their responsibilities within the RbnSystemPro application. New services should follow these patterns.

## 1. Network Service Integration (`INetworkService`)
- **Purpose:** Handles network configuration, adapter management, and WiFi.
- **Key Methods:**
  - `FlushDnsAsync()`: Clears DNS cache via `ipconfig`.
  - `GetActiveConnectionStatusAsync()`: Finds the primary active interface (WiFi/LAN) and its name.
  - `GetAvailableWifiNetworksAsync()`: Lists visible SSIDs using `netsh wlan show networks`.
  - `ConnectToWifiAsync(ssid, password, auto)`: Generates an XML profile for WPA2-Personal and connects via `netsh`.
  - `GetCurrentWifiSsidAsync()`: Retrieves connected SSID.
  - `GetWifiPasswordAsync(ssid)`: Retrieves stored cleartext passwords (admin required).
- **Usage:** Injected into `NetworkViewModel`.

## 2. Cleaner Service Integration (`ICleanerService`)
- **Purpose:** Manages system cleanup (Browsers, Temps, Registry).
- **Key Methods:**
  - `GetInstalledBrowsersAsync()`: Scans installation paths for Chrome, Edge, Firefox, Brave, Opera.
  - `CleanBrowserAsync(browser)`: Clears cache/cookies/history (using file deletion).
  - `CleanTempFilesAsync()`: Deletes contents of `\AppData\Local\Temp` and `\Windows\Temp`.
- **Integration:** Logs actions to `ITimelineService` for user feedback.

## 3. Driver & Hardware Service Integration (`IEssentialDriverService` / `IGpuDriverService`)
- **Purpose:** Identifies hardware and installs necessary drivers.
- **Logic:**
  - Uses `winget` (Windows Package Manager) for driver installation (`--id`, `--silent`).
  - Detects hardware IDs (Vendor/Product ID) via WMI (`HardwareService`).
  - Provides a curated list of essential drivers (Chipset, Audio, LAN, WiFi).
- **Usage:** Injected into `DriversViewModel`.

## 4. Software Deployment Service (`ISoftwareService`)
- **Purpose:** Bulk installs user-selected applications.
- **Logic:**
  - Uses `winget` integration.
  - Supports silent installation modes.
  - Provides categorized software lists (Development, Browsers, Essentials).
- **Usage:** Injected into `ProgramsViewModel`.

## 5. Timeline & Logging Service (`ITimelineService`, `ILoggerService`)
- **Purpose:** Centralized logging.
- **Logic:**
  - `LoggerService` writes to `Logs\log.txt` (Technical).
  - `TimelineService` writes to `Logs\activity_log.json` (User-facing).
  - **Self-Healing:** Automatically creates `Logs` directory if missing.
  - **Cleanup:** Automatically deletes logs older than 10 days.
- **Workflow:** Every major user action (Connect Wifi, Clean System, Install Driver) MUST trigger an `AddEvent()` call to update the UI timeline.

## 6. BIOS & System Service (`IBiosService`, `ISystemService`)
- **Purpose:** Low-level system modifications.
- **Logic:**
  - `BiosService`: Accesses BIOS/Firmware info.
  - `SystemService`: Handles reboot, shutdown, activation status.

---
**Development Note:**
Do NOT duplicate these logics in ViewModels. Always create a new method in the appropriate Service or create a new Service if functionality is distinctly new.
