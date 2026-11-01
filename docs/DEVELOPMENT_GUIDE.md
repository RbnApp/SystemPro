# User Interface Patterns & Development Guide

This guide ensures consistency and usability across the RbnSystemPro application. Adhere to these principles when adding new UI components.

## 1. Design System
- **Library:** [WPF-UI](https://github.com/lepoco/wpfui) (Fluent Design System).
- **Icons:** Use `ui:SymbolIcon` (Segoe Fluent Icons).
- **Typography:** Segoe UI or System Default.
- **Theme:** Dark Mode by default.
- **Colors:** Use predefined theme brushes (e.g., `{DynamicResource TextFillColorPrimaryBrush}`, `{DynamicResource ControlFillColorSecondaryBrush}`).

## 2. Layout Patterns
- **Grid vs StackPanel:** Use `Grid` for complex layouts (Rows/Columns) and `StackPanel` for simple lists.
- **Page Structure (Standard):**
  - **Header (Title):** At the top with prominent `FontSize="32"` and description.
  - **Sticky Action Cards:** If relevant (e.g., Connected WiFi, Last Clean Status), place immediately below header in a dedicated Row/Grid.
  - **Content (Scrollable):** Wrap the main content in a `ScrollViewer`.
  - **Action Lists:** Use `ui:CardAction` for clickable list items (e.g., Available Networks, Installed Softwares).

## 3. Component Usage
- **Cards:** Use `ui:Card` for grouping related settings or information. Add `Padding="16"`.
- **Buttons through Commands:** Always bind `Command="{Binding ViewModel.MyCommand}"`. Do NOT use `Click="..."` unless for pure navigation/UI logic (e.g., Back Button).
  - Exception: Simple "Back" or "Close Modal" actions can be handled in code-behind if trivial.
- **Modals/Dialogs:** Use `Grid` visibility toggles with `BooleanToVisibilityConverter` for simple overlays OR proper `Dialog` service if complex.
  - Current pattern: `IsModalVisible` property in ViewModel controls the visibility of an overlay Grid in View.

## 4. MVVM & ViewModels
- **Base Class:** Inherit from `ObservableObject`.
- **Commands:** Use `DelegateCommand` or `RelayCommand`.
- **Async Commands:** Use `ExecuteAsync` pattern (defined in Base or ViewModel) to handle `IsBusy` state and show progress/success/error alerts.
- **Notifications:** Use `OnPropertyChanged(nameof(MyProp))` for dependent properties.

## 5. Adding a New Feature (Step-by-Step)
1. **Identify Requirement:** E.g., "Add Disk Cleanup".
2. **Service Interface:** Define `IDiskService` in `/Services`.
3.  **Implement Logic:** Create `DiskService.cs` handling actual cleanup.
4.  **Register:** Add `services.AddSingleton<IDiskService, DiskService>();` in `App.xaml.cs`.
5.  **ViewModel:** Create `DiskViewModel`. Inject `IDiskService, IAlertService, ITimelineService`.
6.  **Command:** Add `CleanDiskCommand` calling service. Log success to `_timeline`.
7.  **View:** Create `DiskPage.xaml`. Bind `DataContext` to ViewModel.
8.  **Navigation:** Add entry in `MainWindow` or `ShellViewModel` to navigate to `DiskPage`.

---
**Note on Logging:**
- Always inject `ITimelineService` into your ViewModel.
- Call `_timeline.AddEvent("Category", "Action Description", TimelineEventType.Success/Error)` upon completion of major user actions.
