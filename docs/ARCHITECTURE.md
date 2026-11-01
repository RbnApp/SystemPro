# RbnSystemPro Architecture & System Overview

## 1. Project Purpose
RbnSystemPro is a comprehensive system optimization and deployment utility designed for Windows post-installation tasks. It streamlines hardware analysis, driver installation, software deployment, system cleaning, and network optimization into a single, modern interface.

## 2. Technology Stack & Core Libaries
- **Framework:** .NET 10 WPF (Windows Presentation Foundation)
- **UI System:** [WPF-UI](https://github.com/lepoco/wpfui) (Fluent Design System)
- **Language:** C#
- **Dependency Injection:** `Microsoft.Extensions.DependencyInjection`
- **Shell Integration:** PowerShell (for advanced system tasks) & `netsh` (for network) & `winget` (for software)

## 3. Core Architecture Principles
The project strictly follows **MVVM (Model-View-ViewModel)** pattern with **Dependency Injection (DI)**.

### 3.1 MVVM Pattern
- **Models:** Pure data objects (e.g., `HardwareInfo`, `EssentialDriverModel`). Located in `/Models`.
- **ViewModels:** Handle business logic, state management, and command execution. Located in `/ViewModels`.
  - Must inherit from `ObservableObject` (in `/Helpers`) for `INotifyPropertyChanged`.
  - Use `DelegateCommand` (or `RelayCommand`) for handling UI actions.
- **Views:** XAML definitions only. Code-behind (`.xaml.cs`) should strictly handle UI-specific logic (e.g., Navigation, specific event routing) and avoid business logic.

### 3.2 Dependency Injection (DI)
- All services are defined by interfaces (e.g., `INetworkService`) and implemented in concrete classes (e.g., `NetworkService`).
- Services are registered in `App.xaml.cs` (or `Program.cs`) and injected into ViewModels via constructor.
- **Benefit:** Allows easy swapping of implementations and creates a testable, modular codebase.

### 3.3 Logging & Activity System
The system employs a dual-logging strategy to separate technical diagnostics from user visibility.
- **ILoggerService (Technical Log):**
  - **Purpose:** Records low-level errors, exceptions, and debug info.
  - **Storage:** Text file in `/Logs/log.txt`.
  - **Usage:** `await _logger.LogErrorAsync("Failed to install...", ex);`
- **ITimelineService (User Activity Log):**
  - **Purpose:** Displays high-level actions (e.g., "Driver Installed", "DNS Updated") to the user on the Dashboard.
  - **Storage:** JSON file in `/Logs/activity_log.json`.
  - **UI Representation:** "Timeline" or "Activities" section.
  - **Usage:** `_timeline.AddEvent("Network", "DNS Set to Google", TimelineEventType.Success);`

## 4. Key Directory Structure
- **/Services:** Core logic implementations (Network, Cleaner, Drivers, etc.). See `SERVICES_CATALOG.md` for details.
- **/ViewModels:** State and commands for screens.
- **/Views:** UI layouts (Dashboards, Pages).
- **/Helpers:** Utility classes (`GlobalConfig`, `ValueConverters`).
- **/Logs:** Runtime logs (auto-generated).
- **/docs:** Documentation.

## 5. Development Workflow for AI Agents & Developers
When adding a new feature (e.g., "Disk Cleanup"):
1.  **Define Interface:** Create `IDiskService.cs` in `/Services`.
2.  **Implement Service:** Create `DiskService.cs` executing logic (via PowerShell/IO).
3.  **Register:** Add to DI container in `App.xaml.cs`.
4.  **ViewModel:** Create/Update `CleanerViewModel.cs` injecting `IDiskService`.
5.  **View:** Update XAML to bind to commands/properties.
6.  **Log:** Use `ITimelineService` for success/fail user notifications.

---
**References:**
- See `SERVICES_CATALOG.md` for detailed service documentation.
- UI patterns follow the existing pages under `Views/` (WPF-UI `ui:` controls); a separate UI guideline document does not exist.
