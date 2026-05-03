# Save/Load System

> **Status**: Complete
> **Author**: Design session + agents
> **Last Updated**: 2026-05-03
> **Implements Pillar**: Pillar 2 (You Choose How Deep)

## Overview

The Save/Load System is Masks' persistence layer. It serializes the full game state to disk at save points, restores it on load, and manages the boundary between in-memory runtime state and durable storage. Its primary responsibility is the Character State Manager — every `CharacterState` record (depth tier, closed-off flag, revealed secrets, narrative flags, scenes played, availability) must survive session end and restore exactly on re-entry.

The system also serializes Player Profile data (name, pronouns, appearance — owned by the Player Profile GDD, design order #4) and any global session state that falls outside the per-character schema.

Save/Load is a Foundation system. The Relationship Depth System and Endings/Route Completion both depend on it — choices that advance depth or trigger endings must persist. Without this system, the game is a visual novel that remembers nothing: every session starts at zero, the Player Fantasy of consequence and memory collapses, and Pillar 2 ("You Choose How Deep") becomes a lie. Nothing the player does lasts.

The system operates automatically — it does not ask the player to press a save button. Saves are triggered on scene exit and at explicit game-design-designated checkpoints. There is no manual save UI at MVP. The player does not need to think about saving.

One significant technical constraint shapes this GDD: the CSM schema uses `Dictionary<string, bool>` (narrative_flags) and `HashSet<string>` (scenes_played) — types that Unity's built-in `JsonUtility` cannot serialize. The serialization format choice must be explicitly resolved here.

## Player Fantasy

The Save/Load System has no player-facing surface. There is no save button, no save menu, no confirmation dialog. The player never thinks about it. That is the point.

What the player feels is not the system — it is what the system makes possible: **the game remembers**.

You close Masks at 1 a.m. with the taste of a confession still in your mouth — something she only said because you were quiet long enough for her to say it. Three days later you come back. You open her route. She doesn't restart. She doesn't re-introduce herself. The version of her that knows what you did with that silence is still there, waiting exactly where you left her. The private self you earned is still private, still yours, still real.

That continuity is not the system's fantasy — it is Pillar 1 (Every Character Has a Private Self) in operation across time. The save system is the infrastructure that keeps the promise. If it fails — if she forgets — the intimacy collapses instantly. The player doesn't think "the save system broke." They think "this relationship wasn't real."

The secondary beat is consequence. You took her to depth four and stopped. You chose not to push. That line you drew is still drawn when you come back. Nothing has been reset, reconsidered, or forgiven on your behalf. The save system enforces Pillar 2 (You Choose How Deep) across sessions — choices that shape a relationship persist because the system serializes them, not because the narrative acknowledges them explicitly. The player feels ownership of their choices because time cannot undo them.

The fantasy served here is **earned continuity**: the sense that what you built is real, that it persists, that the characters you've come to know are waiting — not restarting. The save system is invisible when it works. It is catastrophic when it doesn't.

## Detailed Design

### Core Rules

**Serialization**

1. The Save/Load System uses **Newtonsoft.Json** (`com.unity.nuget.newtonsoft-json`) as its sole serialization library. JsonUtility is not used for save data — it cannot serialize `Dictionary<string, bool>` (narrative_flags) or `HashSet<string>` (scenes_played).

2. Save data is written as UTF-8 JSON to `Application.persistentDataPath/save.json`. There is one save slot. No save browser, no slot selection UI.

3. In development builds the JSON is pretty-printed for designer and QA debugging. In release builds it is minified.

4. The save file always includes a root `schema_version` integer field. Current version: **1**. A mismatch between the file's schema_version and the current application version triggers migration (see Edge Cases).

**Save File Structure**

5. The save file serializes two top-level objects:
   - `characterStates` — all CharacterState records from CSM, including quarantine map entries, keyed by character_id
   - `playerProfile` — name, pronouns, appearance (schema owned by Player Profile GDD)

6. CSM fields that cannot serialize directly are converted to DTO types before writing:
   - `narrative_flags: Dictionary<string, bool>` → `NarrativeFlagDto[]` (array of `{ key: string, value: bool }`)
   - `scenes_played: HashSet<string>` → `string[]` (order not preserved — not required)

   On load, these are converted back to their runtime types before `RestoreState()` is called.

**Save Triggers**

7. `SaveManager.Save()` is called by designated external callers. SaveManager does not listen to events — callers invoke it directly. The three trigger points are:
   - **Scene exit**: Scene Management / Flow Controller calls `Save()` when any dialogue scene ends normally. This is the primary save trigger.
   - **Depth tier change**: The Relationship Depth System and The Dig call `Save()` immediately after any successful `SetDepthTier()`, `SetClosedOff()`, or `SetSecretRevealed()` on CSM. These are the game's highest-stakes state changes and must survive an immediate crash.
   - **Session launch checkpoint**: On application start, after loading the main save, `SaveManager.WriteCheckpoint()` writes a snapshot to `save_checkpoint.json`. This preserves the state at session start and supports crash recovery.

8. Save is **synchronous** at MVP. There is no async write queue. Frame spike on save is acceptable — saves occur at scene exit and depth gate moments, which are not frame-critical. If profiling reveals this is a problem post-MVP, async write can be introduced without changing the GDD's save-trigger contract.

**Load Sequence**

9. On application start, the load sequence runs once in this order:
   1. Check for `save.json` at `Application.persistentDataPath`
   2. If present: deserialize → validate schema_version → convert DTOs back to runtime types → call `CSM.RestoreState(charId, state)` for every character entry (including quarantine entries) → call `PlayerProfile.RestoreProfile(profile)` for the player profile block
   3. If absent: initialize default state — empty character registry, default player profile
   4. Write session launch checkpoint (`save_checkpoint.json`)

10. `CSM.RestoreState()` is the bulk-bypass path that skips individual field guards. This prevents invariant double-firing during restore (e.g., restoring depth_tier=5 alongside secret_revealed=true — each would otherwise trigger the auto-set logic, which is already reflected in the serialized data).

**Failure Handling**

11. On any load failure (JSON parse error, schema version unrecoverable, file locked):
    - Rename the corrupt file to `save_corrupt_[unix_timestamp].json` in the same directory
    - Initialize default state (new game start)
    - Log an error with the failure reason and the corrupt file path
    - Do not crash, do not show an error screen to the player

12. SaveManager does not cache state. Every `Save()` call reads from CSM directly. CSM is the source of truth; the save file is its persisted form.

---

### States and Transitions

SaveManager is not a state machine. It has two operations:

| Operation | Caller | When |
|---|---|---|
| `Save()` | Scene Management, Relationship Depth System, The Dig | Scene exit; depth tier / closed_off / secret_revealed change |
| `WriteCheckpoint()` | Application startup sequence | Once per session, after main save loads |
| `Load()` | Application startup sequence | Once at launch |

There is no "saving in progress" state visible to the player. No spinner, no lock icon, no confirmation.

---

### Interactions with Other Systems

| System | Direction | Interface |
|---|---|---|
| Character State Manager | Bidirectional | **Save**: calls `CSM.GetAllStates()` to collect all CharacterState records. **Load**: calls `CSM.RestoreState(charId, state)` for each deserialized record. |
| Scene Management / Flow Controller | Inbound | Scene Management calls `SaveManager.Save()` on scene exit. |
| Relationship Depth System / The Dig | Inbound | These systems call `SaveManager.Save()` after writing depth-altering state changes to CSM. |
| Player Profile | Bidirectional | **Save**: reads the Player Profile's serializable representation. **Load**: passes the deserialized profile block to `PlayerProfile.RestoreProfile()`. |
| Application Lifecycle | Inbound | The startup sequence calls `SaveManager.Load()` once, then `SaveManager.WriteCheckpoint()` once. |

SaveManager has no dependency on the Dialogue/Narrative Engine, UI System, or audio systems. It is below those layers in the dependency graph.

## Formulas

The Save/Load System contains no gameplay math. The "formulas" here are the data contracts for types that require conversion to pass through JSON.

### Save File Schema

**Root object**
```
{
  schema_version: int,           // current: 1
  saved_at: string,              // ISO 8601 UTC timestamp
  characterStates: {
    [character_id: string]: SerializableCharacterState
  },
  playerProfile: SerializablePlayerProfile
}
```

**SerializableCharacterState**
```
{
  character_id: string,
  depth_tier: int,               // range [0, 5]
  closed_off: bool,
  secret_revealed: bool,
  is_available: bool,
  availability_lock_reason: string,
  narrative_flags: NarrativeFlagDto[],
  scenes_played: string[]
}
```

**NarrativeFlagDto**
```
{
  key: string,
  value: bool
}
```

NarrativeFlagDto is the serialized form of `Dictionary<string, bool>`. On load, the array is reconstructed into a Dictionary using `key` as the dictionary key. `scenes_played` is serialized as a plain `string[]`; on load it is reconstructed into a `HashSet<string>`.

**SerializablePlayerProfile** — schema deferred to Player Profile GDD (design order #4). The Save/Load System reserves the `playerProfile` root key; the Player Profile GDD owns the field list inside it.

### Pre-Restore Invariant Repair

Before calling `CSM.RestoreState()`, SaveManager validates each deserialized CharacterState:

| Condition | Correction | Log level |
|---|---|---|
| `depth_tier == 5` and `secret_revealed == false` | Set `secret_revealed = true` | Warning |
| `depth_tier < 0` | Clamp to 0 | Warning |
| `depth_tier > MAX_DEPTH_TIER (5)` | Clamp to 5 | Warning |

These corrections are applied at the deserialization boundary, before RestoreState() receives the data, to prevent the bulk-bypass path from injecting invalid state into CSM.

## Edge Cases

**E-1: First Launch — No Save File**
The save file does not exist (new player, or file manually deleted). Load sequence detects absence of `save.json`, initializes default state (empty CSM registry, default player profile), and writes the session launch checkpoint. Game starts as a new session. No error shown to player.

**E-2: Corrupt Save File (Unrecoverable)**
`save.json` exists but cannot be parsed (JSON syntax error, file truncated, encoding error, or any deserialization exception). SaveManager renames it to `save_corrupt_[unix_timestamp].json`, logs the failure with file path and exception type, initializes default state. Player starts from scratch. The corrupt file is preserved for QA/support debugging. No crash.

**E-3: Schema Version Mismatch (Future Migration)**
`save.json` has `schema_version` less than the current version. At MVP there is only version 1, so no migration path exists yet. If a mismatch is detected: log a warning, attempt to load anyway using Newtonsoft.Json's partial deserialization (unknown fields are ignored; missing fields take their C# default values). If partial load succeeds, treat as loaded with possible data loss. If partial load fails, fall through to corrupt-save handling (E-2). A formal migration system is a post-MVP concern.

**E-4: Save File Locked / Write Failure**
`SaveManager.Save()` fails because the file is locked by another process, the disk is full, or write permissions are denied. Log the error with the IO exception. Do not crash. The in-memory CSM state is unaffected — the game continues. The save will be attempted again at the next trigger point. If write failure persists across multiple triggers, the player is at risk of progress loss; this is surfaced as a logged error only (no player-facing UI at MVP).

**E-5: Partial Character State in Save File**
A character exists in CSM's registry (registered this session) but has no corresponding entry in `save.json`. On load, the character is initialized to default state (depth_tier=0, all defaults). This is correct: the character had no prior progress that predates this session.

**E-6: Save File Contains Unknown Character IDs (Quarantine)**
`save.json` contains a character_id not in the current roster (character was cut, or save was created with a different build). These entries are loaded into CSM's quarantine map — not the live state map — via `CSM.RestoreState()`. They are preserved in the save file on the next `Save()` call and do not appear in live queries. Behavior mirrors CSM Edge Case E-5.

**E-7: Checkpoint File Absent on Crash Recovery**
The session launch checkpoint (`save_checkpoint.json`) is missing or corrupt when the player returns after a crash. Fall back to `save.json` as normal. No special handling needed — the checkpoint is a belt-and-suspenders measure, not the primary save.

**E-8: Simultaneous Save Calls**
Two callers invoke `SaveManager.Save()` in the same frame (e.g., a scene exit and a depth tier change fire simultaneously). Since Save() is synchronous at MVP, the second call simply overwrites the first write with the same CSM state. No data loss — both writes produce identical output. If async writes are introduced post-MVP, write coalescing must be added at that time.

**E-9: RestoreState() Called With Residual Invariant Violation**
SaveManager's pre-restore invariant repair corrects known depth_tier/secret_revealed mismatches before calling RestoreState(). If RestoreState() still receives a state that violates a CSM invariant despite this repair, CSM enforces the invariant as it would for any write and logs a warning. The result is a partially corrected restore, not a crash.

**E-10: Depth Tier Change During Scene**
Save() is not called mid-scene at MVP — only on scene exit. Depth tier changes (from The Dig) occur at scene conclusion, after the Dig sequence resolves and before the scene exits. SaveManager receives the depth-change Save() call followed by the scene-exit Save() call; both produce valid serialized state. No mid-dialogue save occurs.

## Dependencies

### Upstream Dependencies (this system depends on)

| System | Dependency | What Save/Load needs |
|---|---|---|
| Character State Manager (#6) | Hard | `CSM.GetAllStates()` — provides all CharacterState records for serialization. `CSM.RestoreState(charId, state)` — bulk-bypass restore on load. CSM must be initialized before Load() completes. |
| Player Profile / PC Data (#12) | Hard | Player Profile provides a serializable representation for the save file and receives deserialized data on load via `RestoreProfile()`. Interface is provisional until Player Profile GDD (design order #4) is authored. |

### Downstream Dependents (systems that depend on this one)

| System | How it depends |
|---|---|
| Relationship Depth System (#1) | Depth tier progression persists across sessions only because Save/Load serializes depth_tier. Without Save/Load, every session starts at tier 0. |
| The Dig / Depth Gate (#3) | closed_off state must survive session end. Calls Save() on depth-altering writes. |
| Secret Reveal System (#5) | secret_revealed must be permanent. Save/Load is the enforcement mechanism across sessions. |
| Scene Management / Flow Controller (#9) | Calls Save() on scene exit. Depends on SaveManager being available at scene end. |
| Endings / Route Completion (#18) | Route completion state persists only via Save/Load. |

### Library Dependency

| Library | Package ID | Version | Role |
|---|---|---|---|
| Newtonsoft.Json | `com.unity.nuget.newtonsoft-json` | Latest stable | Serialize/deserialize Dictionary, HashSet, and the full save file |

This is the first external library dependency in the project. It must be added to the Allowed Libraries list in `.claude/docs/technical-preferences.md` before implementation begins.

## Tuning Knobs

| Knob | Type | Default | Safe Range | Effect |
|---|---|---|---|---|
| `SCHEMA_VERSION` | int | 1 | [1, ∞) | Save file schema version. Increment when adding new fields that require migration. Changing requires implementing a migration handler for the previous version. |
| `SAVE_FILE_NAME` | string | `"save.json"` | Any valid filename | Filename for the main save file at `Application.persistentDataPath`. Changing post-launch requires migrating existing save files on upgrade. |
| `CHECKPOINT_FILE_NAME` | string | `"save_checkpoint.json"` | Any valid filename | Filename for the session launch checkpoint. |
| `PRETTY_PRINT_IN_DEV` | bool | true | — | If true, JSON is pretty-printed in development builds. No gameplay effect. Disable if save file size becomes a concern during testing. |
| `MAX_NARRATIVE_FLAG_COUNT` | int | unbounded | [100, unbounded] | Optional ceiling on narrative flags per character. Not enforced at MVP — a safeguard if flag proliferation becomes a concern. Enforcement requires an authoring-time linter. |

All values are configuration constants in `SaveConfig.cs` or equivalent. None are exposed in player-facing settings.

## Visual/Audio Requirements

None — the Save/Load System has no output surface. It produces no visual or audio events.

## UI Requirements

None — there is no save UI at MVP. The system operates silently. No spinner, no confirmation toast, no save indicator.

## Acceptance Criteria

Legend: **Logic** = Edit Mode automated | **Integration** = Play Mode automated | **Manual** = QA walkthrough

### Category 1: Save Trigger Behavior

| ID | Criterion | Classification |
|---|---|---|
| SL-001 | GIVEN a dialogue scene ends normally / WHEN Scene Management calls Save() / THEN `save.json` is written to `Application.persistentDataPath` within the same frame | Logic / BLOCKING |
| SL-002 | GIVEN `SetDepthTier()` succeeds on CSM / WHEN the caller invokes Save() immediately after / THEN the serialized `depth_tier` in `save.json` matches the new value | Integration / BLOCKING |
| SL-003 | GIVEN `SetClosedOff()` succeeds on CSM / WHEN Save() is called / THEN `closed_off` in `save.json` reflects the new value | Logic / BLOCKING |
| SL-004 | GIVEN `SetSecretRevealed()` succeeds on CSM / WHEN Save() is called / THEN `secret_revealed` in `save.json` reflects the new value | Logic / BLOCKING |
| SL-005 | GIVEN application startup completes / WHEN the load sequence finishes / THEN `save_checkpoint.json` is written to `Application.persistentDataPath` | Integration / BLOCKING |

### Category 2: Load — Happy Path

| ID | Criterion | Classification |
|---|---|---|
| SL-010 | GIVEN `save.json` exists with valid content / WHEN the application starts / THEN all CharacterState records are restored to CSM via RestoreState() before the first scene is available | Integration / BLOCKING |
| SL-011 | GIVEN a character with depth_tier=3 was saved / WHEN the application restarts and loads / THEN CSM.GetState(charId).depth_tier == 3 | Integration / BLOCKING |
| SL-012 | GIVEN a character with closed_off=true was saved / WHEN loaded / THEN CSM.GetState(charId).closed_off == true | Integration / BLOCKING |
| SL-013 | GIVEN a character with 5 narrative flags was saved / WHEN loaded / THEN CSM.GetState(charId).narrative_flags contains all 5 entries with correct keys and values | Logic / BLOCKING |
| SL-014 | GIVEN a character with 10 scenes_played entries was saved / WHEN loaded / THEN CSM.GetState(charId).scenes_played contains all 10 scene IDs | Logic / BLOCKING |
| SL-015 | GIVEN no `save.json` exists / WHEN the application starts / THEN CSM initializes with default state (empty registry) and no error is thrown | Integration / BLOCKING |

### Category 3: Serialization Round-Trip

| ID | Criterion | Classification |
|---|---|---|
| SL-020 | GIVEN a CharacterState with narrative_flags: {"met_her": true, "heard_secret": false} / WHEN saved and loaded / THEN the Dictionary round-trips exactly | Logic / BLOCKING |
| SL-021 | GIVEN a CharacterState with scenes_played: {"intro_01", "cafe_02", "park_01"} / WHEN saved and loaded / THEN all three entries are present in the restored HashSet | Logic / BLOCKING |
| SL-022 | GIVEN a save file with `schema_version: 1` / WHEN loaded / THEN no migration warning is logged | Logic / BLOCKING |
| SL-023 | GIVEN a full roster of 30 characters each with a large narrative flag set / WHEN saved / THEN the file is written without error and loaded back without error | Integration / BLOCKING |

### Category 4: Failure Handling

| ID | Criterion | Classification |
|---|---|---|
| SL-030 | GIVEN `save.json` is syntactically invalid JSON / WHEN the application starts / THEN the game initializes with default state and does not crash | Integration / BLOCKING |
| SL-031 | GIVEN `save.json` is corrupt / WHEN loaded / THEN the file is renamed to `save_corrupt_[timestamp].json` and a warning is logged with the file path | Integration / BLOCKING |
| SL-032 | GIVEN the save directory has no write permission / WHEN Save() is called / THEN an error is logged and the game continues without crash | Manual / BLOCKING |
| SL-033 | GIVEN a corrupt save is recovered / WHEN the player reaches a save trigger / THEN `save.json` is written successfully with the current (default) state | Integration / BLOCKING |

### Category 5: Pre-Restore Invariant Repair

| ID | Criterion | Classification |
|---|---|---|
| SL-040 | GIVEN a serialized CharacterState with depth_tier=5 and secret_revealed=false / WHEN SaveManager applies pre-restore repair / THEN secret_revealed is set to true before RestoreState() is called | Logic / BLOCKING |
| SL-041 | GIVEN a serialized CharacterState with depth_tier=-1 / WHEN pre-restore repair runs / THEN depth_tier is clamped to 0 and a warning is logged | Logic / BLOCKING |
| SL-042 | GIVEN a serialized CharacterState with depth_tier=99 / WHEN pre-restore repair runs / THEN depth_tier is clamped to 5 and a warning is logged | Logic / BLOCKING |

### Category 6: Quarantine

| ID | Criterion | Classification |
|---|---|---|
| SL-050 | GIVEN `save.json` contains a character_id not in the current roster / WHEN loaded / THEN the entry is passed to CSM via RestoreState() and CSM places it in the quarantine map | Integration / BLOCKING |
| SL-051 | GIVEN a character is in the quarantine map / WHEN Save() is called / THEN the quarantine entry is serialized to `save.json` alongside live characters | Integration / BLOCKING |

### Category 7: Schema Integrity

| ID | Criterion | Classification |
|---|---|---|
| SL-060 | GIVEN a development build / WHEN a save file is inspected in a text editor / THEN the JSON is pretty-printed and human-readable | Manual / ADVISORY |
| SL-061 | GIVEN any saved file / WHEN the root object is inspected / THEN `schema_version` is present | Logic / BLOCKING |
| SL-062 | GIVEN any saved file / THEN `saved_at` contains a valid ISO 8601 UTC timestamp | Logic / BLOCKING |

### QA Engineering Flags

1. **SL-023** (30-character load) should also be run as a performance check — flag if file write or load time exceeds 200ms on minimum-spec hardware.
2. **SL-032** (write permission denied) requires manual environment setup or a mock filesystem; cannot be fully automated in Play Mode.
3. **Newtonsoft.Json must be added** (`com.unity.nuget.newtonsoft-json`) before SL-020, SL-021, and SL-023 can run. Block these tests until the package is present in the project.

## Open Questions

All design questions are resolved. Engineering items to address before implementation:

1. **Add Newtonsoft.Json** to `com.unity.nuget.newtonsoft-json` in Package Manager and to `technical-preferences.md` Allowed Libraries.
2. **Player Profile interface** is provisional — SaveManager's `SerializablePlayerProfile` schema and `RestoreProfile()` call are defined once Player Profile GDD (design order #4) is authored.
3. **Post-MVP async writes** — if SL-023 performance testing shows save spikes, introduce async file write with write coalescing. This does not require a GDD change — it is a SaveManager implementation detail.
