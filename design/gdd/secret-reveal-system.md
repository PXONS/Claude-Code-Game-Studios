# Secret Reveal System

> **Status**: Designed (pending review)
> **Author**: Design session + agents
> **Last Updated**: 2026-05-04
> **Implements Pillar**: Pillar 1 (Every Face Hides a Face), Pillar 4 (Designed to Be Shared)

## Overview

The Secret Reveal System is the orchestration layer for each character's revelation arc — the multi-scene sequence that fires when `depth_tier` reaches 5. Its entry condition is a `DIG_PASS` result at the tier 4→5 boundary: when The Dig signals completion and `CSM.SetDepthTier(charId, 5)` is called, the CSM auto-sets `secret_revealed = true` and this system takes over, sequencing three authored Yarn scenes — a confrontation or build-up, the disclosure moment itself, and a short aftermath that establishes the post-reveal relationship state before handoff to the Endings system. The system does not write `secret_revealed`; it reads it as its entry condition.

The Secret Reveal System owns the rules that revelation arcs must follow: which tone categories are valid, which CSM flags it reads to shape arc content, how it distinguishes first-time reveals from revisited states, and what it signals on completion. It does not own the visual presentation of the reveal moment — that belongs to the Revelation Presentation System (#14), which subscribes to this system's disclosure event. Without the Secret Reveal System, the depth progression has no climax: the entire mechanical arc of pursuing a character through five tiers of earned trust would resolve into nothing. The reveal is why the player pushed this far.

## Player Fantasy

The revelation arc is not a surprise. By the time it fires, you have been paying attention for hours — scenes at every tier, each one adding a layer of evidence you weren't consciously cataloguing. The disclosure scene doesn't land as *revelation*. It lands as *recognition*. Of course. It was always there. You just needed the eyes for it, and the depth system gave you those eyes. The fantasy isn't discovery — it's finally having language for something you already half-knew.

The build-up scene carries a specific quality: dread-tinged certainty. You know you're close. Small details from earlier scenes are suddenly loud — a phrase they used at tier 2, the subject they never let fully land, the side of the room they always chose. The player enters the build-up scene already looking, already finding. The disclosure itself is not the emotional peak; the moment the character finishes speaking and looks up to see if you're still there — that's the peak. The aftermath scene is where it settles: the player mentally re-reads every prior scene with this character. This is the re-play hook the game is built on.

The system must serve this fantasy across all secret tones. A dark secret lands as "they trusted me with something that costs them." A heartbreaking one lands as "I caught this all along and didn't know what I was seeing." A surprising or good secret lands as "everything I thought was a gap was actually *this*, and I can live with that better than I expected." The framing doesn't change. The eyes the player arrives with do the work. That is what the depth system earned: the eyes.

**Design test this must pass**: After the disclosure scene, a player should be able to name at least one specific scene or moment from earlier in the relationship where the secret was visible in hindsight. If no earlier scene contained legible signal — if the secret lands as a non-sequitur — the authoring failed, not the system. Every character's secret must be retroactively discoverable in the depth content that precedes it.

## Detailed Design

### Core Rules

**Rule 1 — Entry is read-only.** The Secret Reveal System does not write `secret_revealed`. It observes `CSM.GetState(charId).secret_revealed == true` as its sole entry condition. The CSM is the only system that sets this field, triggered internally when `SetDepthTier(charId, 5)` is called by the Relationship Depth System. No other condition, flag, or command causes this system to activate.

**Rule 2 — One arc per character, ever.** Once this system has entered the revelation arc for a given `charId`, it never re-enters. `secret_revealed == true` is permanent and monotonic (CSM enforces this). If the system detects `secret_revealed == true` for a character already in ARC_COMPLETE, it takes no action and logs `[WARN]`.

**Rule 3 — Arc content is defined by an Arc Definition Asset.** Each character has exactly one Arc Definition Asset, a ScriptableObject at `assets/data/reveal-arcs/{charId}-arc.asset`, authored by the narrative team. It contains exactly three fields: `buildUpNodeName`, `disclosureNodeName`, `aftermathNodeName` (Yarn node names). If the asset is missing or any field is empty, the system logs `[ERROR]`, raises no scenes, enters ARC_FAILED, and does not fire the terminal signal. This is a content pipeline bug, not a recoverable runtime error.

**Rule 4 — The arc is a fixed three-scene sequence.** The system instructs Scene Management to run exactly: (1) build-up, (2) disclosure, (3) aftermath — in that order, without skipping or paralleling. Structure does not change based on character, tone category, or any CSM flag.

**Rule 5 — `dig_critical_failed` gates alternate Yarn nodes, not alternate scenes.** If `CSM.GetState(charId).narrative_flags["dig_critical_failed"] == true` at arc entry, the system writes `secret_reveal_critical_failed = true` to the Yarn variable context before each scene executes. Narrative designers branch within the same node using `<<if secret_reveal_critical_failed>>`. The three node names passed to Scene Management are always the same — this flag changes authored content only.

**Rule 6 — Tone is an authoring convention, not a runtime category.** The system has no `toneType` field, no tone enum, and no tone-conditional logic at runtime. Tone categories (`DARK`, `HEARTBREAKING`, `BITTERSWEET`, `SURPRISING_GOOD`, `COMEDIC`) are authoring metadata defined in the Reveal Arc Authoring Guide. Cast distribution is a production constraint, not a system constraint.

**Rule 7 — Scene handoff goes through Scene Management.** This system calls `SceneManagement.EnqueueRevealArc(charId, [buildUpNodeName, disclosureNodeName, aftermathNodeName])` once at arc entry. Scene Management treats the three scenes as a locked sequential queue, suppressing normal scene selection for this character until aftermath completes. Scene Management runs each scene through its standard lifecycle and calls `SceneComplete(sceneId)` on this system after each scene finishes.

**Rule 8 — The disclosure event fires at the start of the disclosure scene.** When Scene Management calls `SceneComplete(buildUpNodeName)`, this system transitions to ARC_DISCLOSURE and immediately raises `OnSecretDisclosure(charId)` before Scene Management begins the disclosure scene. The Revelation Presentation System (#14) subscribes to this event and prepares its visual layer in response. This system does not await confirmation from the Revelation Presentation System.

**Rule 9 — The arc cannot be interrupted by the player mid-session.** Once in ARC_RUNNING, the player cannot switch to another character's scenes through normal navigation — Scene Management's locked queue suppresses this. A mid-arc application exit is safe (see Rule 10).

**Rule 10 — Session interruption is safe.** This system writes two reserved `narrative_flags` to CSM at each scene completion: `"reveal_arc_in_progress": true` and `"reveal_arc_last_completed_scene": <sceneId | null>`. On session resume, these flags drive arc resumption: if `last_completed_scene == null`, restart from build-up; if `== buildUpNodeName`, continue from disclosure; if `== disclosureNodeName`, continue from aftermath. Both flags are cleared at ARC_COMPLETE.

**Rule 11 — The terminal signal fires once, after aftermath completes.** When `SceneComplete(aftermathNodeName)` is received, this system transitions to ARC_COMPLETE and raises `<<notify_state_change secret_arc_complete {charId}>>`. This fires exactly once per character, exactly at aftermath completion — never earlier, never conditionally. The Endings System (#18) depends on this guarantee.

**Rule 12 — This system does not evaluate aftermath content.** Whatever choices the player makes inside the aftermath Yarn scene are handled entirely within the authored content and CSM `narrative_flags`. This system's only postcondition from aftermath is receipt of `SceneComplete(aftermathNodeName)`.

---

### States and Transitions

State is re-derived from persistent CSM data on session resume; the system holds no in-memory enum at runtime.

| State | Entry Condition | Valid Exits |
|---|---|---|
| **ARC_IDLE** | `secret_revealed == false` for this charId | → ARC_RUNNING (when `secret_revealed` becomes true) |
| **ARC_RUNNING (build-up)** | Arc entered; build-up enqueued; `reveal_arc_in_progress = true`, `last_completed_scene = null` | → ARC_DISCLOSURE (on `SceneComplete(buildUpNodeName)`) |
| **ARC_DISCLOSURE** | Build-up complete; `OnSecretDisclosure(charId)` raised; disclosure enqueued; `last_completed_scene = buildUpNodeName` | → ARC_AFTERMATH (on `SceneComplete(disclosureNodeName)`) |
| **ARC_AFTERMATH** | Disclosure complete; aftermath enqueued; `last_completed_scene = disclosureNodeName` | → ARC_COMPLETE (on `SceneComplete(aftermathNodeName)`) |
| **ARC_COMPLETE** | Aftermath complete; terminal signal raised; arc flags cleared | Terminal |
| **ARC_FAILED** | Arc Definition Asset missing or invalid | Terminal — content fix + session restart required |

---

### Interactions with Other Systems

| System | Direction | Interface |
|---|---|---|
| Character State Manager (#6) | Reads and writes | **Reads**: `secret_revealed`, `narrative_flags["dig_critical_failed"]`, `narrative_flags["reveal_arc_in_progress"]`, `narrative_flags["reveal_arc_last_completed_scene"]`. **Writes** (via `SetFlag()`): `reveal_arc_in_progress`, `reveal_arc_last_completed_scene`. Does not write: `secret_revealed`, `dig_critical_failed`, `depth_tier`. |
| Scene Management (#9) | Bidirectional | **Outbound**: `EnqueueRevealArc(charId, [buildUpNodeName, disclosureNodeName, aftermathNodeName])` — activates locked sequential queue. **Inbound**: `SceneComplete(sceneId)` callback after each scene. |
| Revelation Presentation System (#14) | Event — outbound only | `OnSecretDisclosure(charId)` raised at ARC_DISCLOSURE entry. Revelation Presentation subscribes and owns all visual behavior during the disclosure scene. This system does not await confirmation. |
| Endings System (#18) | Event — outbound only | `<<notify_state_change secret_arc_complete {charId}>>` raised at ARC_COMPLETE. Endings System subscribes and determines route completion state. |
| Save/Load System (#7) | Indirect | Arc-progress flags (`reveal_arc_in_progress`, `reveal_arc_last_completed_scene`) are CSM fields included in the standard CSM save payload. No special save hook required. |
| The Dig (#3) | Reads CSM flag — no direct call | This system reads `narrative_flags["dig_critical_failed"]` at arc entry. The Dig wrote it; this system observes it. No direct communication. |

---

### Reveal Arc Authoring Rules

The Secret Reveal System enforces structural rules through its Arc Definition Asset format and the `OnSecretDisclosure` event contract. All remaining authoring rules are the responsibility of the Reveal Arc Authoring Guide (a separate document). This GDD records the rules that have system-level implications.

**Tone categories** — authoring metadata, not runtime types. Every character's secret must be assigned one of:

| Category | Core Emotion |
|---|---|
| `DARK` | Weight, unease, protection — the character carries something costly |
| `HEARTBREAKING` | Grief, loss, longing — something happened to them; they survived it sideways |
| `BITTERSWEET` | Complicated relief — not as bad as feared, not good either; aftermath is suspended |
| `SURPRISING_GOOD` | Warmth, reframe — the gap you read as flaw was actually something else entirely |
| `COMEDIC` | Release, delight — the secret punctures the mystery; character becomes more themselves, not less |

**Cast distribution (producer constraint, not system constraint):** Minimum 2 characters per category. No category exceeds 40% of cast. `DARK` + `HEARTBREAKING` combined may not exceed 50%.

**Required beats per scene** — mandatory for every character, every tone:

*Build-up:* (1) Convergence beat — player character recognizes an old detail with new significance, without naming the secret. (2) Stall beat — character's public-face avoidance is louder than its baseline. (3) Permission beat — player character does or says the thing that makes disclosure possible in hindsight. (4) Dread-certainty close — scene ends on anticipated arrival, not mystery.

*Disclosure:* (5) Backward look — character references a specific playable moment from tier 1–4 by content, not generically. (6) Cost statement — what the disclosure costs the character, expressed before or simultaneous with the secret's content. (7) Look-up beat — authored pause after disclosure; character waits and does not fill silence. This is the primary screenshot moment. (8) Player's first response is emotional, not informational — no clarifying questions as the first option.

*Aftermath:* (9) Settled-register beat — at least one signature public-face behavior is visibly absent or inverted. (10) Named-change beat — character communicates, in behavior or word, what has changed between them. (11) Open thread — at least one element explicitly unresolved, handed to the Endings system.

**Aftermath constraints:** No apology for the secret. No recap of the disclosure. Aftermath must not resolve the emotional valence of the relationship's long-term future.

**Shareability (Pillar 4):** Every disclosure scene must contain one key line — one sentence that works as a standalone screenshot caption, identifies the character's emotional truth, and does not require game knowledge to land. Writer identifies this line explicitly in scene notes. It appears during or adjacent to the look-up beat. No competing UI elements are visible at this moment.

**Retroactive foreshadowing constraint:** Before a character's arc is approved, the writer submits a "signal inventory" — at least three specific moments in tier 1–4 content where the secret is visible in hindsight. This is a required deliverable. A secret that cannot populate three signals has failed the Player Fantasy design test.

## Formulas

The Secret Reveal System performs no mathematical calculations. All arc control is boolean and event-driven:

1. **Entry condition**: `secret_revealed == true` — a single boolean read from CSM.
2. **Critical fail flag check**: `narrative_flags["dig_critical_failed"] == true` — a single boolean read used to set the Yarn variable context.
3. **Arc resumption logic**: a three-way string comparison against `reveal_arc_last_completed_scene` (`null` → restart from build-up; `buildUpNodeName` → continue from disclosure; `disclosureNodeName` → continue from aftermath).

None of these require formula specification — they are direct reads with no computation.

All formulas relevant to this system's entry condition are defined upstream: Formulas F-1 through F-4 in the Relationship Depth System GDD govern how `depth_tier` advances to 5 and `secret_revealed` is set. The Dig GDD defines the boolean flag evaluation for `dig_critical_failed`.

The absence of formulas here is a deliberate design characteristic: the revelation arc's content is authored narrative; its sequencing is a deterministic state machine with no numeric inputs.

## Edge Cases

**EC-01 — Arc Definition Asset missing entirely**
If `assets/data/reveal-arcs/{charId}-arc.asset` does not exist when arc entry is attempted: log `[ERROR]`, enter ARC_FAILED, raise no scenes, do not fire the terminal signal. `reveal_arc_in_progress` is NOT written. On each subsequent session start, the system re-attempts arc entry (since `secret_revealed == true` with no arc flags), hits the same missing asset, and re-enters ARC_FAILED. This repeats until the asset ships. Content fix required; no runtime recovery.

**EC-02 — Arc Definition Asset present but partially populated**
If the asset exists but any of `buildUpNodeName`, `disclosureNodeName`, or `aftermathNodeName` is null or empty: log `[ERROR]` on the first empty field found, enter ARC_FAILED, raise no scenes. Partial validation is not attempted — a partially valid asset that fails mid-arc is more dangerous than failing on entry.

**EC-03 — Arc Definition Asset references a non-existent Yarn node name**
This is not detectable at arc entry — the failure manifests when YarnSpinner 3.0 attempts to start the missing node. The Dialogue Engine reports a runtime error; `SceneComplete` is never called for that scene, leaving the arc stuck in its current state with `reveal_arc_in_progress == true`. Recovery requires a content fix and session resume (which re-attempts the same missing node). **Authoring gate required**: the Arc Definition Asset validator must confirm all three node names exist in the Yarn project before content ships.

**EC-04 — Session crash during build-up (before SceneComplete(buildUpNodeName))**
On resume: `reveal_arc_in_progress == true`, `reveal_arc_last_completed_scene == null`. System restarts the build-up scene from its beginning. YarnSpinner does not mid-node checkpoint; the player replays the full build-up. This is correct — build-up is a complete dramatic unit.

**EC-05 — Session crash during disclosure or aftermath**
On resume, the interrupted scene restarts from its beginning. The disclosure scene re-raises `OnSecretDisclosure(charId)` on re-entry into ARC_DISCLOSURE. The Revelation Presentation System (#14) must be idempotent on receiving `OnSecretDisclosure(charId)` when `secret_revealed` is already true — a second invocation after crash resume must not produce duplicate visual state or errors.

**EC-06 — `secret_revealed == true` for a character with `closed_off == true` at arc entry**
Unreachable through normal gameplay: `DIG_PASS` (the only path to depth 5) requires `closed_off == false`. This combination indicates state corruption or a direct CSM write outside gameplay. This system reads `secret_revealed` only — it does not inspect `closed_off` and has no alternate behavior. Log `[WARN]` when arc entry is initiated with `closed_off == true`. Proceed normally.

**EC-07 — `secret_revealed` becomes true for character B while character A is mid-arc**
Scene Management's locked queue is per-character. Character B reaching depth 5 while A's arc is running does not interrupt A's arc. This system must NOT attempt to enqueue B's arc while A's locked queue is active. B's arc entry is deferred as a pending intent. When A's aftermath completes and Scene Management releases the locked queue, this system evaluates all characters with `secret_revealed == true` and no arc flags, and initiates the next arc. The deferred intent requires no additional persistence — `secret_revealed == true` with no arc flags is itself the deferred state.

**EC-08 — Multiple characters with `secret_revealed == true` at session start (no arc flags)**
On session resume, the system evaluates all characters and may find more than one qualifying for arc entry. Only one arc may be active at a time. Priority: the character whose `depth_tier` most recently reached 5 goes first. Remaining characters are queued as deferred intents per EC-07.

**EC-09 — Player force-quits mid-arc; standard save/load during locked queue**
Saves fire on scene exit (standard trigger). The locked queue suppresses scene selection but not saves. A player who force-quits mid-scene loses only in-scene progress; arc state flags written at scene entry are persisted. There is no mid-scene manual save UI at MVP. On load, arc resumption logic applies normally per Rule 10.

**EC-10 — `OnSecretDisclosure(charId)` fires when Revelation Presentation System is not subscribed**
C# events with no subscribers do not throw; the call returns silently. The disclosure scene plays without its presentation layer — a content degradation, not a system crash. The Revelation Presentation System must validate its subscription on scene load and log `[ERROR]` if it is not subscribed when the disclosure scene begins. The failure surface belongs to RPS, not this system.

**EC-11 — `dig_critical_failed` flag set after arc entry**
Not reachable through normal gameplay: a Dig attempt that produces `DIG_CRITICAL_FAIL` cannot simultaneously produce `DIG_PASS` (which triggers arc entry). This system reads `dig_critical_failed` once at arc entry and does not re-evaluate it during the arc. If a direct CSM write outside gameplay sets this flag mid-arc, this system will not observe it. Design-time constraint: `dig_critical_failed` is read once, at arc entry only.

**EC-12 — `secret_arc_complete` terminal signal fires when Endings System is not yet subscribed**
The terminal signal fires exactly once and is not re-fired. If the Endings System (#18) has not subscribed at that moment, the signal is missed. Mitigation: the Endings System must subscribe at session initialization, before any scene can complete. Additionally, the Endings System should call a query (`SecretRevealSystem.IsArcComplete(charId)`) on initialization to catch any characters already in ARC_COMPLETE from a prior session. This query interface is an open question — see Open Questions.

**EC-13 — `reveal_arc_last_completed_scene` contains an unrecognized node name**
If the persisted string does not match `null`, `buildUpNodeName`, or `disclosureNodeName` from the current Arc Definition Asset (e.g., arc asset was updated post-ship with renamed nodes): log `[ERROR]`, treat as `null`, restart arc from build-up. The player may replay content they have already seen, but no arc state is permanently lost and the disclosure fires correctly.

## Dependencies

### Upstream (systems this one depends on)

**Relationship Depth System (#1)** — `design/gdd/relationship-depth-system.md`
Hard dependency. Entry condition for this system is `depth_tier == 5` AND `secret_revealed == true`, both resulting from `SetDepthTier(charId, 5)` called by RDS on `DIG_PASS` at tier 4→5. Depends on: the monotonic `depth_tier` invariant, the CSM auto-set of `secret_revealed = true` on reaching tier 5, and the `secret_revealed` field being read-accessible via `CSM.GetState(charId)`.

**The Dig (#3)** — `design/gdd/the-dig.md`
Indirect dependency. The tier 4→5 `DIG_PASS` is the trigger that causes RDS to advance `depth_tier` to 5. Also the indirect source of the `dig_critical_failed` flag — if a prior DIG_CRITICAL_FAIL occurred at any earlier tier, The Dig set `narrative_flags["dig_critical_failed"] = true`, which this system reads at arc entry. No direct communication with The Dig.

**Character State Manager (#6)** — `design/gdd/character-state-manager.md`
Hard dependency. All reads and writes go through CSM. Depends on: `CSM.GetState(charId)` returning `secret_revealed`, `dig_critical_failed`, `reveal_arc_in_progress`, `reveal_arc_last_completed_scene`; `CSM.SetFlag(charId, key, value)` for arc-progress bool flags. `reveal_arc_last_completed_scene` is a string value — the current CSM schema defines `narrative_flags` as `Dictionary<string, bool>` only.

> **⚠ Interface gap**: The CSM schema must be extended to support `reveal_arc_last_completed_scene` as a string. Options: add a `narrative_strings: Dictionary<string, string>` field to CSM, or encode the node name as a bool-flag sequence. The CSM GDD must be updated before implementation. See Open Questions.

**Scene Management / Flow Controller (#9)** — `design/gdd/scene-management.md`
Hard dependency. Depends on `EnqueueRevealArc(charId, [nodeNames])` — a new API that does not exist in the current Scene Management GDD. Scene Management's pull model (`GetAvailableScenes()`) does not cover a locked sequential queue. The Scene Management GDD must be updated to specify: the `EnqueueRevealArc` entry point, locked queue behavior, scene selection suppression during the locked queue, and the `SceneComplete(sceneId)` callback to this system.

**Save/Load System (#7)** — `design/gdd/save-load-system.md`
Indirect dependency. Arc-progress flags are CSM fields included in the standard CSM save payload. No new save trigger or hook required. Depends on `RestoreState()` correctly restoring all `narrative_flags` (and `narrative_strings` if added) to drive arc resumption on session start.

---

### Downstream (systems that depend on this one)

**Revelation Presentation System (#14)** — no GDD yet
Depends on this system defining: `OnSecretDisclosure(charId)` as the disclosure trigger, the timing of that event (before the disclosure scene begins), and the idempotency requirement for crash-resume re-fires. Must subscribe at session initialization and be resilient to re-invocation when `secret_revealed` is already true.

**Endings / Route Completion (#18)** — no GDD yet
Depends on this system defining: `<<notify_state_change secret_arc_complete {charId}>>` as the route terminal signal, the guarantee that it fires exactly once at aftermath completion, and the open interface question of a `SecretRevealSystem.IsArcComplete(charId)` catch-up query.

## Tuning Knobs

This system's behavior is almost entirely determined by authored data (Arc Definition Assets, Yarn content) rather than numeric configuration. Runtime tuning knobs are minimal.

| Knob | Default | Safe Range | What It Affects |
|---|---|---|---|
| `REVEAL_ARC_QUEUE_PRIORITY` | Most recently depth-advanced | n/a (enum) | When multiple characters reach `secret_revealed == true` simultaneously, determines which arc runs first. Default: character most recently advanced to depth tier 5. |

**Authoring knobs (not runtime config):**
- **Tone category** — per-character authoring metadata (`DARK`, `HEARTBREAKING`, `BITTERSWEET`, `SURPRISING_GOOD`, `COMEDIC`). Cast distribution enforced by production review.
- **Required beats per scene** — enforced by editorial review of Yarn content.
- **Signal inventory** — minimum 3 retroactive foreshadowing moments per character; required deliverable, not configurable.
- **Node names** (`buildUpNodeName`, `disclosureNodeName`, `aftermathNodeName`) — changing node names post-ship risks EC-13 if saves persist old values.

**Cross-reference — knobs NOT owned by this system:**
- `DIG_RESPONSE_PAUSE_MS`, `DIG_SCREEN_TRANSITION_MS` — owned by The Dig (#3); control the tier 4→5 Dig interaction preceding this arc.
- `SCENES_REQUIRED[4]` — owned by Relationship Depth System (#1); controls how many tier-4 scenes precede the tier 4→5 Dig.

## Visual/Audio Requirements

### Visual

**Build-up scene**
The dialogue UI uses the character's palette at its depth-5 saturation state — the most saturated the palette reaches. No scene background animation; static environment only. The character portrait holds expressions for longer than the tier 1-4 norm, with reduced micro-expression variety — stillness is itself a signal. All character-specific decorative UI elements present in tier 1-4 scenes appear unchanged; build-up must feel continuous with what preceded it, not announced as special.

**Look-up beat — structural requirements (downstream to Revelation Presentation System #14)**
The following are mandated by this system; implementation is owned by RPS:
- All dialogue UI chrome must fully recede: no name plate, no text box, no advance prompt, no decorative layer. The character portrait is the sole focal element on screen.
- The look-up beat advance prompt must be suppressed for a minimum defined duration. RPS specifies the exact timing; this system mandates suppression.
- No new text renders during the look-up beat. The key line appears immediately before it; the beat itself is silent.
- The look-up beat portrait is a dedicated expression asset — not reused from any other scene. It is the primary screenshot artifact of the game (Pillar 4).

**Aftermath scene**
- Character palette shifts to its `depth_5_revealed` variant from this scene forward, permanently. This is the new baseline, not a transition effect — first appearing in aftermath.
- At least one audio element present in all prior tier 1-4 scenes for this character must be absent or replaced. Aftermath marks the removal of the character's public-face sonic signature.
- Standard scene UI chrome (character-specific decorative elements tied to pre-reveal presentation) does not appear. Aftermath uses the base dialogue UI structure only.

**Portrait asset requirements**
A new "reveal-register" portrait series is required per character, distinct from the Dig portrait series (`char_[name]_dig_*.png`). Minimum 4 new assets per character:

| Asset name | Scene | Description |
|---|---|---|
| `char_[name]_reveal_stall_01.png` | Build-up | Public face showing visible effort — the mask under strain |
| `char_[name]_reveal_disclosure_01.png` | Disclosure | Expression at the moment of telling |
| `char_[name]_reveal_lookup_01.png` | Disclosure (look-up beat) | Dedicated look-up beat expression. Must read without game context — standalone screenshot asset. |
| `char_[name]_reveal_settled_01.png` | Aftermath | Post-reveal, unmasked register. Quieter than disclosure. |

The look-up beat portrait (`_lookup_`) is the highest-priority art asset in the revelation arc — designed to function as a standalone image.

---

### Audio

**Build-up to disclosure audio continuity**
The character's theme plays at reduced instrumentation (1–2 elements) during build-up, with a sustained low-register note introduced partway through and left unresolved. This note must carry across the scene boundary into the disclosure scene — a cross-scene audio continuity requirement. The Audio System GDD must define a "held layer" mechanism that survives scene transitions when explicitly flagged. The Secret Reveal System is the first consumer of this capability.

**Look-up beat silence contract**
The look-up beat is sonically empty by design. No new audio cue fires at its start. Any music playing continues or fades; no additional layer is introduced during the beat. The Audio System GDD must define the look-up beat as a "dead zone." Timing of any post-beat resolution cue is specified by RPS's implementation.

**Post-reveal audio state**
After the aftermath scene, this character's scenes use a different ambient layer baseline — the "revealed register." This is a permanent state change triggered by `ARC_COMPLETE`. The Audio System subscribes to the terminal signal (`secret_arc_complete`) and switches the character's ambient layer baseline on receipt. The Audio System GDD must define the `depth_5_revealed` audio layer per character.

> 📌 **Asset Spec** — Visual/audio requirements are defined for the revelation arc. After the art bible is fully approved, run `/asset-spec system:secret-reveal-system` to produce per-asset visual descriptions, dimensions, and generation prompts from this section.

## UI Requirements

**Look-up beat UI suppression** — the only mandatory UI behavior this system specifies. During the look-up beat, all standard dialogue UI elements must be suppressed: no text box, no name plate, no advance prompt or "tap to continue" hint, no choice buttons. The character portrait is the only element on screen. RPS (#14) implements this; this system mandates it. The suppression duration and transition in/out are RPS implementation concerns.

**No arc progress indicator** — at no point during the revelation arc is a progress indicator, chapter counter, or scene index shown to the player. The arc is a narrative experience, not a tracked completion sequence.

**Post-arc scene access** — once ARC_COMPLETE fires, Scene Management must reflect the character's new availability state. What scenes (if any) are available to the player for this character after the arc completes is specified by the Endings System GDD (#18) and Scene Management GDD (#9), not this system.

**Controller navigation** — all choice interactions within the arc's dialogue scenes (build-up, aftermath) must support keyboard/arrow navigation and Steam Deck controller d-pad navigation, per the platform requirements in technical-preferences.md. The look-up beat requires a single dismiss input (Enter/Space/A button/click), identical to The Dig's dismiss model.

> **📌 UX Flag — Secret Reveal System**: This system has UI requirements for the look-up beat. In Pre-Production, run `/ux-design` to create a UX spec for the disclosure screen (the look-up beat state) before writing epics. Stories referencing the look-up beat UI should cite `design/ux/disclosure-screen.md`, not this GDD directly.

## Acceptance Criteria

### Category 1: Arc Entry

| ID | Criterion | Type |
|---|---|---|
| SRS-001 | GIVEN a character with `secret_revealed == false` / WHEN `CSM.SetDepthTier(charId, 5)` fires and CSM auto-sets `secret_revealed = true` / THEN SRS calls `SceneManagement.EnqueueRevealArc(charId, [buildUpNodeName, disclosureNodeName, aftermathNodeName])` exactly once | Logic / BLOCKING |
| SRS-002 | GIVEN a character already in ARC_COMPLETE (`secret_revealed == true`, arc flags cleared) / WHEN SRS evaluates entry conditions on session start / THEN SRS takes no action, logs `[WARN]`, and `EnqueueRevealArc` is never called again | Logic / BLOCKING |

### Category 2: Arc Definition Asset Validation

| ID | Criterion | Type |
|---|---|---|
| SRS-003 | GIVEN the Arc Definition Asset is missing / WHEN arc entry is attempted / THEN SRS logs `[ERROR]`, transitions to ARC_FAILED, does not write `reveal_arc_in_progress`, does not fire the terminal signal, and does not call `EnqueueRevealArc` | Logic / BLOCKING |
| SRS-004 | GIVEN the Arc Definition Asset exists but any node name field is null or empty / WHEN SRS reads the asset / THEN SRS logs `[ERROR]` on the first invalid field, transitions to ARC_FAILED, and raises no scenes | Logic / BLOCKING |
| SRS-005 | GIVEN a character is in ARC_FAILED (missing asset) / WHEN a new session starts / THEN SRS re-attempts arc entry, hits the same missing asset, and re-enters ARC_FAILED — silent swallowing of persistent failure is not permitted | Logic / BLOCKING |

### Category 3: Scene Sequencing and Flag Propagation

| ID | Criterion | Type |
|---|---|---|
| SRS-006 | GIVEN a valid Arc Definition Asset and `dig_critical_failed == false` / WHEN arc entry fires / THEN `EnqueueRevealArc` is called with node names in build-up → disclosure → aftermath order, and Yarn variable `secret_reveal_critical_failed` is NOT set | Logic / BLOCKING |
| SRS-007 | GIVEN `dig_critical_failed == true` at arc entry / WHEN SRS initializes Yarn variable context / THEN `secret_reveal_critical_failed = true` is written to Yarn storage before the first scene begins, and the same three node names are passed to Scene Management | Integration / BLOCKING |
| SRS-008 | GIVEN arc entry succeeded and `SceneComplete(buildUpNodeName)` is received / WHEN SRS handles the callback / THEN `OnSecretDisclosure(charId)` fires BEFORE Scene Management begins the disclosure scene — verifiable via call-order assertion on injected doubles | Logic / BLOCKING |
| SRS-009 | GIVEN disclosure is running and `SceneComplete(disclosureNodeName)` is received / WHEN SRS handles the callback / THEN `reveal_arc_last_completed_scene = disclosureNodeName` is written to CSM and aftermath is enqueued — the terminal signal is NOT fired at this point | Logic / BLOCKING |
| SRS-010 | GIVEN Scene Management's locked queue is active for a character mid-arc / WHEN the player attempts to select a different scene for that character / THEN `GetAvailableScenes(charId)` returns an empty list and no `StartScene` call is possible through normal navigation | Integration / BLOCKING |

### Category 4: Terminal Signal

| ID | Criterion | Type |
|---|---|---|
| SRS-011 | GIVEN aftermath is running and `SceneComplete(aftermathNodeName)` is received / WHEN SRS handles the callback / THEN the terminal signal fires exactly once, SRS transitions to ARC_COMPLETE, and `reveal_arc_in_progress` and `reveal_arc_last_completed_scene` are cleared from CSM | Logic / BLOCKING |
| SRS-012 | GIVEN a character is already in ARC_COMPLETE / WHEN any subsequent session starts / THEN the terminal signal is NOT re-fired | Logic / BLOCKING |
| SRS-013 | GIVEN no subscriber for the terminal signal event / WHEN aftermath completes and SRS fires the signal / THEN no C# exception is thrown and SRS logs `[WARN]` | Logic / BLOCKING |

### Category 5: Session Crash Recovery

| ID | Criterion | Type |
|---|---|---|
| SRS-014 | GIVEN `reveal_arc_in_progress == true` and `reveal_arc_last_completed_scene == null` at session start / WHEN SRS re-derives arc state / THEN SRS resumes from build-up: enqueues build-up as the first scene | Logic / BLOCKING |
| SRS-015 | GIVEN `reveal_arc_last_completed_scene == buildUpNodeName` at session start / WHEN SRS re-derives arc state / THEN SRS raises `OnSecretDisclosure(charId)` and enqueues disclosure as the first scene — build-up is NOT replayed | Logic / BLOCKING |
| SRS-016 | GIVEN `reveal_arc_last_completed_scene == disclosureNodeName` at session start / WHEN SRS re-derives arc state / THEN SRS enqueues aftermath directly, without re-raising `OnSecretDisclosure(charId)` | Logic / BLOCKING |
| SRS-017 | GIVEN `reveal_arc_last_completed_scene` contains an unrecognized string / WHEN SRS re-derives arc state / THEN SRS logs `[ERROR]`, treats the value as `null`, and restarts from build-up | Logic / BLOCKING |

### Category 6: Multiple Simultaneous Arcs

| ID | Criterion | Type |
|---|---|---|
| SRS-018 | GIVEN character A is mid-arc and character B's `secret_revealed` becomes true / WHEN SRS evaluates B's entry condition / THEN SRS does NOT call `EnqueueRevealArc` for B — B's entry is deferred, represented by `secret_revealed == true` with no arc flags | Logic / BLOCKING |
| SRS-019 | GIVEN character A's aftermath completes and both B and C have `secret_revealed == true` with no arc flags / WHEN SRS evaluates post-complete state / THEN exactly one arc begins; the character whose `depth_tier` reached 5 most recently is selected first | Logic / BLOCKING |
| SRS-020 | GIVEN at session start two characters both have `secret_revealed == true` and no arc flags / WHEN SRS initializes / THEN exactly one arc is initiated (most-recently-depth-advanced first); the other character remains deferred with no additional flags written | Logic / BLOCKING |

**Implementation prerequisites** (required before test execution):
- SRS must accept `ISceneManagement` and `ICsmAdapter` via dependency injection — required for Edit Mode unit tests
- SRS must expose `CurrentArcState(charId)` query — required for state assertions
- SRS must expose testable hooks for `OnSecretDisclosure` and the terminal signal
- **SRS-007**: Yarn variable write requires a live YarnSpinner runtime; must be a Play Mode integration test
- **SRS-010**: Locked queue suppression requires Scene Management running; must be a Play Mode integration test
- **SRS-019/020**: "Most recently depth-advanced" ordering requires a CSM-accessible timestamp or ordering signal not yet in the CSM schema — see Open Questions

## Open Questions

**OQ-1: CSM schema extension for string flag** — `reveal_arc_last_completed_scene` requires storing a string value in CSM. The current `narrative_flags: Dictionary<string, bool>` schema cannot hold it. Options: (a) add `narrative_strings: Dictionary<string, string>` to CSM, (b) encode the node-name as a bool sequence using reserved keys, (c) store the value in a separate SRS-owned persistence layer. This must be resolved before implementation. Flag for `/architecture-decision`.

**OQ-2: `EnqueueRevealArc` API in Scene Management** — This system depends on a `SceneManagement.EnqueueRevealArc(charId, [nodeNames])` method that does not exist in the current Scene Management GDD. The locked sequential queue behavior (suppression of normal scene selection, `SceneComplete` callback per scene) must be specified in the Scene Management GDD before this system can be implemented. Flag for Scene Management GDD update or `/architecture-decision`.

**OQ-3: Endings System catch-up query** — EC-12 identifies that the terminal signal fires once and cannot be re-fired. The Endings System (#18) must either subscribe before any scene can complete, or a query method (`SecretRevealSystem.IsArcComplete(charId)`) must exist for initialization-order catch-up. This interface must be defined when the Endings System GDD is authored.

**OQ-4: "Most recently depth-advanced" ordering signal** — SRS-019 and SRS-020 require a deterministic ordering signal from CSM to identify which character's `depth_tier` reached 5 most recently. The current CSM schema has no `depth_tier_advanced_at` timestamp or equivalent. This must be defined in the CSM GDD before the multi-simultaneous-arc tests can be implemented.

**OQ-5: Cross-scene audio continuity mechanism** — The build-up to disclosure audio handoff (sustained unresolved note carrying across scene boundary) requires a "held layer" mechanism in the Audio System. This capability does not exist in the current Audio System GDD. Flag for Audio System GDD update.

**OQ-6: `depth_5_revealed` palette and audio layer spec** — The aftermath scene shifts to a `depth_5_revealed` visual palette and audio layer baseline permanently. These per-character variants must be defined in the art bible and Audio System GDD respectively before aftermath scenes can be produced. Flag for art direction and Audio System GDD.

**OQ-7: Arc Definition Asset validator tooling** — EC-03 identifies that a non-existent Yarn node name in the Arc Definition Asset is not detectable at arc entry — it manifests only when YarnSpinner attempts to start the missing node. A validation tool must check all node names exist in the Yarn project at asset save time. Flag for tools programmer as part of the Arc Definition Asset implementation sprint.

**OQ-8: Signal inventory review process** — The retroactive foreshadowing constraint (3+ foreshadowing moments per character as a required deliverable before arc approval) requires a designated editorial reviewer. No review process is currently defined. Flag for narrative director and producer when the content pipeline is established.
