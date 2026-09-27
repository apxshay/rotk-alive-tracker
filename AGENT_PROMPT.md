# Planning task

You are taking over an existing request. Your job in this turn is to **plan** how to build the overlay. Do not implement it yet. Do not choose a stack by habit and start scaffolding. Produce a concrete build plan that decides the language and runtime, how the program stays lightweight, how it behaves as an overlay on this machine, and every caveat that can make the alive-list wrong, late, invisible, or disruptive.

The human who will use it is on Windows 10 (10.0.19045), PowerShell, display name Gabriel. The project directory is `C:\Users\Gabriel\Desktop\rotk-alive-overlay`. This file is the brief. There is no code yet.

Do not reopen the question of whether this should be built. The agreed product is a local overlay that reads log files the game already writes. It is in the same category as a Hearthstone deck tracker or Porofessor: it remembers public events the client has already printed. It is not a request to read game memory, inject a DLL, hook the client, sniff packets, or evade anti-cheat.

---

# What the overlay is

While a ROTK match is running (ROTK is a community launcher around a legitimate local copy of Z1 Battle Royale / H1Z1), show a small overlay of **players who have appeared at least once in the kill feed and are still alive**, with **the rank from the first line that introduced that player**.

Purpose: a player can see whether a high rank is still among the people the feed has revealed, and play accordingly. The game already shows each kill for a moment and then scrolls on. The overlay keeps the result.

Out of the overlay:

- Players who have never appeared in the kill feed. They are unknown, not “alive.”
- Players who have appeared only as the one who died, or who later died.
- Ping, weapon, headshot flags, damage reports, XP, gas timers, and vehicle-selection console spam. Those were in an on-screen console paste. They are not the product.
- A copy-paste box. That was an earlier fallback. The logs make it unnecessary.

The list updates by itself for the whole time the game is open. The user must not copy console text.

---

# Hard boundaries

- Read the log files below. Do not open `H1Z1.exe`. Do not use `ReadProcessMemory`, injection, DLL hooks, overlay hooks into the game’s DirectX device, packet capture, or anything whose purpose is to hide from anti-cheat.
- Anti-cheat flags opening or modifying the game process. It does not need to understand intent. Staying outside the process is the point.
- Do not add a feature that reveals positions, health, unshown inventories, or any fact that is not in these logs.
- Server rules can still forbid external overlays. Do not build detection evasion, process hiding, or a “stealth” mode. A normal visible window that reads text files is the whole design space.
- Do not raise `FileLogLevel` or turn on extra diagnostic logging. File logging is already on. Extra logging has a known FPS and disk cost on this client.

---

# Confirmed environment

ROTK install, from `%APPDATA%\ROTK Launcher\config.v1.backup.json`:

- `installation.root`: `C:\Games\ROTK`
- `installation.sourceRoot`: `C:\SteamLibrary\steamapps\common\H1Z1`
- Client build: `h1z1-1.0.326.439939`
- Launcher starts `C:\Games\ROTK\H1Z1.exe` and does not modify the Steam install.
- A diagnostics session recorded `playerName` as `Tottinho`. The user did **not** ask for a “this is you” marker. Include the local player in the alive list only when the kill feed has introduced them, same as anyone else.

The launcher command line already contains:

`Logging:ConsoleLogLevel=999 Logging:FileLogLevel=999 Logging:LocalLogLevel=999`

and also passes:

- `Logging:Directory=C:\Users\Gabriel\AppData\Roaming\ROTK Launcher\logs\992f2355-4302-4a02-ab9a-cad2f5dfa569`
- `Logging:LocalDirectory=C:\Users\Gabriel\AppData\Roaming\ROTK Launcher\logs\992f2355-4302-4a02-ab9a-cad2f5dfa569\local`

Those AppData directories were **not** the files that were verified. The verified, live game logs are:

- `C:\Games\ROTK\Logs\KillFeed.log`
- `C:\Games\ROTK\Logs\MatchEndScreen.log`

Prefer these. If you mention the AppData logging directory, treat it as unverified unless you check it yourself.

User display settings from `C:\Users\Gabriel\Desktop\ROTK-my-settings-backup.ini` (a backup, not necessarily the live ini):

- `Mode=Fullscreen`
- `FullscreenWidth=1920`
- `FullscreenHeight=1440`
- `MaximumFPS=500`

Fullscreen is a fact the overlay plan must confront. Exclusive fullscreen often hides ordinary topmost windows. The plan must say what happens in this display mode and what the user would have to change, if anything. Do not assume a borderless window.

---

# Data sources

## Kill feed

`C:\Games\ROTK\Logs\KillFeed.log`

Tab-separated. The message is the last field. Observed columns:

1. Date `YYYY-MM-DD`
2. Time `HH:MM:SS` (local, second resolution)
3. Host token. In one session this started as the literal `[HOST]` and later became the machine name `DESKTOP-0SJTF3G`. Do not hard-require either string.
4. An integer that stayed constant for a client run (`1790529693` in the inspected session) and changed across client runs.
5. A line sequence integer that increases.
6. A small integer. Kill-feed rows used `4`.
7. A larger increasing counter (examples `450385324`, `452465013`).
8. The message.

Real lines from the file (tabs between fields):

```
2026-09-27	19:24:21	[HOST]	1790529693	11721	4	450385324	theCITYisRED (3497557662702907908) [rank:5.3] [ping:3] KILLED Cyb (5619943147552144769) [rank:4.3] [ping:3]
2026-09-27	19:24:21	[HOST]	1790529693	11724	4	450385724	Screedy (10206652050108690840) [rank:5.4] [ping:3] KILLED Karspin (13263735598424155381) [rank:4.4] [ping:3] HEADSHOT
2026-09-27	19:24:26	[HOST]	1790529693	11740	4	450390373	Lekid (6758572958123711573) [rank:5.3] [ping:3] (ASSIST Horizon_ (14461580563613653473) [rank:5.3] [ping:3]) KILLED Hostyle (17945733976376050602) [rank:5.1] [ping:3]
2026-09-27	19:29:18	[HOST]	1790529693	13794	4	450682643	DEATH KingDady (12808836654268928371) [rank:5.3] [ping:3]
2026-09-27	19:58:40	DESKTOP-0SJTF3G	1790529693	23143	4	452444332	kapiszi (7258563311314439046) [rank:4.2] [ping:3] KILLED rmbx (16794321960974638935) [rank:5.3] [ping:3]
```

Message shapes that were actually observed:

- `{name} ({id}) [rank:{n.n}] [ping:{n}] KILLED {name} ({id}) [rank:{n.n}] [ping:{n}]`
- the same, plus a trailing `HEADSHOT`
- `{name} ({id}) [rank:{n.n}] [ping:{n}] (ASSIST {name} ({id}) [rank:{n.n}] [ping:{n}]) KILLED {name} ({id}) [rank:{n.n}] [ping:{n}]` with optional `HEADSHOT`
- `DEATH {name} ({id}) [rank:{n.n}] [ping:{n}]`

Only **one** `ASSIST` group was seen on a line. The plan must say what happens if a line ever contains more than one, or none. Do not invent a second-assist grammar that was not in the file, but do not crash if the line is unfamiliar. Skip unparseable lines and keep going.

Names are not a single token. Observed examples include spaces, punctuation, digits, and non-ASCII:

`AJ DOINKS`, `Kushaaa TTV`, `Bilbo Bortek`, `Marine LA PEN`, `2tapjamin Netanyahu`, `NOSKIN REAPER`, `Ruby_de_Puteaux`, `Mbappé`, `Rüdiger`, `käpy`, `shôyo`, `XD.`, `Riko1805.`, `.................`

The reliable identity is the integer inside the parentheses immediately before `[rank:`. Display names collide and are reused with different ids. Examples seen across matches in one console paste: `Tottinho`, `Lekid`, `alesso_ox`, `Pabloo`, `G.Nox`. Same letters, different id, different person for this list.

## Match start

`C:\Games\ROTK\Logs\MatchEndScreen.log`

Same tab-separated prefix. The level field was `2` instead of `4`. The message is again the last field.

Real lines:

```
2026-09-27	19:24:02	DESKTOP-0SJTF3G	1790529693	10954	2	450366918	HandleMatchStargetPacket
2026-09-27	19:24:02	DESKTOP-0SJTF3G	1790529693	10955	2	450366918	EVENT_START_MATCH
```

`EVENT_START_MATCH` is the reset signal. It is the file form of the on-screen “The Match has begun!” / `handleEventStartMatch`.

`HandleMatchStargetPacket` is spelled exactly that way in the file (not `Start`). It was written at the same timestamp as `EVENT_START_MATCH`, four times in the inspected session. Use `EVENT_START_MATCH` as the signal. Mention the sibling line only so a planner does not mistake it for a separate event.

In the inspected evening session, `EVENT_START_MATCH` occurred at:

- 19:24:02, first kill 19:24:21
- 19:28:13, first kill 19:28:30
- 19:34:19
- 19:57:46

The same file is mostly `HandleMatchPlayerSummaryPacket. InputFlags: 0x........` rows, one per kill-feed update. Those rows are **not** a better alive-list source. They do not contain names or ranks. Ignore them except as noise the parser must tolerate.

No match-end message was found in that file. Do not invent one. Clearing on the next `EVENT_START_MATCH` is the confirmed reset. A gap with no kills is not a reset: late circles go quiet.

`KillFeed.log` does not contain `EVENT_START_MATCH`. The two files have to be correlated. Both use the client’s local timestamps at one-second resolution, and in this session those timestamps agreed.

---

# Alive-list rules already agreed with the user

Apply these. Do not replace them with “last rank seen” or “everyone who ever got a kill, including the dead.”

- Process events in time order.
- On `EVENT_START_MATCH`, the alive set becomes empty. Kills from earlier matches in the same file must not leak in.
- On a kill line, the killer is alive. The assist, if present, is alive. The victim is dead and leaves the list.
- On `DEATH {name}`, that player is dead and leaves the list. A `DEATH` line can name someone who was never a killer or assist. They must not be added as alive.
- A player is shown only if they have been seen at least once **and** are not currently dead.
- Rank is the rank on the **first** line that introduced that player id in the current match. Later lines for the same id must not replace it. This was an explicit correction from the user. Observed drift: `SanchezZzTV` was `5.3` as a killer and later `5.2` as an assist. Show `5.3`.
- If a later line names a previously dead id as killer or assist, the text says they are alive again. Battle royale normally does not respawn. The plan must state the chosen behavior and why, in one sentence, against that fact. Do not silently drop the line and do not silently resurrect without noting it.
- Sort and presentation are part of the plan. The user wants a quick read of who is up and whether a high rank is among them. Rank is a number like `7.4`, `5.3`, `0.0`. Higher means stronger in the user’s usage. `0.0` showed up on many new or odd accounts. Do not translate the number into a medal name. The log does not define one.

---

# Proof the files are live

Do not design around a copy-paste buffer or a “flush at match end” assumption. These facts were checked on disk for 27 September 2026.

`MatchEndScreen.log`

- Filesystem creation time `19:24:04`.
- First `EVENT_START_MATCH` inside the file is timestamped `19:24:02`.
- First kill of that match is `19:24:21`.
- The start line was on disk before the fighting.

`KillFeed.log` for the same client run

- Filesystem creation time `19:24:22`.
- First kill line is timestamped `19:24:21`.
- More kills continue at `19:24:56`, then three further matches, through `19:59:01`.
- The file was created on the first kill, not at the end of the match.
- Last kill line `19:59:01`. Filesystem last-write time `19:59:01.296`.
- The launcher recorded client exit at `19:59:12` (`diagnostics\b5dd6fb6-5a27-4065-aba5-e4b59610e3cc\session.json`, `endedAt` `2026-09-27T17:59:12.802Z`, kind `exit`). The file was not touched at exit. It was touched at the kill.

Previous client run, used as a wider gap:

- Last kill line in that `KillFeed.log` is `19:16:06`.
- Filesystem mtime captured at the next launch was `19:16:06.604` (`mtimeMs` `1790529366604`).
- That client exited at `19:21:23` local (`9f3e52cc-d8fa-4a22-8127-0608f7f47ade`, `endedAt` `2026-09-27T17:21:23.049Z`).
- Five minutes of client lifetime after the last kill did not rewrite the file. The write happened with the kill.

Expect about a one-second delay, not a batch at the scoreboard.

---

# File lifetime the plan must handle

These are observed, not hypothetical.

- Both logs append for the whole client session. Several matches sit in one file. Creation time stays at the first write. A watcher that only reacts to `EVENT_START_MATCH` after the overlay has started is not enough: if the overlay is opened mid-match, it has to reconstruct the current match from the last `EVENT_START_MATCH` onward, including kills already in the file.
- A new client launch replaces the log. The launcher diagnostic called this `log_rotated_or_rewritten_since_launch`. Inode changed. The previous session’s `KillFeed.log` was 90278 bytes; the new file was created at `19:24:22` and was 58271 bytes at `19:59:01`. A tailer that keeps a byte offset across replacement will mis-read.
- The game keeps the file open while it appends. The reader must not block the game’s writes. The plan must say how the file is opened and what happens if the read fails because the file does not exist yet (game not started, or log not created until the first event).
- `KillFeed.log` does not exist until the first kill of a client run. `MatchEndScreen.log` in this run existed from the first match start, which can be earlier than the first kill. The overlay can be running with a match started and an empty alive list. That empty state is correct.
- Partial last lines are possible if a read lands mid-append. The plan must say how a short line is retried rather than parsed as a weird player name.
- Timestamps have one-second resolution. Two files can record different events in the same second. In the captured data, match start was 15–20 seconds before the first kill, so this did not bite. The plan still needs an ordering rule when timestamps tie.
- The on-screen console the user originally pasted (`handleStageEvent`, vehicle selection, “Match starts in 30 seconds”, damage reports, `You have received -331865 xp`) is **not** in these two files. Do not scrape it. Do not depend on it.

---

# Overlay behavior the plan must decide

Decide and justify each of these. Do not leave them as “TBD” unless you are blocked on a fact you cannot see from this machine.

- Language and runtime, on this Windows 10 PC, with a bias toward something that starts quickly and sits idle at nearly no CPU and a small memory footprint. Name what you rejected and the cost you refused (a bundled browser, a heavy UI framework, a polling loop, and so on).
- How the window stays above the game, whether it takes mouse clicks, whether it takes keyboard focus, and what fullscreen `1920x1440` does to that choice.
- Where it sits, how dense the rows are, and how a long alive list stays usable. Early game can reveal a large number of names. The list then shrinks.
- How a high rank is visible without requiring the user to scan every row. The user called out “is there a very strong player still up.”
- What the window shows before the first `EVENT_START_MATCH`, during a started match with zero kills, and after the process is running but the game is closed.
- How the user starts and stops it. One obvious launch path. No installer requirement unless the plan explains why a portable file is not enough.
- How it finds the log directory if `C:\Games\ROTK` is only the current machine’s path. The launcher config at `%APPDATA%\ROTK Launcher\` is the place that recorded `installation.root`. There may be `config.v1.json` as well as `config.v1.backup.json`.
- CPU and I/O while idle: the game appends a line every few seconds in a busy fight and then goes quiet. The plan must not use a tight spin. It must also not miss a line that was written during a one-second filesystem delay.
- What is logged or shown when a line fails to parse, without popping a dialog over the game.

---

# Caveats you must account for in the plan

Cover each item with the behavior you chose. Add any further caveat you find by looking at the logs. Do not “solve” a caveat by reading the game process.

1. Display name is not identity. Id is.
2. First rank sticks. Later ranks for that id do not.
3. Victims and `DEATH` lines leave the list. Killer and assist enter or remain.
4. Unknown assist counts and unknown future message text.
5. Names contain spaces, dots, digits, and non-ASCII. A parser that splits on whitespace will corrupt them.
6. Host field changes shape inside one file (`[HOST]` versus a computer name).
7. Several matches per file. Only the match after the latest `EVENT_START_MATCH` counts. Starting the overlay mid-match must still be correct.
8. Log file replaced on a new client launch. Offset and file identity must be reset.
9. File missing until the first relevant event.
10. Game holds the write handle.
11. Torn last line.
12. One-second timestamps when ordering the two files.
13. No match-end event. Do not clear on a quiet gap.
14. `HandleMatchPlayerSummaryPacket` noise in `MatchEndScreen.log`.
15. `HandleMatchStargetPacket` is not a second start.
16. Fullscreen 1920x1440 may hide a normal topmost window.
17. The overlay must not steal input during a gunfight unless the plan deliberately adds a non-default way to move or close it.
18. A long early-game list. A one-name late-game list. Both have to be readable.
19. Rank `0.0` is real data, not “unknown.”
20. The same person can look alive under a new id after a new match because ids change. The reset handles this only if the reset actually runs.
21. Resurrection if a dead id later appears as killer or assist. State the rule.
22. The local player is not special.
23. Do not block or rotate the game’s logs.
24. Disk and CPU must stay negligible next to a client whose FPS cap is 500.
25. If the logs stop growing because the game froze or the path is wrong, the overlay should look stale on purpose rather than keep a convincing list with no indication of the last applied event.

---

# What to deliver

A build plan, written into this project, that a later implementation pass can follow without another research pass through these logs.

The plan should include:

- The decisions above, each with the reason.
- The parsing rules, including the exact fields and the identity key.
- The state machine for a match: start, kill, assist, death, file replacement, overlay launched mid-match.
- How the two files are read and ordered.
- The window behavior, including fullscreen.
- A short list of sample lines you will use as checks, taken from `C:\Games\ROTK\Logs\KillFeed.log` and `MatchEndScreen.log`, covering a kill, a headshot, an assist, a `DEATH`, a non-ASCII or spaced name, and a second `EVENT_START_MATCH` that must drop the previous match’s survivors.
- What you will not build.

Do not implement the overlay in the same step as the plan.
