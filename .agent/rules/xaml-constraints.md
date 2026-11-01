# WPF & XAML Constraints

## ProgressBar
- **NO CornerRadius**: Standard WPF `ProgressBar` does NOT support the `CornerRadius` property. Do not use it as it will cause a `XamlParseException` or build error (MC3072).
- **Mode=OneWay**: When binding the `Value` property to a read-only property (like a calculated percentage), always explicitly set `Mode=OneWay`. Otherwise, WPF may attempt a `TwoWay` binding which fails on read-only properties.

## Wpf.Ui (Lepo.co) Specifics
- **ui:Tag**: This control might not be available in the current project version. Use `Border` with `CornerRadius` and a `TextBlock` inside to create custom tags.
- **Symbols**: Ensure `ui:SymbolIcon` uses common symbols like `Pulse24`, `HardDrive24`, `ArrowLeft24`, etc.
