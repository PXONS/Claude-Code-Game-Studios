# Dialogue/Narrative Engine

> **Status**: Complete
> **Author**: Design session + agents
> **Last Updated**: 2026-05-02
> **Implements Pillar**: Pillar 2 (You Choose How Deep), Pillar 4 (Designed to Be Shared)

## Summary

[To be designed]

> **Quick reference** — Layer: `Foundation` · Priority: `MVP` · Key deps: `None`

## Overview

The Dialogue/Narrative Engine is the script runner that delivers every scene, character interaction, and choice in Masks. Built on YarnSpinner 3.0, it parses and executes `.yarn` script files, displays dialogue lines through the UI system, presents player choices, and reads and writes per-character relationship state through a custom `VariableStorageBehaviour` integration. It is the game's single point of execution for all authored content.

From the player's perspective, the engine is invisible — what they experience is a character's face, a line of dialogue, a choice that matters. The engine's job is to ensure that every authored moment is delivered with perfect fidelity: the right line, at the right depth tier, with the player's chosen name and pronouns woven through naturally. When the engine works correctly, the player is thinking about the character, not the software.

The engine is a Foundation system — 8 other systems depend on it, and no scene content can exist without it. The architectural decision to use YarnSpinner (over Ink) was resolved in the dialogue-engine spike: YarnSpinner's live variable binding eliminates the state-flush gap that would otherwise create a class of silent data-loss bugs at scale. The spike report is at `prototypes/dialogue-engine-spike/REPORT.md`.

## Player Fantasy

The player leans forward. They've just said something risky — something that admits too much. The text box holds. One beat. Two. And then the response arrives, and it's not what they expected, and it changes everything.

Masks lives or dies in those pauses. The player isn't reading dialogue — they're being read by it. Every line that surfaces, every choice that appears (and every choice that pointedly doesn't), every name spoken back to them in the right cadence is the engine whispering: *this person is real, this person is listening, this person remembers.* When the timing is right, the player forgets they're playing. When the timing is wrong, the spell breaks and they remember it's a game.

**The fantasy is intimacy that feels earned.** The engine's job is to never remind the player that intimacy is being rendered.

**Supporting fantasies this engine must also deliver:**
- *Pillar 4 — Designed to Be Shared*: Somewhere in every route there's a line the player will screenshot and send at 2 AM. The engine places it at the right pacing, with the right name, with the player's pronoun threaded so cleanly it was never a variable. If the substitution is awkward, if the timing breaks, the line doesn't land and the shareable moment is lost.
- *Pillar 5 — No Wrong Type*: The player's name and pronouns are not a customisation surface — they are the player's identity inside the fiction. When a character addresses the player correctly without hesitation, it affirms that the game was written for them. When it doesn't, the entire relationship is revealed as scaffolding.

**Design test**: If a programmer is deciding whether to pre-load the next dialogue line or fetch it on choice-confirm — this fantasy says pre-load. The pause must feel authored, never technical.

## Detailed Design

### Core Rules

**Scene Loading**

1. A scene is a single YarnSpinner node. The canonical identifier is a fully-qualified node name string: `{CharacterID}_{SceneSlug}` (e.g., `ren_surface_01`, `ren_diggate_02`). No other identifier type is used at runtime.
2. The Scene Management system provides the engine with a node name string via `DialogueEngineController.StartScene(string nodeName)`. The engine does not decide which node to run — it executes the node it is given.
3. Before starting execution, the engine verifies the node exists in the loaded `YarnProject`. If the node does not exist, the engine logs an error at `[ERROR]` severity and transitions to `Complete` without running content. It does not throw an exception.
4. All `.yarn` script assets are loaded via Unity Addressables. The engine expects them to be loaded and registered with the `YarnProject` before `StartScene()` is called. Asset loading is the caller's responsibility, not the engine's.
5. Only one node runs at a time. Concurrent dialogue execution is not supported. Calling `StartScene()` while in `Running` or `AwaitingInput` is an error; the engine logs it and ignores the call.

**Execution Model**

6. The engine executes YarnSpinner nodes in document order: lines are delivered top-to-bottom; commands execute inline; choices block until input is received. This is the standard YarnSpinner execution model — no modifications to execution order are made.
7. Each dialogue line is passed to the UI system via `DialogueViewBase.RunLine()`. The engine does not format the line — it passes the raw `LocalizedLine` object. Speaker identification is a field on `LocalizedLine`; the UI reads it.
8. When YarnSpinner delivers a choice set, the engine passes it to the UI via `DialogueViewBase.RunOptions()` and transitions to `AwaitingInput`. The engine takes no action until the UI calls `OnOptionSelected(DialogueOption)`.
9. The engine enforces no timeout on `AwaitingInput`. Choice presentation is indefinite until the player selects.

**Variable Binding**

10. All per-character depth state is read and written exclusively through `MasksVariableStorage`, a `VariableStorageBehaviour` subclass that wraps `CharacterStateManager`. The engine never calls `CharacterStateManager` directly.
11. Variable naming convention in `.yarn` scripts: `$char_{CharacterID}_{variable}` (e.g., `$char_ren_depth_tier`, `$char_ren_closed_off`). `MasksVariableStorage` maps these string keys to the correct `CharacterStateManager` fields. This convention is required — all writers must use it; deviations will silently return defaults.
12. PC identity variables are injected at session start as YarnSpinner variables with these fixed names:
    - `$pc_name` (string) — the player's display name
    - `$pc_pronoun_subject` (string) — `"he"`, `"she"`, or `"they"`
    - `$pc_pronoun_object` (string) — `"him"`, `"her"`, or `"them"`
    - `$pc_pronoun_possessive` (string) — `"his"`, `"her"`, or `"their"`
    - `$pc_pronoun_reflexive` (string) — `"himself"`, `"herself"`, or `"themself"`
13. PC variable injection is performed by Scene Management immediately after scene load, before any `StartScene()` call, by calling `MasksVariableStorage.SetValue()` for each PC variable. After injection, writers use these variables directly in `.yarn` scripts (e.g., `Hi, {$pc_name}!`). YarnSpinner's line compiler resolves substitution before the line reaches the UI.
14. `MasksVariableStorage` must immediately persist every variable write to `CharacterStateManager` — not on scene end, not on save, but synchronously during the `SetValue()` call.
15. `MasksVariableStorage` must return a valid typed value for every variable key in the schema. If queried for an unknown key, it returns the YarnSpinner type default (`0`, `false`, or `""`) and logs a `[WARNING]`. It does not throw.

**Custom Command Protocol**

16. Custom commands are declared in `.yarn` scripts as `<<command_name arg1 arg2>>` and registered with `DialogueRunner` via `AddCommandHandler`. All custom commands must be registered before `StartScene()` is called on any node that uses them.
17. When a custom command is encountered, the engine transitions to `ExecutingCommand`, invokes the handler, and waits for completion before advancing. Handlers returning `void` are synchronous and complete immediately. Handlers returning `Awaitable` are asynchronous — the engine waits for resolution using Unity's native async pattern (Unity 6.3 `Awaitable`).
18. `<<notify_state_change event_type char_id>>` is the prescribed mechanism for signaling state changes to downstream systems.
    - Valid `event_type` values for MVP: `depth_tier_changed`, `closed_off`, `secret_revealed`. Additional types will be added as downstream system GDDs are authored.
    - On invocation, the engine raises `MasksEvents.StateChanged(eventType, charId)` on the project's event bus. No payload is included — subscribers query `CharacterStateManager` directly for current values.
    - The command is synchronous: it fires the event and returns immediately.
    - `<<notify_state_change>>` must be called *after* the variable write that caused the change (Rule 14 ensures the write is already persisted when the event fires).
19. No custom command may write to `CharacterStateManager` directly. The required protocol is always: (a) YarnSpinner variable assignment → `MasksVariableStorage` persists → (b) `<<notify_state_change>>` fires the event. A command that bypasses this protocol is a defect.
20. Unrecognized command names produce a `[WARNING]` log; the engine skips them and continues. This allows `.yarn` files to be forward-authored against commands not yet implemented.
21. `<<pause duration_ms>>` is a built-in MVP command. It causes the engine to transition to `ExecutingCommand` and wait `duration_ms` milliseconds (using `Awaitable.WaitForSecondsAsync`) before advancing. Writers use this to insert authored timing holds — the pause in "the pause before they answer." Minimum duration: 0ms. Maximum: 5000ms (clamped by the engine; longer pauses are a content error, not a runtime feature).

**Scene End and Control Handback**

22. A scene ends when YarnSpinner's `DialogueCompleteHandler` is called. This occurs when the running node reaches its end without a `<<jump>>` or `<<stop>>` directive.
23. `<<jump OtherNodeName>>` causes the engine to immediately begin executing `OtherNodeName` without returning to `Idle`. Control is not returned to Scene Management between chained nodes.
24. On natural end or `<<stop>>`, the engine transitions to `Complete` and raises `MasksEvents.SceneComplete(nodeName)`. Scene Management subscribes to this event and decides what happens next. The engine does not make this decision.
25. Scene Management calls `DialogueEngineController.EndScene()` to reset the engine to `Idle` after consuming `SceneComplete`. `EndScene()` is a wrapper around `DialogueRunner.Stop()` — Scene Management is decoupled from YarnSpinner's API.

---

### States and Transitions

The engine is a finite state machine with six states. Only one state is active at a time.

```
Idle ──StartScene──▶ Loading ──NodeReady──▶ Running
 ▲                                          │    │
 │                          AwaitingInput ◀─┘    │
 │                               │               │
 │                      OnOptionSelected          │
 │                               └───────▶ Running│
 │                                          │    │
 │                          ExecutingCommand◀─────┘
 │                               │
 │                      CommandComplete
 │                               └───────▶ Running
 │                                          │
 └──EndScene()──── Complete ◀──────────────┘
```

| State | Entry | Exit Condition | Behavior |
|---|---|---|---|
| **Idle** | Initialization; after `EndScene()` | `StartScene(nodeName)` called | Engine inactive; all dialogue UI hidden |
| **Loading** | After `StartScene()` | Node found → `Running`; not found → `Complete` (error) | Synchronous node validation against `YarnProject`. Target: < 1 frame. |
| **Running** | From Loading, AwaitingInput, ExecutingCommand | Choice set → `AwaitingInput`; command → `ExecutingCommand`; node end → `Complete` | YarnSpinner executing. Lines delivered to UI. Commands dispatched. |
| **AwaitingInput** | Choice set delivered to UI | `OnOptionSelected()` received | Blocked. No processing. Indefinite duration. |
| **ExecutingCommand** | Command encountered during Running | Handler completes (sync: same frame; async: when `Awaitable` resolves) | Command handler executing. If handler throws, log `[ERROR]`, treat as complete, continue. |
| **Complete** | Node ends naturally or via `<<stop>>`; or error in Loading | `EndScene()` called by Scene Management | Raises `MasksEvents.SceneComplete`. UI still visible until Scene Management acts. No new dialogue may start. |

---

### Interactions with Other Systems

**Character State Manager**
- Direction: bidirectional, mediated exclusively through `MasksVariableStorage`
- Engine reads: depth tier (`int` 0–5), closed-off flag (`bool`), any per-character bool flags used in `.yarn` branch conditions
- Engine writes: same fields, via YarnSpinner `<<set>>` in `.yarn` scripts → `MasksVariableStorage.SetValue()` → `CharacterStateManager`
- No other code path within the engine accesses `CharacterStateManager`
- Ownership: `CharacterStateManager` owns the data. `MasksVariableStorage` owns the YarnSpinner interface. The engine owns neither.

**Player Profile / PC Data**
- Direction: one-way inward; engine never writes back to Player Profile
- Injection: Scene Management calls `MasksVariableStorage.SetValue()` for the 5 PC variables (Rule 12) before the first `StartScene()` of a session
- After injection, PC variables behave identically to any other YarnSpinner variable; YarnSpinner resolves `{$pc_name}` etc. at line compilation time before the line reaches the UI
- Ownership: Player Profile owns PC data. Scene Management owns the injection step. The engine is a passive consumer.

**Scene Management / Flow Controller**
- Direction: bidirectional
- Inbound: `DialogueEngineController.StartScene(string nodeName)` — begins execution; `DialogueEngineController.EndScene()` — resets to Idle
- Outbound: `MasksEvents.SceneComplete(string nodeName)` — raised on transition to Complete
- Scene Management is the sole caller of `StartScene()`. No other system starts dialogue.
- The engine does not maintain a scene queue. Scene Management owns all sequencing logic.

**Audio System**
- Direction: one-way outbound (notifications only)
- Mechanism: Audio system subscribes to `MasksEvents.StateChanged(string eventType, string charId)`
- The event carries no payload beyond `eventType` and `charId`. Audio system queries `CharacterStateManager` directly for current depth tier or other state values before acting.
- Audio system's responsibility: transition music layers, trigger stings, update audio state — all driven by the notification + direct state query pattern.

**UI System**
- Direction: outbound for content delivery; inbound callback for choice selection
- Engine → UI: `DialogueViewBase.RunLine(LocalizedLine, Action)`, `DialogueViewBase.RunOptions(DialogueOptionCollection, Action<DialogueOption>)`, `DialogueViewBase.DialogueStarted()`, `DialogueViewBase.DialogueComplete()`
- UI → Engine: `OnOptionSelected(DialogueOption)` callback (from `RunOptions`)
- Speaker name, character ID, and any metadata tags (e.g., expression markers) are fields in `LocalizedLine`. The UI reads them to look up portraits and name pills. The engine does not perform character lookups.
- The UI owns text reveal animation. The engine delivers the full line and waits for the UI to call `onLineFinished` before requesting the next line.

**Text Substitution**
- Not a separate runtime system. YarnSpinner's built-in `{$variable}` inline expression evaluation handles all PC name and pronoun substitution at line compile time, before the line reaches `RunLine()`. By the time the UI receives a `LocalizedLine`, all substitutions are resolved — the UI displays a plain string.
- Defense against empty-string artifacts: Rule 15 requires `MasksVariableStorage` to always return a valid value for declared variables. The Acceptance Criteria include a playtest with each pronoun setting to verify no empty substitution artifacts appear in rendered dialogue.

## Formulas

### F-1: Text Reveal Rate

```
chars_per_second = BASE_REVEAL_RATE * speed_multiplier
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `BASE_REVEAL_RATE` | float (constant) | 45.0 cps | Baseline reveal speed tuned for comfortable reading pace at narrative tension |
| `speed_multiplier` | float | 0.0–10.0 (user setting) | Accessibility multiplier. `0.0` = instant reveal. `1.0` = default. Clamped by engine to [0.0, 10.0]. |
| `chars_per_second` | float | 0.0–450.0 | Output rate passed to UI's typewriter controller. `0.0` is the instant-reveal sentinel. |

**Output range:** [0.0, 450.0] cps after clamping. Values above ~10× base are perceptually indistinguishable from instant.

**Accessibility requirement:** Player must be able to set `speed_multiplier` to `0.0` from settings at any time. The engine passes the computed value to the UI — it does not own the animation.

**Examples:**
- Default: `45.0 * 1.0 = 45.0 cps` → 90 characters in 2.0 seconds
- Slow: `45.0 * 0.5 = 22.5 cps` → 90 characters in 4.0 seconds
- Instant: `45.0 * 0.0 = 0.0` → full line renders on the same frame it is received

---

### F-2: `<<pause>>` Duration

```
pause_ms = clamp(arg_ms, PAUSE_MIN_MS, PAUSE_MAX_MS)
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `arg_ms` | int | 0–∞ (script-authored) | Duration argument from `.yarn` script. Absent = 750ms default. |
| `PAUSE_MIN_MS` | int (constant) | 0 | Floor. Zero-duration pause is a valid no-op. |
| `PAUSE_MAX_MS` | int (constant) | 5000 | Ceiling. Durations above this are a content error — engine clamps and logs `[WARNING]`. |
| `pause_ms` | int | 0–5000 | Clamped duration passed to `Awaitable.WaitForSecondsAsync(pause_ms / 1000f)`. |

**Default when no argument:** 750ms — calibrated as the minimum duration perceived as an intentional hesitation rather than a technical delay.

**Examples:**
- `<<pause 1200>>` → 1200ms
- `<<pause 8000>>` → clamped to 5000ms + `[WARNING]`
- `<<pause>>` → 750ms (default)

**Timing note:** The pause timer begins only after `onLineFinished` is called by the UI. Pauses apply between engine instructions, not mid-reveal.

---

### F-3: Choice Display Stagger Offset

```
stagger_offset_ms(i) = i * STAGGER_STEP_MS
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `i` | int | 0–(N−1) | Zero-based option index. Option 0 is the topmost choice. |
| `STAGGER_STEP_MS` | int (constant) | 60 | Per-option delay increment in ms. Sourced from art bible Section 7.4. |
| `N` | int | 1–4 | Total options in the set (see F-4). |
| `stagger_offset_ms(i)` | int | 0–180 | Delay before option `i` begins its entry animation. |

**Output range:** [0, (N−1) × 60] ms. Option 0 always appears immediately.

**Engine responsibility:** Engine computes offsets and includes them as metadata in the `DialogueOptionCollection` passed to `RunOptions()`. UI reads offsets and schedules animations.

**Example (3 options):** Option 0: 0ms · Option 1: 60ms · Option 2: 120ms

---

### F-4: Maximum Options Per Choice Set

```
valid = (N <= MAX_CHOICES_PER_SET)
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `N` | int | 1–∞ (script-authored) | Number of options in the choice set delivered by YarnSpinner. |
| `MAX_CHOICES_PER_SET` | int (constant) | 4 | Engine-enforced ceiling. Sourced from UI layout constraints: 4 options fill the choice panel without scrolling. |
| `valid` | bool | — | True if `N <= 4`. |

**Enforcement:** Validated in `RunOptions()`. If `N > 4`: log `[ERROR]`, present first 4 options, discard remainder. Does not crash. A `.yarn` script exceeding this limit is a content error.

**Examples:**
- 3 options → valid, all 3 delivered
- 6 options → invalid, first 4 delivered, options 5–6 discarded, `[ERROR]` logged with node name

---

### F-5: Node Name Validation Pattern

```regex
^[a-z][a-z0-9]*_[a-z][a-z0-9_]*$
```

| Component | Rule |
|---|---|
| `^[a-z][a-z0-9]*` | CharacterID — lowercase alphanumeric, starts with letter |
| `_` | Exactly one separator underscore |
| `[a-z][a-z0-9_]*$` | SceneSlug — lowercase alphanumeric with internal underscores, starts with letter |

**Valid:** `ren_surface_01`, `sol_diggate_02`, `mav_intro`, `sol_route_a_end`
**Invalid:** `Ren_surface_01` (uppercase), `ren_` (missing slug), `ren-surface` (hyphen)

**Enforcement:** Validated in `DialogueEngineController.StartScene()` before YarnProject lookup. Failure → `[ERROR]` log, transition to `Complete`, raise `MasksEvents.SceneComplete(nodeName)`. Same failure path as "node not found."

---

### F-6: Inter-Line Delay (Engine Floor)

The engine imposes **no minimum delay** between consecutive lines. After the UI calls `onLineFinished`, the engine immediately requests the next instruction from YarnSpinner. Engine floor: 0ms.

Any pacing hold between lines (e.g., a brief pause after the player advances) is owned entirely by the UI layer. Authored timing hesitations use `<<pause>>`; UI responsiveness timing is a UI concern. This is a declared non-formula that closes the boundary between engine and UI timing ownership.

## Edge Cases

- **If `StartScene()` is called while engine is in `Running` state**: log `[ERROR]`, ignore the call, continue active node. Concurrent dialogue is prohibited (Rule 5); silently ignoring is safer than crashing.

- **If `StartScene()` is called while engine is in `AwaitingInput` state**: identical to above — log `[ERROR]`, discard the call, leave choice set visible, remain blocked. The player has an unresolved choice; discarding it would corrupt narrative state.

- **If the node name passes validation but is not present in the `YarnProject`**: log `[ERROR]`, transition to `Complete`, raise `MasksEvents.SceneComplete(nodeName)`. Scene Management receives the terminus signal and handles recovery. No exception thrown.

- **If the `YarnProject` reference is null or unloaded when `StartScene()` is called**: log `[ERROR]`, transition to `Complete`, raise `MasksEvents.SceneComplete(nodeName)`. The null check runs synchronously at the top of `StartScene()`, before any node lookup.

- **If the node name string fails the validation regex**: log `[ERROR]` with the invalid string and expected pattern, transition to `Complete`, raise `MasksEvents.SceneComplete(nodeName)`. Validation runs before any `YarnProject` lookup so the failure signal identifies the bug location: caller string construction, not missing content.

- **If `MasksVariableStorage` is queried for an unregistered variable key**: return the YarnSpinner type default (`0`, `false`, or `""`), log `[WARNING]`. Execution continues. Unregistered bool keys return `false` — their branch silently fails, which is conservative and prevents phantom content.

- **If `<<pause N>>` is authored with `N > 5000`**: clamp to 5000ms, log `[WARNING]` with node name and original value. Scene continues after the clamped wait.

- **If `<<pause>>` is called with no argument**: use default 750ms. No warning.

- **If `<<pause 0>>` is authored**: wait 0ms (`Awaitable.WaitForSecondsAsync(0f)` yields one frame). Valid, no warning.

- **If a choice set is delivered with `N > 4` options**: log `[ERROR]` with node name and discarded option texts, present only the first 4 options, discard the remainder. "First 4" rule is defined so writers know which options survive truncation — most important branches must be placed first.

- **If `<<notify_state_change event_type char_id>>` is called with an unrecognized `event_type`**: fire `MasksEvents.StateChanged` with the literal unrecognized string anyway, log `[WARNING]`. Writers may forward-author against event types not yet wired in downstream systems; the engine fires faithfully and moves on.

- **If `<<jump TargetNode>>` is encountered and `TargetNode` is not in the `YarnProject`**: catch the YarnSpinner runtime error, log `[ERROR]` with originating node name and missing target, transition to `Complete`, raise `MasksEvents.SceneComplete(originatingNode)`. No fallback content. *Implementation note*: YarnSpinner 3.0's exact error callback API for jump failures should be verified against the spike report before implementation.

- **If `StartScene()` is called before PC variables are injected**: `MasksVariableStorage` returns `""` for each missing PC variable key, logs `[WARNING]` per key. Player sees empty substitution artifacts (e.g., `"Hi, !"`) — visible, broken. Enforcement is at Scene Management; the engine specifies this failure mode precisely so QA can write a regression test against it.

- **If the player taps through dialogue faster than the text reveal animation completes**: handled entirely by the UI layer. First advance input: UI instantly reveals the full line. Second advance input: UI calls `onLineFinished`. The engine is waiting for `onLineFinished` and receives it when the UI sends it — rapid tapping produces no engine-level edge case. This boundary is declared explicitly: rapid tapping is not an engine event.

- **If `OnOptionSelected()` is received when engine state is not `AwaitingInput`**: log `[ERROR]` with current state, discard the option, take no action. Indicates a UI bug (double-tap, stale click handler). Forwarding to YarnSpinner while outside `AwaitingInput` would corrupt the execution cursor.

- **If a custom command handler throws an unhandled exception**: catch at the `DialogueRunner` dispatch level, log `[ERROR]` with command name, exception type, and node name, treat command as complete, transition back to `Running`, continue execution. Exception does not propagate to Unity's unhandled exception path. The scene runs to its end; the command failure is recorded for QA investigation. If the failed command was a state-write, the state change may not have completed — the error log must contain enough context to detect this.

- **If `<<stop>>` is encountered mid-execution of a multi-node chain (after one or more `<<jump>>` directives)**: terminate normally — transition to `Complete`, raise `MasksEvents.SceneComplete(currentNode)` where `currentNode` is the node containing `<<stop>>`, not the original `StartScene()` node. Scene Management receives exactly one `SceneComplete` per `StartScene()` call regardless of chain length. The `<<stop>>` node name is the most useful payload — it identifies which authored endpoint was reached.

## Dependencies

| System | Direction | Nature of Dependency |
|---|---|---|
| Character State Manager | Bidirectional (mediated by MasksVariableStorage) | Data dependency — engine reads and writes depth tier, closed-off flag, and character bool flags through YarnSpinner's variable storage protocol. CharacterStateManager owns the data; MasksVariableStorage owns the YarnSpinner interface. |
| Player Profile / PC Data | This depends on Player Profile | Data dependency — PC name and pronoun variables (5 forms) are read from Player Profile and injected into MasksVariableStorage at session start before any scene runs. Engine is a passive consumer. |
| Scene Management / Flow Controller | Bidirectional | State trigger — Scene Management calls `DialogueEngineController.StartScene()` and `EndScene()`; engine raises `MasksEvents.SceneComplete(nodeName)` when a scene ends. Scene Management decides what happens next; engine decides nothing about sequencing. |
| Audio System | Audio depends on this | State trigger — engine fires `MasksEvents.StateChanged(eventType, charId)` via `<<notify_state_change>>`; Audio subscribes and acts. Engine sends no payload; Audio queries CharacterStateManager directly. |
| UI System | UI depends on this | Data dependency — engine passes `LocalizedLine` (text + speaker + metadata), `DialogueOptionCollection` (choices + stagger offsets), and lifecycle events (`DialogueStarted`, `DialogueComplete`) to the UI via `DialogueViewBase`. UI owns display; engine owns delivery. |

**Bidirectionality note**: CharacterStateManager is listed as bidirectional. CharacterStateManager's GDD must also list the Dialogue/Narrative Engine (via MasksVariableStorage) as a dependent system that reads and writes its state.

## Tuning Knobs

| Parameter | Current Value | Safe Range | Effect of Increase | Effect of Decrease |
|---|---|---|---|---|
| `BASE_REVEAL_RATE` | 45 cps | 30–75 cps | Text reveals faster — risk of feeling rushed for slow readers | Text reveals slower at default speed; sessions feel longer |
| `PAUSE_MAX_MS` | 5000ms | 2000–8000ms | Allows longer authored silences; values > 5s risk players thinking the game froze | Caps dramatic holds shorter; some authored moments lose impact |
| `STAGGER_STEP_MS` | 60ms | 30–120ms | Choices appear more theatrically spaced; full choice set takes longer to display | Choices appear nearly simultaneously; presentation less curated |
| `MAX_CHOICES_PER_SET` | 4 | 3–4 | More simultaneous options; UI layout becomes more constrained | Limits narrative branching per beat; affects how writers structure decisions |
| Default `<<pause>>` duration | 750ms | 400–1500ms | Longer default hesitation; if writers overuse `<<pause>>` without args, pacing slows | Shorter default hesitation; may feel abrupt in scenes relying on the default |

**Note on `MAX_CHOICES_PER_SET`**: This value is constrained by the UI layout spec (art bible Section 7.5: maximum 4 choices visible without scrolling). Increasing beyond 4 requires coordinated UI layout changes — it is not a free tuning knob. Flag as requiring UI sign-off if a content designer requests > 4.

## Visual/Audio Requirements

The Dialogue/Narrative Engine is a behavioral Foundation system. It has no direct visual or audio output — it passes structured data to downstream systems that own presentation.

- **Visual output**: The engine passes `LocalizedLine` objects to the UI system, which owns all dialogue display, text reveal animation, speaker name rendering, and portrait presentation. Visual requirements for these elements belong in the UI System GDD.
- **Audio output**: The engine fires `MasksEvents.StateChanged` events; the Audio System owns all sound and music responses. Audio requirements (reactive music layers, depth-change stings) belong in the Audio System GDD.
- **The `<<pause>>` command**: The engine's pause creates authored silence. It does not trigger audio. Any audio behavior during a pause (e.g., ambient sound continuing, a music layer sustaining) is the Audio System's concern.

No asset specs, event tables, or feedback requirements are owned by this GDD.

## UI Requirements

The engine delivers content to the UI via YarnSpinner's `DialogueViewBase` protocol. The engine's UI contract — what it sends and when — is specified here. The UI System GDD specifies how the UI presents what it receives.

| Engine Output | Data Delivered | When | UI Responsibility |
|---|---|---|---|
| `DialogueStarted()` | No data | On transition to `Running` from `Loading` | Show dialogue panel; hide non-dialogue UI elements |
| `RunLine(LocalizedLine, onLineFinished)` | Text string (post-substitution), speaker name, metadata tags, voiceover asset ref | Each dialogue line | Display line with text reveal animation; call `onLineFinished` when player advances |
| `RunOptions(DialogueOptionCollection, onOptionSelected)` | Array of choice texts with stagger offsets (in ms), option index | When a choice set is reached | Display choice panel, apply stagger animation, call `onOptionSelected` on player input |
| `DialogueComplete()` | No data | On transition to `Complete` | Begin dialogue panel hide animation; do not immediately snap to hidden (Scene Management may display subsequent content in the same panel) |

**Speaker identification**: the `LocalizedLine.CharacterName` field contains the character ID string as authored in the `.yarn` script (`CharacterName: dialogue text`). The UI uses this to look up the character's display name, name pill color (public palette accent), and portrait. The engine does not perform these lookups.

**Metadata tags**: YarnSpinner supports `#tag` syntax in `.yarn` files (e.g., `"She hesitates." #expression:guard`). These appear as metadata on `LocalizedLine`. The engine passes all tags through. The UI reads them to trigger sprite changes or expression swaps. The set of valid metadata tags is defined in the writer's authoring guide, not in this GDD.

## Acceptance Criteria

> **Test Classification Legend**
> - Logic/BLOCKING — automated unit test, must pass before story is Done
> - Integration/BLOCKING — automated or documented playtest, must pass before Done
> - Visual/ADVISORY — screenshot + lead sign-off
> - Manual/ADVISORY — manual walkthrough documented in `production/qa/evidence/`

### AC-C1: Variable Binding

| ID | GIVEN | WHEN | THEN | Type |
|----|-------|------|------|------|
| AC-001 | A `.yarn` script reads `$char_ren_depth_tier` | The scene runs | `MasksVariableStorage.TryGetValue` returns the value from `CharacterStateManager` without a push step | Logic/BLOCKING |
| AC-002 | A `.yarn` script executes `<<set $char_ren_closed_off = true>>` | The line executes | `CharacterStateManager.SetClosedOff("ren", true)` is called immediately — before the next line runs | Logic/BLOCKING |
| AC-003 | A character does not exist in `CharacterStateManager` | A Yarn script reads `$char_unknown_depth_tier` | `MasksVariableStorage` returns the declared default (0) and logs a warning — it does not throw | Logic/BLOCKING |
| AC-004 | `$pc_name`, `$pc_pronoun_subject`, `$pc_pronoun_object`, `$pc_pronoun_possessive`, `$pc_pronoun_reflexive` are read | Any `.yarn` script runs | All five return the values from `PlayerProfile` — never null, never empty string | Logic/BLOCKING |
| AC-005 | An undeclared variable is read (not matching `$char_{ID}_{var}` or `$pc_*` patterns) | `TryGetValue` is called | Returns `false` and `result = default` — does not throw | Logic/BLOCKING |

### AC-C2: Scene Lifecycle

| ID | GIVEN | WHEN | THEN | Type |
|----|-------|------|------|------|
| AC-006 | Engine is in Idle state | `StartScene(nodeName)` is called | State transitions Idle → Loading → Running within one frame | Logic/BLOCKING |
| AC-007 | Engine is in Running state | `StartScene(nodeName)` is called again | Call is rejected with a logged error; state does not change | Logic/BLOCKING |
| AC-008 | A `.yarn` node runs to its final line | No choice set is presented | State transitions to Complete; `MasksEvents.SceneComplete(nodeName)` fires | Integration/BLOCKING |
| AC-009 | Engine is in Complete state | `EndScene()` is called by Scene Management | State transitions Complete → Idle; all per-scene state is cleared | Logic/BLOCKING |
| AC-010 | A node name not present in any loaded `.yarn` file is passed | `StartScene(nodeName)` is called | State transitions to Idle; `SceneComplete` does NOT fire; error is logged with the invalid node name | Logic/BLOCKING |

### AC-C3: Custom Commands

| ID | GIVEN | WHEN | THEN | Type |
|----|-------|------|------|------|
| AC-011 | `<<notify_state_change depth_tier_changed ren>>` is in a `.yarn` script | The line executes | `MasksEvents.StateChanged("depth_tier_changed", "ren")` fires; no payload is attached | Logic/BLOCKING |
| AC-012 | `<<pause 1000>>` is in a `.yarn` script | The line executes | Dialogue output halts for 1000ms (±50ms tolerance) before the next line | Integration/BLOCKING |
| AC-013 | `<<pause 9999>>` is in a `.yarn` script | The line executes | Duration is clamped to 5000ms — the pause does not exceed PAUSE_MAX_MS | Logic/BLOCKING |
| AC-014 | `<<pause>>` is called with no argument | The line executes | Default 750ms pause is applied; no error is thrown | Logic/BLOCKING |
| AC-015 | `<<pause 0>>` is called | The line executes | No pause; next line runs on the following frame | Integration/BLOCKING |
| AC-016 | An unregistered command (e.g., `<<fly_away>>`) is in a `.yarn` script | The scene runs | YarnSpinner logs an error to the Unity console; the engine does not crash; scene continues if possible | Integration/BLOCKING |

### AC-C4: Choice Presentation

| ID | GIVEN | WHEN | THEN | Type |
|----|-------|------|------|------|
| AC-017 | A choice set with 2 options is presented | The player selects option 1 | Option index 0 is passed to `DialogueRunner.SetSelectedOption`; narrative continues on the selected branch | Integration/BLOCKING |
| AC-018 | A choice set with 4 options is presented | The UI renders | All 4 options are visible; stagger offsets are `0ms, 60ms, 120ms, 180ms` from the formula `i * STAGGER_STEP_MS` | Visual/ADVISORY |
| AC-019 | A choice set with 5 or more options is authored in a `.yarn` file | Scene validation runs | Build-time or runtime warning is logged: "Choice set exceeds MAX_CHOICES_PER_SET (4)"; no crash | Logic/BLOCKING |
| AC-020 | A choice set is rendered | Gamepad d-pad is used to navigate | All choices are reachable via d-pad; selected choice is highlighted; confirm button triggers selection | Manual/ADVISORY |

### AC-C5: Text Reveal

| ID | GIVEN | WHEN | THEN | Type |
|----|-------|------|------|------|
| AC-021 | `speed_multiplier = 1.0` (default) | A 45-character line is delivered | All characters are revealed in exactly 1.0 second (45 chars ÷ 45cps) | Logic/BLOCKING |
| AC-022 | `speed_multiplier = 2.0` | Same 45-character line | All characters revealed in 0.5 seconds | Logic/BLOCKING |
| AC-023 | `speed_multiplier = 0.0` | Any line is delivered | All characters are revealed on the same frame (instant reveal) | Logic/BLOCKING |
| AC-024 | Player presses the advance input during text reveal | Any line is mid-reveal | Remaining characters are revealed instantly; state moves to AwaitingInput | Integration/BLOCKING |
| AC-025 | Text reveal is active | No input for the duration | Reveal completes naturally; state moves to AwaitingInput; no auto-advance | Integration/BLOCKING |

### AC-C6: Pronoun Substitution

| ID | GIVEN | WHEN | THEN | Type |
|----|-------|------|------|------|
| AC-026 | `PlayerProfile.PronounSet = They/Them` | A line containing `{$pc_pronoun_subject}` is rendered | Rendered text shows "they" (lowercase, inline) | Logic/BLOCKING |
| AC-027 | `PlayerProfile.PronounSet = She/Her` | A line containing `{$pc_pronoun_possessive}` is rendered | Rendered text shows "her" | Logic/BLOCKING |
| AC-028 | `PlayerProfile.PronounSet = He/Him` | A line containing `{$pc_pronoun_reflexive}` is rendered | Rendered text shows "himself" | Logic/BLOCKING |
| AC-029 | A line is authored with `{$pc_pronoun_subject}` at sentence start | Any pronoun set | First character of the revealed text is capitalised by the UI layer (not in Yarn) | Manual/ADVISORY |
| AC-030 | PC name is changed via Character Creator | A Yarn line with `{$pc_name}` is rendered | The new name appears — not the default "Alex" | Integration/BLOCKING |

### AC-C7: State Machine Guards

| ID | GIVEN | WHEN | THEN | Type |
|----|-------|------|------|------|
| AC-031 | Engine is in Loading state | `StartScene` is called | Rejected; logged error; state unchanged | Logic/BLOCKING |
| AC-032 | Engine is in AwaitingInput state | `StartScene` is called | Rejected; logged error; state unchanged | Logic/BLOCKING |
| AC-033 | Engine is in Running state | `EndScene` is called | Rejected; logged error; state unchanged | Logic/BLOCKING |
| AC-034 | Engine is in Idle state | `EndScene` is called | No-op (idempotent); no error | Logic/BLOCKING |
| AC-035 | Engine is in Complete state | `StartScene` is called without a prior `EndScene` | Rejected; logged error; previous scene's state is preserved | Logic/BLOCKING |

### AC-C8: Integration and Performance

| ID | GIVEN | WHEN | THEN | Type |
|----|-------|------|------|------|
| AC-036 | A `.yarn` script for Ren covers all 4 patterns (depth conditional, pronoun substitution, Dig gate, revelation scene) | Scene runs end-to-end with a real `CharacterStateManager` and `PlayerProfile` | All state reads and writes are correct; no flush step is required; `SceneComplete` fires at end | Integration/BLOCKING |
| AC-037 | All 5 pronoun sets (she/her, he/him, they/them, it/its, any/all) are authored in the Ren reference script | Manual playtest by a writer | All substitutions read naturally in context; no grammatical artifacts | Manual/ADVISORY |
| AC-038 | `DialogueEngineController.StartScene` is called before a character ID has been injected | Pre-condition guard check | An `InvalidOperationException` is thrown with message: "No character loaded. Call LoadCharacter before StartScene." | Logic/BLOCKING |
| AC-039 | A `.yarn` file is loaded that references an undeclared variable (not matching `$char_{ID}_{var}` or `$pc_*` patterns) | Scene runs | `MasksVariableStorage.TryGetValue` returns `false`; no null ref; warning logged with variable name | Logic/BLOCKING |
| AC-040 | `DialogueEngineController` is profiled in a scene with 45cps text reveal, one `<<pause 750>>`, and one 4-choice set | Unity Profiler, minimum-spec PC | `DialogueEngineController` contributes ≤1ms to frame time during peak activity | Manual/ADVISORY |

### QA Flags for Engineering Review

1. **AC-040** requires Unity Profiler measurement on minimum-spec hardware — cannot be verified headlessly.
2. **Rule 11 variable naming convention** has no authoring-time enforcement — writers can author `$renDepthTier` and it silently falls through. Consider a Yarn linter or `MasksVariableStorage.Contains()` validation step.
3. **AC-012 and AC-015** (pause timing) must be Play Mode tests — Edit Mode headless tests cannot measure frame timing.
4. **Rule 19** (no direct `CharacterStateManager` writes from outside `MasksVariableStorage`) has no architectural guard — enforced by convention. Consider an interface boundary.
5. **F-3 stagger metadata** on `DialogueOptionCollection` — verify that YarnSpinner 3.0 exposes option metadata before implementing the stagger formula.

## Open Questions

All design questions raised during authoring were resolved. The following items were closed:

- **Ink vs. YarnSpinner** — Resolved by spike. YarnSpinner selected. See `prototypes/dialogue-engine-spike/REPORT.md`.
- **Variable naming convention** — Resolved: `$char_{CharacterID}_{variable}` + fixed `$pc_*` set.
- **`<<pause>>` scope** — Resolved: MVP-included for authored timing control.
- **StateChanged payload** — Resolved: notification-only; subscribers query `CharacterStateManager` directly.
- **PC pronoun injection** — Resolved: `MasksVariableStorage` reads from `PlayerProfile` live; Scene Management injects character ID explicitly before `StartScene`.

**Engineering items surfaced by QA (not design questions — track in implementation):**
- Yarn variable naming linter / `Contains()` validation (QA Flag 2)
- YarnSpinner 3.0 API verification for `DialogueOptionCollection` metadata (QA Flag 5)
- Architectural guard for Rule 19 (`CharacterStateManager` write isolation) (QA Flag 4)
