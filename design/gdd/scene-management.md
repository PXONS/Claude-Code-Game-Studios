# Scene Management / Flow Controller

> **Status**: Complete
> **Author**: Design session + agents
> **Last Updated**: 2026-05-03
> **Implements Pillar**: Pillar 2 (You Choose How Deep), Pillar 1 (Every Face Hides a Face)

## Overview

The Scene Management / Flow Controller is the sequencing layer that governs every scene transition in Masks. It decides which scene to run next, loads the required assets, injects the player's identity into the dialogue engine, starts execution, handles completion, and signals the downstream systems that need to respond. Every moment of content the player sees passes through this system — it is the single point of authority between the player's choices and the dialogue engine's execution.

From the player's perspective, Scene Management is invisible. What they experience is a character select screen that shows the right options at the right time: scenes they haven't played, scenes they're eligible for at their current depth tier, the Dig available when they've earned it. The system enforces the access rules of Pillar 2 (You Choose How Deep) — the player controls pacing, but the system enforces the preconditions that make depth meaningful. A scene at tier 3 does not appear until the relationship has earned it. The player never sees the gate; they only experience the sense that the game is keeping up with them.

Mechanically, Scene Management owns three responsibilities: scene availability (which scenes can a player access right now, for which character), scene execution (the full StartScene → SceneComplete → EndScene lifecycle), and scene accounting (recording completion, signaling the Relationship Depth System, triggering saves). It does not generate content, author scenes, or make creative decisions — it sequences, tracks, and reports.

## Player Fantasy

You open the character select screen and feel it before you choose: a pull, quiet but specific, toward one face among many. You click, and the scene that opens is exactly the one you didn't know you were ready for — not a door you've already walked through, not one locked against you, but the next room in a house you've been slowly learning. The game doesn't ask you to remember where you left off with them. It remembers for you, and it has been waiting.

This is Scene Management working correctly: no friction between wanting and having. The friction belongs in the relationships — in the gap before the right word, in the moment before someone decides whether to tell you something true. It does not belong in the act of reaching them. When the system is invisible, the player is thinking about the character. When it surfaces — a wrong scene appearing, a scene locked without reason, the game failing to find them where they left off — the spell breaks, and what was intimacy becomes navigation.

**Design north star**: Friction belongs in the relationships, not in reaching them.

## Detailed Design

### Core Rules

**The Scene Registry**

1. The Scene Registry is a ScriptableObject asset authored by narrative designers in the Unity Editor. It is a flat list of Scene Registry Entries. Each entry has exactly four required fields:
   - `sceneId` (string) — must exactly match the Yarn scene node name passed to `DialogueEngineController.StartScene()`. Must be globally unique across the entire registry; no two entries may share a sceneId regardless of character.
   - `charId` (string) — must match a character_id registered in CharacterStateManager.
   - `tier` (int, [0, 4]) — the relationship depth tier at which this scene is available. No scenes are authored for tier 5; tier 5 is a terminal state reached only through The Dig.
   - `engagementType` — one of `CORE`, `DEPTH`, `AMBIENT`, `RECOVERY`, `ACTIVITY`. ACTIVITY was added by scene-activity-loop.md (design order #9) and must be accepted as a valid value by the startup validator. ACTIVITY does not count toward GATE_READY threshold but participates in the full COMPLETING accounting chain.

2. The Scene Registry is a pure data asset. No runtime state is stored on the ScriptableObject. Scene Management reads from it; nothing writes to it. The asset is loaded at session start as a pre-loaded persistent reference (not via Addressables) and is available synchronously throughout the session.

3. A Scene Registry Entry is invalid if: `sceneId` is empty, `charId` is not registered in CSM, `tier` is outside [0, 4], or `engagementType` is not a valid enum value. Invalid entries are excluded from availability at startup with an `[ERROR]` log. The system does not crash on invalid entries.

**Scene Availability**

4. Scene Management exposes a single availability query: `GetAvailableScenes(charId)`. This is called on demand only — when the UI opens the scene select for a character. It is not polled per frame. The UI caches the result and uses it until a scene completes or the scene select is closed.

5. `GetAvailableScenes(charId)` computes availability live from current CSM state on every call. No pre-computed availability list is maintained between calls. The computation:
   - Query `CSM.GetState(charId)` → `CharacterState`
   - If `state.is_available == false` → return empty list (character quarantined)
   - If `state.closed_off == true` → return all registry entries where `charId` matches AND `engagementType == RECOVERY` AND `tier == state.depth_tier`. If none exist → return empty list and log `[ERROR] No recovery scene for {charId} at tier {depth_tier} — character is stuck`
   - Otherwise (normal state) → return all registry entries where `charId` matches AND `tier == state.depth_tier` AND `sceneId NOT IN state.scenes_played`
   - If `charId` is in the GATE_READY set (Rule 11) → append one synthetic `DigEntry` to the end of the result

6. **Scenes from earlier tiers are not available for replay.** Once a sceneId is in `scenes_played`, it never returns to any character's available scene list. This is a deliberate design choice: scenes in Masks are unrepeatable moments. A player who did not play a tier-1 AMBIENT scene cannot access it at tier 2.

7. **Tier-5 terminal state.** When `state.depth_tier == 5` (MAX_DEPTH_TIER), `GetAvailableScenes()` returns an empty list. No scenes are registered for tier 5; no `DigEntry` is injected. The character is complete. They may appear in the UI as a completed portrait but have no playable scenes.

8. **Narrative flag prerequisites are not evaluated by the Scene Registry or `GetAvailableScenes()`.** All flag-conditional scene access (e.g., a scene that requires a specific prior choice) is handled inside the Yarn script at runtime. The Scene Registry does not support prerequisite fields.

**The DigEntry**

9. `GetAvailableScenes()` returns a list of `SceneListEntry` items. Each item is one of:
   - `SceneEntry` — backed by a Scene Registry entry; carries `sceneId`, `charId`, `tier`, `engagementType`.
   - `DigEntry` — synthetic; not backed by the Scene Registry. Carries `charId` and reserved identifier `"dig_available"`. Has no `tier` or `engagementType`. Always appears as the last item in the list when present.

10. `StartScene()` accepts either type. When passed a `DigEntry`, Scene Management hands off to The Dig subsystem (#3) rather than the Dialogue Engine.

**GATE_READY and DigEntry Injection**

11. Scene Management maintains an in-memory set: `gateReadyCharacters`. It subscribes to `RDS.OnGateReady` at initialization. When RDS fires `OnGateReady(charId)`, Scene Management adds `charId` to `gateReadyCharacters`. The next `GetAvailableScenes(charId)` call will append a `DigEntry`.

12. Gate state is not persisted. On session load, Scene Management calls `RDS.IsDigAvailable(charId)` for each registered character to re-populate `gateReadyCharacters` before the first scene select is accessible.

13. When a character becomes `closed_off` (DIG_FAIL), `charId` is removed from `gateReadyCharacters`. When `closed_off` is cleared by a recovery arc, RDS re-evaluates and may re-fire `OnGateReady(charId)` if the threshold was already met before close-off.

**Scene Lifecycle — State Machine**

14. Scene Management is always in one of five states:

| State | Description | Entry Condition |
|---|---|---|
| IDLE | No scene active. Scene select accessible. | Session start; scene complete; scene abandoned; load failure |
| LOADING | Re-validating availability; verifying player profile; preparing engine handoff | `StartScene(entry)` called |
| RUNNING | Dialogue Engine or Dig subsystem has control. No other scene may start. | Handoff complete |
| COMPLETING | Post-scene accounting: RecordScenePlayed → save trigger | `DialogueStopped(CLEAN_COMPLETION)` received |
| ERROR | Unrecoverable failure. UI notified. | LOADING failure; COMPLETING exception |

15. Valid transitions:
   - IDLE → LOADING: UI calls `StartScene(entry)` with an entry from a prior `GetAvailableScenes()` call
   - LOADING → RUNNING: all preconditions pass; engine or Dig subsystem invoked
   - LOADING → IDLE: availability re-validation fails (scene no longer valid); `[WARNING]` logged; UI refreshed
   - RUNNING → COMPLETING: `DialogueStopped(CLEAN_COMPLETION)` received
   - RUNNING → IDLE: `DialogueStopped(ABANDONED)` received — scene not recorded
   - COMPLETING → IDLE: all accounting complete; UI signaled
   - Any → ERROR: unrecoverable failure

16. `StartScene()` called while in LOADING, RUNNING, or COMPLETING state is a no-op with a `[WARNING]` log. Two scenes cannot run simultaneously.

17. The scene select screen is only accessible when Scene Management is in IDLE state. Scene Management signals the UI System on every IDLE transition.

**LOADING Phase — Preconditions**

18. Before handing off to the Dialogue Engine or Dig, Scene Management validates in order:
   1. `entry.sceneId` is still present in `GetAvailableScenes(charId)` (live re-check; protects against state changes between UI render and tap)
   2. `CSM.GetState(charId).is_available == true` (re-validated; quarantine may have been set after list render)
   3. Player Profile is loaded: `display_name` is non-empty and all four pronoun fields are populated. If not: `StartScene()` is aborted, Character Creator is surfaced, and Scene Management transitions to IDLE. MasksVariableStorage will silently return defaults without this check.
   - If any check fails → transition to IDLE, log appropriately, refresh UI.

**COMPLETING Phase — Accounting Order**

19. On `DialogueStopped(CLEAN_COMPLETION)`, Scene Management executes in strict order:
   1. Call `RDS.OnSceneCompleted(charId, sceneId, engagementType)` — RDS calls `CSM.RecordScenePlayed(charId, sceneId)` and evaluates the scene threshold. If GATE_READY fires, Scene Management updates `gateReadyCharacters`.
   2. Call `SaveManager.Save()`. If save fails: log `[ERROR]`, retry once. If retry fails: continue with correct in-memory state — the next save trigger (depth tier advance, session exit) will capture it.
   3. Transition to IDLE and signal the UI System.

20. On `DialogueStopped(ABANDONED)`: `RecordScenePlayed` is NOT called. No save is triggered. Transition directly to IDLE. The player can play the abandoned scene in a future session.

**Scene Management and Unity Scenes**

21. Scene Management operates within a single persistent Unity scene. It never calls `UnityEngine.SceneManagement.SceneManager.LoadScene()`. All content is delivered through the Dialogue Engine (Yarn nodes) within the persistent scene.

---

### States and Transitions

Covered in Core Rules 14–17. State diagram summary: `IDLE ↔ LOADING → RUNNING → COMPLETING → IDLE`. All states can transition to `ERROR`.

---

### Interactions with Other Systems

| System | Direction | Interface |
|---|---|---|
| Character State Manager (#6) | Reads | `GetState(charId)` — called in `GetAvailableScenes()` and LOADING validation. Scene Management never writes to CSM directly. |
| Dialogue/Narrative Engine (#8) | Triggers | `DialogueEngineController.StartScene(sceneId)` to begin a scene. Subscribes to `DialogueStopped(completionReason)` for completion notification. |
| Relationship Depth System (#1) | Bidirectional | **Inbound**: subscribes to `RDS.OnGateReady(charId)`. **Outbound**: calls `RDS.OnSceneCompleted(charId, sceneId, engagementType)` after each clean completion. Also calls `RDS.IsDigAvailable(charId)` on session load to restore gate state. |
| Save/Load System (#7) | Triggers | Calls `SaveManager.Save()` in COMPLETING phase. Scene exit is one of three save triggers. |
| Player Profile (#12) | Reads | Validates player profile is loaded before LOADING proceeds. Does not read profile data directly — MasksVariableStorage handles Yarn variable binding. |
| The Dig (#3) | Triggers | `StartScene(DigEntry)` hands off to The Dig subsystem. Dig returns `DIG_PASS / PARTIAL / FAIL / CRITICAL_FAIL`. Scene Management re-evaluates gate state via RDS accordingly. |
| UI System (#16) | Signals | Notifies UI of IDLE transitions (show scene select). `GetAvailableScenes(charId)` is the UI's population call. |
| Character Roster / Availability (#17) | Reads | Checks `is_available` per character via CSM to exclude quarantined characters. |

> ⚠️ **Cross-GDD flag**: The Dialogue Engine GDD specifies that `DialogueStopped` fires when "dialogue ends" without distinguishing clean completion from abandonment. This GDD requires `DialogueStopped` to carry a `completionReason` field (`CLEAN_COMPLETION` or `ABANDONED`). The Dialogue Engine GDD must be updated to add this field before implementation begins.

## Formulas

Scene Management contains no mathematical formulas. All computation is boolean: set membership (`sceneId NOT IN scenes_played`), equality checks (`tier == state.depth_tier`, `engagementType == RECOVERY`), and state comparisons (`is_available == true`, `closed_off == true`). These rules are fully specified in the Detailed Design section.

The availability filter is reproduced here for reference:

```
GetAvailableScenes(charId):
  state = CSM.GetState(charId)
  if NOT state.is_available → return []
  if state.closed_off:
    return Registry.Where(charId == charId AND engagementType == RECOVERY AND tier == state.depth_tier)
  scenes = Registry.Where(charId == charId AND tier == state.depth_tier AND sceneId NOT IN state.scenes_played)
  if charId IN gateReadyCharacters: scenes.Append(DigEntry(charId))
  return scenes
```

No arithmetic, no output ranges — this is a filter, not a formula.

## Edge Cases

### Registry Validation (fired at session startup)

**EC-1: Empty or null sceneId in registry entry**
Entry is excluded from the live registry with `[ERROR] Scene Registry: entry at index {N} has empty sceneId — excluded`. The system does not crash. An empty sceneId would reach `DialogueEngineController.StartScene("")` and fail silently mid-session — catching it at startup surfaces the defect before it can cause data loss.

**EC-2: Duplicate sceneId across registry entries**
During startup, a duplicate-detection pass runs after parsing. All entries in any duplicate group are excluded (not just the second): `[ERROR] Scene Registry: duplicate sceneId '{sceneId}' at indices {N, M} — all occurrences excluded`. Excluding all prevents an arbitrary authoring choice from being silently chosen as authoritative.

**EC-3: Registry entry charId not registered in CSM**
Entries whose `charId` is absent from CSM are excluded at startup: `[ERROR] Scene Registry: entry '{sceneId}' references unregistered charId '{charId}' — excluded`. Other characters' availability is unaffected.

**EC-4: Registry entry tier outside [0, 4]**
Entry excluded: `[ERROR] Scene Registry: entry '{sceneId}' has invalid tier {tier} (valid: 0–4) — excluded`. The system rejects rather than clamps — a tier-5 scene would never be reachable and silently registering it would mask an authoring error.

**EC-5: Registry contains zero valid entries after startup validation**
System initializes normally. All `GetAvailableScenes()` calls return empty lists. `[WARNING] Scene Registry: zero valid entries — no scenes will be available`. Not treated as an unrecoverable error; valid in early development with no content yet authored.

---

### Availability Filter

**EC-6: All scenes at current tier already played (non-closed-off)**
`GetAvailableScenes()` returns an empty list. No log — this is a valid end state. If the tier threshold was never reached because too few scenes were authored, RDS (not Scene Management) logs the content defect.

**EC-7: Tier-5 terminal state**
When `state.depth_tier == 5`, `GetAvailableScenes()` returns an empty list immediately — before the `closed_off` branch, before the registry filter, before any DigEntry check. This prevents the theoretically reachable state of `depth_tier == 5 AND closed_off == true` from routing to a recovery scene lookup that cannot return anything useful.

**EC-8: GetAvailableScenes called for a charId not registered in CSM**
`CSM.GetState(charId)` returns a null-safe default object with `is_available: true`, meaning the normal filter path would proceed incorrectly. Scene Management must check registration before calling `GetState()`. If `charId` is not registered: return empty list and log `[ERROR] GetAvailableScenes: charId '{charId}' is not registered in CSM`.

**EC-9: is_available toggled to false between list render and StartScene**
LOADING precondition check 2 catches this. `StartScene()` is aborted → IDLE → UI refresh → `[WARNING] StartScene aborted: '{charId}' is_available == false at validation time`.

**EC-10: Scene entry no longer available between list render and StartScene**
LOADING precondition check 1 catches this. `GetAvailableScenes(charId)` is re-run live; selected sceneId is verified present. If absent: IDLE → UI refresh → `[WARNING] StartScene aborted: '{sceneId}' no longer in available scene list at validation time`.

---

### State Machine

**EC-11: DialogueStopped fires while not in RUNNING state**
Scene Management checks current state at the top of the handler. If state ≠ RUNNING: `[WARNING] DialogueStopped received in state {currentState} — ignored`. No accounting runs. Prevents a spurious event from falsely recording a scene as played or triggering an erroneous save.

**EC-12: COMPLETING phase spans multiple frames**
The COMPLETING → IDLE transition fires only after both `RDS.OnSceneCompleted()` and `SaveManager.Save()` complete in order. If awaited across frames (Unity 6.3 `Awaitable`), the COMPLETING state acts as the mutex: `StartScene()` is blocked and any additional `DialogueStopped` events are discarded per EC-11.

---

### GATE_READY and DigEntry

**EC-13: GATE_READY fires while character is concurrently closed_off**
`gateReadyCharacters` stores `{charId → tier}`. The add fires unconditionally when `OnGateReady` fires. In `GetAvailableScenes()`, the DigEntry is appended only if `charId IN gateReadyCharacters AND gateReadyCharacters[charId] == state.depth_tier AND NOT state.closed_off`. The gate entry is preserved through the close-off period so that when recovery completes and `closed_off` is cleared, the DigEntry re-appears correctly.

**EC-14: DigEntry selected while character becomes closed_off between list render and StartScene**
LOADING adds a check specific to `DigEntry` requests: if `state.closed_off == true` at validation time, abort → IDLE → UI refresh → `[WARNING] DigEntry start aborted: '{charId}' became closed_off before Dig could begin`. The `is_available` check alone does not cover this.

**EC-15: OnGateReady fires after tier has already advanced (stale gate event)**
`gateReadyCharacters` is a `Dictionary<string charId, int tier>`. When `OnGateReady(charId)` fires, Scene Management stores `gateReadyCharacters[charId] = state.depth_tier` (queried from CSM at event-receipt time). In `GetAvailableScenes()`, the DigEntry is appended only if `gateReadyCharacters[charId] == state.depth_tier`. If a `DIG_PASS` advanced the tier in the same frame, the stored tier will not match, the DigEntry is suppressed, and the entry is removed: `[WARNING] Stale GATE_READY for '{charId}' at tier {stored} (current: {depth_tier}) — gate cleared`.

*This amends Core Rule 11: `gateReadyCharacters` is `Dictionary<string, int>` mapping charId to the tier at which GATE_READY was signaled, not a `HashSet<string>`.*

**EC-16: Session load — gate state must be re-populated before UI is accessible**
`RDS.IsDigAvailable(charId)` is called synchronously for all registered characters during session initialization, before Scene Management transitions to IDLE and before the character select screen is interactive. The loop must complete in full before the IDLE signal fires. This is a blocking sequential step, not deferred to a callback.

---

### Error Handling

**EC-17: RDS.OnSceneCompleted throws during COMPLETING**
Wrapped in `try/catch`. On exception: `[ERROR] RDS.OnSceneCompleted threw: {message} — scene '{sceneId}' for '{charId}' may not be recorded`. Do NOT call `SaveManager.Save()` — saving over a partially-committed state is worse than not saving. Transition to ERROR. Player is informed to reload.

**EC-18: Save fails after retry during COMPLETING**
`[ERROR] Save failed after retry following scene '{sceneId}' — in-memory state correct but not persisted`. Continue to IDLE — in-memory state is accurate. Do not enter ERROR for save failure alone. The next save trigger (tier advance, session exit) will persist the correct state.

**EC-19: Dig subsystem reference is null when DigEntry is started**
`[ERROR] DigEntry start failed: Dig subsystem reference is null for '{charId}'`. Transition to ERROR. A `DigEntry` has no `sceneId` and cannot fall back to the Dialogue Engine.

**EC-20: Yarn node named by sceneId does not exist in the YarnProject**
`DialogueEngineController.StartScene(sceneId)` is invoked but the node is missing. The Dialogue Engine raises `DialogueStopped` immediately with no scene having run. If `CLEAN_COMPLETION` is returned for this failure, Scene Management would falsely record the scene as played.

Resolution: on receipt of `NODE_NOT_FOUND` completion reason: do NOT call `RDS.OnSceneCompleted()`. Transition to ERROR. Signal UI.

> ⚠️ **Cross-GDD flag (second)**: Dialogue Engine GDD must add a `NODE_NOT_FOUND` completion reason to `DialogueStopped`, in addition to the already-flagged `CLEAN_COMPLETION / ABANDONED` distinction. Both flags must be resolved before implementation of either system begins.

**EC-21: scenes_played may contain retired sceneIds**
If a scene is played and its registry entry is later removed (content cut), the sceneId remains in `scenes_played` permanently. This is correct: a played scene must never re-appear. `GetAvailableScenes()` filters by `sceneId NOT IN scenes_played` regardless of whether the sceneId still has a registry entry. No action required; documented as an intentional invariant.

## Dependencies

### Upstream Dependencies (systems Scene Management depends on)

| System | Dependency Type | Interface Contract |
|---|---|---|
| Character State Manager (#6) | Hard — cannot function without it | `GetState(charId)` → `CharacterState` (synchronous). `IsRegistered(charId)` → bool. Scene Management never writes to CSM directly. |
| Dialogue/Narrative Engine (#8) | Hard — scene execution requires it | `DialogueEngineController.StartScene(sceneId)`. Subscribes to `DialogueStopped(completionReason)` event. completionReason must include: `CLEAN_COMPLETION`, `ABANDONED`, `NODE_NOT_FOUND`. *(See cross-GDD flags in Edge Cases.)* |
| Relationship Depth System (#1) | Hard — COMPLETING phase depends on it | `RDS.OnSceneCompleted(charId, sceneId, engagementType)`. `RDS.IsDigAvailable(charId)` → bool. Subscribes to `RDS.OnGateReady(charId, tier)`. |
| Save/Load System (#7) | Hard — persistence depends on it | `SaveManager.Save()` called after each CLEAN_COMPLETION. Returns success/failure indicator. |
| Player Profile (#12) | Hard — LOADING validation depends on it | Read access to `display_name` (non-empty check) and all four pronoun fields (non-empty check). Profile must be loaded before any scene starts. |

### Downstream Dependents (systems that depend on Scene Management)

| System | What it depends on |
|---|---|
| The Dig (#3) | Receives handoff when `StartScene(DigEntry)` is called. Returns a `DIG_PASS / PARTIAL / FAIL / CRITICAL_FAIL` outcome to Scene Management. Depends on Scene Management to enforce that Dig is only reachable when GATE_READY is active. |
| Scene & Activity Loop (#2) | Depends on `GetAvailableScenes(charId)` to populate scene lists and on IDLE-state signaling to know when scene select is accessible. |
| UI System (#16) | Depends on `GetAvailableScenes(charId)` for scene list population; on IDLE-state signals for when to show the scene select; on ERROR-state signals for error display. |
| Character Roster / Availability (#17) | Depends on Scene Management to enforce `is_available` at `GetAvailableScenes()` and LOADING time. Writes `is_available` to CSM; Scene Management reads it. |
| Activity System (#10) | Depends on scene lifecycle events (IDLE signal, scene completion) to know when activity transitions are valid. |

### Bidirectional Notes

- **Scene Management ↔ RDS**: Scene Management calls RDS on scene completion; RDS calls back on GATE_READY. Both directions are code-level events — no UnityEvent Inspector wiring.
- **Scene Management ↔ UI System**: Scene Management signals state transitions to the UI; UI queries availability on demand. Neither system holds a direct reference to the other's internal state.

## Tuning Knobs

| Knob | Type | Default | Safe Range | Effect if Too High | Effect if Too Low | Notes |
|---|---|---|---|---|---|---|
| `SCENE_LOADING_TIMEOUT_MS` | int | 2000 | [500, 10000] | Players wait unnecessarily long before load failure is surfaced | Legitimate slow asset loads abort prematurely | Maximum time Scene Management stays in LOADING before transitioning to ERROR. Only relevant if async asset loading is added to the LOADING phase in a future revision; currently LOADING is synchronous. |
| `SAVE_RETRY_COUNT` | int | 1 | [0, 3] | More retries extend COMPLETING → IDLE delay and block the player longer | 0 = no retry; one transient disk error loses scene progress | Number of `SaveManager.Save()` retry attempts after initial failure during COMPLETING. |
| `GATE_READY_STALE_LOG_LEVEL` | enum | WARNING | WARNING / ERROR / SILENT | ERROR floods logs for a recoverable state | SILENT hides a real data inconsistency | Log severity for EC-15 (stale GATE_READY event — gate tier does not match current depth_tier). WARNING during development; SILENT acceptable in release builds if proven not to cause user-visible issues. |

No scene pacing values are owned by this system — `SCENES_REQUIRED` thresholds are tuning knobs of the Relationship Depth System. Character roster caps are owned by the Character State Manager (`MAX_CHARACTERS`).

## Visual/Audio Requirements

None — Scene Management is a Core sequencing system with no visual or audio output of its own. Visual transitions (fade, scene entry animations) are owned by the UI System (#16). Audio reactions to scene completion (depth-tier stings, ambient music changes) are owned by the Audio System (#15), which subscribes to the same `MasksEvents.StateChanged` events fired by RDS.

## UI Requirements

Scene Management constrains the UI System in the following ways:
- The character/scene select screen must be rendered only when Scene Management is in IDLE state. Scene Management signals IDLE on every valid transition.
- The UI must not call `StartScene()` while Scene Management is in any non-IDLE state (UI is responsible for disabling scene selection inputs on non-IDLE signal).
- `GetAvailableScenes(charId)` is the UI's population call — the UI must not construct its own availability logic. The result is a `List<SceneListEntry>` containing `SceneEntry` and optionally a terminal `DigEntry`.
- The UI must handle an empty `GetAvailableScenes()` result gracefully for any character — rendering the character as having no available scenes without crashing.
- On ERROR state signal: the UI must surface an error state to the player. The specific error message copy is owned by the UI System GDD.

> **📌 UX Flag — Scene Management**: This system has UI constraints (IDLE/ERROR state rendering, scene list population). In Phase 4 (Pre-Production), run `/ux-design` to create a UX spec for the character select screen before writing epics. Stories referencing the character/scene select UI should cite `design/ux/character-select.md`, not this GDD.

## Acceptance Criteria

*Legend: **Logic** = NUnit Edit Mode / BLOCKING | **Integration** = Play Mode / BLOCKING | **Manual** = QA walkthrough / ADVISORY*

> **Testability requirement**: Scene Management must accept `IRdsAdapter` and `ISaveManager` interfaces via dependency injection. Without test doubles for these two interfaces, SM-050 (accounting order) and SM-052/SM-053/SM-054 (save failure paths) cannot be verified in automated tests. Expose a read-only `gateReadyCharacters` snapshot and an `OnIdleStateEntered` event for SM-065 and SM-041 respectively.

---

### Category 1: Scene Registry

| ID | Criterion | Classification |
|---|---|---|
| SM-001 | GIVEN a registry entry with an empty sceneId / WHEN startup validation runs / THEN the entry is excluded AND an `[ERROR]` log names the entry index AND no crash occurs | Logic / BLOCKING |
| SM-002 | GIVEN two registry entries sharing the same sceneId / WHEN the duplicate-detection pass runs / THEN both entries are excluded AND an `[ERROR]` log names the sceneId and both indices | Logic / BLOCKING |
| SM-003 | GIVEN a registry entry whose charId is not registered in CSM / WHEN startup validation runs / THEN that entry is excluded AND an `[ERROR]` names the sceneId and charId AND other entries are unaffected | Logic / BLOCKING |
| SM-004 | GIVEN a registry entry with tier -1 or tier 5 / WHEN startup validation runs / THEN the entry is excluded AND an `[ERROR]` is emitted AND the tier is not clamped | Logic / BLOCKING |
| SM-005 | GIVEN all registry entries fail validation / WHEN startup completes / THEN the system reaches IDLE normally AND a `[WARNING]` reports zero valid entries AND all GetAvailableScenes() calls return empty lists | Logic / BLOCKING |
| SM-006 | GIVEN a registry entry with an engagementType value not in the valid enum (CORE/DEPTH/AMBIENT/RECOVERY) / WHEN startup validation runs / THEN that entry is excluded AND an `[ERROR]` is logged AND other entries are unaffected | Logic / BLOCKING |

---

### Category 2: Scene Availability

| ID | Criterion | Classification |
|---|---|---|
| SM-010 | GIVEN a character with is_available == false / WHEN GetAvailableScenes(charId) is called / THEN an empty list is returned without consulting the registry | Logic / BLOCKING |
| SM-011 | GIVEN a character with depth_tier == 5 / WHEN GetAvailableScenes(charId) is called / THEN an empty list is returned regardless of closed_off status or registry contents | Logic / BLOCKING |
| SM-012 | GIVEN a charId not registered in CSM / WHEN GetAvailableScenes(charId) is called / THEN an empty list is returned AND an `[ERROR]` names the charId AND no registry filter executes | Logic / BLOCKING |
| SM-013 | GIVEN a character in normal state at depth_tier 2 with three unplayed tier-2 registry entries / WHEN GetAvailableScenes(charId) is called / THEN all three entries are returned | Logic / BLOCKING |
| SM-014 | GIVEN a character at depth_tier 2 with one tier-2 scene already in scenes_played / WHEN GetAvailableScenes(charId) is called / THEN only the unplayed tier-2 scene(s) are returned | Logic / BLOCKING |
| SM-015 | GIVEN a character at depth_tier 2 with tier-1 scenes in scenes_played / WHEN GetAvailableScenes(charId) is called / THEN no tier-1 scenes appear (no earlier-tier replay) | Logic / BLOCKING |
| SM-016 | GIVEN a character where all tier-2 scenes are in scenes_played and charId is not in gateReadyCharacters / WHEN GetAvailableScenes(charId) is called / THEN an empty list is returned AND no log is emitted | Logic / BLOCKING |
| SM-017 | GIVEN a character with closed_off == true at depth_tier 2 AND a RECOVERY scene registered for that character at tier 2 / WHEN GetAvailableScenes(charId) is called / THEN only the RECOVERY scene is returned AND no CORE/DEPTH/AMBIENT scenes appear | Logic / BLOCKING |
| SM-018 | GIVEN a character with closed_off == true at depth_tier 2 AND no RECOVERY scene at tier 2 / WHEN GetAvailableScenes(charId) is called / THEN an empty list is returned AND an `[ERROR]` log names the charId and depth_tier | Logic / BLOCKING |
| SM-019 | GIVEN scenes_played contains a sceneId whose registry entry was later removed / WHEN GetAvailableScenes(charId) is called / THEN the filter operates correctly on remaining valid entries AND no crash or error occurs | Logic / BLOCKING |

---

### Category 3: State Machine

| ID | Criterion | Classification |
|---|---|---|
| SM-030 | GIVEN Scene Management is in IDLE / WHEN StartScene(validEntry) is called / THEN state immediately transitions to LOADING | Logic / BLOCKING |
| SM-031 | GIVEN Scene Management is in LOADING, RUNNING, or COMPLETING / WHEN StartScene(anyEntry) is called / THEN the call is a no-op AND a `[WARNING]` is logged AND state does not change | Logic / BLOCKING |
| SM-032 | GIVEN Scene Management is in LOADING and all three preconditions pass / WHEN validation completes / THEN state transitions to RUNNING AND DialogueEngineController.StartScene(sceneId) is invoked | Integration / BLOCKING |
| SM-033 | GIVEN Scene Management is in RUNNING / WHEN DialogueStopped(CLEAN_COMPLETION) is received / THEN state transitions to COMPLETING | Logic / BLOCKING |
| SM-034 | GIVEN Scene Management is in RUNNING / WHEN DialogueStopped(ABANDONED) is received / THEN state transitions directly to IDLE AND RecordScenePlayed is NOT called AND SaveManager.Save is NOT called | Integration / BLOCKING |
| SM-035 | GIVEN Scene Management is in any state other than RUNNING / WHEN DialogueStopped fires / THEN the event is ignored AND a `[WARNING]` names the current state AND no accounting runs | Logic / BLOCKING |
| SM-036 | GIVEN COMPLETING phase accounting succeeds / WHEN all steps complete / THEN state transitions to IDLE AND the UI System receives the IDLE signal | Integration / BLOCKING |
| SM-037 | GIVEN LOADING precondition 1 fails (scene no longer in available list on re-check) / WHEN StartScene aborts / THEN state returns to IDLE AND a `[WARNING]` is logged AND UI is refreshed | Integration / BLOCKING |
| SM-038 | GIVEN LOADING precondition 2 fails (is_available == false at validation time) / WHEN StartScene aborts / THEN state returns to IDLE AND a `[WARNING]` names the charId AND UI is refreshed | Integration / BLOCKING |
| SM-039 | GIVEN LOADING precondition 3 fails — display_name is empty / WHEN StartScene aborts / THEN state returns to IDLE AND the Character Creator is surfaced | Integration / BLOCKING |
| SM-040 | GIVEN LOADING precondition 3 fails — one or more pronoun fields are empty / WHEN StartScene aborts / THEN state returns to IDLE AND the Character Creator is surfaced | Integration / BLOCKING |
| SM-041 | GIVEN any valid IDLE transition / WHEN IDLE is entered / THEN the UI System receives the OnIdleStateEntered event AND the character select screen becomes accessible | Integration / BLOCKING |

---

### Category 4: COMPLETING Phase Accounting

| ID | Criterion | Classification |
|---|---|---|
| SM-050 | GIVEN a CLEAN_COMPLETION / WHEN COMPLETING accounting runs / THEN RDS.OnSceneCompleted is called BEFORE SaveManager.Save — verifiable via ordered call recording on injected test doubles | Logic / BLOCKING |
| SM-051 | GIVEN RDS.OnSceneCompleted returns successfully / WHEN SaveManager.Save succeeds / THEN state transitions to IDLE AND the scene is recorded in scenes_played | Integration / BLOCKING |
| SM-052 | GIVEN RDS.OnSceneCompleted throws during COMPLETING / WHEN the exception is caught / THEN SaveManager.Save is NOT called AND state transitions to ERROR AND the UI receives an ERROR signal | Logic / BLOCKING |
| SM-053 | GIVEN SaveManager.Save fails once / WHEN Scene Management retries / THEN exactly one retry is attempted AND on retry success state transitions to IDLE | Logic / BLOCKING |
| SM-054 | GIVEN SaveManager.Save fails on both attempt and retry / WHEN retry is exhausted / THEN an `[ERROR]` is logged naming the scene AND state transitions to IDLE (not ERROR) AND in-memory state remains correct | Logic / BLOCKING |
| SM-055 | GIVEN DialogueStopped(ABANDONED) / THEN RDS.OnSceneCompleted is NOT called AND SaveManager.Save is NOT called AND the scene remains absent from scenes_played | Logic / BLOCKING |
| SM-056 | GIVEN DialogueStopped(NODE_NOT_FOUND) / THEN RDS.OnSceneCompleted is NOT called AND state transitions to ERROR AND the UI receives an ERROR signal | Logic / BLOCKING |

---

### Category 5: GATE_READY and DigEntry

| ID | Criterion | Classification |
|---|---|---|
| SM-060 | GIVEN RDS.OnGateReady(charId) fires for a character in normal state / WHEN GetAvailableScenes(charId) is next called / THEN a DigEntry appears as the last item in the result | Logic / BLOCKING |
| SM-061 | GIVEN RDS.OnGateReady(charId) fires while the character is closed_off == true / WHEN GetAvailableScenes(charId) is called / THEN the result contains only RECOVERY scenes AND no DigEntry appears | Logic / BLOCKING |
| SM-062 | GIVEN a character is in gateReadyCharacters AND subsequently becomes closed_off / WHEN GetAvailableScenes(charId) is called / THEN no DigEntry appears AND the gate entry remains in gateReadyCharacters for post-recovery use | Logic / BLOCKING |
| SM-063 | GIVEN gateReadyCharacters stores charId at gate tier 2, but state.depth_tier has advanced to 3 / WHEN GetAvailableScenes(charId) is called / THEN no DigEntry appears AND a `[WARNING]` notes the stale gate AND the entry is removed from gateReadyCharacters | Logic / BLOCKING |
| SM-064 | GIVEN a DigEntry is in the scene list AND the character becomes closed_off between list render and StartScene / WHEN StartScene(DigEntry) runs its LOADING checks / THEN StartScene aborts AND state returns to IDLE AND a `[WARNING]` is logged AND UI is refreshed | Integration / BLOCKING |
| SM-065 | GIVEN a character had GATE_READY before the session ended / WHEN a new session loads and Scene Management initialises / THEN gateReadyCharacters is fully populated via IsDigAvailable() BEFORE the first IDLE signal fires AND GetAvailableScenes returns a DigEntry for that character | Integration / BLOCKING |
| SM-066 | GIVEN session load completes with gate characters / WHEN the character select screen opens / THEN DigEntry appears for the correct characters without OnGateReady needing to re-fire | Integration / BLOCKING |

---

### Category 6: Edge Cases

| ID | Criterion | Classification |
|---|---|---|
| SM-070 | GIVEN the Dig subsystem reference is null / WHEN StartScene(DigEntry) is called / THEN state transitions to ERROR AND an `[ERROR]` log notes the null reference AND the Dialogue Engine is not invoked | Logic / BLOCKING |
| SM-071 | GIVEN is_available changes to false after scene list render but before StartScene / WHEN LOADING precondition 2 re-evaluates / THEN StartScene aborts AND state returns to IDLE AND UI is refreshed | Integration / BLOCKING |
| SM-072 | GIVEN a scene entry leaves the available list after render but before StartScene / WHEN LOADING precondition 1 re-evaluates GetAvailableScenes / THEN StartScene aborts AND state returns to IDLE AND UI is refreshed | Integration / BLOCKING |
| SM-073 | GIVEN COMPLETING spans multiple frames (async accounting) / WHEN StartScene is called mid-COMPLETING / THEN the call is a no-op per SM-031 AND no double-accounting occurs | Integration / BLOCKING |
| SM-074 | GIVEN scenes_played contains retired sceneIds / WHEN GetAvailableScenes is called / THEN the filter applies correctly to remaining valid entries AND no error is logged for retired ids | Logic / BLOCKING |

---

### Testability Implementation Flags

These flags must be communicated to the programmer before implementation begins:

**T-01** — Scene Management must accept `IRdsAdapter` and `ISaveManager` interfaces via constructor/property injection. Required for SM-050 (call order), SM-052 (RDS throws), SM-053/054 (save failure paths).

**T-02** — `gateReadyCharacters` (or a read-only snapshot equivalent) must be accessible via an internal/test API for SM-065 and SM-063 assertion.

**T-03** — Scene Management must expose an `Action OnIdleStateEntered` event for SM-041 and SM-036 to hook without requiring a real UI System instance.

**T-04** — SM-073 (COMPLETING spans frames) requires a Play Mode test that yields frames between COMPLETING entry and a `StartScene()` call. Mark as Integration/BLOCKING; cannot be covered by Edit Mode alone.

## Open Questions

### Cross-GDD Flags (must resolve before implementation)

**OQ-1: DialogueStopped completionReason field — Dialogue Engine GDD**
Owner: Dialogue Engine GDD (`design/gdd/dialogue-narrative-engine.md`)
Status: Unresolved

The Dialogue Engine GDD currently specifies that `DialogueStopped` fires "when dialogue ends" with no distinction between outcomes. This GDD requires the event to carry a `completionReason` field with at least three values:
- `CLEAN_COMPLETION` — scene ran to its natural end
- `ABANDONED` — player exited mid-scene, Yarn error, or application interrupt
- `NODE_NOT_FOUND` — the scene node named by `sceneId` did not exist in the YarnProject

Without this field, Scene Management cannot distinguish a false completion from a real one, and risks falsely recording `ABANDONED` or `NODE_NOT_FOUND` scenes as played in `scenes_played`.

Resolution target: Dialogue Engine GDD must be updated before implementation of Scene Management's RUNNING → COMPLETING / IDLE transition logic begins.

---

**OQ-2: CSM.IsRegistered(charId) method — Character State Manager GDD**
Owner: Character State Manager GDD (`design/gdd/character-state-manager.md`)
Status: Unresolved

`GetAvailableScenes(charId)` and LOADING validation require a registration check for `charId` that is distinct from `GetState(charId)` — the CSM returns a null-safe default state for unregistered characters, which would silently pass the availability filter. Either:
- CSM exposes `IsRegistered(charId)` → bool, or
- `GetState(charId)` is updated to return `null` (not a default) for unregistered characters, and Scene Management null-checks the result

The CSM GDD must specify which pattern is used before Scene Management's `GetAvailableScenes()` and LOADING precondition logic can be finalized.

Resolution target: Before implementation of `GetAvailableScenes()`.

---

**OQ-3: scenes_played write ownership — Character State Manager GDD (informational)**
Owner: Character State Manager GDD
Status: Informational note (not a blocker — call path is clear)

The CSM schema table lists Scene Management as the write owner of `scenes_played`. The actual call path is: Scene Management → `RDS.OnSceneCompleted()` → `CSM.RecordScenePlayed()`. RDS is the actual writer, acting on Scene Management's behalf. The CSM schema table should be updated to reflect "Relationship Depth System (on behalf of Scene Management)" as the write owner, to prevent a programmer from writing a direct Scene Management → CSM write path that bypasses RDS's threshold evaluation logic.
