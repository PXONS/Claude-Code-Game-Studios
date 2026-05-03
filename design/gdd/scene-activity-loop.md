# Scene & Activity Loop

> **Status**: Designed
> **Author**: Design session + agents
> **Last Updated**: 2026-05-03
> **Implements Pillar**: Pillar 2 (You Choose How Deep), Pillar 3 (Desire Drives Discovery)

## Overview

The Scene & Activity Loop is the moment-to-moment structure of Masks: the rhythm by which a player chooses a character, selects what to do with them, and moves through the content that builds depth over time. Every session of meaningful play passes through this loop. It is the player-visible surface of a system stack that runs beneath it — Scene Management handles sequencing, the Relationship Depth System tracks progress, the Character State Manager holds state — but from the player's perspective, the loop is simply the experience of returning to someone, choosing again, and feeling the relationship move.

The loop has three recurring elements that a player can encounter in any session: **scenes**, **activities**, and **the Dig**. Scenes are narrative encounters delivered through dialogue — a conversation, a shared moment, a confrontation. Activities are structured interactions distinct from pure dialogue: something the characters do together rather than just say. Activities may unlock or lead into scenes; scenes generate the emotional material that scenes at higher tiers respond to. At the Dig threshold, the player may choose to attempt a Dig — the risk-and-consequence mechanic that gates tier advancement. These three elements are not a queue the player works through linearly; they are a set of options the player chooses from freely, with availability governed by tier, character state, and scene history.

At MVP, activities are present in the loop's design as a first-class element but content-minimal — one or two activity slots per character provide the loop structure without requiring the full Activity System (#10), which is a Vertical Slice deliverable. The loop GDD defines the full intended structure; Activity System (#10) will elaborate the activity mechanic in detail. At MVP, an activity may be implemented as a structured scene variant — same Yarn execution path, different framing and optional mechanical wrapper — tagged with a dedicated `engagementType` that the loop and Scene Registry understand.

## Player Fantasy

The select screen is the heart of the loop, and it rewards returning. On a first session, it is a grid of strangers. On the twentieth session, it is a private gallery — and what makes it private is yours alone.

By the time the loop has run a dozen times, no character on the select screen looks the same to you as they did when you started. Some faces you've barely touched — their public performance still intact, nothing you've seen behind it. Some faces you've pressed past the surface: you know the texture of their deflection, the specific way they change the subject. One face you know the shape of the secret, or you've already seen it. The select screen holds all of this simultaneously, and the act of opening it is the act of consulting what you privately know. Not content to unlock. Not quests to clear. People you have or haven't reached, yet.

The fantasy of the loop is **private knowledge accumulating**. Every scene played, every Dig attempted or deferred, every time you chose to linger in a character's current tier instead of pushing — all of it lives in how you see them the next time you return. The grid doesn't annotate itself. Nothing on the screen tells you what you've earned. But you know. And the pleasure of the loop is the pleasure of knowing things about people that aren't yours to know.

Within a character's scene list, the feeling narrows: **quiet deliberation before an intimate act**. You can see their available scenes. You know which one is the Dig. You choose to pick a softer scene, and that choice is real — you're deciding tonight's dosage. You're choosing to stay one more session in the comfort of their known face before you push into the part they don't show anyone. That decision costs nothing mechanically. It costs something emotionally. That's why it matters.

**Design test this must pass**: A returning player who has played for ten hours should look at the character select screen and feel the *specific* weight of what they know and don't know about each face. If they feel like they're scanning a content menu — checking who has unplayed scenes — the loop failed to make the knowledge personal.

## Detailed Design

### Core Rules

**Character Select Screen**

1. The character select screen is displayed whenever Scene Management is in IDLE state. The loop surfaces a grid of character portraits — one per registered, non-quarantined character (`is_available == true` in CSM). Characters appear in **fixed authoring order** defined by the roster data asset. Order does not change based on availability state, depth tier, or session. Reordering by state would make the grid feel like a task list rather than a gallery.

2. Characters whose `depth_tier == 5` (route complete) remain visible on the select screen permanently. Their portrait enters a **completed visual state** (distinct expression or treatment authored per character). They are non-interactive — selecting them is a no-op or opens a non-scene summary state. They are never removed from the grid.

3. Each character portrait carries **two optional signal states** beyond the base portrait:
   - **GATE_READY state**: when `charId` is in Scene Management's `gateReadyCharacters` set, the portrait shifts to an authored GATE_READY expression. No text badge, no label, no icon. The change is atmospheric — a different expression, a subtle lighting shift. The player notices something changed; the loop does not name it.
   - **Closed-off state**: when `CSM.GetState(charId).closed_off == true`, the portrait uses an authored closed-off expression — withdrawn, guarded, less available. No text label. The player understands through the visual that this character is not currently reachable.
   - Portrait signal state is evaluated at the moment the character select screen is rendered (IDLE transition). It is not polled per-frame.

4. The character select screen has no numeric progress indicators: no depth counter, no "X scenes remaining," no percentage bar. Players discover availability by selecting a character.

5. *(Provisional — pending Character Roster #17)* The loop provisionally shows all CSM-registered characters with `is_available == true`. Character Roster (#17) may introduce additional availability filtering (e.g., characters unlocked progressively). When #17 is designed, this rule must be revisited.

**Scene Select Within a Character**

6. Selecting a character with available scenes opens their **scene select list** — the result of `Scene Management.GetAvailableScenes(charId)`. Each `SceneEntry` in the list carries two display fields (extending the Scene Registry entry):
   - `displayName` — a short title shown in the list (e.g., "The Museum, Late")
   - `teaserLine` — a 1–2 sentence mood hook shown on hover or selection (e.g., "She's never brought anyone here before.")
   - `engagementType` is **not shown** to the player. No label distinguishes a CORE scene from an AMBIENT one.
   The `DigEntry`, when present, also carries a `displayName` and `teaserLine` authored separately per character and tier in the Dig Library.

7. Within the scene select, entries appear in **authoring order from the Scene Registry** — the order in which entries appear in the registry asset. The `DigEntry` always appears **last** (enforced by Scene Management Rule 9). The loop does not re-sort or re-group by `engagementType`.

8. If the scene list is **empty** (all scenes played, no DigEntry, not closed-off): the loop shows the character's portrait with no available entries. No error message, no "come back later" text — the portrait simply has nothing below it. This state is a **content-debt signal**: if reachable during playtesting, it indicates the narrative team has authored too few scenes at this tier for this character. The loop surfaces it visibly so content gaps are caught early.

9. If the scene list is **empty because the character is closed_off**: `GetAvailableScenes()` returns only RECOVERY scenes (if authored) or an empty list. The loop presents whatever is returned. If empty and `closed_off == true`, the portrait is in closed-off state (Rule 3) and has no entries — the player cannot engage this character until a recovery scene is authored and available.

10. The loop provides **no "recommended first scene"** for new or returning characters. All entries in `GetAvailableScenes()` are presented as equally valid options. This is intentional: the player's choice of what to pick first is the beginning of "quiet deliberation before an intimate act" (see Player Fantasy). An algorithmic recommendation would substitute a machine read for the player's own desire.

**Activities in the Loop**

11. Activities are a **5th engagementType**: `ACTIVITY`. At MVP, activities appear in the scene list as authored `ACTIVITY`-typed Scene Registry entries. They are displayed identically to other entries — a `displayName` and `teaserLine` — with no type label shown. The Dig always remains last.

12. At MVP, `ACTIVITY` entries **do not count toward the GATE_READY threshold** (same rule as `AMBIENT`). The Relationship Depth System counts only `CORE` and `DEPTH` types toward `SCENES_REQUIRED`. This may be revisited when Activity System (#10) is designed at Vertical Slice — activities may earn their own threshold or explicitly count as `DEPTH` for certain character routes.

13. The Scene Registry startup validator (Scene Management Rule 3) must accept `ACTIVITY` as a valid `engagementType`. Any Scene Management implementation that validates against a 4-value enum must be updated to include `ACTIVITY` as a 5th valid value. *(Cross-GDD flag: Scene Management GDD must note this extension before implementation begins.)*

**Post-Completion Navigation**

14. When Scene Management transitions to IDLE after a `CLEAN_COMPLETION`:
    - If the completed scene's character **still has available scenes** (the next call to `GetAvailableScenes(charId)` returns a non-empty list): the loop returns to that character's **scene select**.
    - If the completed scene's character's list is **now empty**: the loop returns to the **character select**.
    - The post-completion `GetAvailableScenes(charId)` call is the navigation branch decision. The result is also used to immediately populate the scene select if the loop stays within that character.

15. When Scene Management transitions to IDLE after a **Dig completion** (any `DigOutcome`): the loop **always returns to the character select**, regardless of whether scenes remain. A Dig is a significant relational moment; the loop grants a beat between the outcome and the player's next choice. If `DIG_PASS` advanced the tier, the player may immediately re-enter the character's scene select to see the new tier's content — but this is their choice, not the loop's.

16. When Scene Management transitions to IDLE after an `ABANDONED` completion: the loop returns to the **scene select for the same character** — the abandoned scene remains in the list and can be re-played. The player is exactly where they were before.

**Scene Abandonment**

17. Within a scene or activity, the player may exit before the scene completes. The loop presents an **exit prompt** ("Leave this scene?" with confirm/cancel). If confirmed, the loop signals Scene Management to stop the current scene. Scene Management transitions to IDLE via `DialogueStopped(ABANDONED)` — the scene is not recorded. The loop returns to the scene select for that character.

18. The exit prompt is available throughout the scene. No minimum playtime is required before the player may exit. Exiting is a clean operation — no consequence beyond the scene not being recorded.

---

### States and Transitions

The loop has two navigation states. These are UI-layer states, not Scene Management states.

| State | Description | Entry Condition |
|---|---|---|
| CHARACTER_SELECT | Character grid visible; player selects a character | Session start; post-scene (empty list); post-Dig (any outcome); post-complete (empty list) |
| SCENE_SELECT | Scene list for one character visible; player selects a scene | Character selected with non-empty available scenes |

The loop transitions between these states in response to Scene Management's IDLE signal. Scene Management's own state machine (IDLE/LOADING/RUNNING/COMPLETING/ERROR) governs execution; the loop only acts on IDLE transitions.

Valid loop transitions:
- CHARACTER_SELECT → SCENE_SELECT: player selects a character with `GetAvailableScenes()` returning non-empty
- CHARACTER_SELECT → CHARACTER_SELECT (no transition): player selects a character with empty scene list — stays in CHARACTER_SELECT, shows empty list inline
- SCENE_SELECT → (Scene Management RUNNING): player selects an entry; `StartScene()` is called
- SCENE_SELECT → CHARACTER_SELECT: player uses back navigation; no scene started
- Post-IDLE (CLEAN_COMPLETION, non-empty list) → SCENE_SELECT for same character
- Post-IDLE (CLEAN_COMPLETION, empty list) → CHARACTER_SELECT
- Post-IDLE (Dig complete, any outcome) → CHARACTER_SELECT
- Post-IDLE (ABANDONED) → SCENE_SELECT for same character

---

### Interactions with Other Systems

| System | Direction | Interface |
|---|---|---|
| Scene Management (#9) | Bidirectional | **Inbound**: IDLE-state signal (loop re-evaluates navigation destination). **Outbound**: `GetAvailableScenes(charId)` on character or post-scene evaluation; `StartScene(entry)` on scene/activity/Dig selection. |
| Character State Manager (#6) | Reads | `CSM.GetState(charId)` — evaluated at character select render for portrait signal state (`closed_off`). `is_available` accessed via Scene Management's `GetAvailableScenes()` (not queried directly). |
| Relationship Depth System (#1) | Indirect | Loop responds to GATE_READY state via Scene Management's `gateReadyCharacters` (surfaced by DigEntry presence in `GetAvailableScenes()`). Loop does not call RDS directly. |
| UI System (#16) | Renders | The loop defines what states the UI must render: CHARACTER_SELECT grid, SCENE_SELECT list, portrait signal states (base/GATE_READY/closed-off/completed), empty-list state, scene entry display (displayName + teaserLine). UI System GDD owns visual implementation. |
| Character Roster (#17) | Reads (provisional) | *(Undesigned — provisional)* Loop shows all `is_available == true` CSM-registered characters. Roster (#17) may add filtering. Flag for revisit when #17 is designed. |
| Activity System (#10) | Deferred | *(Vertical Slice)* Loop defines the `ACTIVITY` engagementType slot. Activity System (#10) elaborates mechanics. No direct interface at MVP. |

## Formulas

The Scene & Activity Loop owns no mathematical formulas. All numeric operations are delegated to upstream systems:

- **Threshold math** (how many scenes are required, whether the player has met the threshold, whether the Dig is available): defined in Relationship Depth System GDD, Formulas F-1 through F-4 (`SCENES_REQUIRED`, `CountScenesAtTier`, `CanAdvanceTier`, `IsDigAvailable`).
- **Availability filtering** (which scenes are visible for a character at a given tier and state): defined in Scene Management GDD, Formulas section (`GetAvailableScenes` pseudocode).

The loop's own logic is entirely boolean and navigational: state comparisons (`closed_off == true`, `depth_tier == 5`), list non-empty checks (`GetAvailableScenes().Count > 0`), and authoring-order indexing (constant at data-asset load time). None of these constitute formulas requiring variable definitions or output ranges.

## Edge Cases

**EC-1: Portrait signal priority conflict — `closed_off == true` AND `charId` in `gateReadyCharacters`**
Both conditions can be true simultaneously (Scene Management retains gate entries through close-off per SM-062). `closed_off` always takes precedence. Portrait renders the closed-off expression; GATE_READY expression is suppressed until `closed_off` is cleared. Portrait evaluation order must be explicit: check `closed_off` first, then check `gateReadyCharacters`.

**EC-2: Portrait state is not re-evaluated while in SCENE_SELECT**
Portrait signal state is evaluated only at CHARACTER_SELECT render (IDLE transition). If an IDLE signal fires while the player is in SCENE_SELECT, no character portrait is re-evaluated. Portraits are re-evaluated only when the loop transitions into CHARACTER_SELECT — not per-frame, not during SCENE_SELECT. This prevents CSM polling for characters not currently displayed.

**EC-3: Character with empty scene list (non-closed-off) tapped on CHARACTER_SELECT**
The loop does not transition to SCENE_SELECT for an empty list. The loop stays in CHARACTER_SELECT and renders the empty-list inline view for that character. Tapping any other character immediately replaces the view. No back navigation is required. The empty view is not a SCENE_SELECT sub-state.

**EC-4: `displayName` or `teaserLine` missing from a `SceneEntry`**
Scene Registry startup validation does not check `displayName` or `teaserLine` (loop-level additions). A null or empty `displayName` renders as `"[untitled]"` with an `[ERROR]` log naming the `sceneId`. A null `teaserLine` renders with no teaser — no crash, no skip. The entry remains selectable. Flag as a content-authoring bug.

**EC-5: `displayName` or `teaserLine` missing from a `DigEntry`**
`DigEntry` is synthetic — not backed by the Scene Registry, so startup validation never checks its display fields. Scene Management must verify that the Dig Library contains a `displayName` for the injected `DigEntry` before returning it. If missing: the `DigEntry` is still returned (Dig remains available), an `[ERROR]` log names `charId` and `depth_tier`, and the loop renders a fallback `displayName` per EC-4 rules.

**EC-6: Post-CLEAN_COMPLETION, `GetAvailableScenes()` returns only a `DigEntry`**
A DigEntry-only list is non-empty. The loop returns to SCENE_SELECT for that character, showing only the Dig entry. A DigEntry-only list is not equivalent to an empty list for navigation purposes.

**EC-7: Post-DIG_PASS, GATE_READY portrait state may linger before `gateReadyCharacters` is updated**
Portrait state is evaluated only after the IDLE signal fires — which fires only after all COMPLETING accounting (including RDS updates) is complete per Scene Management Rule 19. Portrait re-evaluation on IDLE is always post-accounting. No interim portrait flash is possible if this sequence is respected.

**EC-8: ACTIVITY completion must not suppress COMPLETING accounting chain**
`ACTIVITY` entries do not count toward GATE_READY threshold, which could lead a programmer to incorrectly infer they bypass COMPLETING entirely. ACTIVITY completion is a `CLEAN_COMPLETION` like any other `engagementType`. `RDS.OnSceneCompleted()` is called in full — including `dig_partial_cooldown` clearance if set. No special-casing of ACTIVITY in COMPLETING.

**EC-9: `DialogueStopped(CLEAN_COMPLETION)` fires while abandonment exit prompt is displayed**
If the Yarn script reaches its end node while the exit prompt is open, `CLEAN_COMPLETION` takes priority. The prompt is dismissed and the loop follows the CLEAN_COMPLETION navigation path (Rule 14). The abandonment path is only taken if the player explicitly confirms exit before `CLEAN_COMPLETION` fires.

**EC-10: Back navigation from SCENE_SELECT — other characters' portrait state may have changed during the visit**
Portrait state is not cached across SCENE_SELECT visits. Returning to CHARACTER_SELECT via back navigation triggers a full portrait state re-evaluation for all `is_available == true` characters — identical to a fresh IDLE-triggered render.

**EC-11: All characters reach `depth_tier == 5` — CHARACTER_SELECT grid is entirely non-interactive**
The loop must detect this condition (all `is_available == true` characters have `depth_tier == 5`) and signal a **game-complete state** to the UI System (#16) / Endings system (#18) rather than leaving the player on a permanently non-interactive grid. The game-complete screen is owned by those downstream systems; the loop owns the detection signal.

**EC-12: `ACTIVITY` engagementType absent from Scene Management's startup validator**
If Scene Management's validator recognizes only 4 `engagementType` values, all ACTIVITY registry entries are rejected at startup with `[ERROR]` and silently unavailable. This cannot be self-healed at runtime. The acceptance criteria (SAL-020) must include a test verifying an ACTIVITY-typed entry passes startup validation and appears in `GetAvailableScenes()` before any ACTIVITY content enters the registry.

## Dependencies

### Upstream (systems this one depends on)

**Scene Management / Flow Controller (#9)** — `design/gdd/scene-management.md`
Hard dependency. The loop is the presentation layer over Scene Management. Depends on: `GetAvailableScenes(charId)` (availability query and DigEntry injection), `StartScene(entry)` (scene execution handoff), IDLE-state signal (navigation trigger), and Scene Management correctly distinguishing `CLEAN_COMPLETION`, `ABANDONED`, and Dig outcomes. Also requires Scene Management's startup validator to accept `ACTIVITY` as a 5th valid `engagementType` (Rule 13 / EC-12 cross-GDD flag).

**Character State Manager (#6)** — `design/gdd/character-state-manager.md`
Reads for portrait signal state. Depends on: `CSM.GetState(charId).closed_off` (closed-off portrait state) and `depth_tier == 5` (completed character state). The loop does not write to CSM directly. `is_available` is accessed indirectly via Scene Management's availability filter.

**Relationship Depth System (#1)** — `design/gdd/relationship-depth-system.md`
Indirect dependency — no direct interface. The loop reads GATE_READY state through Scene Management's `gateReadyCharacters` (surfaced as DigEntry presence in `GetAvailableScenes()`). Depends on RDS correctly maintaining GATE_READY state, the `dig_partial_cooldown` clearance rule, and `SCENES_REQUIRED` defaults that determine how many scenes a player must play before the Dig is surfaced.

**Character Roster / Availability (#17)** — *(Undesigned — provisional)*
Provisional dependency. The loop provisionally shows all `is_available == true` CSM-registered characters. Roster (#17) may add its own availability filtering (e.g., characters unlocked progressively or gated behind story events). When #17 is designed, Rule 5 and the character select screen behavior must be revisited. This dependency must be flagged for the Roster design sprint.

---

### Downstream (systems that depend on this one)

**UI System (#16)** — undesigned
Depends on this system defining: the CHARACTER_SELECT and SCENE_SELECT states, portrait signal states (base/GATE_READY/closed-off/completed) and what data backs each, the scene list entry structure (`displayName` + `teaserLine`), empty-list behavior, game-complete signal (EC-11), and back navigation behavior. UI System GDD cannot be authored without this GDD specifying the loop's navigation contract.

**Activity System (#10)** — undesigned (Vertical Slice)
Depends on this system defining: the `ACTIVITY` engagementType slot, the rule that ACTIVITY entries do not count toward GATE_READY threshold at MVP, and the fact that ACTIVITY entries participate in the full COMPLETING accounting chain (including `dig_partial_cooldown` clearance). Activity System (#10) elaborates activity mechanics without restructuring the loop.

**Endings / Route Completion (#18)** — undesigned (Alpha)
Depends on EC-11's game-complete detection: when all `is_available == true` characters reach `depth_tier == 5`, the loop signals a game-complete state. The Endings system owns the presentation of this state. The loop owns the detection.

### Bidirectional Notes

- **Loop ↔ Scene Management**: the loop calls Scene Management for availability queries and execution; Scene Management signals the loop on every IDLE transition. This is the primary bidirectional interface.
- **Loop ↔ UI System**: the loop defines navigation states and signals; the UI System renders them. The UI System queries availability on demand via the loop's Scene Management interface; it does not hold its own availability cache.

## Tuning Knobs

| Knob | Type | Default | Safe Range | What It Affects |
|---|---|---|---|---|
| `CHARACTER_SELECT_PORTRAIT_COLS` | int | 4 | [3, 6] | Number of character portrait columns in the select grid. At 4 columns, 20 characters fill 5 rows. Fewer columns = larger portraits, less scrolling at smaller cast sizes. More columns = denser grid, smaller portraits. |
| `TEASER_LINE_MAX_CHARS` | int | 120 | [60, 200] | Maximum authored character length for a `teaserLine`. Enforced by the content authoring validation tool, not at runtime. Longer teasers risk layout overflow. Shorter teasers may not convey enough mood. |

**Cross-reference — pacing and timing knobs NOT owned by this system:**
- `SCENES_REQUIRED[tier]` — Relationship Depth System (#1). Controls how many scenes must be completed at each tier before Dig is available. This is the primary pacing control for the whole game.
- `DIG_PARTIAL_COOLDOWN_SCENE_COUNT` — Relationship Depth System (#1). Controls recovery wait after DIG_PARTIAL.
- `DIG_RESPONSE_PAUSE_MS`, `DIG_SCREEN_TRANSITION_MS` — The Dig (#3).
- `SCENE_LOADING_TIMEOUT_MS`, `SAVE_RETRY_COUNT` — Scene Management (#9).

The loop's moment-to-moment pacing — how quickly players move between characters and through tiers — is entirely determined by `SCENES_REQUIRED` in RDS. The loop has no pacing knob of its own: it surfaces whatever is available and lets the player choose.

## Visual/Audio Requirements

### Visual — Character Grid (CHARACTER_SELECT)

Mental model: a wall of framed photographs. Every portrait is cropped identically — collar to crown — with individual composition fitting inside the uniform crop geometry. At 4 columns and 1920×1080, target ~220px portrait width. The grid does not paginate; it scrolls with momentum (gallery feel, not page-turn). Gutter spacing ~20–24px. Character name caption sits below the portrait in the character's palette color at 14–16px. No icons, badges, or status indicators anywhere on the grid — signal state is carried by the portrait itself.

**Portrait states (four states; all resolved at IDLE entry, not in real-time):**

- **base** — the character's public face. Polished, attractive, slightly unknowable. The expression they give the world.
- **GATE_READY** — an authored expression shift: more open, guard loosened. Warm lighting shift toward the character's personal palette color. No glow, ring, animation, or overlay. The change should read as "something is different about them today," not as a UI badge.
- **closed_off** — withdrawn expression. The character's personal palette desaturates and shifts cool. Crop may tighten slightly to emphasize containment. No label. Prioritised over GATE_READY when both flags are simultaneously true.
- **completed** (tier-5) — luminous, memory-quality treatment. Different composition: more ambient space around the figure. Non-interactive. Not greyed out — the character is finished, not locked. Should feel like a photograph you keep rather than a door you've passed.

### Visual — Scene List (SCENE_SELECT)

Upper portion of the screen shows the character portrait at larger scale than the grid version. The scene list flows below. `displayName` at 16–18px in high-opacity character palette color. `teaserLine` at 13–14px neutral near-white. DigEntry: full-saturation palette color, with a thin separator line above it — the only separator in the list, marking the threshold without labeling it. Empty-state when no scenes remain after a Dig: silence. No "nothing left" message. The list simply ends.

### Audio — CHARACTER_SELECT

Minimal interior ambient on screen entry — no music track, no theme. The space should feel like a room the player has stepped into alone. No hover sounds anywhere on this screen. Portrait selection (tapping a character to enter SCENE_SELECT): a "entering a quieter room" quality — a subtle acoustic pressure shift, or a single character-specific pitched note at low amplitude. Short, non-repeating.

### Audio — SCENE_SELECT

Character-specific ambient signature plays while on this screen — low-level, present, evoking the character's personality in sound without being a full musical cue. Scene entry (selecting a scene or activity): door-opening quality — the ambient shifts or resolves as the scene begins. DigEntry selection: a brief tension harmonic swell in the ambient layer, distinct from scene selection, signaling that a different kind of threshold is being crossed.

No audio event is attached to portrait state changes (GATE_READY, closed_off, completed states are evaluated at render time, not in real-time). Tapping a completed (tier-5) portrait is a no-op and produces no sound.

## UI Requirements

### Navigation

Both CHARACTER_SELECT and SCENE_SELECT must be fully navigable by keyboard and d-pad (Steam Deck compatibility). CHARACTER_SELECT grid: d-pad moves focus between portraits in row-major order; no wrap at row edges (focus stops at boundary). SCENE_SELECT list: d-pad up/down moves through scene entries; confirm selects; back/cancel returns to CHARACTER_SELECT. The exit prompt (Rule 18) must be reachable by keyboard/gamepad without mouse.

### Input Model

No hover-only interactions anywhere in the loop. All affordances (portrait selection, scene selection, DigEntry selection, exit) must be reachable by confirm/cancel/directional input alone. Mouse click is an alias for confirm on the focused element, not a separate input path.

### Inaccessibility Rule

Both CHARACTER_SELECT and SCENE_SELECT are inaccessible outside RUNNING state. Attempting to navigate to the select screens while Scene Management is in LOADING, COMPLETING, or ERROR state must be blocked at the loop level — not hidden at the UI level. The loop does not render the select screens during those states.

### Text Scaling

`displayName` and `teaserLine` in the scene list must respect the global text scale setting (from Accessibility System #19 when designed). At MVP: no text scaling implemented, but font sizes must be specified in a central style asset — not hardcoded — so scaling can be added without touching individual UI components.

### Character Name Caption

Name caption below each portrait in CHARACTER_SELECT: the character's palette color, 14–16px, truncated with ellipsis if the name exceeds the portrait width. No tooltip on truncation at MVP.

### DigEntry Visual Distinction

The DigEntry in the scene list must be visually distinct through color and separator alone — no icon, no badge, no label. The thin separator line above DigEntry is the only separator in the list. Implementation must not use a button component with a built-in border or background.

### UX Spec Dependency

Detailed screen layout, focus ring style, scroll behavior parameters, and portrait grid responsive behavior are deferred to `design/ux/character-select.md` and `design/ux/scene-select.md` (authored by `/ux-design` — not yet created). This GDD specifies behavioral requirements; the UX spec specifies visual geometry.

## Acceptance Criteria

*Legend: **Logic** = NUnit Edit Mode / BLOCKING | **Integration** = Play Mode / BLOCKING | **Manual** = QA walkthrough / ADVISORY*

> **Testability flags — communicate to programmer before implementation:**
> **T-SAL-01** — Loop must accept an injectable `ISceneManagement` seam so that `GetAvailableScenes()` return values can be controlled in Logic-level tests without a real Scene Management instance. Required for SAL-009, SAL-010, SAL-021, SAL-022.
> **T-SAL-02** — Portrait signal state evaluation must be expressible as a pure function `EvaluatePortraitState(charId, csmState, gateReadySet)` for Logic-level testing without a running UI. Required for SAL-EC1, SAL-007.
> **T-SAL-03** — The post-IDLE navigation decision must be expressible as a testable method or event; otherwise navigation criteria become Integration tests by default. Required for SAL-020 through SAL-025.

---

### Category 1: Character Select Screen

| ID | Criterion | Classification |
|---|---|---|
| SAL-001 | GIVEN 5 `is_available == true` characters in defined roster order / WHEN CHARACTER_SELECT renders on IDLE / THEN all 5 portraits appear in roster authoring order AND no reordering by tier, state, or session history occurs | Logic / BLOCKING |
| SAL-002 | GIVEN a character with `is_available == false` / WHEN CHARACTER_SELECT renders / THEN that portrait does NOT appear in the grid | Logic / BLOCKING |
| SAL-003 | GIVEN a character with `depth_tier == 5` / WHEN CHARACTER_SELECT renders / THEN the portrait is present using the completed visual state AND all interaction affordances are disabled | Integration / BLOCKING |
| SAL-004 | GIVEN a character with `depth_tier == 5` / WHEN the player selects that portrait / THEN no SCENE_SELECT transition occurs AND no `StartScene()` call is made | Integration / BLOCKING |
| SAL-005 | GIVEN a character whose `charId` is in `gateReadyCharacters` AND `closed_off == false` / WHEN CHARACTER_SELECT renders on IDLE / THEN the portrait uses the GATE_READY expression AND no text badge, label, or icon appears alongside it | Manual / ADVISORY |
| SAL-006 | GIVEN a character with `closed_off == true` / WHEN CHARACTER_SELECT renders on IDLE / THEN the portrait uses the closed-off expression AND no text label appears | Manual / ADVISORY |
| SAL-007 | GIVEN the loop is in SCENE_SELECT (not CHARACTER_SELECT) / WHEN portrait signal state evaluation is checked / THEN CSM is not polled for any character AND no portrait state changes occur until the next CHARACTER_SELECT IDLE transition | Logic / BLOCKING |
| SAL-008 | GIVEN the CHARACTER_SELECT and SCENE_SELECT screens / WHEN inspecting all rendering / THEN no numeric depth counter, "X scenes remaining" indicator, or percentage bar appears anywhere | Manual / ADVISORY |

---

### Category 2: Scene List

| ID | Criterion | Classification |
|---|---|---|
| SAL-009 | GIVEN a character in normal state with 3 unplayed scenes at current tier / WHEN the player selects that character / THEN the scene list is populated by `GetAvailableScenes(charId)` in registry authoring order with no sorting or grouping applied | Logic / BLOCKING |
| SAL-010 | GIVEN a scene list that includes a `DigEntry` / WHEN rendered / THEN the `DigEntry` appears last, after all `SceneEntry` items | Logic / BLOCKING |
| SAL-011 | GIVEN a scene list entry of any `engagementType` / WHEN displayed / THEN `displayName` is shown AND `teaserLine` is shown AND `engagementType` is NOT shown AND no entry is visually emphasized as "Start here" or "Recommended" | Manual / ADVISORY |
| SAL-012 | GIVEN a character in normal state with all tier scenes played and no DigEntry / WHEN the player selects that character / THEN the loop STAYS in CHARACTER_SELECT AND renders an empty inline view AND no SCENE_SELECT transition occurs AND no error message or "come back later" text appears | Integration / BLOCKING |
| SAL-013 | GIVEN a character with `closed_off == true` AND no RECOVERY scenes / WHEN their portrait is selected / THEN the loop stays in CHARACTER_SELECT AND shows the portrait in closed-off state with no scene entries below | Integration / BLOCKING |
| SAL-014 | GIVEN a `SceneEntry` with a null or empty `displayName` / WHEN the scene list renders / THEN a fallback `"[untitled]"` is shown AND an `[ERROR]` log names the `sceneId` AND the entry remains selectable | Logic / BLOCKING |
| SAL-015 | GIVEN a `DigEntry` or `SceneEntry` with a null `teaserLine` / WHEN rendered / THEN no teaser appears AND no crash or skip occurs AND the entry remains selectable | Logic / BLOCKING |

---

### Category 3: Post-Completion Navigation

| ID | Criterion | Classification |
|---|---|---|
| SAL-020 | GIVEN CLEAN_COMPLETION AND `GetAvailableScenes(charId)` returns a non-empty list / WHEN IDLE fires / THEN loop navigates to SCENE_SELECT for that character AND list is populated from that call | Integration / BLOCKING |
| SAL-021 | GIVEN CLEAN_COMPLETION AND `GetAvailableScenes(charId)` returns an empty list / WHEN IDLE fires / THEN loop navigates to CHARACTER_SELECT | Logic / BLOCKING |
| SAL-022 | GIVEN CLEAN_COMPLETION AND `GetAvailableScenes(charId)` returns only a `DigEntry` / WHEN IDLE fires / THEN loop navigates to SCENE_SELECT (DigEntry-only list is non-empty) AND the Dig entry is shown | Logic / BLOCKING |
| SAL-023 | GIVEN any `DigOutcome` (PASS, PARTIAL, FAIL, CRITICAL_FAIL) / WHEN IDLE fires / THEN loop ALWAYS navigates to CHARACTER_SELECT regardless of available scene count | Logic / BLOCKING |
| SAL-024 | GIVEN a DIG_PASS that advances the character's tier / WHEN CHARACTER_SELECT renders / THEN the character's portrait reflects the new state AND the loop does NOT auto-navigate back to SCENE_SELECT — the player must actively select the character to see new tier content | Integration / BLOCKING |
| SAL-025 | GIVEN ABANDONED / WHEN IDLE fires / THEN loop returns to SCENE_SELECT for the same character AND the abandoned scene remains in the list | Integration / BLOCKING |

---

### Category 4: Scene Abandonment

| ID | Criterion | Classification |
|---|---|---|
| SAL-030 | GIVEN a scene is RUNNING / WHEN the player triggers the exit action / THEN an exit prompt appears with confirm and cancel options | Manual / ADVISORY |
| SAL-031 | GIVEN the exit prompt is visible / WHEN the player confirms exit / THEN Scene Management receives ABANDONED AND the scene is NOT in `scenes_played` AND the loop returns to SCENE_SELECT for that character | Integration / BLOCKING |
| SAL-032 | GIVEN the exit prompt is visible / WHEN the player cancels / THEN the scene resumes AND `scenes_played` is unchanged | Integration / BLOCKING |
| SAL-033 | GIVEN the exit prompt is displayed / WHEN `DialogueStopped(CLEAN_COMPLETION)` fires before the player confirms exit / THEN the prompt is dismissed AND the loop follows CLEAN_COMPLETION navigation AND the abandonment path is NOT taken | Integration / BLOCKING |

---

### Category 5: ACTIVITY engagementType

| ID | Criterion | Classification |
|---|---|---|
| SAL-040 | GIVEN a Scene Registry entry with `engagementType == ACTIVITY` / WHEN startup validation runs / THEN the entry is accepted (not excluded) AND appears in `GetAvailableScenes()` at the correct tier | Logic / BLOCKING |
| SAL-041 | GIVEN an ACTIVITY-typed scene completes with CLEAN_COMPLETION / WHEN COMPLETING accounting runs / THEN the scene is recorded in `scenes_played` AND `dig_partial_cooldown` is cleared if set AND the scene does NOT increment the GATE_READY threshold counter | Logic / BLOCKING |
| SAL-042 | GIVEN an ACTIVITY-typed scene completes / WHEN COMPLETING runs / THEN the full accounting chain executes (RecordScenePlayed → Save) with no special-casing that bypasses any step | Logic / BLOCKING |

---

### Category 6: Navigation — Back and Transitions

| ID | Criterion | Classification |
|---|---|---|
| SAL-050 | GIVEN the player is in SCENE_SELECT / WHEN the player uses back navigation / THEN the loop transitions to CHARACTER_SELECT AND no `StartScene()` call is made | Integration / BLOCKING |
| SAL-051 | GIVEN the player is in SCENE_SELECT for character A / WHEN back navigation returns to CHARACTER_SELECT / THEN portrait state is re-evaluated for all `is_available == true` characters including any changes (e.g., `closed_off` change) that occurred during the SCENE_SELECT visit | Integration / BLOCKING |
| SAL-052 | GIVEN a character's empty inline view is displayed in CHARACTER_SELECT / WHEN the player selects a different character / THEN the inline view is immediately replaced without requiring back navigation | Integration / BLOCKING |

---

### Category 7: Edge Cases and Game-Complete

| ID | Criterion | Classification |
|---|---|---|
| SAL-EC1 | GIVEN a character with both `closed_off == true` AND `charId` in `gateReadyCharacters` / WHEN CHARACTER_SELECT renders / THEN the portrait uses the closed-off expression AND the GATE_READY expression is suppressed — `closed_off` evaluates first | Logic / BLOCKING |
| SAL-EC2 | GIVEN all `is_available == true` characters have `depth_tier == 5` / WHEN CHARACTER_SELECT renders / THEN the loop emits a game-complete signal to the UI/Endings system AND does not leave the player on a permanently non-interactive grid | Integration / BLOCKING |

## Open Questions

1. **Character Roster source at MVP** — Character Roster (#17) is undesigned. The loop currently assumes all CSM-registered characters with `is_available == true` are shown. When #17 is designed, the availability rule may become more complex (session-based rotation, unlock gates, etc.). Loop Rules 1–3 and EC-1 must be revisited against the #17 GDD.

2. **ACTIVITY content format at MVP** — The loop treats ACTIVITY as a 5th `engagementType` but defers mechanical elaboration to Activity System (#10, Vertical Slice). At MVP, activities may be implemented as structured scene variants using the same YarnSpinner execution path. The loop GDD makes no assumption about the activity wrapper format; Activity System (#10) owns that decision.

3. **engagementType registry** — The `engagementType` constant in `design/registry/entities.yaml` currently lists 4 values: CORE, DEPTH, AMBIENT, RECOVERY. ACTIVITY must be added as a 5th value. Flagged for registry update immediately following GDD completion.

4. **Scene Management cross-GDD flag** — Rule 13 specifies that Scene Management's startup validator must accept ACTIVITY as a 5th valid engagementType. The `design/gdd/scene-management.md` GDD does not yet document this. A retrofit note should be added to scene-management.md after this GDD is complete.

5. **UX specs** — `design/ux/character-select.md` and `design/ux/scene-select.md` do not exist. The UI Requirements section above defers layout geometry and scroll behavior to these files. These should be authored via `/ux-design` before UI implementation begins.

6. **Game-complete signal owner** — EC-9 specifies that when all available characters reach depth_tier == 5, the loop emits a game-complete signal. The receiver (UI System #16 / Endings #18) is not yet designed. The signal interface must be confirmed when those systems are specified.
