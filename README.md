# Power Hunter - .NET MAUI App

Battery Usage Tracker — a cross-platform mobile application built with .NET MAUI, converted from the React/TypeScript web prototype.

## Architecture Overview

```
PowerHunter/
├── PowerHunter.sln
└── PowerHunter/
    ├── Models/           # SQLite entities + DTOs
    ├── Data/             # PowerHunterDatabase (SQLite CRUD layer)
    ├── Services/         # Battery monitoring, alert management, seed data
    ├── ViewModels/       # MVVM ViewModels (CommunityToolkit.Mvvm)
    ├── Views/            # XAML pages (StatsPage, MonitorPage, SettingsPage)
    ├── Converters/       # IValueConverter implementations
    ├── Resources/        # Colors, Styles, Icons, Splash
    └── Platforms/        # Android, iOS, MacCatalyst, Windows
```

## React → MAUI Mapping

| React Component | MAUI Equivalent | Description |
|---|---|---|
| `App.tsx` (Tab nav) | `AppShell.xaml` (TabBar) | Bottom tab navigation |
| `StatsView.tsx` | `StatsPage.xaml` + `StatsViewModel` | Battery analytics dashboard |
| `MonitorView.tsx` | `MonitorPage.xaml` + `MonitorViewModel` | Alert management |
| `SettingsView.tsx` | `SettingsPage.xaml` + `SettingsViewModel` | Preferences & about |
| `Switch.tsx` | MAUI `<Switch>` control | Toggle component |
| `recharts` LineChart | LiveCharts2 CartesianChart | Battery trend visualization |
| `recharts` PieChart | LiveCharts2 PieChart | Category distribution |
| Tailwind CSS colors | `Colors.xaml` resource dictionary | Design tokens |
| React `useState` | `[ObservableProperty]` | Reactive state |

## Tech Stack

| Component | Technology |
|---|---|
| Framework | .NET 8 MAUI |
| Language | C# 12 |
| Architecture | MVVM (CommunityToolkit.Mvvm) |
| Local Storage | SQLite (sqlite-net-pcl) |
| Charts | LiveCharts2 (SkiaSharp) |
| Notifications | Plugin.LocalNotification |
| UI Toolkit | CommunityToolkit.Maui |

## Data Models

### BatteryRecord
Periodic battery level snapshots. Indexed by `RecordedAt` for efficient time-range queries.

### AppUsageRecord
Per-app daily summaries sourced from the operating system. Prefers official per-app power stats when exposed, otherwise stores usage-activity share from system usage stats. Indexed by `Date` and `AppId` for daily/weekly aggregation.

### BatteryAlert
User-configured threshold alerts with enable/disable and last-triggered tracking.

### UserSettings
Single-row settings table (dark mode, notifications, guardian mode).

## Features

### Stats Page (Dashboard)
- Real-time battery percentage and session count
- Official system battery level shown without app-side correction
- Day/Week timeframe toggle
- App selector carousel (All Apps, PUBG, TikTok, Chrome, YouTube, Instagram, Maps)
- Battery trend line chart (LiveCharts2)
- Category distribution donut chart with legend

### Apps Page
- Official system power stats are preferred when available
- Fallback to UsageStats-based activity share when the platform does not expose per-app power
- Periodic auto refresh while the page is visible

### Monitor Page
- Battery Guardian toggle (smart high-usage alerts)
- Background anomaly detection backed by battery drop plus background activity signals
- Alert CRUD (create via prompt dialogs, toggle, delete)
- Empty state when no alerts configured
- Battery Saving Tips accordion (3 tips)

### Settings Page
- Dark Mode toggle (applies `AppTheme.Dark`)
- Night Auto Power Saving toggle reduces overnight monitoring frequency from 22:00 to 07:00
- Notifications toggle
- Version & Build info
- Startup + manual data archiving for records older than 30 days
- Privacy notice (data stays on device)

## Platform-Specific Notes

### Android
- `BATTERY_STATS` — read battery statistics
- `PACKAGE_USAGE_STATS` — app-level usage data (requires manual user grant via Settings > Special access > Usage access)
- `POST_NOTIFICATIONS` — Android 13+ notification permission (requested at launch)
- `FOREGROUND_SERVICE` — background battery monitoring

### iOS
- `UIDevice.BatteryMonitoringEnabled` — enabled in `AppDelegate`
- Per-app battery breakdown is **not** accessible to third-party apps (iOS limitation)
- Background modes: `fetch` + `processing` for periodic updates
- Notification permission prompt via `Plugin.LocalNotification`

### Data Privacy
- All data stored in app-local SQLite database
- **Zero network calls** — no telemetry, no analytics, no server uploads
- Users control all data via the Archive function in Settings

## Build & Run

### Prerequisites
- .NET 8 SDK
- Visual Studio 2022 (17.8+) with MAUI workload
- Android SDK 21+ / Xcode 15+ (for respective platforms)

### Commands
```bash
# Restore packages
dotnet restore

# Build for Android
dotnet build -f net8.0-android

# Build for iOS (macOS only)
dotnet build -f net8.0-ios

# Build for Windows
dotnet build -f net8.0-windows10.0.19041.0

# Run on Android emulator
dotnet build -f net8.0-android -t:Run

# Run on iOS simulator (macOS only)
dotnet build -f net8.0-ios -t:Run
```

## Technical Challenges & Mitigations

### 1. Platform Permission Restrictions
**Problem:** Android requires `PACKAGE_USAGE_STATS` (user must manually grant), iOS doesn't expose per-app battery data at all.
**Mitigation:** App works with available data. On iOS, tracks foreground time internally. SeedDataService provides demonstration data. Platform limitations documented in-app.

### 2. Data Accuracy
**Problem:** Third-party apps cannot always access per-app official battery attribution on every Android build.
**Mitigation:** Uses official `Microsoft.Maui.Devices.Battery` API for battery level and prefers official per-app power signals when the platform exposes them. Otherwise, it clearly falls back to `UsageStatsManager` activity share with periodic refresh and source labeling in the UI.

### 3. Data Volume & Performance
**Problem:** Continuous monitoring generates growing data volumes.
**Mitigation:**
- SQLite indexes on `RecordedAt` and `Date` columns
- Daily JSON partitions with an index manifest under app data storage
- `ArchiveOldRecordsAsync()` removes granular data older than 30 days
- Aggregation queries (trend, category) operate on pre-indexed date ranges
- `GetDatabaseSize()` available for monitoring

### 4. User Privacy
**Problem:** Users may distrust battery monitoring apps.
**Mitigation:**
- Explicit privacy notice in Settings page
- Zero network permissions — app cannot upload anything
- All data in local SQLite only
- Archive function gives users control over stored data
