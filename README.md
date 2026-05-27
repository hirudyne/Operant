# Operant

Save game editor for **Zero Parades** (Operant build, ZA/UM C4 engine).

Edits Sol, inventory item amounts and equipped state, and FELDState counters
(skills, faculties, anchors, conditioning XP, etc.). The save format,
encryption, and per-chunk checksums were reverse-engineered from the IL2CPP
binary; this editor handles the round-trip transparently.

## Quick start

Prebuilt: download `OperantEditor.exe` from a release, double-click.

From source:

```cmd
git clone https://github.com/hirudyne/Operant.git
cd Operant
publish.cmd
publish\OperantEditor.exe
```

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download).

## What it edits

| Tab        | What                                                                        |
|------------|-----------------------------------------------------------------------------|
| Overview   | Save name and label, play time, story year/day/hour/minute, Sol (currency). |
| Inventory  | Per-item Amount, and Status (In inventory ↔ Equipped).                      |
| Stats      | FELDState `m_countersValues` — skills, faculties, anchors, conditioning, limits, etc. By default only commonly-edited groups are shown; toggle "Show all counters" for the full set including quest flags and timestamps. |

Sol writes to both the header summary and the `stats.money` counter
(they're mirrored in the format, and the game expects them to agree).

The editor backs up the existing file to `.bak` before overwriting.

## Save locations

Windows: `%USERPROFILE%\AppData\LocalLow\ZA UM\Zero Parades\<user-id>\Saves\`.
Save files are named with a UUID and `.sav` extension.

## Solution layout

```
src/
  Operant.SaveFormat/        Format library (read/write, AES, Hash128)
  Operant.SaveFormat.Tests/  xUnit tests with sample saves as test data
  Operant.Editor/            WPF GUI, MVVM, no third-party UI deps
publish.cmd / publish.sh     Build a single-file self-contained .exe
Operant.sln                  Solution file
```

The format library has no UI dependencies and could be referenced from a CLI
tool or other host.

## File format (for reference)

Saves are framed by `BinaryWriter` with all integers little-endian:

```
[byte len_prefix=16]"C4SaveFileHeader"
[int32 version=1]
[int32 length][bytes header_json]
[byte len_prefix=32][32 hex chars]   ; Hash128 of (tag, version, header_json)
[byte encryption_required]           ; 0x01 in observed saves
[byte len_prefix=17]"C4SaveFileContent"
[int32 version=0]
[int32 length][bytes content_blob]   ; salt(32) | iv(16) | ciphertext (PKCS7)
[byte len_prefix=32][32 hex chars]   ; Hash128 of (tag, version, content_blob)
```

Encryption: AES-256-CBC with PBKDF2-HMAC-SHA1 (1000 iterations) keyed off a
hardcoded seed found in the binary. Checksums: Unity's `Hash128`, which wraps
SpookyHash V2; rendered as `h0_le_bytes_hex || h1_le_bytes_hex`. Each chunk's
hash takes its content tag (UTF-8), version (int32 LE), and data blob; for the
content chunk, the data blob is the *encrypted* bytes, so any tamper with the
ciphertext is detected without doing the AES work.

## What isn't yet known

- Whether the game cross-checks fields beyond the obvious Sol ↔ stats.money
  mirror (telemetry tallies vs balances, item-usage counts vs amounts, etc.).
- Toggling a non-equippable item to `Equipped` is structurally valid but may
  confuse the in-game UI.
- Editing quest-flow counters (`flowCardCounter.*`, `journalTaskStatusCounter.*`)
  is supported by toggling "Show all counters" but may put quest state into
  combinations the game never expected.

If a saved file fails to load, the `.bak` next to it is the original.
