# Relationship Depth System

> **Status**: Complete
> **Author**: Design session + agents
> **Last Updated**: 2026-05-03
> **Implements Pillar**: Pillar 2 (You Choose How Deep)

## Overview

The Relationship Depth System is the mechanical spine of Masks. It tracks how far the player has advanced into each character's private self and governs the rules by which that advance happens, stalls, or reverses. Every relationship in the game exists at one of six depth tiers (0–5). The player starts every character at tier 0 — surface contact, public face only — and can advance through four increasingly intimate tiers to tier 5, where the character's secret is revealed and the relationship reaches its terminal state.

Depth is not a resource to accumulate. It is a measure of earned trust. Advancement requires completing scenes and activities with a character, making choices that read their signals correctly, and engaging with Depth Gates (The Dig) at tier boundaries. The player chooses which characters to pursue, how quickly, and how deep — the system does not push them. A player can maintain five characters at tier 2 indefinitely, or pour all attention into one character to reach tier 5. Both are valid paths. Pillar 2 (You Choose How Deep) is the design law this system enforces.

Mechanically, the system owns one operation: incrementing `depth_tier` in the Character State Manager. It reads scene completion data and The Dig's advancement signals to determine when a tier advance is warranted, validates that the character is not closed off, and calls `CSM.SetDepthTier()` with the new value. The CSM enforces all storage invariants; this system enforces all advancement rules. The system also defines tier semantics — what each tier number means about the state of the relationship — so that Scene Management, the Dialogue Engine, and the UI System know what content is appropriate at each tier.

## Player Fantasy

Depth advancement is the feeling of being pulled back to a character you can't stop thinking about — and finding, each time you return, that their mask is a little more transparent to you than it was before.

The macro emotion is **compulsive return**. A scene ends. You open the character select screen. Three other characters are waiting, probably with content you haven't touched. You scroll past them to land on the same character again — not because the game directed you there, because something they said in the last scene is still unresolved in your chest. You press in. Again. You don't entirely know why. This is the feeling the Depth System is built to generate and maintain. Every tier advance isn't an unlock — it's a tightening of gravity. The character is more themselves each time, which means more interesting, which means more necessary to return to. The system measures compulsion without ever naming it.

The micro emotion, felt inside each scene, is **vision sharpening**. Early scenes: their public face is a solid thing. They charm. They deflect. They perform. Across several scenes, something changes — not in the writing, but in *you*. You start to catch the half-second before the charm. The breath they take to decide to laugh. The sentence they almost said and didn't. You now see them constructing themselves in real time, and they don't know you see it. This is what tier advancement actually feels like from the inside: not "I unlocked a new scene," but "I now have better eyes." Each tier makes the mask more transparent. What's behind it stays private until tier 4–5 — but you can *feel* the shape of it through the material.

The Depth System does not announce itself. There is no "+1 Depth" notification. The player shouldn't think "I'm advancing." They should think "I understand her a little better now." The system's work is to make that feeling real and earned — and to make tier 5, when it comes, feel inevitable in retrospect. Of course you found out. You were always going to push this far. You couldn't have stopped.

**Design test this must pass**: When a player crosses a tier threshold, they should describe the experience in perception vocabulary — *"I see her differently," "something shifted," "I caught something I hadn't before."* If they reach for progression vocabulary — *"I leveled up," "I unlocked a scene"* — the system failed to hide its skeleton.

## Detailed Design

### Core Rules

**Rule 1 — Single ownership.** The Relationship Depth System is the only caller of `CSM.SetDepthTier()` during normal gameplay. Save/Load may call `CSM.RestoreState()` as a bulk bypass. No other system writes `depth_tier`.

**Rule 2 — Monotonic progression.** `depth_tier` only increases. This system never calls `SetDepthTier(charId, N)` where `N` is less than the current value. Consequence for misread or withdrawal is modeled via `closed_off`, not tier regression.

**Rule 3 — Advancement requires two conditions, in sequence.** To advance from tier N to tier N+1, the player must: (a) complete the required number of qualifying scenes at tier N, then (b) complete a Dig at the tier boundary and receive a `DIG_PASS` result. Scene accumulation alone never triggers a tier advance.

**Rule 4 — Closed-off blocks all advancement.** While `closed_off == true`, no tier advance is possible regardless of scene count or Dig availability. Recovery is required before advancement resumes (see Rules 14–17).

**Rule 5 — Tier 5 is terminal.** Once `depth_tier == 5`, no further advancement is possible. `CSM.SetDepthTier(charId, 5)` auto-sets `secret_revealed = true` (enforced inside CSM; this system does not write `secret_revealed`).

**Rule 6 — No replay credit.** `scenes_played` is a `HashSet<string>`. Replaying a scene that is already in the set produces no change and no additional count toward any threshold. This is the natural behavior of `HashSet.Add()`.

**Rule 7 — Scene count is per-tier.** The threshold at tier N counts only scenes tagged for tier N in the Scene Registry. Scenes played at earlier tiers do not carry forward. Each tier requires genuine engagement at that depth.

**Rule 8 — Not all scenes count.** A scene counts toward the threshold only if: (a) it is tagged for this character in the Scene Registry, (b) its registered tier matches the character's current `depth_tier` at the time of completion, and (c) its `engagement_type` is `CORE` or `DEPTH`. Scenes with `engagement_type: AMBIENT` do not count toward advancement. This allows narrative designers to place incidental scenes without inflating progress.

**Rule 9 — Tier tag is fixed at authoring time.** A scene's tier does not inherit the player's current tier at play time. If a tier-2 scene is played while the character is at tier 3, it counts toward tier-2 progress, not tier-3.

**Rule 10 — GATE_READY state surfaces, does not advance.** When the scene count at tier N reaches `SCENES_REQUIRED[N]`, the state transitions to GATE_READY. This surfaces Dig availability to the player via the UI System. No tier increment fires at this moment.

**Rule 11 — The Dig returns one of four outcomes.** `DIG_PASS`, `DIG_PARTIAL`, `DIG_FAIL`, `DIG_CRITICAL_FAIL`. Only `DIG_PASS` advances the tier.

**Rule 12 — DIG_PASS increments tier.** This system calls `CSM.SetDepthTier(charId, current_depth_tier + 1)`. State transitions to PROGRESSING at the new tier. The per-tier scene counter effectively resets because the new tier has no scenes in `scenes_played` yet.

**Rule 13 — DIG_PARTIAL costs one scene.** The relationship returns to GATE_READY (threshold already met). No depth change, no `closed_off` change. The Dig is not available again until the player completes one additional qualifying scene (any `engagement_type` counts for this cooldown reset). This prevents repeated Dig attempts without engagement.

**Rule 14 — DIG_FAIL closes the character off.** The Dig subsystem calls `CSM.SetClosedOff(charId, true)`. This system observes the resulting state. `depth_tier` is unchanged.

**Rule 15 — DIG_CRITICAL_FAIL closes off and sets a flag.** Same as DIG_FAIL, plus the Dig subsystem sets a character-specific `narrative_flag` (e.g., `"dig_critical_failed"`) in the CSM. This flag gates a harder recovery arc.

**Rule 16 — Closed-off recovery requires a recovery arc.** While `closed_off == true`, standard scenes for this character are unavailable. A recovery scene (tagged `engagement_type: RECOVERY`) appears in their scene list. Completing the recovery arc causes The Dig subsystem to call `CSM.SetClosedOff(charId, false)`. `depth_tier` is unchanged by recovery.

**Rule 17 — Recovery returns to current-tier state.** After `closed_off` is cleared, this system re-evaluates scene count at the current tier. If the threshold was already met before close-off, state returns to GATE_READY. If not, state returns to PROGRESSING.

**Rule 18 — Designer override for `closed_off`.** A Yarn `<<set_closed_off charId false>>` command, routed through `MasksVariableStorage` → CSM, may clear `closed_off` for plot-mandated recovery. This is not player-accessible and must be documented in the character's arc notes when used.

---

### States and Transitions

States are derived from CSM field values. This system holds no separate state enum — state is computed from `{depth_tier, closed_off, secret_revealed, scenes_played}`.

| State | Entry Condition | Valid Exits |
|---|---|---|
| **UNTOUCHED** | `depth_tier == 0` AND `scenes_played` is empty | → PROGRESSING |
| **PROGRESSING** | `depth_tier` ∈ [0, 4] AND `closed_off == false` AND scenes at current tier < threshold | → GATE_READY, → CLOSED_OFF |
| **GATE_READY** | `depth_tier` ∈ [0, 4] AND `closed_off == false` AND scenes at current tier ≥ threshold | → DIGGING, → CLOSED_OFF |
| **DIGGING** | Dig interaction initiated | → PROGRESSING (DIG_PASS), → GATE_READY (DIG_PARTIAL, after one more scene), → CLOSED_OFF (DIG_FAIL, DIG_CRITICAL_FAIL) |
| **CLOSED_OFF** | `closed_off == true` | → GATE_READY or PROGRESSING (recovery arc complete) |
| **REVEALED** | `depth_tier == 5` | Terminal — no exits |

**Tier semantics** — what the player can perceive at each tier:

| Tier | Name | Perception |
|---|---|---|
| 0 | The Public Face | They are their persona. No view behind it. |
| 1 | The Texture | The performance shows its seams. The charm has effort in it. |
| 2 | The Gap | You catch what they almost say. The gap between public and private is legible — you don't know what's in it, but you know it's there. |
| 3 | The Shape | You can feel the shape of the secret through the material. You don't know what it is, but you know its weight and direction. |
| 4 | The Threshold | They know you see something. The dynamic has shifted. The secret is close. |
| 5 | The Truth | The mask is gone — at least with you. You know who they are. They know you know. |

---

### Interactions with Other Systems

**Character State Manager (reads and writes):**
- Reads: `depth_tier`, `closed_off`, `secret_revealed`, `scenes_played`, `narrative_flags`
- Writes: `CSM.SetDepthTier(charId, newTier)` — the only write this system performs
- Does not write: `closed_off` (owned by The Dig), `secret_revealed` (owned by Secret Reveal System), `narrative_flags` (owned by scene content and The Dig)

**The Dig — receives advancement signal:**
- The Dig initiates when this system is in GATE_READY and the player triggers it
- The Dig returns `DIG_PASS | DIG_PARTIAL | DIG_FAIL | DIG_CRITICAL_FAIL` to this system
- The Dig writes `closed_off` and critical narrative flags directly to CSM on fail outcomes — this system observes the result, it does not write those fields

**Scene Management / Flow Controller — provides scene completion events:**
- Scene Management signals scene completion with `{charId, sceneNodeName, engagementType}`
- This system calls `CSM.RecordScenePlayed(charId, sceneNodeName)` on receipt
- This system evaluates threshold after each scene completion and notifies Scene Management if state transitions to GATE_READY (so Scene Management can unlock Dig access in the UI)

**Save/Load System — persistence:**
- Save triggers include depth tier change: on `DIG_PASS`, after `SetDepthTier()` returns, this system raises `<<notify_state_change depth_tier_changed charId>>` which Save/Load observes
- On load, CSM state is fully restored via `RestoreState()`. This system re-derives its state from the restored CSM fields — no additional save data required

**UI System — displays depth state:**
- This system raises `<<notify_state_change depth_tier_changed charId>>` after each tier advance
- UI System subscribes and queries CSM for current tier to update the depth display
- This system raises a GATE_READY notification when scene threshold is met; UI System uses this to surface Dig availability

**Secret Reveal System — terminus handoff:**
- When `depth_tier` reaches 5 and CSM auto-sets `secret_revealed = true`, the Secret Reveal System is responsible for presenting the revelation arc
- This system has no further role at tier 5; the revelation arc is fully owned by the Secret Reveal System

## Formulas

### F-1: Scene Threshold Per Tier

```
SCENES_REQUIRED[tier] → int
```

Default values (configurable per-tier, per-character):

| Tier (from → to) | Default SCENES_REQUIRED |
|---|---|
| 0 → 1 | 2 |
| 1 → 2 | 3 |
| 2 → 3 | 3 |
| 3 → 4 | 4 |
| 4 → 5 | 1 |

The tier-4 value of 1 reflects that The Dig at this boundary is the revelation arc itself. One scene is needed to confirm the character is still in active pursuit before the climactic gate.

Per-character overrides live in the character's config data (e.g., a guarded character may use `[3, 4, 4, 5, 1]`). The system reads the override if present; falls back to defaults if not.

**Safe tuning range:** Each tier value ∈ [1, 8]. Values below 1 are invalid. Values above 8 produce a noticeable grind without meaningful content to fill the gap — flag at design review if any tier exceeds 6.

---

### F-2: Scene Count at Current Tier

```
CountScenesAtTier(scenes_played: HashSet<string>, tier: int) → int

count = 0
for each sceneId in scenes_played:
    entry = SceneRegistry.Lookup(sceneId)
    if entry.tier == tier AND entry.engagementType ∈ {CORE, DEPTH}:
        count += 1
return count
```

**Variables:**
- `scenes_played` — `HashSet<string>` from CSM; contains scene node names for all scenes the player has completed with this character
- `tier` — the character's current `depth_tier`
- `SceneRegistry` — read-only data store mapping scene node names to `{tier, charId, engagementType}`
- `engagementType` — one of `CORE`, `DEPTH`, `AMBIENT`, `RECOVERY`, `ACTIVITY`. ACTIVITY was added by scene-activity-loop.md (design order #9); it does not count toward GATE_READY threshold but participates in the full COMPLETING accounting chain.

**Expected output range:** 0 to N, where N is the total number of CORE/DEPTH scenes authored for this character at this tier. In practice, N should be ≥ `SCENES_REQUIRED[tier]` to ensure the threshold is reachable.

**Example:** Character Mira is at tier 2. `scenes_played` = {`"mira_intro"`, `"mira_coffee"`, `"mira_rain_t2"`, `"mira_gallery_t2"`}. Scene Registry shows `mira_rain_t2` (tier 2, CORE) and `mira_gallery_t2` (tier 2, DEPTH) qualify; `mira_intro` (tier 0, CORE) and `mira_coffee` (tier 1, DEPTH) do not. `CountScenesAtTier(scenes_played, 2)` → 2.

---

### F-3: Advancement Eligibility Check

```
CanAdvanceTier(charId, dig_result) → EligibilityResult

state = CSM.GetState(charId)

if state.depth_tier >= MAX_DEPTH_TIER:        // MAX_DEPTH_TIER = 5
    return BLOCKED_AT_MAX

if state.closed_off == true:
    return BLOCKED_CLOSED_OFF

scenes_at_tier = CountScenesAtTier(state.scenes_played, state.depth_tier)
threshold      = SCENES_REQUIRED[state.depth_tier]
if scenes_at_tier < threshold:
    return BLOCKED_INSUFFICIENT_SCENES

if dig_result == null:
    return GATE_READY                          // threshold met; Dig not yet attempted

if dig_result != DIG_PASS:
    return BLOCKED_DIG_NOT_PASSED

return ELIGIBLE
```

**Calling convention:**
- Scene completion handler calls with `dig_result = null`. `GATE_READY` is the highest reachable result; `ELIGIBLE` is not reachable from this path. Reaching `GATE_READY` surfaces Dig availability.
- Dig resolution handler calls with the actual `dig_result`. `ELIGIBLE` is reachable only from this path.

**Returns:**

| Result | Meaning | Action |
|---|---|---|
| `BLOCKED_AT_MAX` | Already at tier 5 | No action |
| `BLOCKED_CLOSED_OFF` | Character withdrawn | No action |
| `BLOCKED_INSUFFICIENT_SCENES` | Not enough scenes | No action |
| `GATE_READY` | Threshold met; Dig available | Surface Dig to UI |
| `BLOCKED_DIG_NOT_PASSED` | Dig returned PARTIAL, FAIL, or CRITICAL_FAIL | Apply Dig outcome (Rule 13–15) |
| `ELIGIBLE` | All gates pass | Call `CSM.SetDepthTier(charId, state.depth_tier + 1)` |

---

### F-4: DIG_PARTIAL Cooldown

```
IsDigAvailable(charId) → bool

state = CSM.GetState(charId)
if state.narrative_flags["dig_partial_cooldown"] == true:
    return false
return CountScenesAtTier(state.scenes_played, state.depth_tier) >= SCENES_REQUIRED[state.depth_tier]
```

When The Dig returns `DIG_PARTIAL`:
1. CSM sets `narrative_flags["dig_partial_cooldown"] = true` (written by The Dig subsystem)
2. The next scene completion for this character (any `engagement_type`) clears the flag: `narrative_flags["dig_partial_cooldown"] = false`
3. After the flag is cleared, `IsDigAvailable()` re-evaluates to `true` (threshold is still met)

**Note:** The cooldown flag key `"dig_partial_cooldown"` is a reserved key in the character's `narrative_flags`. Narrative designers must not use this key name for other purposes.

## Edge Cases

**EC-1: Scene played at wrong tier (out-of-order play)**
A scene tagged for tier 2 is played while the character is at tier 3 (because the player missed it earlier). The scene counts toward tier-2 progress, not tier-3. If the player has already passed tier 2, this adds to a threshold that is no longer relevant — the count is recorded but produces no threshold effect. No error is raised. Tier-2 scenes played after advancing past tier 2 are permanently inert for advancement purposes; they may still be playable as content.

**EC-2: Content gap — not enough scenes authored to reach threshold**
The total CORE/DEPTH scenes authored for a character at tier N is less than `SCENES_REQUIRED[N]`. The player reaches all available scenes but the threshold is not met. The threshold check will perpetually return `BLOCKED_INSUFFICIENT_SCENES` and The Dig can never be surfaced.

Detection: At scene authoring time, the Scene Registry validator must compare authored scene count per tier per character against `SCENES_REQUIRED[tier]`. If authored count < threshold, flag at content review — this is a content bug, not a system bug. The system does not attempt to auto-lower the threshold; it is the content team's responsibility to ensure coverage.

**EC-3: Player loads a save where depth_tier > scenes_played count suggests**
Save/Load restores CSM state via `RestoreState()`. After restore, `CountScenesAtTier()` may return a value lower than `SCENES_REQUIRED[depth_tier - 1]` — e.g., if scene completion records were partially lost in a corrupt save recovery. The system does not re-validate scene history on load. `depth_tier` as restored is authoritative; no tier is revoked. The player's current tier state is accepted as correct.

**EC-4: Dig attempted while not in GATE_READY**
Scene Management gates Dig availability in the UI based on this system's GATE_READY notification. If a Dig attempt arrives while the system is not in GATE_READY (e.g., a UI bug or edge case timing), `CanAdvanceTier(charId, dig_result)` will return `BLOCKED_INSUFFICIENT_SCENES` or `BLOCKED_CLOSED_OFF` before reaching the Dig outcome check. The Dig result is ignored. No state change occurs. Log a warning.

**EC-5: Closed-off triggered during a scene (mid-scene close-off)**
The Dig subsystem writes `closed_off = true` only at Dig resolution time, not mid-scene. Standard scenes cannot trigger close-off. If a narrative designer needs a scene to close a character off (e.g., a story beat where the character explicitly withdraws), this must be implemented by triggering The Dig subsystem's FAIL outcome at scene end, not by writing `closed_off` directly from Yarn. The Yarn command `<<set_closed_off charId true>>` is not a permitted authoring pattern (see Rule 18 — the override path is clearance only, not setting).

**EC-6: Recovery arc completed while already not closed-off**
If `SetClosedOff(charId, false)` is called when `closed_off` is already false (e.g., a double-fire or stale event), the CSM write is idempotent — the field remains false, no state corruption occurs. This system re-derives state from CSM fields and will compute the same result as before the redundant call.

**EC-7: Multiple characters reach GATE_READY simultaneously**
Each character's state is independent. Multiple characters may be in GATE_READY at the same time. The player chooses which Dig to attempt first. No system-level conflict arises. The UI System is responsible for surfacing multiple GATE_READY states without overwhelming the player (that is a UI design concern, not a rule for this system).

**EC-8: Tier 5 advance attempted when secret_revealed is already true**
`CSM.SetDepthTier(charId, 5)` is idempotent: calling it when `depth_tier` is already 5 produces no change and no error (CSM Rule: `BLOCKED_AT_MAX` returned). This system's `BLOCKED_AT_MAX` check fires first and prevents the redundant call. Both layers agree: no double-reveal is possible.

**EC-9: Character removed from roster mid-playthrough**
If a character is quarantined (cut from the build), CSM marks them inactive. This system must not call `SetDepthTier()` on an inactive character. Scene Management is responsible for not surfacing scenes for inactive characters; this system does not independently check quarantine status, but it will not receive scene completion events for quarantined characters as a result.

## Dependencies

### Upstream (systems this one depends on)

**Character State Manager (#6)** — `design/gdd/character-state-manager.md`
This system reads and writes through the CSM exclusively. Depends on: `SetDepthTier()`, `GetState()`, `RecordScenePlayed()`, the `closed_off` and `secret_revealed` field semantics, the `RestoreState()` bulk bypass, the quarantine model, and the tier-5 `secret_revealed` auto-set invariant (CSM Rule 14). Any change to the CSM schema or write-ownership table must be reviewed for impact on this system.

**Save/Load System (#7)** — `design/gdd/save-load-system.md`
Depth tier changes are a save trigger. This system raises `<<notify_state_change depth_tier_changed charId>>` post-advance; Save/Load observes this event. Depends on Save/Load triggering correctly on that notification and on `RestoreState()` correctly restoring all CSM fields this system reads.

**Dialogue/Narrative Engine (#8)** — `design/gdd/dialogue-narrative-engine.md`
The `<<notify_state_change>>` command used by this system is a YarnSpinner custom command routed through `MasksVariableStorage`. Depends on the Dialogue Engine's notification architecture being in place before this system's events can be consumed by subscribers (UI System, Save/Load).

### Downstream (systems that depend on this one)

**The Dig / Depth Gate (#3)** — undesigned
Depends on this system defining: the GATE_READY state and what surfaces it, the four Dig outcome types (`DIG_PASS`, `DIG_PARTIAL`, `DIG_FAIL`, `DIG_CRITICAL_FAIL`), the DIG_PARTIAL cooldown mechanism (`"dig_partial_cooldown"` flag), and the write-ownership rule (The Dig writes `closed_off`; this system writes `depth_tier`). The Dig GDD must be authored with these contracts as constraints.

**Scene Management / Flow Controller (#9)** — undesigned
Depends on this system defining: the scene completion event contract `{charId, sceneNodeName, engagementType}`, the GATE_READY notification, and the four `engagement_type` values (`CORE`, `DEPTH`, `AMBIENT`, `RECOVERY`). Scene Management must enforce which scenes are available at which tier based on `depth_tier` from CSM.

**Secret Reveal System (#5)** — undesigned
Depends on this system defining: tier 5 as the trigger point, the CSM's auto-set of `secret_revealed = true` on `SetDepthTier(charId, 5)`, and the fact that this system has no role at or after the reveal. The Secret Reveal System's entry condition is `secret_revealed == true`.

**UI System (#16)** — undesigned
Depends on this system defining: `<<notify_state_change depth_tier_changed charId>>` as the tier-advance event, the GATE_READY notification for Dig surfacing, the six tier names (0=The Public Face through 5=The Truth) as the semantic labels for any depth display, and the fact that no "+1 Depth" notification should be shown to the player.

**Endings / Route Completion (#18)** — undesigned
Depends on `depth_tier == 5` being the completion signal for a character's route. The Endings system reads `depth_tier` and `secret_revealed` from CSM to determine route completion state.

### Peer dependency

**Scene Registry** — not a full system (data layer owned by Scene Management)
This system depends on a Scene Registry that maps scene node names to `{tier, charId, engagementType}`. This registry must be in place before `CountScenesAtTier()` can function. The Scene Registry is owned by Scene Management / Flow Controller (#9) but is read by this system.

## Tuning Knobs

All values below are data-driven and configurable without code changes. The safe range specifies the bounds within which the system behaves correctly and the intended feel is preserved. Values outside safe ranges are not rejected by the system but will produce noticeably wrong pacing.

| Knob | Default | Safe Range | What It Affects |
|---|---|---|---|
| `SCENES_REQUIRED[0]` (tier 0→1) | 2 | [1, 4] | Speed of first relationship impression. Lower = faster entry into the relationship; higher = more surface time before any intimacy. |
| `SCENES_REQUIRED[1]` (tier 1→2) | 3 | [2, 6] | Pacing of early intimacy. This tier is where most players will spend the most cumulative time across all characters. |
| `SCENES_REQUIRED[2]` (tier 2→3) | 3 | [2, 6] | Mid-game pacing. Should feel earned but not exhausting. |
| `SCENES_REQUIRED[3]` (tier 3→4) | 4 | [2, 8] | Pre-climax pacing. Higher values here extend the "almost there" tension before the revelation arc. |
| `SCENES_REQUIRED[4]` (tier 4→5) | 1 | [1, 3] | Final confirmation scene before The Dig at tier 4. Rarely needs tuning — the Dig IS the event at this tier. |
| Per-character `SCENES_REQUIRED` override | null (use defaults) | Same as above | Allows guarded characters to require more scenes and open characters to require fewer. Null = use global defaults. |
| `DIG_PARTIAL_COOLDOWN_SCENE_COUNT` | 1 | [1, 3] | Number of scenes required after a DIG_PARTIAL before re-attempting The Dig. Currently hardcoded to 1 (one scene clears the cooldown flag). Increase to 2–3 if playtesters are re-attempting The Dig too mechanically. |

**Pacing model reference:**

At defaults, a player who focuses on a single character will spend approximately:
- Tier 0→1: 2 scenes (~30–40 min at 15–20 min/scene)
- Tier 1→2: 3 scenes (~45–60 min)
- Tier 2→3: 3 scenes + Dig (~50–65 min)
- Tier 3→4: 4 scenes + Dig (~65–85 min)
- Tier 4→5: 1 scene + Dig (~20–35 min)

**Total per character (single-focus playthrough): ~3.5–4.5 hours**

At 20–30 characters, total content potential (if all characters are taken to tier 5): 70–135 hours. In practice, players will take a small number of characters to tier 5 and leave others at lower tiers — this is by design (Pillar 2).

**Playtest signals to watch:**
- If players describe tier advances as "unlocking content" → `SCENES_REQUIRED` values are too low; increase by 1 per tier
- If players abandon a character before tier 3 → `SCENES_REQUIRED[1]` or `[2]` is too high, or content quality at those tiers needs attention (system cannot diagnose content quality)
- If players rarely attempt The Dig when GATE_READY → the UI surfacing is unclear or the cost of DIG_FAIL feels too high; lower `DIG_PARTIAL_COOLDOWN_SCENE_COUNT` and review Dig outcome communication

## Visual/Audio Requirements

Visual and audio requirements for this system are specified in the UI System GDD (#16) and Audio System GDD (#15). This system's contracts with those systems:
- UI System receives `<<notify_state_change depth_tier_changed charId>>` and GATE_READY notifications; it owns how those are expressed visually
- Audio System may subscribe to the same notifications to trigger reactive music cues at tier advances and Dig moments
- No "+1 Depth" or numeric feedback is permitted at the UI layer — this is an acceptance criterion (RDS-060, RDS-061)

## UI Requirements

- Dig availability (GATE_READY state) must be surfaced to the player without numeric depth labels
- No progress bar, counter, or percentage indicating "X scenes until next tier" is shown to the player
- Tier names (The Public Face → The Truth) are internal design vocabulary; whether and how they surface in UI is a decision for the UI System GDD
- All depth-related UI must pass the design test: players should describe advancement in perception vocabulary, not progression vocabulary

## Acceptance Criteria

### Category 1: Tier Advancement — Happy Path

**RDS-001** — Given a character at tier 0 with 0 scenes played, when the player completes 2 CORE/DEPTH scenes tagged for tier 0, then `IsDigAvailable()` returns true.

**RDS-002** — Given a character at tier 0 in GATE_READY, when The Dig returns `DIG_PASS`, then `depth_tier` is 1 and the character is in PROGRESSING state.

**RDS-003** — Given a character at tier 4 with 1 CORE/DEPTH scene completed at tier 4, when The Dig returns `DIG_PASS`, then `depth_tier` is 5 and `secret_revealed` is true (CSM invariant).

**RDS-004** — Given a character at tier 5, when any Dig outcome is received, then `depth_tier` remains 5 and no state change occurs.

**RDS-005** — Given a character at tier N with scene threshold met, when a scene with `engagement_type: AMBIENT` is completed, then `CountScenesAtTier()` is unchanged and GATE_READY state is unaffected.

---

### Category 2: Closed-Off and Recovery

**RDS-010** — Given a character in GATE_READY, when The Dig returns `DIG_FAIL`, then `closed_off` is true, `depth_tier` is unchanged, and the character is in CLOSED_OFF state.

**RDS-011** — Given a character in CLOSED_OFF state, when `CanAdvanceTier()` is called with any `dig_result`, then the result is `BLOCKED_CLOSED_OFF` and `depth_tier` is unchanged.

**RDS-012** — Given a character in CLOSED_OFF state, when a recovery arc scene completes and `SetClosedOff(charId, false)` is called, then `closed_off` is false and the character returns to PROGRESSING or GATE_READY based on scene count at current tier.

**RDS-013** — Given a character closed off at tier 2, after recovery, then `depth_tier` remains 2 (no regression from recovery).

**RDS-014** — Given a character in CLOSED_OFF state, then standard scenes for that character are not available via Scene Management.

---

### Category 3: DIG_PARTIAL and Cooldown

**RDS-020** — Given a character in GATE_READY, when The Dig returns `DIG_PARTIAL`, then `depth_tier` is unchanged, `closed_off` is false, and `narrative_flags["dig_partial_cooldown"]` is true.

**RDS-021** — Given a character with `dig_partial_cooldown == true`, when `IsDigAvailable()` is called, then it returns false.

**RDS-022** — Given a character with `dig_partial_cooldown == true`, when any scene completion is recorded, then `dig_partial_cooldown` is false and `IsDigAvailable()` returns true.

---

### Category 4: Scene Counting

**RDS-030** — Given a character at tier 2 with 3 scenes in `scenes_played` (2 at tier 2, 1 at tier 1), when `CountScenesAtTier(scenes_played, 2)` is called, then the result is 2.

**RDS-031** — Given a character at tier 2, when the same scene is completed twice, then `CountScenesAtTier()` returns the same value as after the first completion (no replay credit).

**RDS-032** — Given a character at tier 2, when a scene with `engagement_type: AMBIENT` is completed, then `CountScenesAtTier()` is unchanged.

**RDS-033** — Given a character at tier 2, when a CORE scene tagged for tier 0 is played, then `CountScenesAtTier(scenes_played, 2)` is unchanged (out-of-order play does not count toward current tier).

---

### Category 5: Save/Load Integration

**RDS-040** — Given a character at tier 3, when `DIG_PASS` is processed and `depth_tier` advances to 4, then a `<<notify_state_change depth_tier_changed charId>>` event is raised within the same frame.

**RDS-041** — Given a saved game with a character at tier 3 `closed_off: true`, when the game is loaded and CSM state is restored, then `CanAdvanceTier()` returns `BLOCKED_CLOSED_OFF` without any additional calls.

**RDS-042** — Given a corrupt save where `depth_tier` is restored to 3 but `scenes_played` contains only 1 tier-3 scene (below threshold), then `depth_tier` remains 3 after load (no revocation) and the system derives PROGRESSING state correctly.

---

### Category 6: Monotonic Progression

**RDS-050** — Given any character at any tier N, when `CanAdvanceTier()` is called in a non-ELIGIBLE state, then `CSM.SetDepthTier()` is never called with a value ≤ N.

**RDS-051** — Given a character at tier 5, when `depth_tier` is checked after any combination of Dig outcomes or scene completions, then `depth_tier` remains 5.

---

### Category 7: No Announcement

**RDS-060** — Given a character advancing from tier 2 to tier 3, when the advancement is processed, then no "+1 Depth" or equivalent progress notification is surfaced to the player. (Verified by UI walkthrough — no numeric depth delta is displayed.)

**RDS-061** — Given a character in GATE_READY, when the player views the character select screen, then Dig availability is communicated without numeric depth labels. (Verified by UI walkthrough.)

## Open Questions

1. **Scene Registry ownership**: The Scene Registry (`{sceneId → tier, charId, engagementType}`) is required by this system but not yet formally owned. It should be formally specified in the Scene Management GDD (#9). Flag for Scene Management authoring.

2. **DIG_CRITICAL_FAIL narrative flag key naming convention**: The flag `"dig_critical_failed"` is referenced as a per-character key. The naming convention for character-specific reserved flag keys should be formalized in the Scene Management or CSM GDD to prevent collisions across 20–30 characters.

3. **GATE_READY notification de-duplication**: If the player completes additional scenes after the threshold is met (e.g., plays ambient scenes while in GATE_READY), this system should not re-fire the GATE_READY notification repeatedly. De-duplication logic (track last-notified state) should be confirmed at implementation.
