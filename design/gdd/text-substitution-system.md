# Text Substitution System

> **Status**: Complete
> **Author**: Design session + agents
> **Last Updated**: 2026-05-03
> **Implements Pillar**: Pillar 5 (No Wrong Type)

## Overview

The Text Substitution System is the layer that makes every scene feel written for this specific player. It governs how the player's chosen name and pronouns — stored in the Player Profile — are resolved into live dialogue at runtime. It defines the authoring rules that writers follow to embed substitution variables in `.yarn` scripts, the capitalization conventions that make substituted text grammatically correct in context, and the fallback behavior when a value is missing or malformed.

The mechanical spine is already provided by YarnSpinner 3.0's line compiler, which resolves `{$variable}` tokens in dialogue strings before they are passed to the UI. This system does not build a new substitution engine — it defines the contract above that engine: which variable names writers use, when to use them, how to handle grammatical edge cases (sentence-start capitalization, possessive constructions), and what happens when the Character Creator has not yet been completed.

From the player's perspective, the system is invisible when it works and devastating when it doesn't. A character speaking the player's chosen name in the right cadence, using the right pronoun without pause or awkwardness, is the system performing its deepest function: confirming that the game was written for them. This is the mechanical expression of Pillar 5 (No Wrong Type) — not a feature, but a guarantee.

## Player Fantasy

Every line lands as though the writer knew who would be reading it. The character leans in close and uses the player's name — not a placeholder, not a guess, not a stumble over a pronoun the game forgot — and the moment holds. The fantasy is the absence of the flinch: the small, full-body relief of a romance scene that never once breaks the spell to remind the player they are an outsider being approximated.

When it works, the player doesn't notice. They're thinking about the character, not the word that appeared in place of a variable. That invisibility is the system's entire purpose — and the proof it worked is in the screenshots shared at 2 AM, where nothing in the language pushes back to suggest the game was written for someone else first.

**Design test this must pass**: A player using a non-binary identity, an unusual name, or a name with special characters must describe the scene in emotional vocabulary — "that moment wrecked me," "I can't stop thinking about it" — not in technical vocabulary — "the pronoun was correct," "my name displayed right." If the system surfaces itself in the player's attention, it has failed its primary job.

## Detailed Design

### Core Rules

**Rule 1 — Substitution engine is YarnSpinner.** This system does not implement a custom substitution engine. YarnSpinner 3.0's line compiler resolves `{$variable}` tokens in dialogue strings before the rendered line is passed to the UI. The Text Substitution System defines the variable contract above that engine.

**Rule 2 — Variable set.** MasksVariableStorage binds exactly 9 substitution variables at session start. All are read-only from `.yarn` scripts — writers may not assign to them:

| Yarn Variable | Source Field | Example (she/her) | Example (they/them) | Example (xe/xem) |
|---|---|---|---|---|
| `$pc_name` | `display_name` | "Ren" | "Ren" | "Ren" |
| `$pc_pronoun_subject` | `pronoun_subject` | "she" | "they" | "xe" |
| `$pc_pronoun_subject_cap` | `pronoun_subject` (first-char uppercased) | "She" | "They" | "Xe" |
| `$pc_pronoun_object` | `pronoun_object` | "her" | "them" | "xem" |
| `$pc_pronoun_object_cap` | `pronoun_object` (first-char uppercased) | "Her" | "Them" | "Xem" |
| `$pc_pronoun_possessive` | `pronoun_possessive` | "her" | "their" | "xir" |
| `$pc_pronoun_possessive_cap` | `pronoun_possessive` (first-char uppercased) | "Her" | "Their" | "Xir" |
| `$pc_pronoun_reflexive` | `pronoun_reflexive` | "herself" | "themselves" | "xemself" |
| `$pc_pronoun_reflexive_cap` | `pronoun_reflexive` (first-char uppercased) | "Herself" | "Themselves" | "Xemself" |

**Rule 3 — Capitalization law.** The `_cap` variants are computed at bind time by first-character uppercasing only — NOT `ToTitleCase()`. This correctly handles multi-word and non-Latin custom pronouns. `$pc_name` has no `_cap` variant; the player's own name casing is preserved exactly as entered.

**Rule 4 — Sentence-start rule (authoring law).** If a pronoun variable is the first word of a sentence, writers MUST use the `_cap` variant. Writers MUST use the standard (lowercase) variant when the pronoun falls mid-sentence. Using `_cap` mid-sentence produces incorrect capitalization for custom pronouns.

**Rule 5 — Binding happens once per scene load.** Variables are injected by Scene Management before each `StartScene()` call, via `MasksVariableStorage.SetValue()`. If the player edits their profile mid-session (via settings), the new values take effect on the next scene load — the current scene completes with the old values.

**Rule 6 — Writers must use variables for every PC pronoun reference.** No static pronoun may be written into a `.yarn` script where it refers to the player character, regardless of how small the usage. A single hardcoded pronoun that reaches a player who chose a different paradigm breaks Pillar 5.

**Rule 7 — Possessive construction rule.** `{$pc_pronoun_possessive}` is the attributive possessive form (before a noun: "their bag", "her voice"). It must NOT be used as a standalone predicate after a linking verb ("the bag is their" is grammatically incorrect for some paradigms). When the predicative possessive is required, writers restructure: prefer `"the bag belongs to {$pc_pronoun_object}"` or `"the bag is {$pc_name}'s"`.

**Rule 8 — Verb conjugation is the writer's responsibility.** The system cannot automatically select "was" vs. "were" based on pronoun paradigm. Writers must structure sentences using verb forms that are grammatically valid for singular "they": prefer past-tense regular verbs ("arrived", "noticed", "said") over "to be" conjugations when the subject is `$pc_pronoun_subject`. When "to be" is required, the lead writer reviews and resolves per-scene.

**Rule 9 — Fallback behavior.** If `$pc_name` is empty string at bind time, MasksVariableStorage substitutes `"Traveler"` and logs a `Debug.LogWarning`. If any pronoun field is empty string at bind time, all four pronoun variables (and their `_cap` variants) are substituted with the They/Them preset values and logged as a warning. Partial fallback is not permitted — if any pronoun field is empty, treat the entire set as corrupted and substitute the full They/Them preset.

---

### States and Transitions

This system is stateless at runtime. There is no state machine to model.

Variables are bound once per scene load and read many times by YarnSpinner's line compiler. The system does not intercept or modify in-flight substitution — it defines the authoring contract, not the execution pipeline.

**Binding lifecycle** (informational, for dependency clarity):

| Event | Who owns it | What happens |
|---|---|---|
| Session start / each scene load | Scene Management calls MasksVariableStorage | All 9 variables are read from Player Profile and bound to YarnSpinner runner |
| Player edits profile (mid-session) | Player Profile updates stored fields | No re-bind occurs mid-scene. MasksVariableStorage is not notified during a running scene. |
| Next scene load after profile edit | Scene Management calls MasksVariableStorage before StartScene() | All 9 variables are re-read from Player Profile and re-bound with new values |
| Line rendered | YarnSpinner line compiler | `{$variable}` tokens resolved inline; result passed to UI |

---

### Interactions with Other Systems

**Player Profile (#12) — source of truth:**
- Reads: `display_name`, `pronoun_subject`, `pronoun_object`, `pronoun_possessive`, `pronoun_reflexive`
- Does not write to Player Profile — read-only consumer
- Profile changes take effect on next scene load, not immediately

**Dialogue/Narrative Engine (#8) — execution host:**
- MasksVariableStorage is the `VariableStorageBehaviour` subclass that bridges Player Profile values into YarnSpinner's variable namespace
- YarnSpinner's line compiler resolves `{$variable}` tokens using the values MasksVariableStorage provides
- This system owns the 9-variable naming convention; the Dialogue Engine owns the binding mechanism

**Scene Management (#9) — bind trigger:**
- Scene Management is responsible for calling `MasksVariableStorage.SetValue()` for all 9 variables before each `StartScene()` call
- This system defines which variables must be set and what their values should be; Scene Management owns the timing of the call

**Character Creator / Player Profile UI (#4, #14) — upstream writer:**
- The Character Creator writes to Player Profile; the Text Substitution System reads the result
- No direct interaction — the dependency is mediated by Player Profile

## Formulas

### F-1: _cap Variable Computation

The `_cap` variant of each pronoun variable is computed by MasksVariableStorage at bind time using the following transformation:

```
Capitalize(input: string) → string

if input is null or empty:
    return input  // fallback already applied by Rule 9 before this is called

return char.ToUpper(input[0]) + input.Substring(1)
```

**Variables:**
| Variable | Type | Input | Output |
|---|---|---|---|
| `input` | string | Any pronoun string from Player Profile | Same string with first Unicode character uppercased |

**Notes:**
- This is NOT `CultureInfo.TextInfo.ToTitleCase()`. Only the first character is uppercased — multi-word custom pronouns are not fully title-cased.
- The transformation is culture-invariant (`char.ToUpper(input[0])`). For non-Latin scripts where uppercasing is not meaningful, the operation is a no-op.
- `$pc_name` is exempt. Names are used as stored, verbatim.

**Example computations:**
| Player input | `_cap` result | Note |
|---|---|---|
| "she" | "She" | Standard preset |
| "they" | "They" | Standard preset |
| "xe" | "Xe" | Custom pronoun |
| "fae" | "Fae" | Custom pronoun |
| "E" | "E" | Already capitalized — no-op |
| "" | "" | Empty — Rule 9 fallback applies before this step |

This system has no other formulas. All substitution logic belongs to YarnSpinner's line compiler.

## Edge Cases

**EC-1: Player edits profile mid-scene**
Profile edit is accessible from settings at any time. If a player edits their name or pronouns while a scene is running, the scene in progress continues with the original values — the variables were bound at scene load and YarnSpinner holds them in its variable store for the duration. The updated values are re-bound by Scene Management before the next `StartScene()` call. No mid-scene rebind occurs, no restart is triggered.

**EC-2: Custom pronoun entered entirely in uppercase**
A player enters "ZE" as their subject pronoun. `$pc_pronoun_subject` stores "ZE" and `$pc_pronoun_subject_cap` stores "ZE" (no-op — first char "Z" is already uppercase). Both variables render identically. This is correct — the player's own casing choice is honored.

**EC-3: Custom pronoun with punctuation or special characters**
A player enters "xe/xem" (with a slash) as a custom subject pronoun. `$pc_pronoun_subject` renders "xe/xem" inline. The Player Profile GDD enforces single-form entry per field via the Character Creator. If a player somehow enters a slash-combined form, it renders verbatim and the `_cap` transform uppercases the first character: "Xe/xem". The system does not validate custom pronoun form.

**EC-4: `$pc_name` contains characters that break Yarn string syntax**
If the player's name contains curly braces, dollar signs, or other YarnSpinner-special characters (e.g., "{Alex}"), YarnSpinner's string interpolation may misparse this as a variable reference. MasksVariableStorage must escape or sanitize `display_name` before binding it as a Yarn string value. This sanitization is this system's responsibility. Escaped rendering (e.g., showing "{{Alex}}") is acceptable; a crash or silent substitution failure is not.

**EC-5: Very long display name**
Player Profile allows up to 30 Unicode characters. Long names in narrow UI contexts may overflow dialogue boxes. This is a UI System concern, not a substitution concern — the system passes the name as-stored. No truncation occurs at the substitution layer.

**EC-6: Scene runs before any profile has been set (first launch, pre-Creator)**
The Character Creator is required before any scene runs (Player Profile GDD rule). If a scene somehow starts before the Creator is completed, all 9 variables will be empty string. Rule 9 fallback applies: name renders as "Traveler", all pronouns render as They/Them preset. All fallbacks are logged as warnings. The scene plays through rather than crashing.

**EC-7: Reflexive `_cap` used at sentence start — rare but valid**
"Themselves, she reflected" or similar archaic constructions are syntactically valid in literary prose. `$pc_pronoun_reflexive_cap` handles this correctly. Writers should flag such constructions in review; sentence-start reflexives are unusual in romance VN prose and may warrant restructuring regardless of the variable choice.

**EC-8: Writer accidentally assigns to a substitution variable in Yarn**
YarnSpinner allows `<<set $pc_name = "Override">>`. If a writer does this, the assignment overwrites the bound value in MasksVariableStorage for the remainder of that scene node, and because MasksVariableStorage persists every write to Player Profile (Dialogue Engine Rule 14), this constitutes a data corruption bug — the player's profile name or pronoun is overwritten. Content review must catch any Yarn assignment to the 9 reserved substitution variables. A Yarn linter check for assignments to `$pc_name` or `$pc_pronoun_*` is strongly recommended.

## Dependencies

### Upstream (systems this one depends on)

**Player Profile / PC Data (#12)** — `design/gdd/player-profile.md`
Source of truth for all 5 profile fields this system reads: `display_name`, `pronoun_subject`, `pronoun_object`, `pronoun_possessive`, `pronoun_reflexive`. Any change to these field names or their storage contract must be reviewed for impact here.

**Dialogue/Narrative Engine (#8)** — `design/gdd/dialogue-narrative-engine.md`
YarnSpinner's line compiler performs `{$variable}` substitution. `MasksVariableStorage` is the bridge between Player Profile values and YarnSpinner's variable namespace. This system depends on the 9-variable naming convention being respected by the engine's `VariableStorageBehaviour` implementation.

### Downstream (systems that depend on this one)

**Character Creator / Player Profile UI (#4, #14)** — undesigned
Depends on this system defining the variable set and naming convention — the Creator must store field values in the exact format this system expects (e.g., lowercase pronoun strings for the standard-form variables).

**Scene Management / Flow Controller (#9)** — undesigned
Depends on this system defining the bind trigger contract: Scene Management must call `MasksVariableStorage.SetValue()` for all 9 variables before each `StartScene()` call. The variable list and their source fields are this system's specification.

**All `.yarn` content** — ongoing authoring dependency
Every writer authoring scenes depends on this system's variable naming convention, sentence-start rule, and possessive rule. The authoring documentation in Section C (Core Rules) is the writer-facing contract.

## Tuning Knobs

This system has one configurable value:

| Knob | Default | Safe Range | What It Affects |
|---|---|---|---|
| `FALLBACK_DISPLAY_NAME` | `"Traveler"` | Any non-empty string ≤ 30 chars | The name substituted when `display_name` is empty. Should be a narratively neutral word fitting the game's world tone. Do not use a pronoun or a placeholder like "[NAME]". |

There are no other tuning knobs. The 9 variable names are fixed by authoring convention and cannot be changed without updating all existing `.yarn` scripts. The `_cap` transformation is not tunable.

## Visual/Audio Requirements

None. This system has no visual or audio output. Its output is text rendered by the Dialogue Engine via the UI System.

## UI Requirements

None. This system has no direct UI. Text rendered from substituted variables is displayed by the Dialogue Engine's UI layer. The UI System GDD must account for maximum-length display names (up to 30 Unicode characters) in all dialogue box layouts.

## Acceptance Criteria

### Category 1: Variable Binding

**TSS-001** — Given a player profile with `display_name="Ash"`, `pronoun_subject="they"`, when a scene loads, then `$pc_name` resolves to "Ash" and `$pc_pronoun_subject` resolves to "they" in Yarn dialogue.

**TSS-002** — Given a player profile with `pronoun_subject="they"`, when a scene loads, then `$pc_pronoun_subject_cap` resolves to "They" (first character uppercased).

**TSS-003** — Given a player profile with `pronoun_subject="xe"`, when a scene loads, then `$pc_pronoun_subject_cap` resolves to "Xe".

**TSS-004** — Given a player profile with `pronoun_subject="ZE"` (already uppercase), when a scene loads, then `$pc_pronoun_subject_cap` resolves to "ZE" (no double-uppercasing).

**TSS-005** — Given a player profile, when a scene loads, then all 9 variables are bound before `StartScene()` is called.

---

### Category 2: Sentence-Start Capitalization

**TSS-010** — Given a `.yarn` line starting with `{$pc_pronoun_subject_cap}` and `pronoun_subject="they"`, when the line is rendered, then the output begins with "They".

**TSS-011** — Given a `.yarn` line containing `{$pc_pronoun_subject}` mid-sentence and `pronoun_subject="they"`, when the line is rendered, then the pronoun renders as "they" (lowercase).

**TSS-012** — Given a `.yarn` line starting with `{$pc_pronoun_possessive_cap}` and `pronoun_possessive="their"`, when the line is rendered, then the output begins with "Their".

---

### Category 3: Fallback Behavior

**TSS-020** — Given `display_name` is empty string at bind time, when a scene loads, then `$pc_name` resolves to "Traveler" and a `Debug.LogWarning` is emitted.

**TSS-021** — Given `pronoun_subject` is empty string at bind time, when a scene loads, then all 8 pronoun variables are substituted with They/Them preset values and a `Debug.LogWarning` is emitted.

**TSS-022** — Given `pronoun_object` is empty string but `pronoun_subject` is not, when a scene loads, then all 8 pronoun variables are substituted with They/Them preset (partial fallback is not permitted).

---

### Category 4: Profile Edit Behavior

**TSS-030** — Given a scene is running with `$pc_name="Ash"`, when the player edits their name to "River" mid-scene, then `$pc_name` continues to resolve to "Ash" for the remainder of the current scene.

**TSS-031** — Given a player edited their name to "River" mid-previous-scene, when the next scene loads, then `$pc_name` resolves to "River".

---

### Category 5: Yarn Syntax Safety

**TSS-040** — Given a `display_name` containing `{` or `$` characters, when the name is bound as a Yarn variable, then the scene loads without error and the name renders safely without misparse.

**TSS-041** — Given a `.yarn` script contains `<<set $pc_name = "Override">>`, when content review tooling runs, then this pattern is flagged as a violation. (Verified by linter or manual review.)

---

### Category 6: Custom Pronouns

**TSS-050** — Given `pronoun_subject="fae"`, when a scene with `{$pc_pronoun_subject_cap}` at sentence start renders, then the output begins with "Fae".

**TSS-051** — Given `pronoun_object="faer"` and a `.yarn` line containing `{$pc_pronoun_object}`, when the line renders, then it contains "faer" verbatim.

## Open Questions

1. **Yarn syntax sanitization scope**: EC-4 requires MasksVariableStorage to sanitize `display_name` before Yarn binding. The exact set of characters that must be escaped needs to be confirmed against YarnSpinner 3.0's string parser. Flag for implementation: test the full Unicode character set against YarnSpinner's binding API before shipping.

2. **Linter tooling**: EC-8 and TSS-041 call for a Yarn linter that catches assignments to the 9 reserved variables. No linter currently exists for this project. This should be raised as a tooling task when the content pipeline is established.

3. **Post-MVP: `pronoun_possessive_absolute`**: The predicative possessive gap ("the jacket is theirs") was deferred to post-MVP (Rule 7, Possessive Construction Rule). If playtest writing frequently encounters this construction, add a fifth pronoun field and a `$pc_pronoun_possessive_absolute` variable in a follow-up GDD revision.
