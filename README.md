# Boatdrill

A backup you have never restored from is a hope, not a backup. Boatdrill is a small Windows app that
rehearses restoring from your backups on a schedule and says, in plain words, whether they would work.

Boatdrill is a [Project Max](https://projectmax-org.github.io) application: free, open source under the
[MIT licence](LICENSE), no account, no telemetry. It is under development and has no release yet; the
drill engine and a command-line runner work today, the Windows app is a shell around them.

---

## What it does

**Reads your backups back.** You name the folders you care about (sources) and the places their copies
should exist (destinations): an external drive, a NAS share, a OneDrive or Dropbox folder. A drill picks a
sample of real files, finds their copies, reads every byte back into a scratch folder on this PC, compares
SHA-256 hashes with the originals, and times the read.

**Says what it found.** One score and one sentence per destination, such as *"Backup drive would restore.
198 of 200 sampled files read back correctly and 2 are older than the original. The oldest change not yet
copied here is from 26 October 2026 (2 days ago). A full restore of 197.8 GB would take about 2 h 10 min
at the speed measured."* The score has four parts that are always shown: coverage, freshness, integrity
and reachability. [docs/SCORING.md](docs/SCORING.md) has the arithmetic.

**Works out where your copies are.** Whether the drive holds the folder's contents directly, a folder
named after it, or a full-path mirror (`D:\Backup\C\Users\...`), the drill tries each layout against the
sample and keeps the one that fits.

**Tells corruption from lag.** A copy that is older than the original is *outdated*; a copy that is not
older yet has different bytes is *corrupt*, and any corruption keeps the score out of the green. A cloud
placeholder whose bytes are not on the disk is reported as exactly that, never downloaded.

**Stays honest about a sample.** Every coverage figure carries its margin of error, the sample size and the
byte cap are settings, and recently changed files are drawn more often because they are the ones a backup
is most likely to be missing.

## What it never does

- It never writes into a destination. The one place a drill writes is its scratch folder on this PC,
  which is emptied when the drill ends. (A planned, opt-in canary file goes into *source* folders only.)
- It never phones home. There is no network code in the app at all.
- It never pulls a cloud placeholder down from OneDrive or Dropbox to check it.
- It never runs while you work: scheduled drills wait for an idle PC on AC power with nothing full-screen
  (the scheduler lands with the v0.1 app milestone).

---

## Try the engine from the command line

Works on Windows, Linux and macOS with the [.NET 10 SDK](https://dotnet.microsoft.com/download):

```
dotnet run --project src/Boatdrill.Cli -- drill --source ~/Documents --dest /mnt/backup/Documents
dotnet run --project src/Boatdrill.Cli -- drill --source C:\Users\max\Pictures --dest E:\ --sample 300 --json
```

`--source` and `--dest` repeat. `--history <file>` appends the report to a JSON-lines file, which is what
makes the reachability part of the score meaningful over time. Exit code 0 means every destination would
restore (fully or mostly), 1 means at least one would not, 2 means bad arguments.

## Building the Windows app

```powershell
.\build.ps1          # tests, then dist\Boatdrill.exe and dist\boatdrill-cli.exe, self-contained
.\build.ps1 -Run     # and launch it
dotnet run --project src/Boatdrill
```

The app reads `%LOCALAPPDATA%\Boatdrill\settings.json`, writes it with defaults (Documents, Pictures,
Desktop as sources, no destinations) on first run, and keeps drill history in `history.jsonl` beside it.
Until the settings screen lands, add destinations by editing that file:

```json
{
  "destinations": [
    { "id": "usb", "name": "Backup drive", "path": "E:\\Backup", "layout": "auto" }
  ]
}
```

## How it's put together

```
src/Boatdrill.Core/          the engine (net10.0, no UI, no Windows API)
  Sampling/                  one-pass weighted reservoir sampling, byte cap, per-folder floor
  Adapters/                  IDestinationAdapter and MirrorFolderAdapter (layout detection, read-back)
  Drill/                     DrillRunner, the report model, the sentence writer
  Scoring/                   the four-part readiness score and its margin of error
  History/                   append-only JSON-lines history
src/Boatdrill/               the WPF app (net10.0-windows)
src/Boatdrill.Cli/           boatdrill-cli, the same engine from a terminal
tests/Boatdrill.Core.Tests/  xunit; every drill runs against temp folders, including deliberately broken ones
```

A drill is a pipeline: **Sampler** picks files, an **adapter** finds their copies and reads them back
into scratch while hashing, the **runner** classifies each file (verified, outdated, corrupt, missing,
placeholder, error) and measures throughput, the **scorer** turns the counts into the score, and
**Verdicts** writes the sentence. The Core project has no Windows dependency, so it builds and its tests
run on any OS; CI does exactly that on Linux before building the app on Windows.

## Roadmap

- **v0.1** folder-mirror destinations, scheduled drills, the dashboard, tray icon and toast on failure.
- **v0.2** opt-in canary files that measure backup lag directly, a hash ledger across drills to catch bit
  rot, the "what exists only on this PC" view, Windows File History and restic/Kopia destinations.
- **v1.0** installer, winget, code signing, exportable report.

## Licence

MIT. See [LICENSE](LICENSE).
