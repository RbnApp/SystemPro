# RbnSystemPro Design Standards

## Page Layout
- **Gutter/Margin**: All major pages MUST have a consistent side margin of `28px` or `40px`. We are currently using **28px** as the standard for card-based pages.
- **Top Spacing**: Pages should start with a `12px` to `20px` top margin to separate from the navigation bar.
- **Header Cards**: 
    - Use a Gradient background (`NvidiaGradient`, `AmdGradient`, etc., or a neutral dark gradient).
    - `CornerRadius="12"`.
    - `Padding="28,24"`.
    - `Margin="28,12,28,20"`.

## Empty States
- **Icon**: Use `Opacity="0.2"` to `0.4` for large icons.
- **Title**: Use `TextFillColorSecondaryBrush` or `TextFillColorPrimaryBrush` with `Opacity="0.8"`. Avoid very low opacity (like 0.1) as it's unreadable.
- **Subtitle**: Use `TextFillColorSecondaryBrush` with `Opacity="0.6"`.

## Timeline & Logs
- **Connector Line**: `Opacity="0.2"` with `ControlStrokeColorDefaultBrush`.
- **Cards**: Consistent with the application's card style.

## Dashboard Overview Section
- Use for "Action History" or "Summary" rather than fake health checks.
- Always ensure "View Details" leads to the Activity Timeline.
