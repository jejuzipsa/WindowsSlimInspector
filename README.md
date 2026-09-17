# WindowsSlimInspector

Portable Windows utility with two focused functions:

1. **Windows Slim** — inspect and selectively disable/restore non-essential Windows features such as ads/recommendations, Bing web search, Search Highlights, Widgets/News, Copilot, Phone Link, Teams/Chat, Tips/Welcome Experience, Consumer Experience, and advertising/personalization settings.
2. **System Inspector** — perform a read-only PC/background inspection and export a detailed TXT report for later analysis.

## Safety scope

This project intentionally does **not** manage or disable:

- OneDrive
- Microsoft Defender
- Windows Update
- Microsoft Store infrastructure
- Windows Search service/indexer itself
- Printing / Print Spooler
- Bluetooth
- LAN / SMB / network discovery
- TV / local media connectivity
- Core networking, audio, device, installer, or security services

The goal is conservative cleanup, not aggressive debloating.

## Portable design

The app is intended to run without installation or resident background services. Logs are written beside the executable under a `Logs` folder.

## Planned UI

- Select All checkbox
- Per-feature checkbox and live status
- Disable selected
- Restore selected
- Refresh status
- Full PC inspection button
- Automatic TXT log export

## Status

Initial project scaffolding in progress.
