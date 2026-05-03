# The Dig (Depth Gate)

> **Status**: Designed
> **Author**: Design session + agents
> **Last Updated**: 2026-05-03
> **Implements Pillar**: Pillar 2 (You Choose How Deep), Pillar 1 (Every Face Hides a Face)

## Overview

The Dig is the Depth Gate mechanic of Masks — the singular point of risk and consequence that separates sustained attention from genuine intimacy. When a player has completed the required number of scenes with a character at their current depth tier, a Dig becomes available. The player may choose it from the character's scene list at any time; nothing forces them to attempt it. When they do, they enter an interaction that asks them to read the character and decide: push further into their private self, or acknowledge the limit of what has been earned so far.

The Dig produces one of four outcomes. A `DIG_PASS` advances the relationship to the next depth tier — the character is more themselves than before. A `DIG_PARTIAL` finds the player close but not yet there: no tier change, no withdrawal, but the Dig is unavailable until one more scene is played. A `DIG_FAIL` closes the character off: they withdraw, standard scenes become unavailable, and a recovery arc must be completed before any further progress is possible. A `DIG_CRITICAL_FAIL` does the same, but also sets a permanent narrative flag that gates a harder recovery arc — evidence that the push left a mark. The Dig writes `closed_off` and critical narrative flags to the Character State Manager; all tier advancement is handled by the Relationship Depth System on receipt of the `DIG_PASS` result.

At MVP, The Dig is a standalone interaction invoked by Scene Management when the player selects a DigEntry. It does not use the Dialogue Engine for execution. It owns no persistent state between Digs; its sole outputs are a typed result and, on fail outcomes, CSM writes.

## Player Fantasy

The Dig is not a confrontation. It is a question — and the fantasy lives in the moment before you ask it.

By the time the Dig becomes available, you have been paying attention. You have watched this character long enough to catch the things they almost say, the subject they steer around, the version of themselves they keep presenting even when you can see the effort in it. You have earned the right to push. That is what the game has been measuring, quietly, while you were just sitting with them.

The Dig is the moment you stop sitting and lean in. The fantasy is not the answer — the answer might be a PASS, might be a FAIL, might be something in between. The fantasy is the held breath between the choice to ask and the character deciding whether to let you in. The anticipation of a person on the edge of honesty. The weight of having pushed just far enough that something real is now possible — and you don't know yet what kind of real it will be.

This is the emotional center of Masks: not discovery as triumph, but discovery as risk. The Dig makes that explicit. You chose to try. They will respond to who you actually are to them. The question is not whether you wanted to push — you clearly did — but whether you read them well enough to be someone they can afford to be honest with.

**Design test this must pass**: When a player reflects on a Dig — regardless of outcome — they should describe the experience in terms of the relationship, not the mechanic. "I thought she was ready" or "I pushed too hard" rather than "I got a FAIL state." If they are describing outcomes, the presentation layer failed to make the stakes feel personal.

## Detailed Design

### Core Rules

**The Admission Interaction**

1. The Dig is invoked when Scene Management calls `StartScene(DigEntry)`. Scene Management transitions to RUNNING state. The Dig subsystem takes full control of the screen until it signals completion.

2. The Dig interaction presents a static character portrait and three short authored lines — each a thing the player character might say to this character at this moment. The lines are plain prose, first-person, present tense. They do not display as dialogue UI; they appear directly on the Dig screen as selectable choices. No timer. No ambient animation pressure. The player reads and decides.

3. The three lines are authored per-character per-tier by the narrative team. They are stored in a data asset — **Dig Library** — keyed by `{charId, tier}`. Each entry contains exactly three lines and a response passage for each outcome. The Dig Library is a required data asset; if the entry for a given `{charId, tier}` is missing, The Dig logs `[ERROR]`, returns DIG_PARTIAL, and transitions back to Scene Management.

4. Each line in a Dig Library entry maps to a **primary outcome**: DIG_PASS, DIG_PARTIAL, or DIG_FAIL. The mapping is authored data. Authors must ensure at least one line maps to DIG_PASS and at least one maps to DIG_FAIL per entry. DIG_PARTIAL may be assigned to one line or left unassigned; if unassigned, DIG_PARTIAL is not achievable at this tier for this character.

5. The mapping is never shown to the player. Players choose by interpretation of the character, not by pattern-matching to known outcome buckets. The mapping must be internally consistent: the DIG_PASS line must most accurately read the character's wound or desire at this tier; the DIG_FAIL line must be a plausible but incorrect read — never an obviously bad thing to say.

6. After the player selects a line, a brief pause (default 800ms, tunable) precedes the character's response. The response is authored prose displayed in the same screen — one short passage per outcome, written from the character's perspective. The passage ends the interaction; no further choice is offered. The screen transitions out and The Dig signals completion to Scene Management.

**DIG_CRITICAL_FAIL Condition**

7. DIG_CRITICAL_FAIL is a conditional escalation of DIG_FAIL. It is not a fourth line choice — it is a modifier applied to a DIG_FAIL result. When the outcome would be DIG_FAIL AND a specified `narrative_flag` is set in the character's CSM record, the outcome escalates to DIG_CRITICAL_FAIL.

8. Each Dig Library entry that can escalate must specify: `critical_fail_flag: "flag_key_name"`. If `CSM.GetState(charId).narrative_flags[critical_fail_flag] == true` at Dig resolution time, the result escalates to DIG_CRITICAL_FAIL. If no `critical_fail_flag` is specified in the entry, DIG_CRITICAL_FAIL is impossible for that character at that tier.

9. The response passage for DIG_CRITICAL_FAIL is authored separately from the DIG_FAIL passage — it must reflect that the push landed in a specific wound flagged through prior narrative choices. A generic "they withdrew" response is not acceptable for DIG_CRITICAL_FAIL.

**Outcome Processing — Write Order**

10. After the response passage completes, The Dig processes the outcome in this order:
    1. If DIG_PASS: signal `DigComplete(charId, DIG_PASS)` to Scene Management. RDS handles the tier advance.
    2. If DIG_PARTIAL: call `CSM.SetFlag(charId, "dig_partial_cooldown", true)`. Signal `DigComplete(charId, DIG_PARTIAL)`.
    3. If DIG_FAIL: call `CSM.SetClosedOff(charId, true)`. Signal `DigComplete(charId, DIG_FAIL)`.
    4. If DIG_CRITICAL_FAIL: call `CSM.SetClosedOff(charId, true)`, then call `CSM.SetFlag(charId, "dig_critical_failed", true)`. Signal `DigComplete(charId, DIG_CRITICAL_FAIL)`.
    - All CSM writes are synchronous and complete before the signal fires.

11. The Dig holds no persistent state between invocations. After signaling completion, it resets to IDLE. All state for the Dig mechanic lives in the CSM (`closed_off`, `dig_partial_cooldown`, `dig_critical_failed`) and in the Dig Library (authored data).

**Tier Escalation — Content, Not Structure**

12. The structure of the Dig interaction (three lines, player picks one, brief pause, character response) is identical across all tiers 0→4. What escalates is the *interpretive difficulty* of the authored content:
    - Tier 0→1: The DIG_FAIL line is clearly out of register with the character's established persona. An attentive player can identify it.
    - Tiers 1→2 and 2→3: All three lines are plausible. The correct read requires having noticed a specific behavioral pattern across scenes.
    - Tier 3→4: All three lines are close. The distinction requires understanding the character's specific pattern, not just their general mood.
    - Tier 4→5: The climactic Dig. The lines probe what the player understands about the character's private truth. A DIG_FAIL at this tier is the most devastating outcome in the game.

13. Difficulty escalation through content means that a player who has played more scenes at a given tier has more evidence for their choice — scenes contain behavioral signals that are answered by the correct line. This is the informal precursor to the Signal Reading system (#11, Vertical Slice). At MVP, there is no formal "signal score"; the player carries knowledge in memory, and the Dig lines are designed to be discoverable through attentive play.

**Authoring Constraints for the Dig Library**

14. The Dig Library is a required data asset with one entry per `{charId, tier}` for all tier-advances the character can undergo (tiers 0–4, up to 5 entries per character). At MVP with 3–5 characters: 15–25 entries. Each entry contains: three lines, three outcome assignments (PASS/PARTIAL/FAIL), three or four response passages (PASS, PARTIAL, FAIL, and optionally CRITICAL_FAIL), and an optional `critical_fail_flag` key.

15. A Dig entry with DIG_PARTIAL unassigned is valid — it means this character has no partial result at this tier; a near-miss produces DIG_FAIL. Authors may choose this for characters authored as "less forgiving." This choice must be documented in the character's arc notes.

---

### States and Transitions

The Dig subsystem has an internal state machine, active only while Scene Management is in RUNNING state.

| State | Description | Entry Condition |
|---|---|---|
| IDLE | Awaiting invocation | Initialization; after COMPLETE |
| PRESENTING | Portrait and three lines visible; waiting for player selection | `StartScene(DigEntry)` received |
| RESOLVING | Outcome determined; checking CRITICAL_FAIL condition; writing CSM | Player selects a line |
| RESPONDING | Character response passage visible; waiting for player dismiss | CSM writes complete |
| COMPLETE | Result signaled to Scene Management; resetting to IDLE | Response dismissed |
| ERROR | Dig Library entry missing or CSM write failure | Entry not found; write exception |

Valid transitions:
- IDLE → PRESENTING: Scene Management invokes `StartScene(DigEntry)`
- PRESENTING → RESOLVING: Player selects one of the three lines
- RESOLVING → RESPONDING: CSM writes complete without exception
- RESPONDING → COMPLETE: Player dismisses response (confirm/tap)
- COMPLETE → IDLE: Signal sent, reset
- Any → ERROR: Dig Library entry missing; CSM write throws exception. On ERROR: signal `DigComplete(charId, DIG_PARTIAL)` as safe fallback (no CSM state written) and log `[ERROR]`.
1
---

### Interactions with Other Systems

| System | Direction | Interface |
|---|---|---|
| Scene Management (#9) | Bidirectional | **Inbound**: `StartScene(DigEntry)` — invokes The Dig for `charId`. **Outbound**: `DigComplete(charId, result)` event fires on completion. Scene Management re-evaluates gate state via RDS based on result. |
| Character State Manager (#6) | Writes | `CSM.SetClosedOff(charId, true)` (DIG_FAIL, DIG_CRITICAL_FAIL); `CSM.SetFlag(charId, "dig_partial_cooldown", true)` (DIG_PARTIAL); `CSM.SetFlag(charId, "dig_critical_failed", true)` (DIG_CRITICAL_FAIL). Reads: `CSM.GetState(charId).narrative_flags[critical_fail_flag]` to evaluate CRITICAL_FAIL condition. |
| Relationship Depth System (#1) | Indirect — via Scene Management | The Dig does not call RDS directly. Scene Management receives `DigComplete(charId, DIG_PASS)` and signals RDS to advance the tier. |
| Dig Library (authored data) | Reads | Static ScriptableObject asset loaded at session start. Keyed by `{charId, tier}`. Read-only at runtime. |
| UI System (#16) | Output | The Dig drives its own screen. On completion, it signals Scene Management which signals the UI System on IDLE transition. The Dig does not signal the UI System directly. |
| Dialogue/Narrative Engine (#8) | None | The Dig does not use YarnSpinner. All content (lines, responses) is displayed through The Dig's own screen, not through the dialogue UI. |

## Formulas

The Dig performs no mathematical calculations. All outcomes are determined by two mechanisms:
1. **Authored mapping**: a player's line selection maps directly to a typed outcome (`DIG_PASS`, `DIG_PARTIAL`, or `DIG_FAIL`) defined in the Dig Library for that `{charId, tier}` entry.
2. **Boolean flag evaluation**: the DIG_CRITICAL_FAIL escalation is a single boolean gate — `narrative_flags[critical_fail_flag] == true AND outcome == DIG_FAIL`.

No continuous values, probability distributions, weighted scores, or interpolation exist in this system. The absence of formulas is a deliberate design characteristic: the player's judgment is the only input variable, and it is binary — the chosen line either matches the character's wound or it does not.

All formulas relevant to Dig availability (`IsDigAvailable`, `CountScenesAtTier`, `CanAdvanceTier`) are defined in the Relationship Depth System GDD (#1, Formulas F-1 through F-4) and are not duplicated here.

## Edge Cases

**EC-1: Dig Library entry missing for `{charId, tier}`**
The Dig logs `[ERROR]` naming the missing `{charId, tier}`, signals `DigComplete(charId, DIG_PARTIAL)` as a safe fallback, and returns to IDLE. No CSM writes occur. The player may retry the Dig after playing one more scene. This is a content authoring bug that must be caught before ship.

**EC-2: Dig Library entry has DIG_PARTIAL unassigned (only PASS and FAIL lines)**
Valid authored state. PARTIAL is not reachable for this character at this tier. The Dig resolves as PASS or FAIL only. Authors must document this in the character's arc notes. No runtime error.

**EC-3: All three lines in a Dig Library entry map to DIG_FAIL**
Critical content authoring error — the player can never advance this tier regardless of choice. At runtime: DIG_FAIL fires on every attempt, the recovery arc runs, then the same unpassable Dig repeats. The authoring validation tool must catch this at asset save time. At runtime, the system behaves correctly per its rules; the infinite failure loop is a content defect, not a system defect.

**EC-4: Two lines in a Dig Library entry both map to DIG_PASS**
Authoring error, not a crash. Both lines produce DIG_PASS; no state corruption occurs. Authoring validation should flag this; the runtime accepts it.

**EC-5: `critical_fail_flag` key in Dig Library entry is empty or whitespace**
`CSM.narrative_flags[""]` returns `false` (CSM rule: missing key returns false; empty key is treated as missing). The CRITICAL_FAIL condition evaluates to false; result remains DIG_FAIL. The Dig logs `[WARNING]` noting the invalid key. The authoring tool must validate that `critical_fail_flag`, if set, is non-empty.

**EC-6: `critical_fail_flag` key is set in Dig Library but the flag is not set in CSM**
Expected state for a first-attempt DIG_FAIL on a clean relationship. Condition evaluates to false; result is DIG_FAIL. No escalation. This is the normal path — CRITICAL_FAIL requires a prior flag-setting event in the character's narrative history.

**EC-7: `dig_critical_failed` flag already true when a second DIG_CRITICAL_FAIL fires**
`CSM.SetFlag(charId, "dig_critical_failed", true)` is idempotent. No error. The recovery arc gate is identical for both occurrences. Authors who need first-vs-second critical fail to branch differently must use distinct flag keys in the Dig Library entry.

**EC-8: `closed_off == true` when The Dig is invoked**
Scene Management's `GetAvailableScenes()` never appends a DigEntry when `closed_off == true`. The Dig should therefore never be invoked while closed_off. If invoked despite this (race condition or UI bug): The Dig logs `[WARNING]`, signals `DigComplete(charId, DIG_PARTIAL)` as a safe fallback, and makes no CSM writes. Does not double-set `closed_off`.

**EC-9: `depth_tier == 5` when The Dig is invoked**
Scene Management returns an empty list from `GetAvailableScenes()` at tier 5 — no DigEntry is ever injected. If invoked despite this: The Dig logs `[WARNING]`, signals `DigComplete(charId, DIG_PARTIAL)`, makes no CSM writes.

**EC-10: Character is quarantined (`is_available == false`) when The Dig is invoked**
Scene Management LOADING validation re-checks `is_available` and aborts if false. The Dig is not invoked. If somehow bypassed: The Dig logs `[WARNING]`, signals `DigComplete(charId, DIG_PARTIAL)`, makes no CSM writes.

**EC-11: Application crash during RESOLVING (after line selection, before CSM write)**
CSM was not written. On next session load, `RestoreState()` restores the pre-Dig state. Scene Management re-populates `gateReadyCharacters` via `IsDigAvailable()`. The DigEntry reappears. The player can retry the Dig. No state corruption — the Dig did not complete.

**EC-12: Application crash during RESPONDING (after CSM write, before `DigComplete` signal)**
CSM was written synchronously before the response displayed. On next session load, the written state is restored (`closed_off`, `dig_partial_cooldown`, or `dig_critical_failed` are set as appropriate). Scene Management reconstructs gate state correctly. No re-Dig occurs — the outcome was committed.

**EC-13: CSM write throws an exception during RESOLVING**
The Dig transitions to ERROR. Signals `DigComplete(charId, DIG_PARTIAL)` as a safe fallback. `closed_off` may be in an indeterminate state. Logs `[ERROR]` with charId and tier. A session reload from the most recent save is required to ensure state consistency.

**EC-14: Player confirms response immediately (before passage completes)**
Early confirm is valid — the player may dismiss the response at any time after it begins displaying. The Dig transitions to COMPLETE. No state difference from a full read.

**EC-15: Player selects the same line on a retry after DIG_PARTIAL**
The Dig has no memory of prior attempts. Any line may be selected on any attempt. If the player selects the PARTIAL line again, they receive PARTIAL again. If they select the PASS line, they receive PASS. The retry is a fresh attempt — the player should learn from the prior outcome.

## Dependencies

### Upstream (systems this one depends on)

**Scene Management / Flow Controller (#9)** — `design/gdd/scene-management.md`
Hard dependency. Scene Management is the sole caller of The Dig. Depends on: `StartScene(DigEntry)` as the invocation mechanism, Scene Management being in RUNNING state during the entire Dig interaction (ensuring no concurrent scenes), and Scene Management correctly interpreting the `DigComplete(charId, result)` signal. If Scene Management does not invoke The Dig correctly, The Dig is unreachable.

**Character State Manager (#6)** — `design/gdd/character-state-manager.md`
Hard dependency. All Dig outcomes write to CSM. Depends on: `SetClosedOff(charId, bool)`, `SetFlag(charId, key, bool)`, and `GetState(charId)` (for CRITICAL_FAIL condition check). Also depends on `narrative_flags` being a `Dictionary<string, bool>` with false-returning missing-key behavior. Any change to the CSM schema or write-method signatures must be reviewed for impact.

**Relationship Depth System (#1)** — `design/gdd/relationship-depth-system.md`
Indirect — via Scene Management. The Dig does not call RDS directly; it signals `DigComplete` to Scene Management, which signals RDS for DIG_PASS tier advancement. Depends on: the four outcome types (DIG_PASS/PARTIAL/FAIL/CRITICAL_FAIL) being correctly interpreted by RDS, the `dig_partial_cooldown` flag key being reserved and correctly cleared by RDS after one scene, and the `dig_critical_failed` flag key being treated as the gate for harder recovery arcs.

**Dig Library (authored data)**
Hard dependency. The Dig is non-functional without this asset. Depends on narrative designers authoring one entry per `{charId, tier}` for every character and tier before content ships. Validation of library completeness is a pre-ship content gate, not a runtime concern.

---

### Downstream (systems that depend on this one)

**Scene & Activity Loop (#2)** — undesigned
Depends on The Dig defining: the four outcome types and their effects on character state, the recovery arc as a post-FAIL path, and the fact that The Dig is the only path to tier advancement (no passive tier-up exists).

**Signal Reading Mechanic (#11)** — undesigned
The Vertical Slice evolution of The Dig. At MVP, signal reading is informal (the player's accumulated mental model). Signal Reading (#11) will formalize this as a separate system. At Vertical Slice, Signal Reading may provide a formal "read score" that The Dig uses to weight line outcomes — this would modify the authored-mapping approach. Flag for The Dig's architecture at Vertical Slice: this is the intended post-MVP design change.

**Secret Reveal System (#5)** — undesigned
Depends on The Dig defining: DIG_PASS at tier 4→5 as the trigger that advances `depth_tier` to 5 and auto-sets `secret_revealed = true` (CSM enforces this). The Dig itself does not own the revelation arc — that is fully owned by the Secret Reveal System. The tier 4→5 Dig is The Dig's most consequential invocation; the Secret Reveal System begins immediately after.

## Tuning Knobs

| Knob | Default | Safe Range | What It Affects |
|---|---|---|---|
| `DIG_RESPONSE_PAUSE_MS` | 800 | [300, 2000] | Duration of silence between player's line selection and the character's response appearing. Too low: the emotional beat is lost and the choice feels inconsequential. Too high: the wait becomes anxious rather than tense; breaks the flow. |
| `DIG_SCREEN_TRANSITION_MS` | 400 | [200, 800] | Duration of screen fade-in/out at Dig entry and exit. Affects how abruptly the Dig context separates from the normal scene flow. |

**Cross-reference — knobs NOT owned by The Dig:**
- `SCENES_REQUIRED[tier]` — owned by Relationship Depth System (#1, Tuning Knobs). Controls how many scenes must be completed before the Dig becomes available. Increasing this extends the "held breath" period before the Dig is reachable.
- `DIG_PARTIAL_COOLDOWN_SCENE_COUNT` — owned by Relationship Depth System (#1, Tuning Knobs). Controls how many scenes must be played after a PARTIAL before retrying. The Dig fires the flag; RDS governs the cooldown.

**Authoring knobs (Dig Library, not runtime config):**
Not runtime tuning knobs — authoring decisions made per-character per-tier in the Dig Library:
- Line mapping (which line → which outcome) — authored per entry, not a global tuning value
- `critical_fail_flag` key — authored per entry
- DIG_PARTIAL line assignment — authored per entry (present or absent)

## Visual/Audio Requirements

### Visual

**Screen Design**

The Dig screen replaces the normal dialogue UI entirely on entry. It is a full-bleed overlay drawn from the character's personal color palette — the background is not a neutral or generic dark panel; it carries the emotional temperature of the character. The vignette is always present and deepens slightly during the pause interval. No UI chrome, no decorative borders, no progress bars are visible.

The three choice lines appear as floating text directly on the screen — not inside button containers, not prefixed with labels or bullets. They are plain prose presented as plainly as possible. No timer, no loading indicator, no outcome labeling. The player reads them as words, not as UI elements.

**Portrait Direction**

- Framing: chest-to-crown crop; the character is not centered — they are positioned slightly off-axis to avoid the symmetry of a confrontation. This is an admission, not a standoff.
- Expression: neutral to guarded at PRESENTING entry. Not hostile, not warm — they are waiting to see what you do.
- Expression transitions at 650ms into the response pause (50ms before vignette pulse peak), shifting to the authored outcome expression before the response passage appears.
- Asset naming convention: `char_[name]_dig_base.png` for the PRESENTING portrait; `char_[name]_dig_[outcome].png` for each outcome expression (PASS, PARTIAL, FAIL, CRITICAL_FAIL).

**Outcome Differentiation — Atmospheric, Not Informational**

Outcome differentiation is communicated through atmosphere, never through UI labels or icons. The player should feel the outcome before reading it in the response passage.

- **DIG_PASS**: Screen warms slightly. The vignette softens. The character's palette shifts toward amber or gold tones.
- **DIG_PARTIAL**: Screen holds. No temperature change. The character's expression holds neutral — close, but not yet arrived.
- **DIG_FAIL**: Screen cools. The vignette tightens. Palette desaturates toward cooler grays at the edges.
- **DIG_CRITICAL_FAIL**: Same as DIG_FAIL cooling, plus a single desaturated frame flash (one frame only, not a sustained effect) at the moment the result resolves.

**Response Pause (800ms default)**

During the 800ms pause between line selection and response appearance:
1. The three choice lines fade out (fade duration: 200ms, beginning immediately at selection).
2. The portrait holds its base expression.
3. The vignette pulse peaks at 600ms — a single breath-hold at the threshold.
4. At 650ms, the portrait expression transitions to the outcome expression.
5. At 800ms, the response passage begins appearing.

**Screen Transitions**

- **Entry**: The background desaturates edge-inward as the Dig screen opens — the normal scene context drains away and the Dig palette fills in. Transition duration: `DIG_SCREEN_TRANSITION_MS` (default 400ms).
- **Exit**: The portrait holds 200ms after the player dismisses the response before dissolving. The background desaturates outward. Normal scene context returns.

---

### Audio

**Ambient Layer**

The Dig screen has no music track. In its place, a character-specific ambient — a stripped harmonic residue of the character's music theme, not a full arrangement — plays at −24 to −30 dBFS throughout the interaction. It should read as presence, not score.

The ambient begins with a brief silence gap of approximately 200ms on Dig screen entry — the moment of entering the held breath — before the ambient fades in.

**Selection Sound**

A breath or paper-handling sound on line selection: quiet, organic, non-mechanical. It should sound like a physical act of commitment, not a UI click. No bright UI confirm sound.

**Response Pause**

During the 800ms pause, the ambient continues unchanged. No music sting, no ramp, no additional sound. The silence within the ambient is the tension.

**Outcome Differentiation — Pitch-Based**

Outcome expression is through subtle pitch modulation of the ambient, not through discrete stings or added layers:

- **DIG_PASS**: Ambient shifts +1 to +2 semitones over the course of the response passage. The warmth is gradual.
- **DIG_PARTIAL**: Ambient remains unchanged. No shift.
- **DIG_FAIL**: Ambient drops −1 semitone. The presence becomes heavier.
- **DIG_CRITICAL_FAIL**: Same −1 semitone shift as DIG_FAIL, plus a sub-80Hz transient at the moment of the one-frame flash. The transient is felt more than heard — a physical weight under the threshold of clarity.

No stings, no discrete musical events, no audio cues that announce the outcome before the response passage delivers it.

## UI Requirements

**Input Model**

The three choice lines are navigable via d-pad (required for Steam Deck compatibility) and mouse hover/click. No touch input. Lines are not buttons and must not use Unity button components — they are styled text elements with a custom selection state (highlight or underline on focus, no border or fill box). Keyboard arrow navigation (up/down) cycles through the three lines.

**What Is Never Shown**

- No outcome labels or icons before or during the response passage
- No timer or progress indicator during the selection or pause states
- No loading indicator
- No "retry" button — returning to the Dig is handled through scene flow, not in-screen UI

**Dismiss / Confirm**

After the response passage appears, a single dismiss input (Enter/Space/A button/click) transitions to COMPLETE. The player may dismiss immediately after the passage begins; no minimum wait. No "Next" button is displayed; the dismiss hint (if shown at all) should be subtle — a controller prompt icon fading in at the bottom after 1 second.

**Text Scaling**

Text scaling accessibility settings must apply to the response passages. The three choice lines also scale. At the largest scale setting, lines must not overlap the portrait. The layout must be verified at all supported scale settings before ship.

**Screen Availability Guard**

The Dig screen is inaccessible outside of RUNNING state. If the game pauses or loses focus during a Dig, the Dig screen holds its current state on resume with no input processing during the pause.

> **UX Flag**: A dedicated UX spec (`design/ux/dig-screen.md`) should be created during Pre-Production using `/ux-design`. The spec must cover: line layout and spacing at all scale settings, portrait placement across resolutions, d-pad navigation cycle behavior, the dismiss hint presentation, and behavior during window focus loss.

## Acceptance Criteria

### Category 1: The Admission Interaction

| ID | Criterion | Type |
|---|---|---|
| DIG-001 | GIVEN a valid Dig Library entry for `{charId, tier}` / WHEN `StartScene(DigEntry)` is called / THEN three authored lines are displayed with the character portrait and no timer is shown | Logic / BLOCKING |
| DIG-002 | GIVEN three lines are displayed / WHEN the player selects a line / THEN no additional confirmation is required before RESOLVING begins | Logic / BLOCKING |
| DIG-003 | GIVEN a line is selected / WHEN RESOLVING begins / THEN a pause of `DIG_RESPONSE_PAUSE_MS` elapses before the response passage appears | Logic / BLOCKING |
| DIG-004 | GIVEN a response passage is shown / WHEN the player confirms dismiss / THEN `DigComplete(charId, result)` fires and The Dig transitions to IDLE | Logic / BLOCKING |
| DIG-005 | GIVEN a Dig Library entry with DIG_PARTIAL unassigned / WHEN the player selects any line / THEN only DIG_PASS and DIG_FAIL outcomes are reachable | Logic / BLOCKING |

### Category 2: Outcome Processing

| ID | Criterion | Type |
|---|---|---|
| DIG-010 | GIVEN a line mapped to DIG_PASS / WHEN the player selects it / THEN `DigComplete(charId, DIG_PASS)` fires and no CSM writes occur in The Dig | Logic / BLOCKING |
| DIG-011 | GIVEN a line mapped to DIG_PARTIAL / WHEN the player selects it / THEN `CSM.narrative_flags["dig_partial_cooldown"] == true` AND `DigComplete(charId, DIG_PARTIAL)` fires | Logic / BLOCKING |
| DIG-012 | GIVEN a line mapped to DIG_FAIL AND `critical_fail_flag` is not set in CSM / WHEN the player selects it / THEN `CSM.closed_off == true` AND `CSM.narrative_flags["dig_critical_failed"]` is NOT set AND `DigComplete(charId, DIG_FAIL)` fires | Logic / BLOCKING |
| DIG-013 | GIVEN a line mapped to DIG_FAIL AND `critical_fail_flag` IS set in CSM / WHEN the player selects it / THEN `CSM.closed_off == true` AND `CSM.narrative_flags["dig_critical_failed"] == true` AND `DigComplete(charId, DIG_CRITICAL_FAIL)` fires | Logic / BLOCKING |
| DIG-014 | GIVEN any DIG_PASS outcome / WHEN `DigComplete` fires / THEN The Dig has NOT called `CSM.SetDepthTier()` — tier advance is verified to be handled by RDS via Scene Management | Logic / BLOCKING |
| DIG-015 | GIVEN any outcome / WHEN all CSM writes complete / THEN `DigComplete` fires within the same frame (no deferred or async write path) | Logic / BLOCKING |

### Category 3: Error and Edge Case Handling

| ID | Criterion | Type |
|---|---|---|
| DIG-020 | GIVEN a Dig Library entry is missing for `{charId, tier}` / WHEN `StartScene(DigEntry)` is called / THEN an `[ERROR]` log is emitted AND `DigComplete(charId, DIG_PARTIAL)` fires AND no CSM writes occur | Logic / BLOCKING |
| DIG-021 | GIVEN `CSM.closed_off == true` when `StartScene(DigEntry)` is called / WHEN The Dig is invoked / THEN a `[WARNING]` is logged AND `DigComplete(charId, DIG_PARTIAL)` fires AND no CSM writes occur | Logic / BLOCKING |
| DIG-022 | GIVEN `depth_tier == 5` when `StartScene(DigEntry)` is called / WHEN The Dig is invoked / THEN a `[WARNING]` is logged AND `DigComplete(charId, DIG_PARTIAL)` fires AND `depth_tier` remains 5 | Logic / BLOCKING |
| DIG-023 | GIVEN The Dig transitions to ERROR (CSM write exception) / WHEN ERROR is entered / THEN `DigComplete(charId, DIG_PARTIAL)` fires as fallback AND an `[ERROR]` log names charId and tier | Logic / BLOCKING |
| DIG-024 | GIVEN a DIG_PARTIAL outcome on attempt 1 / WHEN the player retries and selects the DIG_PASS line / THEN DIG_PASS fires (no prior-attempt memory) | Logic / BLOCKING |

### Category 4: State Machine

| ID | Criterion | Type |
|---|---|---|
| DIG-030 | GIVEN The Dig is in PRESENTING state / WHEN `StartScene(DigEntry)` is called again / THEN the second call is a no-op (handled by Scene Management RUNNING lock) | Integration / BLOCKING |
| DIG-031 | GIVEN The Dig completes successfully / WHEN COMPLETE transitions to IDLE / THEN The Dig holds no state from the prior invocation (clean reset verified) | Logic / BLOCKING |

### Category 5: Cross-System Integration

| ID | Criterion | Type |
|---|---|---|
| DIG-040 | GIVEN DIG_PASS fires / WHEN Scene Management receives `DigComplete(charId, DIG_PASS)` / THEN RDS advances `depth_tier` by 1 AND the new tier is reflected in the next `GetAvailableScenes()` call | Integration / BLOCKING |
| DIG-041 | GIVEN DIG_FAIL fires and a RECOVERY scene is registered for this character at the current tier / WHEN `GetAvailableScenes(charId)` is next called / THEN only RECOVERY scenes are returned AND no DigEntry appears | Integration / BLOCKING |
| DIG-042 | GIVEN a DIG_PARTIAL outcome / WHEN one scene of any type is completed for this character / THEN `dig_partial_cooldown` is false AND `IsDigAvailable(charId)` returns true | Integration / BLOCKING |

## Open Questions

**OQ-1: Dig Library asset format**
The Dig Library is specified as a ScriptableObject keyed by `{charId, tier}`. The exact Unity asset structure (a single ScriptableObject with a serialized Dictionary, vs. one ScriptableObject per character, vs. a flat list with runtime lookup) is unresolved. This is an implementation decision for the architecture phase — the GDD specifies behavior only. Flag for `/architecture-decision` before implementation begins.

**OQ-2: Expression portrait count**
Each character requires up to 5 dig-portrait assets (base + PASS + PARTIAL + FAIL + CRITICAL_FAIL) per character. At MVP with 3–5 characters, this is 15–25 art assets. The art budget for this should be confirmed with the Art Director before the Dig Library authoring sprint. Characters with no CRITICAL_FAIL entry may reuse the FAIL portrait.

**OQ-3: Signal Reading integration at Vertical Slice**
At Vertical Slice, Signal Reading (#11) is expected to evolve the authored-mapping approach into a formalized read-score system. The architecture of The Dig should be designed with this extension in mind — the outcome-determination step (Rule 4: "each line maps to a primary outcome") should be replaceable without rebuilding the Dig state machine. Flag for the Vertical Slice design sprint.

**OQ-4: Dig Library validation tooling**
EC-3 (all three lines mapped to DIG_FAIL) and EC-4 (two lines mapped to DIG_PASS) are content authoring errors that must be caught by a validation tool at asset save time. The tools programmer needs to build this validation hook as part of the Dig Library implementation. No runtime guard is sufficient for EC-3 — it produces an infinite failure loop, not a crash.
