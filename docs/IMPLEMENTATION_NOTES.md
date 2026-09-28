# Implementation notes

This file records how the build differs from [BUILD_PLAN.md](BUILD_PLAN.md), the rank-icon redesign that followed it, and the acceptance status. The plan itself is unchanged.

## Using it

- Build: run `build.cmd`. It uses the `csc.exe` from .NET Framework 4.x that ships with Windows and produces:
  - `dist\TottiGol.exe`, the overlay (a single file with the emblems and font embedded)
  - `dist\checks.exe`, the fixture checks
  - `dist\IconExtract.exe`, the one-time emblem extractor
- Start: double-click `dist\TottiGol.exe`. A second launch exits immediately.
- Stop: tray icon, then Exit, or Ctrl+Alt+Q.
- Show or hide: Ctrl+Alt+O, or double-click the tray icon.
- Move: Ctrl+Alt+P or the tray menu. Drag the panel, then press Ctrl+Alt+P again. The position is saved to `dist\overlay.ini` as fractions of the current screen.
- Settings: `dist\overlay.ini`. It is created with defaults on first run and rewritten on each start, so keys from older versions are dropped.
- Diagnostics: `dist\overlay.log`.
- Checks: `dist\checks.exe` runs the fixture checks. `dist\checks.exe replay [LogsDir]` prints the current alive list from the real files.
- Preview: `dist\TottiGol.exe --preview out.png [screenHeight] [list|stale|empty|waiting|nologs]` draws the panel with sample players and exits.

## Ranks

- The log's `[rank:T.D]` is tier `T` and division `D`. The tier names come from the game's text table (`Locale\en_us_data.dat`):

  | T | Tier |
  |---|------|
  | 0 | Placement (no division) |
  | 1 | Bronze |
  | 2 | Silver |
  | 3 | Gold |
  | 4 | Platinum |
  | 5 | Diamond |
  | 6 | Master |
  | 7 | Royalty |

- **Division 1 is the best.** The user confirmed that 5.1 is Diamond 1. So 7.1 is Royalty One, which the game names `ROYALTY ONE` and which gets its own emblem.
- A tier above 7 is drawn with the Royalty emblem and logged once.
- The panel shows no rank numbers. The emblem shows the tier, the name is drawn in the tier color, and the right-hand number is kills.
- **Sort order:** tier (highest first), then division (1 first; a missing division sorts after 5), then kills this match (most first), then the order the player first appeared. The rank is still the one from the first line that mentioned the player.

## Kills

- Each kill line after the latest `EVENT_START_MATCH` adds one kill to its killer.
  - Kills credited after the killer's own death still count, although the player stays off the list.
  - Assists never count.
- Counts are rebuilt from the log on every change, so starting the overlay mid-match still shows full counts. The next match start resets them.

## Emblems and font (bundled)

- **Game art.** The 8 tier emblems are the game's own. They come from `C:\Games\ROTK\Resources\Assets\ui_x64_2.pack2` (client `h1z1-1.0.326.439939`) and are listed by entry hash in `tools\rank-icons.txt`:
  - Bronze, Silver, Gold, Platinum (cyan), Diamond (purple), Master (green), Royalty (crown), Royalty One (crown with gems).
  - They are Daybreak's artwork, bundled into the exe at the user's request.
- **Placement** has no emblem in the UI archives. `assets\ranks\placement.png` is a plain grey medallion drawn by `IconExtract placement`.
- **Re-extracting.** Run `dist\IconExtract.exe dump work\ui` to get every UI texture with numbered contact sheets. Then run `dist\IconExtract.exe export tools\rank-icons.txt assets\ranks` and rebuild. `IconExtract` only reads the archives.
- **Font.** Oswald Bold 2.002 by Vernon Adams, copied from the game's `UI\Resource\Fonts`, is embedded under the SIL Open Font License (`assets\fonts\OFL.txt`). Names containing characters Oswald lacks are drawn in Segoe UI Bold.

## Settings (`overlay.ini`)

| Key | Default | Meaning |
|-----|---------|---------|
| `LogDir` | empty | Empty means `installation.root` from the launcher config, else `C:\Games\ROTK` |
| `Anchor`, `X`, `Y` | `TopLeft`, 0.006, 0.25 | Screen corner and screen fractions |
| `FontScale` | 0.011 | Base text size as a fraction of screen height |
| `PanelWidthScale` | 0.20 | Panel width as a fraction of screen height (about 290 px at 1440) |
| `MaxRows` | 15 | Rows shown before `+N MORE` |
| `UppercaseNames` | 1 | Names in capitals like the game UI |
| `StaleMinutes` | 5 | Grey the panel after this long without new log lines |
| `PanelOpacity` | 0.88 | Background only; emblems and text are always solid |

`HighRank`, `MidRank`, `MaxWidthScale` and `Opacity` were removed.

## Deviations from the plan

- **Move-mode hotkey.** The move-mode shortcut is Ctrl+Alt+P, not Ctrl+Alt+M. On this PC another program already holds Ctrl+Alt+M: `RegisterHotKey` failed, and `overlay.log` recorded it. If any shortcut is taken, the overlay logs it and the tray menu still works.
- **Drawing.** The window is now drawn with per-pixel alpha through `UpdateLayeredWindow`, instead of `Form.Opacity`. It keeps the same styles: topmost, click-through, tool window, layered, no-activate.
- **Replacement detection.** It compares the first 256 raw bytes directly instead of a hash of them. The effect is the same.
- **Unknown `MatchEndScreen.log` messages.** Any message that is neither `EVENT_START_MATCH` nor `HandleMatch...` is ignored and logged once per distinct text.
- **Trailing flags on kill-feed lines.** Live logs on 2026-09-28 end some kill lines with `KILLERFRIEND` or `ASSISTFRIEND`, sometimes after `HEADSHOT`. The parser now accepts any run of all-caps words after the last player. Before that change, those lines were skipped, which left their victims on the list.

## Acceptance status

Verified on this PC (2026-09-28, during a live match):

- [x] `checks.exe`: 82 of 82 pass (the original 58, updated for the new sort order, plus ranks, kills and friend flags).
- [x] `checks.exe replay` on the live match (run `1790595332`) listed 20 alive, with 0 unparsed lines and 16 posthumous credits.
- [x] Live panel: real emblems, tier colors and kill counts. The window style is `0x080900A8` and the desktop shows through the background.
- [x] Move mode clears click-through and restores it afterwards. The saved position uses the current screen's size.
- [x] Cost during the match: 62 ms of CPU over 30 s (0.2 % of one core), 27.5 MB of private memory. The GDI object count stayed flat at 29.
- [x] The game switched the desktop to 1920x1440 and back to 2560x1440. Both switches were logged, the panel re-anchored, and it picked up the next match start at 14:00:42.
- [x] Earlier session: the tracker is only visible when the game is in Windowed Fullscreen (borderless); exclusive fullscreen hides it.
