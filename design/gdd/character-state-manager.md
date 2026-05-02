# Character State Manager

> **Status**: Complete
> **Author**: Design session + agents
> **Last Updated**: 2026-05-02
> **Implements Pillar**: Pillar 1 (Every Face Hides a Face), Pillar 2 (You Choose How Deep)

## Overview

The Character State Manager is Masks' central data store for all per-character relationship state. It is the single authoritative source for every piece of information about a character that persists between scenes: how deep the player's relationship has progressed, whether the character is closed off, which secrets have been revealed, and which flags govern branching within their narrative arc.

The system serves as a read/write hub for the game's core mechanic. Eight downstream systems depend on it — the Relationship Depth system reads and writes depth tiers; the Dialogue/Narrative Engine accesses state through `MasksVariableStorage`; Scene Management consults it to determine which scenes are available; Save/Load serializes and restores it; and the Dig, Secret Reveal, Signal Reading, and Character Roster systems all read from it to drive their logic. None of those systems own state — they all read and write through this manager.

The manager holds state for every character in the roster simultaneously. At MVP scale (3–5 characters) this is trivially small. At full release scale (20–30 characters), it must remain performant for synchronous reads — any dialogue line may trigger a variable read mid-scene. The schema must be designed to be extension-safe: new state fields added for new characters or future systems must not require migrating existing save data.

The system has no player-facing surface. The player never sees or interacts with it directly. What they experience — the feeling that a character remembers them, that depth is real, that choices last — is entirely downstream of this system doing its job correctly.

## Player Fantasy

The player opens Ren's route for the first time in a week. They don't have to remember where they were — the game remembers for them. Ren is guarded in the specific places the player hasn't earned yet, soft in the places they have. The closed-off flag from that fight is still set. The depth hasn't moved. Ren's first line lands differently *because of what's tracked*, not because the writing says so. Nobody resets. Nobody forgets. Every character in this game is keeping their own private ledger of the player, and this system is keeping it for them.

**The fantasy is being known.** Not generically — specifically. The version of this player that exists inside each character's memory is private, separate, and real. That realness is entirely downstream of this system doing its job without a single silent failure.

The second beat of this fantasy is darker: the player pushed too hard last month and they know it. The closed_off flag is still set. There is no undo, no soft reset, no "try the scene again." The game holds the choice the way a real person would — not punitively, but persistently. Consequence is proof of reality. These characters matter *because* they can be hurt. The "I can't stop" at the core of the whole game requires that stopping was a real option the player didn't take, and this system must remember they didn't take it.

**What breaks the fantasy**: A single state desync. Ren acting like depth-2 when the player is at depth-4. A closed_off flag that silently clears between sessions. The player doesn't think "the system glitched" — they think "this character doesn't actually know me." The romance dies in that gap. This system's correctness *is* the intimacy.

## Detailed Design

### Per-Character State Schema

Every field the CSM tracks for one character. "Write Owner" is the single system permitted to write that field.

| Field | Type | Valid Range | Default | Write Owner | Purpose |
|---|---|---|---|---|---|
| `character_id` | string | non-empty, unique | (set at registration) | CSM (registration only) | Stable string key for all lookups. Never changes after registration. |
| `depth_tier` | int | 0–5 | 0 | Relationship Depth System | Current relationship depth. 0 = surface/initial contact. 5 = secret revealed and processed. |
| `closed_off` | bool | true/false | false | The Dig | Character has withdrawn after a push that exceeded their threshold. Recoverable. |
| `secret_revealed` | bool | true/false | false | Secret Reveal System | Whether the character's secret has been surfaced to the player. Never reverts once set true. |
| `narrative_flags` | Dictionary\<string, bool\> | keys: non-empty string | empty | Dialogue/Narrative Engine (via MasksVariableStorage) | Open bag for all `.yarn`-authored branch condition booleans. Created on first write. No reserved keys except the system fields above. |
| `scenes_played` | HashSet\<string\> | set of scene node name strings | empty | Scene Management / Flow Controller | Which scene nodes have completed. Used to determine availability and prevent replays. |
| `is_available` | bool | true/false | true | Character Roster / Availability | Whether the character is currently available for scene selection. |
| `availability_lock_reason` | string | any string or empty | "" | Character Roster / Availability | Debug string when `is_available` is false. Not displayed to player. |

**On `depth_tier` values:**
- `0` is valid and meaningful — the character has been met but no progress beyond surface contact
- Values 1–3 represent increasing intimacy tiers; thresholds and advancement rules defined in the Relationship Depth System GDD
- `4` is the threshold at which the secret arc becomes accessible
- `5` is terminal depth; `depth_tier == 5` implies `secret_revealed == true` — the CSM enforces this invariant on write (Rule 14)

**On `closed_off`:**
- NOT permanent — recoverable. Only The Dig writes it `true`; only The Dig (or a recovery arc) sets it back to `false`
- Does NOT lower `depth_tier`. A closed-off character at depth 3 is still at depth 3
- `false` is the default operating state — it does not mean anything positive, only that the character is not currently withdrawn

**On `narrative_flags`:**
- Open bag: any key written by a `.yarn` script via `MasksVariableStorage` is stored here automatically
- Namespacing convention (not enforced by the CSM): `{scene_slug}_{flag_name}` (e.g., `surface_01_gave_advice`)
- Reading a missing key returns `false`; the key is not created on read
- Per-character: `$char_ren_surface_01_gave_advice` and `$char_jun_surface_01_gave_advice` are separate entries in separate `CharacterState` records

---

### Core Rules

**Registration and Access**

1. Every character must be registered with the CSM before any scene reads or writes its state. Registration creates a `CharacterState` record with the character's `character_id` and all field defaults.
2. Registration happens at session start. All characters defined in the roster asset are registered if their state record does not yet exist in the current save slot. Characters already in save data are restored via `RestoreState`, not re-registered.
3. Characters are accessed exclusively by `character_id` string (e.g., `"ren"`). IDs are lowercase, alphanumeric with underscores, no spaces. No index-based access is exposed.
4. Accessing an unregistered `character_id` is an error. The CSM logs `[ERROR]` and returns a read-only null-safe default state object. Writes to the default object are silently dropped with `[WARNING]`. This prevents a missing registration from crashing a scene but is a defect that must be corrected.
5. All character states are loaded into memory at session start — eager loading, no lazy path. The full state for 30 characters is a handful of primitives plus a small dictionary per character; it fits comfortably in memory. The benefit: any system can query any character at any time without a load gate or async wait.

**Read and Write Access Patterns**

6. All reads are synchronous. Any system may call `GetState(charId)` at any time and receive current in-memory state immediately.
7. All writes are synchronous. When `MasksVariableStorage.SetValue()` fires, the corresponding CSM write must complete in the same call stack before returning. There is no deferred or batched write path.
8. Writes to system fields are exposed as dedicated typed methods: `SetDepthTier(charId, value)`, `SetClosedOff(charId, value)`, `SetSecretRevealed(charId, value)`, `SetAvailable(charId, value, reason)`. These methods enforce valid ranges and invariants.
9. Writes to `narrative_flags` and `scenes_played` are exposed as: `SetFlag(charId, key, value)` and `RecordScenePlayed(charId, sceneNodeName)`.
10. Write ownership (schema table above) is enforced by convention and code review — the CSM has no technical ownership primitive. The schema table is the authoritative contract.
11. The CSM never pushes notifications. After a write, the writer (or `MasksVariableStorage`) is responsible for calling `<<notify_state_change>>` if the change must propagate. The CSM is a data store, not an event source.

**Default Initialization**

12. New `CharacterState` records initialize with schema defaults: `depth_tier = 0`, `closed_off = false`, `secret_revealed = false`, `narrative_flags = {}`, `scenes_played = {}`, `is_available = true`, `availability_lock_reason = ""`.
13. Defaults are not configurable per-character at MVP. If a character must start at a non-default state by design, Scene Management or Character Roster sets it explicitly on first scene load — not by changing the defaults.

**Invariants**

14. `depth_tier == 5` implies `secret_revealed == true`. If `SetDepthTier(charId, 5)` is called when `secret_revealed` is `false`, the CSM auto-sets `secret_revealed = true` and logs `[INFO] secret_revealed auto-set for {charId} on depth_tier write to 5`. This is the only case where one write produces a second field change.
15. `SetSecretRevealed(charId, false)` is rejected when `depth_tier == 5`. Logs `[WARNING]` and does nothing. `secret_revealed` cannot be walked back once depth 5 is reached.
16. `depth_tier` writes are clamped to [0, 5]. Out-of-range writes log `[WARNING]` with the attempted value and clamp silently.

**Cut Characters and Session Reload**

17. If `RestoreState` is called for a `character_id` not present in the current roster asset, the record is placed in a quarantine map rather than the live state map. Quarantined records are excluded from `GetState` lookups but are re-serialized to the save file to prevent data loss. If the character is later re-added to the roster, the CSM promotes the quarantined record to the live state map instead of initializing fresh defaults.
18. If `RestoreState` is called for a `character_id` already present in the live state map, the incoming record overwrites the existing one wholesale. No merge. The CSM logs `[WARNING]` to surface the double-load as a potential initialization bug. If intentional (save slot switch), the warning is expected.

---

### States and Transitions

The CSM is a pure data store. It has no state machine of its own.

Individual fields carry semantic meaning that other systems interpret as states (e.g., "`closed_off` means the character is currently withdrawn"), but the CSM does not model transitions between those states, enforce transition guards, or own recovery logic. That logic lives in the downstream systems.

- `closed_off` is a boolean. The Dig writes it `true` and back to `false` on recovery. The CSM stores the value; it does not know or enforce when the transition is legal.
- `depth_tier` is an integer. The Relationship Depth System increments it. The CSM enforces only the range [0, 5] and the depth-5 invariant.
- The depth-5 / `secret_revealed` invariant (Rule 14) is an invariant enforcement on a write, not a state machine transition.

There is no concept of a locked or frozen CSM. Writes are always accepted subject to range clamping and invariant enforcement, regardless of what other fields contain.

---

### Interactions with Other Systems

| System | Reads from CSM | Writes to CSM | Write method | Notes |
|---|---|---|---|---|
| **Relationship Depth System** | `depth_tier`, `closed_off` | `depth_tier` | `SetDepthTier(charId, value)` | Reads `closed_off` to determine if a push is blocked; does not write it |
| **Dialogue/Narrative Engine** | All fields via `MasksVariableStorage` | `narrative_flags` (and system fields via their write owners) | `SetFlag(charId, key, value)` | `MasksVariableStorage` is the only engine path; direct CSM calls from the engine are forbidden |
| **Scene Management / Flow Controller** | `depth_tier`, `scenes_played`, `is_available`, `closed_off` | `scenes_played` | `RecordScenePlayed(charId, nodeName)` | Reads to build available scene list; records scene completion on exit |
| **Save/Load System** | Full `CharacterState` for all characters | Full `CharacterState` for all characters (restore path) | `RestoreState(charId, CharacterState)` bulk restore — bypasses field-level guards | Bypass is correct: restoring a previously valid state, not making new writes. Field guard double-firing on restore would corrupt invariant enforcement. |
| **The Dig (Depth Gate)** | `depth_tier` | `closed_off` | `SetClosedOff(charId, value)` | Only system that writes `closed_off`. Also writes it back to `false` on recovery. |
| **Secret Reveal System** | `depth_tier`, `secret_revealed` | `secret_revealed` | `SetSecretRevealed(charId, true)` | Cannot write `false` at depth 5 (Rule 15) |
| **Signal Reading Mechanic** | `narrative_flags` (specific per-scene keys) | None | — | Read-only consumer of flags authored in `.yarn` scripts |
| **Character Roster / Availability** | `is_available`, `depth_tier`, `closed_off` | `is_available`, `availability_lock_reason` | `SetAvailable(charId, value, reason)` | Reads to build roster display; sets availability on arc status change |

## Formulas

The Character State Manager contains no formulas. It is a data store — it stores values, it does not compute them.

The following downstream GDDs define formulas whose inputs or outputs are CSM fields:

| Formula | Defined in | CSM fields involved |
|---|---|---|
| Depth tier advancement threshold | Relationship Depth System GDD *(not yet authored)* | `depth_tier` (output) |
| Closed-off recovery condition | The Dig GDD *(not yet authored)* | `closed_off` (output) |
| Secret arc unlock gate | Secret Reveal System GDD *(not yet authored)* | `depth_tier` (input), `secret_revealed` (output) |
| Scene availability logic | Scene Management GDD *(not yet authored)* | `depth_tier`, `closed_off`, `scenes_played` (inputs) |

Any numeric threshold or calculation involving CSM fields is defined in the GDD for the system that owns the write, not here.

## Edge Cases

### Concurrent Writes

- **If two systems write the same field for the same character in the same frame**: The last write wins. All writes are synchronous and in-memory; the result is deterministic based on call order. The write ownership contract (Rule 10) is what prevents this — if it happens anyway, the `[WARNING]` logs are the audit trail.
- **If `SetDepthTier(charId, 5)` and `SetSecretRevealed(charId, true)` are called in the same call stack in either order**: No conflict. Rule 14's auto-set is skipped if `secret_revealed` is already true. No duplicate notification fires — the caller is responsible for `notify_state_change` per Rule 11.

### Save/Load Invariant Violations (Corrupt Save)

- **If `RestoreState` receives a record with `depth_tier == 5` and `secret_revealed == false`**: The CSM logs `[ERROR]`, sets `secret_revealed = true`, and commits the repaired record. This is the only case where `RestoreState` modifies an incoming record before storing it.
- **If `RestoreState` receives a record with `depth_tier` outside [0, 5]** (e.g., 7 from a hand-edited save): Logs `[ERROR]`, clamps to 5, commits. Severity is escalated from `[WARNING]` (live write) to `[ERROR]` (restore) because save data should never contain an invalid range — this indicates tampering or a serialization bug.
- **If `RestoreState` receives `secret_revealed == true` with `depth_tier < 5`**: Valid. The invariant is one-directional — depth 5 requires revealed, but revealed does not require depth 5. Stored as-is, no log.
- **If `RestoreState` receives `closed_off == true` at any `depth_tier`**: Valid. Stored as-is.

### Cut Characters (Roster Removed Mid-Development)

- **If `RestoreState` is called for a `character_id` not in the current roster asset**: The CSM logs `[WARNING]` and places the record in a quarantine map — separate from the live state map, but re-serialized back to the save file to prevent data loss. `GetState` calls for that ID follow Rule 4 (unregistered → null-safe read-only default). The character does not appear in scene or roster lookups, which consult the roster asset, not the CSM.
- **If a quarantined character is re-added to the roster**: The CSM detects the match in the quarantine map, promotes it to the live state map instead of initializing fresh defaults, and logs `[INFO]`. Player progress with a re-released character is preserved.

### `narrative_flags` Growth

- **If `narrative_flags` accumulates hundreds of entries across a long playthrough**: No action is taken. All entries serialize with the save record. The practical upper bound is authoring-constrained (one flag per boolean branch in `.yarn` scripts). At 30-character scale, memory and serialization cost is negligible. If this becomes measurable at full release scale, the mitigation is a flag archival pass in Save/Load, not the CSM.
- **If `SetFlag(charId, "", value)` or `SetFlag(charId, null, value)` is called**: The CSM logs `[WARNING]` and drops the write. An empty or null key would create an invisible, unreachable flag.

### `scenes_played` Duplicate

- **If `RecordScenePlayed(charId, sceneNodeName)` is called for a node already in the set**: `HashSet<string>.Add()` silently ignores the duplicate. No log, no error. Scene Management may call this defensively on every scene exit without a pre-check.

### Idempotent Writes

- **If `SetDepthTier(charId, N)` is called when `depth_tier` is already N**: The write completes normally. The CSM does not detect or suppress same-value writes. The Relationship Depth System GDD must specify whether it guards against same-value writes before calling `notify_state_change` — a same-value write followed by a notify would produce a spurious downstream notification.
- **If `SetDepthTier(charId, 5)` is called when `depth_tier` is already 5 and `secret_revealed` is already true**: Rule 14 runs, sees `secret_revealed` is already true, skips the auto-set. Write completes normally.

### Rapid Session Reload

- **If `RestoreState` is called for a character whose record already exists in the live state map** (e.g., double initialization, slot switch): The incoming record overwrites the existing one wholesale. No merge. The CSM logs `[WARNING] RestoreState called for {charId} which already has a live state record. Overwriting.` to surface the double-load as a potential initialization bug. If intentional (slot switch), the warning is expected and ignorable.

### Depth Tier / `secret_revealed` Invariant Boundaries

- **If `SetDepthTier(charId, 4)` is called when `depth_tier` is 5**: Accepted. `depth_tier` becomes 4. `secret_revealed` remains true — the CSM does not revert reveals on backward depth movement. Whether backward depth movement is permitted is a Relationship Depth System design question. This state (depth 4, already revealed) is valid to store.
- **If `SetDepthTier(charId, 5)` is called when `closed_off` is true**: Accepted. Both fields are stored. `closed_off == true` at depth 5 is valid to store; whether it is narratively reachable is the Relationship Depth and Dig systems' concern.
- **If `SetSecretRevealed(charId, true)` is called when `depth_tier` is 0**: Accepted. The one-directional invariant is not violated. Whether this is narratively valid is the Secret Reveal System's concern.
- **If `SetSecretRevealed(charId, false)` is called when `depth_tier` is 4**: Accepted. Rule 15 only blocks this at depth 5. The CSM logs nothing.
- **If `SetSecretRevealed(charId, false)` is called when `depth_tier` is 5**: Rejected. Logs `[WARNING] SetSecretRevealed(false) rejected for {charId}: depth_tier is 5. Revealed state is permanent at this depth.` State unchanged.

## Dependencies

| System | Direction | Nature | Interface |
|---|---|---|---|
| **Relationship Depth System** | Downstream (dependent) | Hard — cannot function without CSM | Reads `depth_tier`, `closed_off`; writes `depth_tier` via `SetDepthTier()` |
| **Dialogue/Narrative Engine** | Downstream (dependent) | Hard — all state reads during scenes route through here | Reads/writes all fields via `MasksVariableStorage`; direct CSM calls from engine are forbidden |
| **Scene Management / Flow Controller** | Downstream (dependent) | Hard — scene availability is derived from state | Reads `depth_tier`, `scenes_played`, `is_available`, `closed_off`; writes `scenes_played` via `RecordScenePlayed()` |
| **Save/Load System** | Downstream (dependent) | Hard — state is meaningless without persistence | Serializes full state store; restores via `RestoreState()` bulk method |
| **The Dig (Depth Gate)** | Downstream (dependent) | Hard — `closed_off` flag is this system's sole output | Reads `depth_tier`; writes `closed_off` via `SetClosedOff()` |
| **Secret Reveal System** | Downstream (dependent) | Hard — reveal state tracked here | Reads `depth_tier`, `secret_revealed`; writes `secret_revealed` via `SetSecretRevealed()` |
| **Signal Reading Mechanic** | Downstream (dependent) | Soft — reads flags for display logic | Reads `narrative_flags` (specific per-scene keys); no writes |
| **Character Roster / Availability** | Downstream (dependent) | Soft — availability display derived from state | Reads `is_available`, `depth_tier`, `closed_off`; writes `is_available` via `SetAvailable()` |

The CSM has no upstream dependencies. It is a Foundation layer system — it does not read from any other system to do its job. All data enters the CSM via writes from downstream systems; all data leaves via reads from downstream systems.

## Tuning Knobs

The CSM has no balance formulas and therefore no numeric tuning knobs of the kind found in gameplay systems. The knobs here are structural parameters that control runtime and production behavior.

| Knob | Current Value | Safe Range | Effect of Too High | Effect of Too Low | Notes |
|---|---|---|---|---|---|
| `MAX_DEPTH_TIER` | 5 | [3, 7] | More tiers = more content required per character; writers must author additional tier-gated scenes | Fewer tiers = shallower relationship arcs; secrets surface too quickly | Hard-coded constant. Changing it requires updating every downstream GDD that references [0–5]. A save migration is needed for saves created under a different value. |
| `MAX_CHARACTERS` | 30 (full vision) / 5 (MVP) | [3, 50] | Memory and serialization cost grows linearly but stays negligible through 50 characters | Below 3 the core multi-character premise collapses | Not enforced by the CSM at runtime — the roster asset determines how many characters exist. This is a design cap, not a technical one. |
| `NARRATIVE_FLAG_KEY_MAX_LENGTH` | Unlimited (no enforcement) | [32, 128] characters | Long keys increase dictionary serialization size marginally | Very short keys risk naming collisions in the flag bag | Currently unenforced. If Save/Load profiling reveals serialization cost at scale, a key length cap can be added as a `[WARNING]` guard in `SetFlag()` without a schema migration. |

**Downstream knobs that feed into CSM behavior (owned by other GDDs):**
- Depth tier advancement thresholds — owned by Relationship Depth System GDD
- Closed-off recovery conditions — owned by The Dig GDD
- Scene availability unlock gates — owned by Scene Management GDD

## Visual/Audio Requirements

None. The CSM has no visual or audio output. State changes fire `MasksEvents.StateChanged` — the Audio System and UI System subscribe to those events and own all reactive output. Visual and audio specifications for state-change moments belong in those systems' GDDs.

## UI Requirements

None. The CSM has no UI surface. UI elements that reflect CSM state (depth meter, availability indicators, closed-off visual treatment) are owned by the UI System GDD, which reads CSM data via the same `GetState()` interface as any other system.

## Acceptance Criteria

> **Test Classification Legend**
> - Logic/BLOCKING — automated NUnit unit test (Edit Mode), must pass before story is Done
> - Integration/BLOCKING — automated Play Mode test or documented playtest, must pass before Done
> - No Manual/ADVISORY criteria — the CSM has no visual, audio, or feel surface; all behavior is data-testable

### CSM-REG: Registration

| ID | GIVEN | WHEN | THEN | Type |
|---|---|---|---|---|
| CSM-REG-01 | A roster defines `["ren", "jun", "sol"]` and no save data exists | Session start initializes the CSM | All three characters are in the live state map with schema defaults: `depth_tier=0`, `closed_off=false`, `secret_revealed=false`, `narrative_flags={}`, `scenes_played={}`, `is_available=true`, `availability_lock_reason=""` | Logic/BLOCKING |
| CSM-REG-02 | `"ren"` is already registered with `depth_tier=3` | `RegisterCharacter("ren")` is called again | Existing record is not overwritten; `depth_tier` remains 3; no ERROR | Logic/BLOCKING |
| CSM-REG-03 | A fresh session starts with a 30-character roster | All 30 characters are eager-loaded | All 30 records are in memory and individually readable before any scene begins; no async gate is required | Logic/BLOCKING |
| CSM-REG-04 | A 30-character roster is initialized (performance gate) | All 30 characters are registered with defaults | Completes in ≤10ms (Stopwatch, Edit Mode) | Logic/BLOCKING |

### CSM-READ: Reads

| ID | GIVEN | WHEN | THEN | Type |
|---|---|---|---|---|
| CSM-READ-01 | `"ren"` is registered with `depth_tier=3` | `GetState("ren")` is called | Returns the live record with `depth_tier=3`; no async, no null | Logic/BLOCKING |
| CSM-READ-02 | `"ren"` is registered | `GetState("ren")` is called mid-scene dialogue tick | Returns synchronously; no yield, no callback | Logic/BLOCKING |
| CSM-READ-03 | `"ghost"` has never been registered | `GetState("ghost")` is called | Returns read-only null-safe default; `[ERROR]` logged; no exception thrown | Logic/BLOCKING |
| CSM-READ-04 | `"surface_01_gave_advice"` is absent from `"ren"`'s `narrative_flags` | The flag is read | Returns `false`; the key is NOT added to the dictionary | Logic/BLOCKING |
| CSM-READ-05 | `"ren"` is registered (performance gate) | `GetState("ren")` is called in isolation | Completes in ≤0.1ms (Stopwatch, Edit Mode) | Logic/BLOCKING |

### CSM-WR: Writes — Typed Methods

| ID | GIVEN | WHEN | THEN | Type |
|---|---|---|---|---|
| CSM-WR-01 | `"ren"` has `depth_tier=2` | `SetDepthTier("ren", 3)` | `depth_tier=3`; `secret_revealed` unchanged; no log | Logic/BLOCKING |
| CSM-WR-02 | `"ren"` has `depth_tier=2`, `secret_revealed=false` | `SetDepthTier("ren", 5)` | `depth_tier=5`; `secret_revealed` auto-set to `true`; `[INFO]` log mentions "ren" | Logic/BLOCKING |
| CSM-WR-03 | `"ren"` has `depth_tier=5`, `secret_revealed=true` | `SetDepthTier("ren", 5)` again | Write completes; auto-set skipped (already true); no duplicate log | Logic/BLOCKING |
| CSM-WR-04 | `"ren"` has `depth_tier=2` | `SetDepthTier("ren", 2)` (same value) | Write completes normally; no suppression, no ERROR | Logic/BLOCKING |
| CSM-WR-05 | `"ren"` is registered | `SetDepthTier("ren", 7)` | Clamped to 5; `[WARNING]` logged with attempted value 7 | Logic/BLOCKING |
| CSM-WR-06 | `"ren"` is registered | `SetDepthTier("ren", -1)` | Clamped to 0; `[WARNING]` logged with attempted value -1 | Logic/BLOCKING |
| CSM-WR-07 | `"ren"` has `closed_off=false` | `SetClosedOff("ren", true)` | `closed_off=true`; no other field changed | Logic/BLOCKING |
| CSM-WR-08 | `"ren"` has `closed_off=true` | `SetClosedOff("ren", false)` (recovery) | `closed_off=false`; `depth_tier` unchanged | Logic/BLOCKING |
| CSM-WR-09 | `"ren"` has `depth_tier=3`, `secret_revealed=false` | `SetSecretRevealed("ren", true)` | `secret_revealed=true`; `depth_tier` unchanged | Logic/BLOCKING |
| CSM-WR-10 | `"ren"` has `depth_tier=5`, `secret_revealed=true` | `SetSecretRevealed("ren", false)` | Rejected; state unchanged; `[WARNING]` logged | Logic/BLOCKING |
| CSM-WR-11 | `"ren"` has `depth_tier=4`, `secret_revealed=true` | `SetSecretRevealed("ren", false)` | Accepted (Rule 15 only blocks at depth 5); `secret_revealed=false`; no log | Logic/BLOCKING |
| CSM-WR-12 | `"ren"` has `depth_tier=0`, `secret_revealed=false` | `SetSecretRevealed("ren", true)` | Accepted; `secret_revealed=true`; `depth_tier` remains 0 | Logic/BLOCKING |
| CSM-WR-13 | `"ren"` has `is_available=true` | `SetAvailable("ren", false, "arc_complete")` | `is_available=false`; `availability_lock_reason="arc_complete"` | Logic/BLOCKING |
| CSM-WR-14 | `"ren"` has `is_available=false` | `SetAvailable("ren", true, "")` | `is_available=true`; `availability_lock_reason=""` | Logic/BLOCKING |

### CSM-WR: Writes — Open Fields

| ID | GIVEN | WHEN | THEN | Type |
|---|---|---|---|---|
| CSM-WR-15 | `"ren"` has empty `narrative_flags` | `SetFlag("ren", "surface_01_gave_advice", true)` | `narrative_flags["surface_01_gave_advice"]=true`; dictionary has exactly one entry | Logic/BLOCKING |
| CSM-WR-16 | `"ren"` has `narrative_flags["surface_01_gave_advice"]=true` | `SetFlag("ren", "surface_01_gave_advice", false)` | `narrative_flags["surface_01_gave_advice"]=false`; no error | Logic/BLOCKING |
| CSM-WR-17 | `"ren"` is registered | `SetFlag("ren", "", true)` | Write dropped; `[WARNING]` logged; `narrative_flags` unchanged | Logic/BLOCKING |
| CSM-WR-18 | `"ren"` is registered | `SetFlag("ren", null, true)` | Write dropped; `[WARNING]` logged; no NullReferenceException | Logic/BLOCKING |
| CSM-WR-19 | `"ren"` has empty `scenes_played` | `RecordScenePlayed("ren", "surface_01")` | `scenes_played` contains `"surface_01"` | Logic/BLOCKING |
| CSM-WR-20 | `"ren"` has `scenes_played` containing `"surface_01"` | `RecordScenePlayed("ren", "surface_01")` again | `scenes_played` still has exactly one `"surface_01"` entry; no log, no error | Logic/BLOCKING |

### CSM-UNREG: Unregistered Character Writes

| ID | GIVEN | WHEN | THEN | Type |
|---|---|---|---|---|
| CSM-UNREG-01 | `"ghost"` is not registered | `SetDepthTier("ghost", 3)` | Write dropped; `[WARNING]` logged; no exception | Logic/BLOCKING |
| CSM-UNREG-02 | `"ghost"` is not registered | `SetFlag("ghost", "some_flag", true)` | Write dropped; `[WARNING]` logged | Logic/BLOCKING |
| CSM-UNREG-03 | `"ghost"` is not registered | `RecordScenePlayed("ghost", "surface_01")` | Write dropped; `[WARNING]` logged | Logic/BLOCKING |
| CSM-UNREG-04 | `"ghost"` is not registered | `GetState("ghost")` fields are read from the returned default | All reads return schema defaults; no exception | Logic/BLOCKING |

### CSM-INV: Invariants

| ID | GIVEN | WHEN | THEN | Type |
|---|---|---|---|---|
| CSM-INV-01 | `"ren"` has `depth_tier=4`, `secret_revealed=false` | `SetDepthTier("ren", 5)` | `depth_tier=5`, `secret_revealed=true`; `[INFO]` log confirms auto-set | Logic/BLOCKING |
| CSM-INV-02 | `"ren"` has `depth_tier=5`, `secret_revealed=true` | `SetDepthTier("ren", 5)` again | Write completes; auto-set skipped; no duplicate log | Logic/BLOCKING |
| CSM-INV-03 | `"ren"` has `depth_tier=5`, `secret_revealed=true` | `SetSecretRevealed("ren", false)` | Rejected; state unchanged; `[WARNING]` logged | Logic/BLOCKING |
| CSM-INV-04 | `"ren"` has `depth_tier=5`, `secret_revealed=true` | `SetDepthTier("ren", 4)` (backward) | `depth_tier=4`; `secret_revealed` remains `true`; no log | Logic/BLOCKING |
| CSM-INV-05 | `"ren"` has `depth_tier=5`, `closed_off=false` | `SetClosedOff("ren", true)` | `closed_off=true`; `depth_tier` unchanged; write accepted | Logic/BLOCKING |

### CSM-RESTORE: Save/Load RestoreState

| ID | GIVEN | WHEN | THEN | Type |
|---|---|---|---|---|
| CSM-RESTORE-01 | CSM has fresh defaults for `"ren"` | `RestoreState("ren", record{depth_tier=3, closed_off=true})` | `GetState("ren")` reflects restored values; no clamp or invariant re-firing on valid data | Logic/BLOCKING |
| CSM-RESTORE-02 | `"ren"` is in the live state map with `depth_tier=2` | `RestoreState("ren", record{depth_tier=4})` (double-load) | Existing record overwritten wholesale; `[WARNING]` logged | Logic/BLOCKING |
| CSM-RESTORE-03 | Save contains `"ren"` with `depth_tier=5`, `secret_revealed=false` (corrupt) | `RestoreState("ren", corruptRecord)` | `[ERROR]` logged; `secret_revealed` auto-repaired to `true`; `GetState("ren").secret_revealed=true` | Logic/BLOCKING |
| CSM-RESTORE-04 | Save contains `depth_tier=7` for `"ren"` | `RestoreState("ren", outOfRangeRecord)` | `[ERROR]` logged; `depth_tier` clamped to 5; repaired record committed | Logic/BLOCKING |
| CSM-RESTORE-05 | Save contains `depth_tier=-2` for `"ren"` | `RestoreState("ren", negativeRecord)` | `[ERROR]` logged; `depth_tier` clamped to 0; repaired record committed | Logic/BLOCKING |
| CSM-RESTORE-06 | Save contains `secret_revealed=true` with `depth_tier=2` | `RestoreState("ren", record)` | Stored as-is; no log; one-directional invariant not violated | Logic/BLOCKING |
| CSM-RESTORE-07 | Save contains `closed_off=true` at `depth_tier=1` | `RestoreState("ren", record)` | Stored as-is; no log | Logic/BLOCKING |
| CSM-RESTORE-08 | 30-character save exists | `RestoreState` called for all 30 characters | All 30 restored and readable; completes in ≤10ms | Integration/BLOCKING |

### CSM-QUAR: Cut Characters

| ID | GIVEN | WHEN | THEN | Type |
|---|---|---|---|---|
| CSM-QUAR-01 | Save contains `"dev_char"` but roster does not include it | `RestoreState("dev_char", record)` | Record placed in quarantine map; `[WARNING]` logged; not in live state map | Logic/BLOCKING |
| CSM-QUAR-02 | `"dev_char"` is in the quarantine map | `GetState("dev_char")` is called | Returns null-safe default; quarantined record not surfaced | Logic/BLOCKING |
| CSM-QUAR-03 | `"dev_char"` is in the quarantine map | Save file is serialized | `"dev_char"`'s record is included in the serialized output; no data loss | Integration/BLOCKING |
| CSM-QUAR-04 | `"dev_char"` is in the quarantine map with `depth_tier=3` | `"dev_char"` is re-added to the roster and a new session starts | Quarantined record promoted to live map with `depth_tier=3` preserved; `[INFO]` logged | Integration/BLOCKING |

### CSM-NOTIF: No-Notification Contract

| ID | GIVEN | WHEN | THEN | Type |
|---|---|---|---|---|
| CSM-NOTIF-01 | A subscriber is attached to `MasksEvents.StateChanged` | Any CSM write method is called | The subscriber is NOT called from within the CSM write path; no event fires from the CSM itself | Logic/BLOCKING |

### CSM-PERF: Performance

| ID | GIVEN | WHEN | THEN | Type |
|---|---|---|---|---|
| CSM-PERF-01 | 30 characters are registered | `GetState(charId)` called for one character in isolation | Completes in ≤0.1ms (Stopwatch, Edit Mode) | Logic/BLOCKING |
| CSM-PERF-02 | 30-character roster, no save data | Full session initialization (all 30 registered with defaults) | Completes in ≤10ms total | Logic/BLOCKING |
| CSM-PERF-03 | 30-character valid save | `RestoreState` called 30 times in sequence | Completes in ≤10ms total | Logic/BLOCKING |

### CSM-INT: Multi-System Integration

| ID | GIVEN | WHEN | THEN | Type |
|---|---|---|---|---|
| CSM-INT-01 | `MasksVariableStorage` is wired to the CSM; `"ren"` is registered | A `.yarn` script sets `$char_ren_surface_01_gave_advice = true` | `GetState("ren").narrative_flags["surface_01_gave_advice"]` is `true`; write completes synchronously within the dialogue tick | Integration/BLOCKING |
| CSM-INT-02 | `"ren"` is registered; Scene Management records scene exit | Scene `"surface_01"` exits after playing | `GetState("ren").scenes_played` contains `"surface_01"`; a second exit does not double-count | Integration/BLOCKING |
| CSM-INT-03 | A full character arc plays: register → scenes → depth advance → Dig → recovery → depth 5 | State is saved, session closed, and re-opened | All fields restore to pre-close values; `depth_tier`, `closed_off`, `secret_revealed`, `narrative_flags`, `scenes_played` all round-trip correctly | Integration/BLOCKING |

## Open Questions

All design questions raised during authoring were resolved. The following items were closed:

- **Schema field set** — Resolved: 8 fields defined. `narrative_flags` uses open Dictionary<string,bool> bag; system fields are fixed typed.
- **Lazy vs. eager loading** — Resolved: eager loading at session start. 30 characters is trivially small in memory.
- **Write ownership enforcement** — Resolved: convention + code review. No technical primitive available in Unity C#.
- **RestoreState invariant handling** — Resolved: bypass field guards on restore; repair corrupt invariants (depth_tier==5 + secret_revealed==false) with ERROR log.
- **Cut character handling** — Resolved: quarantine map — data preserved in save, excluded from live lookups.
- **scenes_played ownership** — Resolved: Scene Management owns writes. Dialogue Engine is a pure script runner; it does not record its own completions.

**Engineering items for implementation (not design questions):**
- Save/Load GDD (design order #3) must confirm the `RestoreState` bulk bypass approach is compatible with its serialization strategy.
- Backward `depth_tier` movement (e.g., `SetDepthTier(4)` after reaching 5) is technically accepted by the CSM — the Relationship Depth System GDD must specify whether this is reachable in normal gameplay and how it should be handled at that level.
