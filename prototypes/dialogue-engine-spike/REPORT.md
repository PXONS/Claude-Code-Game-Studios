# Prototype Report: Dialogue Engine Spike

## Hypothesis

One of Ink or YarnSpinner can represent Masks' depth state, drive The Dig choice
gates with state write-back, handle PC pronoun/name substitution, and scale to
20–30 independent character state machines — without requiring a custom narrative
engine.

## Approach

Built working script samples for one character (Rei) in both Ink and YarnSpinner,
covering all four critical requirements. Built Unity integration stubs showing
how each tool's runtime connects to a CharacterStateManager. Evaluated both tools
against a requirements matrix plus practical factors (license, Unity 6.3 compat,
writer ergonomics).

**Shortcuts taken**: No playable Unity build — stubs are structural, not runnable.
No content volume testing (20–30 characters). Evaluation is architectural.

---

## Requirements Matrix

| Requirement | Ink | YarnSpinner | Notes |
|---|---|---|---|
| **1. Per-character depth state readable in scripts** | ✅ Via `VAR depth_tier` — set before running each knot | ✅ Via `VariableStorageBehaviour` subclass — GetValue called at runtime | Both work. YarnSpinner's live-read is slightly cleaner for multi-character scenarios. |
| **2. The Dig: choice gate with closed_off write-back** | ⚠️ Works, but requires manual `FlushStateToManager()` after every scene end | ✅ `SetValue` fires immediately on `<<set>>` — no flush step, no missed writes | **Ink has a real bug surface**: writers add a new `~ some_var = x` and forget to add it to `FlushStateToManager()`. Silent failure. YarnSpinner writes are automatic. |
| **3. PC pronoun/name substitution** | ✅ `{pc_pronoun_sub}` inline — clean syntax | ✅ `{$pc_pronoun_sub}` inline — equally clean, slightly more verbose | Ink syntax is marginally friendlier for writers. |
| **4. 20–30 independent character state machines** | ⚠️ All characters share one Ink story instance per session OR require one compiled `.ink.json` per character | ✅ Per-character state is namespaced by variable naming convention (`$rei_depth_tier`) — one VariableStorage handles all characters | **Ink's multi-character scaling is genuinely awkward.** One story = one .ink.json. Either bundle all 30 characters into one massive file (maintenance nightmare) or manage 30 separate Story instances (memory + lifecycle complexity). YarnSpinner's node-graph model handles a large cast naturally. |

---

## Detailed Findings

### Ink

**How state flows**: Variables are set in Unity BEFORE starting a knot, and read
back OUT after it ends. This creates a "push-pull" contract at the C# layer.

**Critical bug surface — the flush gap**:
```csharp
// Every .ink state write MUST be manually synced back here.
// If a writer adds a new variable and the programmer doesn't update
// FlushStateToManager(), the write is silently dropped.
private void FlushStateToManager()
{
    int newDepth = (int)_story.variablesState["depth_tier"];
    bool closedOff = (bool)_story.variablesState["closed_off"];
    // If writer adds ~ new_var = x and this method isn't updated: silent data loss.
}
```

**Multi-character scaling**: Ink compiles a `.ink` script to a single `.ink.json`
binary. For 30 characters, options are:
- One `.ink.json` containing all characters' scripts → one Story instance, but
  the file grows to thousands of lines; cross-character variable namespace
  collisions become a real management problem.
- One `.ink.json` per character → 30 separate Story instances; each costs memory
  and has its own lifecycle. Manageable but adds engine-side complexity.
- Knot-based namespacing (e.g., `=== rei_dig_gate ===`) within one file is
  viable but provides no variable isolation between characters.

**Writer ergonomics**: Ink's syntax is genuinely clean. The `~` for variable
assignment and `->` for flow are minimal-friction for non-programmers. This is
Ink's strongest differentiator.

**Unity 6.3 compatibility**: The ink-unity-integration package (official) targets
Unity 2020+. Unity 6.3 LTS should be compatible, but requires verification —
the package's .asmdef references may need updates. No breaking changes identified
in Unity 6 API surface for Ink's usage patterns.

**License**: MIT. Free, no royalties, actively maintained by Inkle.

---

### YarnSpinner

**How state flows**: `VariableStorageBehaviour` subclass provides live read/write.
When Yarn evaluates `$rei_depth_tier`, it calls `TryGetValue` immediately —
no push step required. When `<<set>>` executes, `SetValue` fires immediately —
no flush step required.

**No flush gap**: State writes are real-time. A writer adds `<<set $rei_closed_off = true>>`
and it fires on execution, not at scene end. The C# integration layer is thinner.

**Multi-character scaling**: The variable naming convention (`$rei_depth_tier`,
`$mav_depth_tier`) provides per-character namespacing within one VariableStorage.
All 30 characters' state lives in one backing store — the CharacterStateManager —
accessed through one VariableStorageBehaviour. No per-character Story instances.

**Custom command system**: `<<notify_state_change>>` demonstrates YarnSpinner's
`AddCommandHandler` — clean, strongly typed, easy for programmers to extend without
touching script files.

**Writer ergonomics**: YarnSpinner's node-based structure (one node = one scene or
beat) maps more naturally to a VN scene graph than Ink's flow-based knot system.
The `<<if>>` / `<<else>>` / `<<endif>>` tags are more verbose than Ink's `{ }` conditionals
but are more readable for writers unfamiliar with programming syntax.
The Yarn Spinner Visual Editor (in-engine graph view) is a significant ergonomic
advantage for non-programmer writers on a VN project.

**Unity 6.3 compatibility**: YarnSpinner 3.0+ supports Unity 2021+; Unity 6.3 LTS
is supported. The package is actively maintained by Secret Lab. The visual editor
integrates into the Unity Editor window natively.

**License**: MIT. Free, no royalties.

---

## Recommendation: PROCEED — YarnSpinner

The spike result is clear. Both tools can technically satisfy requirements 1 and 3.
YarnSpinner decisively wins on requirements 2 and 4 — the two that scale with the
game's core architecture.

**The Ink flush gap is a production liability**: On a project with 20–30 characters
and a writing team (or even a solo writer adding scenes months apart), the manual
`FlushStateToManager()` contract will be broken. It is not a hard problem to solve,
but it requires discipline and tooling that adds overhead. YarnSpinner eliminates
this class of bug by design.

**Ink's multi-character scaling is friction, not fatal**: It's solvable, but requires
either a large monolithic script file or 30 separate Story instances. YarnSpinner's
variable namespacing and single VariableStorage handle the same problem more
naturally.

**The Yarn Visual Editor is a real production advantage**: On a VN, writers need to
author content in the narrative tool. The node graph editor reduces the
programming-literacy requirement for content authoring. This matters for the
project's production velocity.

**Ink's syntax ergonomics are better** — but this is a secondary concern outweighed
by the architectural wins.

---

## If Proceeding: Production Requirements

1. **Implement `MasksVariableStorage`** as the central integration layer between
   YarnSpinner and `CharacterStateManager`. This is the most critical production
   class — all state reads/writes pass through it.

2. **Establish variable naming convention** for all character state vars before
   any script is written: `$[characterId]_[fieldName]`. Document in the Dialogue
   Engine GDD. Enforce by convention, not tooling (at MVP scale).

3. **Register all custom commands** (`<<notify_state_change>>`, and any future
   scene-transition or audio-trigger commands) in a bootstrapper. Document the
   command library in the GDD.

4. **Decide on Yarn node granularity**: one node per scene beat? One node per
   scene? Recommendation: one node per logical scene beat (consistent with how
   writers think in VN terms). Define in the GDD before content authoring begins.

5. **YarnSpinner version**: pin to YarnSpinner 3.0+ (Unity Package Manager).
   Add to `Allowed Libraries` in `.claude/docs/technical-preferences.md` once
   the GDD is approved.

6. **Writer onboarding**: Build a one-page Yarn script template showing the
   four patterns (depth conditional, pronoun substitution, Dig gate, revelation
   scene). This is the content team's reference, not the GDD.

---

## Lessons Learned

- **Ink is excellent for contained, author-controlled narratives** (the use case
  it was designed for: parser games, single-protagonist stories). The flush-gap
  problem only becomes significant when external state management systems need
  bidirectional live sync — which Masks requires.

- **YarnSpinner's architecture was designed with game-state integration in mind**
  — the VariableStorageBehaviour abstraction exists specifically for this use case.
  Ink treats external state as an import/export problem; YarnSpinner treats it as
  a live binding problem. For Masks, the live binding model is correct.

- **Multi-character VNs at scale should evaluate YarnSpinner first**: The node graph
  model, variable namespacing, and custom command system are better fits than
  Ink's flow model for games with large casts and persistent per-character state.

- **The Dialogue/Narrative Engine decision was the right first architectural choice**:
  8 other systems depend on it. Resolving it as a spike before GDD authoring begins
  saves design rework across the entire dependency chain.
