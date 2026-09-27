# Implementation notes

This file records how the build differs from [BUILD_PLAN.md](BUILD_PLAN.md) and the acceptance status. The plan itself is unchanged.

## Using it

- Build: run `build.cmd`. It produces `dist\RotkAliveOverlay.exe` and `dist\checks.exe` using the `csc.exe` from .NET Framework 4.x that ships with Windows.
- Start: double-click `dist\RotkAliveOverlay.exe`. A second launch exits immediately.
- Stop: tray icon, then Exit, or Ctrl+Alt+Q.
- Show or hide: Ctrl+Alt+O, or double-click the tray icon.
- Move: Ctrl+Alt+P or the tray menu. Drag the window, then press Ctrl+Alt+P again. The position is saved to `dist\overlay.ini` as screen fractions.
- Settings: `dist\overlay.ini`, created with defaults on first run.
- Diagnostics: `dist\overlay.log`.
- Checks: `dist\checks.exe` runs the fixture checks. `dist\checks.exe replay [LogsDir]` prints the current alive list from the real files.

## Deviations from the plan

- **Move-mode hotkey.** The move-mode shortcut is Ctrl+Alt+P, not Ctrl+Alt+M. On this PC another program already holds Ctrl+Alt+M: `RegisterHotKey` failed, and `overlay.log` recorded it. If any shortcut is taken, the overlay logs it and the tray menu still works.
- **Maximum width.** The default `MaxWidthScale` is 0.16 of the screen height (about 230 px at 1440), not 0.11. At 0.11, common names such as `ClashRoyaleHogrider` and the `ALIVE n | TOP r name` header were cut off. Width is still derived from screen height only, so it does not depend on the game's resolution.
- **Opacity range.** Opacity is clamped to 0.3 to 0.99. WinForms only makes the window layered below 1.0, and click-through requires a layered window.
- **Replacement detection.** It compares the first 256 raw bytes directly instead of a hash of them. The effect is the same.
- **Unknown `MatchEndScreen.log` messages.** Any message that is neither `EVENT_START_MATCH` nor `HandleMatch...` is ignored and logged once per distinct text.

## Acceptance status (2026-09-27, desktop, game idle)

Verified on this PC:

- [x] `checks.exe`: 58 of 58 pass. The checks cover every section 10 line, plus a start arriving late from the other file, a read while a writer handle is open, the Italian decimal-comma locale, and staleness.
- [x] `checks.exe replay` against the live `C:\Games\ROTK\Logs` found run `1790534889` and the match started at 21:14:40, and listed 4 alive players with 0 unparsed lines.
- [x] Window extended style is `0x080900A8`: topmost, click-through, tool window, layered, and no-activate.
- [x] Move mode clears click-through and restores it afterwards. The saved position is `X=0.0059`, `Y=0.25`.
- [x] Idle cost is 0 ms of CPU over 20 s, a 34.7 MB working set, and 26.5 MB of private memory.
- [x] The stale state renders in grey with `STALE | last event 21:14:57` once the game has been quiet for more than 5 minutes.

These still need a real match and can only be checked in game by the user:

- [ ] Visible above the game in the current exclusive fullscreen mode at 1920x1440. If it is hidden or flickers, switch the game to Windowed Fullscreen (borderless). No resolution change is needed.
- [ ] After the game switches the desktop between 2560x1440 and 1920x1440, the overlay is in the same relative spot at the same relative size. Every switch writes a `display changed` line in `overlay.log`.
- [ ] Starting the overlay mid-match shows the current match only.
- [ ] A new client launch resets the list. Look for `current client run id` and `file replaced` lines in `overlay.log`.
- [ ] Borderless mode needed: not yet known.
