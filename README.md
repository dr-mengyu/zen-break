# Zen Break

A lightweight, non-intrusive Windows system tray utility built with **.NET 8** and **Avalonia UI** that gently reminds you to stand up, stretch, and hydrate during long coding or writing sessions.

---

## ✨ Features

- **🪑 Continuous Sitting-Time Tracker**:
  - Tracks both your **next reminder countdown** and your **total continuous sitting time** separately.
  - Clicking **Later (5m)**, **Skip**, or changing the reminder interval only resets the next reminder countdown—your continuous working timer keeps counting until you actually take a break.
- **🔔 Non-Intrusive, Always-on-Top Overlay**:
  - Appears at the **bottom-right corner** of your primary screen without stealing keyboard focus (`ShowActivated="False"` + Win32 `SWP_NOACTIVATE`), so it never interrupts your typing in an IDE or document.
  - Configured as a floating Win32 overlay that remains visible across **all Windows Virtual Desktops**.
- **🥛 Break Duration Tracking**:
  - Tracks how long you have been away on break and resets your sitting timer when you click **"I'm Back!"**.
- **🔒 Smart Session & Power Awareness**:
  - **Short Lock / Sleep (`< 1 min`)**: Automatically pauses both timers and resumes seamlessly when you unlock.
  - **Extended Lock / Sleep (`>= 1 min`)**: Automatically transitions into **On Break** mode (backdated to when you locked the screen) so you can see how long you rested when you return.
- **⚡ Keep Windows Unlocked**:
  - Optional system tray toggle (backed by Win32 `SetThreadExecutionState`) to prevent Windows from turning off the display or locking due to idle timeout.
- **📦 Portable Single-File Executable**:
  - Self-contained `.exe` with zero installation required. Settings (`ReminderInterval`, `AutoStart`, `KeepUnlocked`) are persisted cleanly in `HKCU\Software\ZenBreak`.

---

## 🚀 Getting Started

### Download Prebuilt Release
Download the latest `ZenBreak.exe` from the [Releases](../../releases) page and run it directly. It will sit quietly in your Windows System Tray.

### Build from Source

#### Prerequisites
- Windows 10 or Windows 11 (x64)
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

#### Build & Run
```powershell
# Clone the repository
git clone https://github.com/dr-mengyu/zen-break.git
cd zen-break

# Build in Release mode
dotnet build -c Release

# Publish a self-contained single-file executable
dotnet publish src/ZenBreak/ZenBreak.csproj -c Release -o publish
```
The standalone executable will be generated at `publish/ZenBreak.exe`.

---

## 🧭 How It Works (State Machine)

| State | Tray Indicator | Behavior |
| :--- | :--- | :--- |
| **Working** | `💻 Next reminder in MM:SS` | Counts down toward the selected interval (`30`, `45`, or `60` minutes) while tracking continuous sitting time (`🪑 Working: ...`). |
| **Reminding** | `⏰ Time for a break!` | Shows the non-intrusive bottom-right popup with three options: **Take Break**, **Later (5m)**, or **Skip**. Locking the screen immediately starts a break. |
| **Delayed** | `⏳ Remind in MM:SS` | Hides the reminder for 5 minutes while continuous sitting time keeps accumulating. |
| **Paused** | `⏸️ Paused at MM:SS` | Triggered when the screen is locked or PC sleeps. Unlocks within `< 1 min` resume where you left off; locks `>= 1 min` transition to **On Break**. |
| **On Break** | `🥛 On Break (...)` | Displays the centered Break Window counting up your rest duration until you click **I'm Back!**, which resets both timers to `0`. |

---

## 📄 License

Licensed under the [MIT License](LICENSE).
