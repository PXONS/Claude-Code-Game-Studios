# Audio System / Reactive Music

> **Status**: Designed
> **Author**: Design session + agents
> **Last Updated**: 2026-05-03
> **Implements Pillar**: Pillar 3 (Desire Drives Discovery), Pillar 4 (Designed to Be Shared)

## Overview

The Audio System / Reactive Music is the layer that makes Masks *sound* like a relationship is deepening. It operates as a trigger-driven audio controller: it subscribes to `MasksEvents.StateChanged` (raised by the Dialogue/Narrative Engine on `<<notify_state_change>>` calls) and to scene navigation events from Scene Management, then translates those game-state transitions into music and ambient sound behavior. The system owns no persistent state of its own — it reads current character state from the Character State Manager and drives the audio output accordingly.

At MVP, the Audio System defines the **trigger architecture** only. Audio content (character themes, tier-specific music layers, ambient signatures) is authored as placeholder. The system's design must be stable enough that real audio content can be dropped in during Early Access without architectural changes — placeholder clips are a content gap, not a structural gap.

The player never interacts with this system directly. What they feel is that each character sounds different from the others; that something in the music shifts when a relationship moves; that the silence before a Dig and the sound of its outcome are distinct. The system's job is to make the emotional state of the game audible — to serve as a second signal layer beneath the text, present but not announced.

## Player Fantasy

Every character in Masks is performing. The audio is the one thing in the game that isn't.

Characters have polished public faces and careful words. The music does not. As depth deepens and the public mask begins to slip — as the player pushes past surface and into the texture of who someone actually is — the audio reflects a truth the character hasn't spoken yet. When a character starts closing off mid-conversation, the room changes before they show it. When the secret is closer than the dialogue admits, something in the score knows. The player learns, without being taught, to trust the sound over the words.

The fantasy is intuition. A sixth sense for what people are hiding, calibrated by an instrument the player never consciously hears. Not "the music swelled, so something important happened" — but the subtler version: the way a room goes quiet when someone is about to say something they won't. The player who pays attention will feel it before they can name it. That's the system working.

**Design test**: Can a player who closes their eyes and listens to a scene tell whether the character is being honest? Not because the system announces it — but because the sound of the room is different when they are.

## Detailed Design

### Core Rules

**Layer Model**

1. The Audio Controller operates four named layers. All AudioSources use `spatialBlend = 0` (2D VN — no spatial positioning).

| Layer | Name | Role | Max simultaneous AudioSources | Replacement |
|---|---|---|---|---|
| L1 | MUSIC | Depth-reactive track. One composition active at a time. | 2 (pingpong crossfade) | Crossfade |
| L2 | AMBIENT | Room / character signature. Continuous loop. | 2 (crossfade) | Crossfade |
| L3 | STING | One-shot emotional punctuation. Non-looping. | 1 | Interrupt (stop current, fire new) |
| L4 | UI | Discrete interaction sounds. Very short, fire-and-forget. | 1 | Fire-and-forget (overlap allowed) |

Total AudioSource budget: **6**.

**Event → Audio Action Rules**

2. The Audio Controller subscribes to two event sources: (a) `MasksEvents.StateChanged(string eventType, string charId)` from the Dialogue/Narrative Engine; (b) screen navigation events from the Scene & Activity Loop (portrait tap, scene selection, screen transition). No other system calls AudioController methods directly.

3. `MasksEvents.StateChanged` event handling by eventType:

| `eventType` | L1 (MUSIC) | L2 (AMBIENT) | L3 (STING) |
|---|---|---|---|
| `depth_tier_changed` | Crossfade to `music/{charId}/tier_{N}` (N = CSM.GetState(charId).depth_tier after event) | Crossfade to `ambient/{charId}/tier_{N}` | Fire `sting/{charId}/tier_advance` if authored, else `sting/tier_advance` |
| `closed_off` | Fade out active track over `CLOSED_OFF_FADE_DURATION`. L1 silent. | Crossfade to `ambient/{charId}/closed_off` | Fire `sting/closed_off` |
| `secret_revealed` | Crossfade to `music/{charId}/tier_5_reveal` | Crossfade to `ambient/{charId}/tier_5_reveal` | Fire `sting/secret_revealed` |

4. `depth_tier_changed` and `closed_off` StateChanged events are processed only in AC_SCENE or AC_DIG states. If they arrive outside a scene context (AC_IDLE, AC_CHARACTER), they are queued and dequeued when the system next enters AC_SCENE. `secret_revealed` is processed in any state — it overrides current audio immediately.

5. Screen navigation event → audio action mapping:

| Event | L1 | L2 | L3 | L4 |
|---|---|---|---|---|
| Enter CHARACTER_SELECT | Fade out — L1 silent | Crossfade to `ambient/interior_base` | — | — |
| Portrait tapped (enter SCENE_SELECT) | No change | Crossfade to `ambient/{charId}/signature` | — | Fire `ui/{charId}/portrait_tap_note` |
| Scene / Activity selected (scene entry) | Load `music/{charId}/tier_{N}` — begin if clip present | Crossfade to `ambient/{charId}/tier_{N}` | — | Fire `ui/scene_entry_resolve` |
| DigEntry selected (Dig scene entry) | No change | Crossfade to `ambient/{charId}/dig_entry` | — | Fire `ui/dig_entry_swell` |
| Back (SCENE_SELECT → CHARACTER_SELECT) | Fade out — L1 silent | Crossfade to `ambient/interior_base` | — | — |
| Scene completes (CLEAN_COMPLETION) | Fade out | Crossfade to destination screen ambient | — | — |
| Scene abandoned (ABANDONED) | Fade out | Crossfade to `ambient/{charId}/signature` | — | — |

**Character Audio Identity**

6. Each character has a Character Audio Profile: a named data asset declaring all clip slots for that character. A Character Audio Profile consists of:
   - **Ambient signature**: one tier-neutral ambient loop (`ambient/{charId}/signature`) played while SCENE_SELECT is active for this character. Does not reveal tier information. Also: per-tier variants (`ambient/{charId}/tier_1` through `tier_5`), a `closed_off` variant, a `tier_5_reveal` variant, and a `dig_entry` variant.
   - **Portrait tap note**: one short audio event (`ui/{charId}/portrait_tap_note`). Max duration: `PORTRAIT_TAP_NOTE_MAX_DURATION`. Played only on CHARACTER_SELECT → SCENE_SELECT transition.
   - **Music tracks**: per-tier tracks (`music/{charId}/tier_1` through `tier_5`) and a `tier_5_reveal` track. Tier-1 may be absent (silence is valid on L1 at low tiers).
   - **Optional character stings**: `sting/{charId}/tier_advance` as a per-character override of the global tier advance sting.

**Content Registry**

7. The Audio Controller does not know file paths. It constructs a **semantic clip key** and passes it to the **Audio Content Registry** — a ScriptableObject at `assets/data/audio/audio-content-registry.asset`. The registry maps `string semanticKey → AudioClip`. `Registry.Resolve(key)` returns either a clip or null (missing).

8. Fallback rules when `Registry.Resolve(key)` returns null:
   - L1 (MUSIC): play silence. Do not fall back to another tier's track. A missing clip is a content gap.
   - L2 (AMBIENT): fall back to `ambient/interior_base`. Ambient layer is never dead.
   - L3 (STING): skip. A missing sting fires nothing.
   - L4 (UI): skip. A missing UI sound fires nothing.
   - `ambient/{charId}/signature`: fall back to `ambient/interior_base`.

9. On startup, the Audio Controller logs a `[WARNING]` for each required clip slot that resolves to null. This is a content-debt warning, not a runtime failure. The session continues.

**Crossfade Mechanics**

10. L1 and L2 each use a pingpong pair of AudioSources. On crossfade trigger: the current AudioSource fades out over the layer's crossfade duration; the incoming AudioSource fades in simultaneously. After crossfade completes, the outgoing source is stopped and marked available.

11. If a second crossfade is requested before the first completes: the in-progress crossfade is abandoned. The incoming source of the abandoned crossfade becomes the outgoing source of the new crossfade, continuing from its current volume level.

12. Crossfade curves are linear at MVP. Non-linear curves are a Vertical Slice polish concern.

**Authoring Constraint — Secret Reveal Timing**

13. The `secret_revealed` StateChanged event must be placed within the reveal scene's Yarn script at the dramatically appropriate moment — not at scene start. Yarn command: `<<notify_state_change secret_revealed {charId}>>`. If placed at scene start, AC_REVEAL ambient plays from the first line, which will break the narrative reveal arc. Narrative team must coordinate placement. See Open Questions.

### States and Transitions

| State | Description | L1 | L2 |
|---|---|---|---|
| `AC_IDLE` | CHARACTER_SELECT active; no character selected | Silent | `ambient/interior_base` |
| `AC_CHARACTER` | SCENE_SELECT active for a character | Silent | `ambient/{charId}/signature` |
| `AC_SCENE` | A scene or activity is running | Tier-appropriate track (or silent if absent) | `ambient/{charId}/tier_{N}` |
| `AC_DIG` | A Dig scene is running | No change from entry (L1 changes only on depth_tier_changed outcome) | `ambient/{charId}/dig_entry` |
| `AC_CLOSED_OFF` | SCENE_SELECT active; character has `closed_off == true` | Silent | `ambient/{charId}/closed_off` |
| `AC_REVEAL` | `secret_revealed` event received during a running scene | `music/{charId}/tier_5_reveal` | `ambient/{charId}/tier_5_reveal` |

Valid transitions:

- `AC_IDLE → AC_CHARACTER`: portrait tapped
- `AC_CHARACTER → AC_IDLE`: back navigation
- `AC_CHARACTER → AC_SCENE`: scene or activity selected
- `AC_CHARACTER → AC_DIG`: DigEntry selected
- `AC_CHARACTER → AC_CLOSED_OFF`: character has `closed_off == true` on SCENE_SELECT entry
- `AC_SCENE → AC_CHARACTER`: scene completes (loop returns to SCENE_SELECT)
- `AC_SCENE → AC_IDLE`: scene completes (loop returns to CHARACTER_SELECT)
- `AC_SCENE → AC_REVEAL`: `secret_revealed` StateChanged event received
- `AC_DIG → AC_SCENE`: `depth_tier_changed` received during Dig (DIG_PASS outcome)
- `AC_DIG → AC_CLOSED_OFF`: `closed_off` received during Dig (DIG_FAIL outcome)
- `AC_DIG → AC_CHARACTER`: Dig scene abandoned
- `AC_CLOSED_OFF → AC_CHARACTER`: `closed_off` clears (detected on next SCENE_SELECT entry)
- `AC_REVEAL → AC_IDLE`: reveal scene completes

### Interactions with Other Systems

| System | Direction | Interface |
|---|---|---|
| Dialogue/Narrative Engine (#8) | Inbound | Subscribes to `MasksEvents.StateChanged(string eventType, string charId)`. Audio system is a passive subscriber — it does not call the engine. |
| Scene Management (#9) | Inbound | Subscribes to screen navigation events (CHARACTER_SELECT entry, portrait tap, scene selection, scene completion, scene abandon, DigEntry selection). Interface TBD in Scene Management architecture — expected as MasksEvents or direct callback. |
| Character State Manager (#6) | Inbound (read-only) | Calls `CSM.GetState(charId).depth_tier` on receipt of `depth_tier_changed` to resolve N. No writes to CSM. |
| Revelation Presentation System (#14) | Inbound | `secret_revealed` StateChanged event triggers `AC_REVEAL` state. Revelation Presentation System owns visual presentation; Audio System owns audio independently. Both react to the same event. |

## Formulas

The Audio System / Reactive Music contains no mathematical formulas. All Audio Controller decisions are boolean state checks (`depth_tier_changed → crossfade to tier_N clip`) and registry key lookups. There is no scoring, no damage, no progression curve.

Volume crossfades use linear interpolation (`Mathf.Lerp` over `Time.deltaTime`), which is standard Unity audio behavior — not a designed formula but an engine implementation detail. The crossfade durations themselves are tuning knobs (see Tuning Knobs section).

The only designer-facing calculation in this system is the linear volume ramp during crossfade:

```
volume(t) = Mathf.Lerp(start_volume, target_volume, t / crossfade_duration)
```

Where `t` is elapsed time in seconds, `start_volume` is the AudioSource's current volume at crossfade start, `target_volume` is 0.0 (fade out) or 1.0 (fade in), and `crossfade_duration` is the relevant tuning knob. This is engine-standard and not a tunable formula — it is stated here for implementation clarity.

## Edge Cases

**EC-1: Two StateChanged events arrive simultaneously — `depth_tier_changed` and `closed_off` for the same charId in AC_SCENE**
`closed_off` takes priority. Events are processed sequentially in delivery order; the last-processed event wins audio state. The event bus must guarantee `closed_off` is delivered after `depth_tier_changed` when both fire in the same frame (flagged for Dialogue Engine interface — see Dependencies). Result: L1 fades to silence, L2 crossfades to `ambient/{charId}/closed_off`, L3 fires `sting/closed_off`. The in-progress `depth_tier_changed` crossfade is abandoned per Rule 11.

**EC-2: `depth_tier_changed` arrives during AC_IDLE or AC_CHARACTER (background state write outside a scene)**
Event is queued per Rule 4. Queue holds at most one pending `depth_tier_changed` per character — a second arrival for the same charId replaces the first (only current tier is relevant on dequeue). Queue is drained when AC_SCENE is entered for that charId. If AC_SCENE is entered for a different character, the queued event is discarded.

**EC-3: `secret_revealed` arrives during AC_IDLE (upstream narrative engine bug fires event outside a scene)**
AC_REVEAL is entered immediately per Rule 4 (`secret_revealed` processes in any state). L1/L2/L3 fire the reveal audio. With no running scene, no scene-completion event will fire — the controller would be stuck in AC_REVEAL. **Additional rule**: if a CHARACTER_SELECT entry event arrives while in AC_REVEAL, transition to AC_IDLE and apply CHARACTER_SELECT audio rules. Prevents unexitable reveal state on upstream event misfires.

**EC-4: Rapid back-and-forth navigation while L2 crossfade is in progress**
Each navigation event triggers a new L2 crossfade request. Per Rule 11, each request abandons the in-progress crossfade — the currently-fading-in source becomes the outgoing source of the next crossfade at its current partial volume. This is correct behavior. Audible clicking from low-volume source reassignment is a Vertical Slice polish concern (non-linear crossfade curves).

**EC-5: `depth_tier_changed` arrives during AC_DIG**
AC_DIG → AC_SCENE transition fires. The `depth_tier_changed` audio action fires as part of the state transition: L1 crossfades to `music/{charId}/tier_{N}`, L2 crossfades to `ambient/{charId}/tier_{N}`, L3 fires the tier advance sting. No conflict — the audio action is the correct consequence of the state transition.

**EC-6: DIG_PARTIAL outcome — Dig scene completes; neither `depth_tier_changed` nor `closed_off` fires**
The controller receives a scene-completion navigation event (loop returns to SCENE_SELECT) but remains in AC_DIG because no StateChanged arrived. **Additional rule**: scene completion from AC_DIG always transitions to AC_CHARACTER, regardless of whether a StateChanged event was received. L2 crossfades to `ambient/{charId}/signature`. L1 remains at whatever level it held at Dig entry (typically silent).

**EC-7: `ambient/{charId}/closed_off` registry key resolves null during `closed_off` handling**
L2 falls back to `ambient/interior_base` per Rule 8. L1 still fades to silence. L3 fires `sting/closed_off` if resolved; skips if null. AC_CLOSED_OFF is still entered correctly — missing ambient content does not block the state transition. `[WARNING]` logged per Rule 9.

**EC-8: `music/{charId}/tier_5_reveal` resolves null during AC_REVEAL entry**
L1 plays silence per Rule 8 (L1 missing-key rule). L2 and L3 proceed normally. AC_REVEAL is entered. Reveal plays with ambient and sting but no music track. Treat a null resolve on this key as a P1 content bug given its narrative weight.

**EC-9: Session start after crash — Unity reloads into a saved state**
The Audio Controller initialises in AC_IDLE on every session start. It has no persistent state. Scene Management re-emits navigation events as it restores the saved screen state. If restored to SCENE_SELECT, the portrait-tap event fires and the controller enters AC_CHARACTER normally. If restored mid-scene, the scene-entry event fires and the controller reads current `depth_tier` from CSM. No special crash-recovery path needed in the Audio Controller.

**EC-10: `closed_off` clears between sessions — player returns to a character whose closed_off flag was true last session**
The controller checks `CSM.GetState(charId).closed_off` on every SCENE_SELECT entry. If false, enters AC_CHARACTER normally with the character's signature ambient. The prior session's AC_CLOSED_OFF state is not persisted — the in-memory event queue does not survive session boundaries.

**EC-11: L3 sting still playing when a second sting fires (e.g., tier advance immediately followed by `secret_revealed`)**
L3 has a single AudioSource. Replacement is "interrupt" — stop current, fire new immediately. For `secret_revealed`, this is correct: the reveal sting must not be delayed by an overlapping tier-advance sting. The cut is instantaneous. If audible clicking results, add a short fade-out (target: 50ms) before firing the incoming sting — Vertical Slice polish concern.

---

*State transition additions from EC-3 and EC-6 (additions to States and Transitions table):*
- `AC_REVEAL → AC_IDLE`: CHARACTER_SELECT entry event received while in AC_REVEAL (escape hatch for event-bus misfires)
- `AC_DIG → AC_CHARACTER`: scene-completion event with no StateChanged received (DIG_PARTIAL outcome)

## Dependencies

**Upstream (this system depends on these)**

| System | Dependency Type | Interface |
|---|---|---|
| Dialogue/Narrative Engine (#8) | Hard | Subscribes to `MasksEvents.StateChanged(string eventType, string charId)`. The engine is the sole publisher of this event. Audio system cannot function without it. **Cross-GDD flag**: the event bus must guarantee delivery order of `closed_off` after `depth_tier_changed` when both fire in the same frame for the same charId (see EC-1). This ordering contract must be specified in the Dialogue Engine architecture. |
| Character State Manager (#6) | Hard (read-only) | Calls `CSM.GetState(charId).depth_tier` on receipt of `depth_tier_changed`. No writes to CSM. |
| Scene & Activity Loop (#2) | Hard | Screen navigation events (portrait tap, scene selection, scene completion, Dig entry, back navigation) drive AC state transitions. Interface contract: the loop must emit events for all navigation actions listed in Core Rule 5. Event mechanism TBD in architecture (MasksEvents or direct callback). |
| Scene Management (#9) | Soft | Scene Management is the source of scene completion signals. The loop layer surfaces these; the audio system receives them via loop navigation events. No direct Scene Management interface needed. |

**Downstream (these systems depend on this one)**

| System | Dependency |
|---|---|
| Revelation Presentation System (#14) | Both systems subscribe to `secret_revealed` StateChanged independently. Revelation Presentation owns visual presentation; Audio System owns audio. No direct interface between them. **Coordination point**: both react to the same event via independent subscriptions — execution order must be defined at the event bus level to prevent reveal-moment conflicts. |

**External data dependency**

The Audio Content Registry (`assets/data/audio/audio-content-registry.asset`) must be populated before scenes can run with non-placeholder audio. Registry management is the audio content team's responsibility, not a system dependency.

## Tuning Knobs

| Knob | Type | Default | Safe Range | Effect if Too High | Effect if Too Low |
|---|---|---|---|---|---|
| `MUSIC_CROSSFADE_DURATION` | float (seconds) | 1.5 | [0.5, 4.0] | Transition feels sluggish; tier music starts too late | Abrupt cut; tier advance feels mechanical |
| `AMBIENT_CROSSFADE_DURATION` | float (seconds) | 0.4 | [0.1, 1.5] | Character ambient "blurs" on fast navigation between screens | Audible pop between ambient loops |
| `CLOSED_OFF_FADE_DURATION` | float (seconds) | 2.0 | [0.5, 5.0] | Shutdown feels labored | Cut is too abrupt for the emotional weight of closure |
| `PORTRAIT_TAP_NOTE_MAX_DURATION` | float (seconds) | 0.8 | [0.2, 2.0] | Note overlaps scene ambient start | Insufficient time to establish character identity on tap |
| `MASTER_MUSIC_VOLUME` | float (0–1) | 0.8 | [0.0, 1.0] | Music overwhelms dialogue audio | Music inaudible |
| `MASTER_AMBIENT_VOLUME` | float (0–1) | 0.6 | [0.0, 1.0] | Ambient swamps music at higher tiers | Character signature absent; scenes feel empty |
| `MASTER_STING_VOLUME` | float (0–1) | 0.9 | [0.0, 1.0] | Stings are jarring; tier advances feel punishing | Reveal moment is underwhelming |

**Interactions between knobs**: `MUSIC_CROSSFADE_DURATION` and `AMBIENT_CROSSFADE_DURATION` are independent. Volume knobs interact with player-facing audio settings (when Accessibility System #19 is designed, these knobs will be the internal targets of the player's volume sliders — they are not player-exposed values directly).

**Content-side tuning**: Per-character and per-tier feel is controlled by audio content, not code knobs. Clip selection, frequency content, mix levels within clips, and emotional arc are the audio director's domain.

## Visual/Audio Requirements

This system has no visual output. All requirements are audio-level.

**Audio layer mix targets (design intent — not player-exposed dB values):**
- L2 AMBIENT sits below L1 MUSIC in perceived loudness. Players should feel ambient presence without consciously noticing it. Target: ambient is "what the room sounds like," music is "what the scene sounds like."
- L3 STING is the loudest transient. It must cut through L1 and L2 clearly; the moment of tier advance or secret reveal should register before the player reads the next dialogue line.
- L4 UI sounds are the shortest events in the system. They must register and decay before the ambient settles — not merge into a combined texture with L2.

**Per-character audio content spec (for audio director and content team):**
- Each character's ambient signature (`ambient/{charId}/signature`) must be immediately legible as belonging to that character. A player who knows the cast should be able to identify a character by their ambient before any dialogue.
- Tier progression in ambient must be directional — tier 4 should feel measurably more intimate or exposed than tier 1, regardless of character. The direction of change is part of the system's grammar.
- Character-specific portrait tap notes must be distinct from each other and from the `ui/scene_entry_resolve` sound. They are identity sounds, not UI sounds.
- The `sting/secret_revealed` is the most important audio moment in the game. It must feel unlike any other sting — it is not a level-up; it is a truth surfacing. Direction deferred to the Audio Director (see Open Questions).

**Format and encoding (Unity import settings):**
- L1 MUSIC (streaming): Vorbis compression, streaming load type.
- L2 AMBIENT (looping): Vorbis compression, Compressed in Memory, `loop = true`.
- L3 STING (one-shot): ADPCM compression, Decompress on Load.
- L4 UI (short, one-shot): ADPCM compression, Decompress on Load.

## UI Requirements

The Audio System has no UI. It produces no player-facing screen elements. All audio control surfaces (volume sliders, mute toggles) belong to the Accessibility System (#19) when designed — those systems will expose `MASTER_MUSIC_VOLUME`, `MASTER_AMBIENT_VOLUME`, and `MASTER_STING_VOLUME` as player-adjustable values.

At MVP: no audio settings UI. Volume knobs are constants set in the Unity AudioMixer.

## Acceptance Criteria

Criteria use Given-When-Then format. Gate level: **BLOCKING** = must pass before ship; **ADVISORY** = human review required (audio/feel); **Code Review** = verified by code inspection, not runtime test.

### State Machine

| ID | Given | When | Then | Gate |
|---|---|---|---|---|
| AC-AUDIO-SM-01 | Controller has just initialized | No navigation event has occurred | Controller is AC_IDLE; L1 silent; L2 playing `ambient/interior_base` | Logic/BLOCKING |
| AC-AUDIO-SM-02 | Controller is AC_IDLE | Portrait-tap event fires for charId X | Controller → AC_CHARACTER; L2 crossfades to `ambient/X/signature`; L4 fires `ui/X/portrait_tap_note` | Logic/BLOCKING |
| AC-AUDIO-SM-03 | Controller is AC_CHARACTER | Back navigation fires (SCENE_SELECT → CHARACTER_SELECT) | Controller → AC_IDLE; L1 remains silent; L2 crossfades to `ambient/interior_base` | Logic/BLOCKING |
| AC-AUDIO-SM-04 | Controller is AC_CHARACTER; character `closed_off == false` | Scene-selection event fires | Controller → AC_SCENE; L1 begins `music/X/tier_N` (or silence if absent); L2 crossfades to `ambient/X/tier_N` | Logic/BLOCKING |
| AC-AUDIO-SM-05 | Controller is AC_CHARACTER; character `closed_off == true` | SCENE_SELECT entry fires for that character | Controller → AC_CLOSED_OFF (not AC_SCENE); L1 silent; L2 crossfades to `ambient/X/closed_off` | Logic/BLOCKING |
| AC-AUDIO-SM-06 | Controller is AC_CHARACTER | DigEntry-selection event fires | Controller → AC_DIG; L2 crossfades to `ambient/X/dig_entry`; L1 unchanged; L4 fires `ui/dig_entry_swell` | Logic/BLOCKING |
| AC-AUDIO-SM-07 | Controller is AC_SCENE | `secret_revealed` StateChanged fires | Controller → AC_REVEAL; L1 crossfades to `music/X/tier_5_reveal`; L2 crossfades to `ambient/X/tier_5_reveal`; L3 fires `sting/secret_revealed` | Logic/BLOCKING |
| AC-AUDIO-SM-08 | Controller is AC_DIG | `depth_tier_changed` StateChanged fires | Controller → AC_SCENE; L1 crossfades to `music/X/tier_N`; L2 crossfades to `ambient/X/tier_N`; L3 fires tier-advance sting | Logic/BLOCKING |
| AC-AUDIO-SM-09 | Controller is AC_DIG | `closed_off` StateChanged fires | Controller → AC_CLOSED_OFF; L1 fades to silence over `CLOSED_OFF_FADE_DURATION`; L2 crossfades to `ambient/X/closed_off`; L3 fires `sting/closed_off` | Logic/BLOCKING |
| AC-AUDIO-SM-10 | Controller is AC_DIG | Scene-completion fires; no `depth_tier_changed` or `closed_off` received (DIG_PARTIAL) | Controller → AC_CHARACTER; L2 crossfades to `ambient/X/signature` | Logic/BLOCKING |
| AC-AUDIO-SM-11 | Controller is AC_REVEAL; no running scene | CHARACTER_SELECT entry event fires | Controller → AC_IDLE (escape hatch); L1 fades to silence; L2 crossfades to `ambient/interior_base` | Logic/BLOCKING |
| AC-AUDIO-SM-12 | Controller is AC_REVEAL | Scene-completion event fires normally | Controller → AC_IDLE | Logic/BLOCKING |
| AC-AUDIO-SM-13 | Controller is AC_SCENE | Scene abandoned (ABANDONED navigation event) | L1 fades out; L2 crossfades to `ambient/X/signature` | Logic/BLOCKING |

### Layer Architecture

| ID | Given | When | Then | Gate |
|---|---|---|---|---|
| AC-AUDIO-LAYER-01 | Controller is initialized | Inspected in Unity editor | Exactly 6 AudioSource components exist on the AudioController GameObject (2 L1, 2 L2, 1 L3, 1 L4); all have `spatialBlend == 0` | Logic/BLOCKING |
| AC-AUDIO-LAYER-02 | An L3 sting is playing | A second L3 request fires | First source is stopped immediately; new clip begins from start with no delay | Logic/BLOCKING |
| AC-AUDIO-LAYER-03 | An L4 sound is playing | A second L4 request fires | Both sounds are audible simultaneously (fire-and-forget overlap) | ADVISORY |

### Event Routing

| ID | Given | When | Then | Gate |
|---|---|---|---|---|
| AC-AUDIO-EVENT-01 | Controller is AC_SCENE | `depth_tier_changed` fires for active charId | L1 crossfades to `music/X/tier_N` (N from CSM after event); L2 crossfades to `ambient/X/tier_N`; L3 fires tier-advance sting | Integration/BLOCKING |
| AC-AUDIO-EVENT-02 | Controller is AC_SCENE | `closed_off` fires | L1 fades to silence over `CLOSED_OFF_FADE_DURATION`; L2 crossfades to `ambient/X/closed_off`; L3 fires `sting/closed_off` | Integration/BLOCKING |
| AC-AUDIO-EVENT-03 | Controller is AC_SCENE; `depth_tier_changed` and `closed_off` arrive same frame (closed_off delivered last) | Both events processed | Final state matches `closed_off` handling: L1 silent, L2 on closed_off ambient, L3 fired sting. In-progress tier crossfade abandoned. *(Requires Dialogue Engine event-order contract confirmed — blocked until then)* | Integration/BLOCKING |
| AC-AUDIO-EVENT-04 | Controller is AC_IDLE or AC_CHARACTER | `depth_tier_changed` fires | No audio action fires immediately; event held in per-character queue | Logic/BLOCKING |
| AC-AUDIO-EVENT-05 | Controller is in any state | `secret_revealed` fires | AC_REVEAL entered; reveal audio fires regardless of prior state | Logic/BLOCKING |
| AC-AUDIO-EVENT-06 | Any | Codebase inspected | No public method calls to AudioController exist outside its own event-handler subscriptions | Code Review |

### Crossfade Mechanics

| ID | Given | When | Then | Gate |
|---|---|---|---|---|
| AC-AUDIO-XFADE-01 | A crossfade on L1 or L2 is in progress | New crossfade request arrives for same layer | In-progress crossfade abandoned; previously-incoming source becomes outgoing at current volume; new incoming fades from 0 | Logic/BLOCKING |
| AC-AUDIO-XFADE-02 | A crossfade completes normally | Outgoing reaches 0, incoming reaches 1 | Volume trajectory is linear (no step, no curve bend) | ADVISORY |
| AC-AUDIO-XFADE-03 | `MUSIC_CROSSFADE_DURATION` set to minimum (0.5s) | Crossfade triggered | Crossfade completes within 0.5s ± 16ms | Logic/BLOCKING |

### Fallback Rules

| ID | Given | When | Then | Gate |
|---|---|---|---|---|
| AC-AUDIO-FALLBACK-01 | `Registry.Resolve("music/X/tier_N")` returns null | L1 audio action triggered | L1 plays silence; no other tier substituted; no exception | Logic/BLOCKING |
| AC-AUDIO-FALLBACK-02 | `Registry.Resolve("ambient/X/tier_N")` returns null | L2 audio action triggered | L2 falls back to `ambient/interior_base`; no exception | Logic/BLOCKING |
| AC-AUDIO-FALLBACK-03 | `Registry.Resolve("sting/X/tier_advance")` returns null | Tier-advance sting triggered | L3 fires nothing; L1 and L2 actions proceed normally; no exception | Logic/BLOCKING |
| AC-AUDIO-FALLBACK-04 | `Registry.Resolve("ui/X/portrait_tap_note")` returns null | Portrait-tap event fires | L4 fires nothing; L2 crossfade to signature proceeds; no exception | Logic/BLOCKING |
| AC-AUDIO-FALLBACK-05 | `Registry.Resolve("ambient/X/signature")` returns null | L2 would play character signature | L2 falls back to `ambient/interior_base` | Logic/BLOCKING |
| AC-AUDIO-FALLBACK-06 | Any required clip slot resolves null on startup | Controller Awake/Start completes | `[WARNING]` in Unity log per null slot; controller continues without exception | Logic/BLOCKING |

### Queue Semantics

| ID | Given | When | Then | Gate |
|---|---|---|---|---|
| AC-AUDIO-QUEUE-01 | `depth_tier_changed` queued for charId X (in AC_CHARACTER) | Second `depth_tier_changed` arrives for same charId | Only the second (most recent) event retained; first discarded | Logic/BLOCKING |
| AC-AUDIO-QUEUE-02 | `depth_tier_changed` queued for charId X | AC_SCENE entered for charId X | Queued event dequeued; audio actions fire immediately on scene entry | Logic/BLOCKING |
| AC-AUDIO-QUEUE-03 | `depth_tier_changed` queued for charId X | AC_SCENE entered for different charId Y | Queued event for X discarded; does not fire | Logic/BLOCKING |

### Session Initialization

| ID | Given | When | Then | Gate |
|---|---|---|---|---|
| AC-AUDIO-INIT-01 | Session starts (including after crash recovery) | Audio Controller initializes | Controller enters AC_IDLE; L1 silent; L2 on `ambient/interior_base`; regardless of saved game state | Logic/BLOCKING |
| AC-AUDIO-INIT-02 | Game restores saved state — player was at SCENE_SELECT for charId X | Scene Management re-emits portrait-tap navigation event | Controller enters AC_CHARACTER with `ambient/X/signature` on L2, as if a fresh navigation action | Integration/BLOCKING |

---

**Untestable items re-routed:**
- **Rule 13 (authoring constraint — `secret_revealed` placement)**: Move to Narrative Content Gate checklist — each reveal scene's Yarn script must be reviewed to confirm `<<notify_state_change secret_revealed {charId}>>` is not placed on the opening node.
- **AC-AUDIO-EVENT-06**: Assigned to code-review checklist, not runtime QA.
- **AC-AUDIO-EVENT-03**: Blocked pending Dialogue Engine delivery-order contract confirmation. Do not write this test until the contract is defined in the Dialogue Engine architecture.

## Open Questions

1. **Audio Director direction for `sting/secret_revealed`** — The secret reveal sting is the most narratively significant audio event in the game. Its character (tonal, atonal, silence-then-sound, harmonic, dissonant?) must be decided by the Audio Director as part of the game's overall sonic identity. This GDD defers that decision. Audio Director must specify this before any reveal scene enters production audio.

2. **Event bus delivery order guarantee (EC-1)** — When `depth_tier_changed` and `closed_off` fire in the same frame for the same character, the audio system requires `closed_off` to be delivered last. This ordering contract must be specified in the Dialogue/Narrative Engine architecture (ADR). AC-AUDIO-EVENT-03 is blocked until this is confirmed.

3. **Scene Management navigation event interface** — The audio system subscribes to screen navigation events from the Scene & Activity Loop. The interface mechanism (MasksEvents bus vs. direct callback vs. UnityEvent) is TBD — it must be decided during architecture (ADR). The audio system has no preference between mechanisms; it only requires that all events in Core Rule 5 are emitted.

4. **Accessibility System volume integration** — When Accessibility System (#19) is designed, it will expose player-facing audio sliders mapping to `MASTER_MUSIC_VOLUME`, `MASTER_AMBIENT_VOLUME`, and `MASTER_STING_VOLUME`. The mapping curve (linear vs. logarithmic perceived loudness) must be decided at that time. Flag for #19 authoring.

5. **Content volume at Early Access** — At MVP, all audio slots are placeholder clips. Before Early Access, all per-character slots must be populated with final content. The Audio Content Registry validation log (Rule 9 startup warnings) is the content team's audit tool. A full P1 content gate before Early Access must require zero startup warnings across all registered characters.
