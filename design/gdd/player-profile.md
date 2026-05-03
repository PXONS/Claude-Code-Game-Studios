# Player Profile / PC Data

> **Status**: Complete
> **Author**: Design session + agents
> **Last Updated**: 2026-05-03
> **Implements Pillar**: Pillar 5 (No Wrong Type) + Pillar 3 (Desire Drives Discovery)

## Overview

The Player Profile system is the persistent identity record for the player character (PC). It stores the data the player sets during character creation — display name, subject/object/possessive/reflexive pronouns, and appearance selections — and makes that data available to all systems that need it. It is the single source of truth for who the player is inside the game world.

The system's scope is intentionally narrow. It is not a stats system, a skill tree, or a progression tracker. It holds three things: what the PC is called, how the PC is referred to grammatically, and how the PC looks. Everything downstream that needs player identity data — the Dialogue/Narrative Engine's text substitution, the Save/Load System's serialized profile block, and future UI systems displaying the PC — reads from this schema. Nothing writes to it except the Character Creator.

Player Profile has no state machine and no runtime behavior. It is populated once during first setup (or when the player re-edits their profile) and then read-only for the duration of all other systems' operation.

## Player Fantasy

The character creator is the first thing the game asks of the player, and the question it's really asking is: *what's your mask?*

Before you meet anyone in this story, before a single character turns their public face toward you, the game asks you to build your own. A name. Pronouns. An appearance. The specifics are small — this isn't a deep character forge, it's a face. But the act is deliberate. The game that is entirely about people wearing masks begins by handing you one.

That's the fantasy: **you are one of them.** Not a neutral observer clicking through a roster. A person who chose a name the world doesn't have to know the meaning of, who decided how to be addressed, who assembled a self for this story and committed to it. The player character is not a blank vessel — they're a specific person with a specific presentation, and that presentation is *yours*.

The second beat is anticipation. The creator is a threshold — the last quiet moment before the door opens. You're getting ready. Deciding how to show up. Somewhere on the other side of this screen are twenty-something people who don't know you're coming, who have built elaborate faces of their own, and who are going to meet the version of you that you just chose. The flutter of that — the charged readiness — is what the creator is supposed to feel like. Not configuration. Preparation.

Pillar 5 (No Wrong Type) is the design law that makes this fantasy work. Any name is right. Any pronoun set is right. Any appearance is right. The game does not second-guess the player's choices or route them differently based on them. The mask the player builds is treated with the same seriousness that every other character's mask is treated with — as real, as valid, as the thing they're going into the story with.

When the creator ends and the game begins, the player should feel: *I know who I am in here. Now let's see who they're pretending to be.*

## Detailed Design

### Core Rules

**The Schema**

1. The Player Profile holds exactly four categories of data:
   - **Display name** — a free-text string the player gives their character. Used in dialogue via `$pc_name`.
   - **Pronouns** — four fields: subject, object, possessive, reflexive (e.g., "she / her / her / herself"). Each field is a free-text string. Players may select from named presets (see Rule 5) or enter all four manually.
   - **Appearance** — a set of named appearance selections (e.g., hair color, skin tone, hair style). Each selection is a string key referencing an approved appearance option. No rendered avatar at MVP.
   - **Profile version** — an integer incremented each time the profile is updated. Used by Save/Load to detect profile edits between sessions.

2. The Player Profile is writable only by the Character Creator. No other system may modify it. All other systems are read-only consumers.

3. The Player Profile is available application-wide once loaded. All reads are synchronous — no async access pattern required.

4. The Player Profile is loaded as part of the application startup sequence, immediately after Save/Load restores the save file. If no save file exists, the Profile is initialized to default values and the Character Creator is presented to the player on first launch.

**Pronoun Rules**

5. The pronoun entry UI offers two paths:
   - **Preset selection**: a named set that populates all four fields at once. The following presets are provided at MVP:
     - She / Her — subject=she, object=her, possessive=her, reflexive=herself
     - He / Him — subject=he, object=him, possessive=his, reflexive=himself
     - They / Them — subject=they, object=them, possessive=their, reflexive=themselves
   - **Custom entry**: player types all four fields individually. No validation — any string is accepted.
   - Selecting "Custom" after a preset clears the preset selection and enables the four text fields. Selecting a preset after custom entry overwrites all four fields with the preset values.

6. All four pronoun fields must be non-empty before the Character Creator can be confirmed. If any field is empty, the confirm button is disabled with a hint indicating which fields are missing. Default to the "They/Them" preset on first load.

7. Pronoun field names in dialogue (`$pc_pronoun_subject`, `$pc_pronoun_object`, `$pc_pronoun_possessive`, `$pc_pronoun_reflexive`) map to these fields exactly. The MasksVariableStorage class reads from Player Profile to bind these Yarn variables.

**Display Name Rules**

8. The display name is a free-text string. Constraints:
   - Minimum length: 1 character
   - Maximum length: 30 characters
   - Allowed characters: Unicode — no ASCII-only restriction
   - The confirm button is disabled when the name field is empty

9. The display name has no uniqueness requirement — players may name themselves anything, including a love interest's name.

**Re-edit Rules**

10. The player may access the Character Creator from the main menu / settings at any time to update their name, pronouns, or appearance. Changes take effect immediately on confirm — the Profile is rewritten and Save/Load persists the updated profile on the next save trigger.

11. Changing the display name or pronouns mid-playthrough does not retroactively alter any previously played scene text. It affects all dialogue rendered after the change.

**Appearance Rules**

12. At MVP, appearance is a set of named string selections. Each selection has a category key (e.g., `hair_color`, `skin_tone`, `hair_style`) and a selected value (e.g., `"dark_brown"`, `"medium"`, `"shoulder_length"`).

13. Appearance selections have no gameplay effect at MVP — they are stored in the profile and made available to future systems but no system currently reads them. The Character Creator GDD owns the full list of appearance categories and valid values.

14. Changing appearance selections has no effect on relationship depth, available scenes, or any game-mechanical outcome. Pillar 5: No Wrong Type applies unconditionally.

---

### States and Transitions

The Player Profile has two states:

| State | Description | Entry Condition |
|---|---|---|
| Uninitialized | No profile exists | First launch with no save file |
| Active | Profile data is populated and available | Character Creator confirmed; or save file loaded |

The only transition from Uninitialized → Active is a successful Character Creator confirmation. Once Active, the profile remains Active for the lifetime of the session. Re-editing updates the Active profile in place — no return to Uninitialized.

---

### Interactions with Other Systems

| System | Direction | Interface |
|---|---|---|
| Character Creator (#4) | Inbound (writes) | The only writer. Submits a fully populated PlayerProfile object on confirm. |
| Dialogue/Narrative Engine (#8) via MasksVariableStorage | Outbound (reads) | MasksVariableStorage reads `pc_name` and the four pronoun fields to bind `$pc_name`, `$pc_pronoun_subject`, `$pc_pronoun_object`, `$pc_pronoun_possessive`, `$pc_pronoun_reflexive` Yarn variables. |
| Save/Load System (#7) | Bidirectional | **Save**: Player Profile provides its serializable form to SaveManager. **Load**: SaveManager calls `PlayerProfile.RestoreProfile(data)` on startup. |
| Text Substitution System (#13) | Outbound (reads) | Text Substitution reads name and pronoun fields to resolve substitution tokens in non-Yarn text. *(Provisional — Text Substitution GDD not yet authored.)* |

## Formulas

The Player Profile system contains no mathematical formulas.

### Save File Schema

**SerializablePlayerProfile**
```
{
  display_name: string,              // player's chosen name; 1–30 chars
  pronoun_subject: string,           // e.g., "she", "he", "they"
  pronoun_object: string,            // e.g., "her", "him", "them"
  pronoun_possessive: string,        // e.g., "her", "his", "their"
  pronoun_reflexive: string,         // e.g., "herself", "himself", "themselves"
  appearance: AppearanceSelectionDto[],
  profile_version: int               // increments on each confirmed edit
}
```

**AppearanceSelectionDto**
```
{
  category: string,    // e.g., "hair_color"
  value: string        // e.g., "dark_brown"
}
```

The `appearance` list serializes as a plain array — no Dictionary conversion required. Category keys are validated against the Character Creator's approved option list at authoring time; no runtime validation is performed on load.

### Yarn Variable Mapping

| Yarn Variable | Profile Field | Type |
|---|---|---|
| `$pc_name` | `display_name` | string |
| `$pc_pronoun_subject` | `pronoun_subject` | string |
| `$pc_pronoun_object` | `pronoun_object` | string |
| `$pc_pronoun_possessive` | `pronoun_possessive` | string |
| `$pc_pronoun_reflexive` | `pronoun_reflexive` | string |

This mapping is implemented in `MasksVariableStorage` (Dialogue Engine GDD). The Player Profile GDD owns the field names; MasksVariableStorage owns the binding logic.

## Edge Cases

**E-1: First Launch — No Profile**
No save file exists. Player Profile initializes to defaults: empty display name, "They/Them" pronoun preset, no appearance selections. Character Creator is presented immediately. The game does not proceed to the main menu or any scene until the Character Creator is confirmed with a valid name.

**E-2: Save File Loaded With Missing Profile Fields**
A save file exists but the `playerProfile` block is missing a field (e.g., from an older schema version). Missing string fields default to empty string; `profile_version` defaults to 0. If `display_name` is empty after load, the Character Creator is presented on startup, with existing appearance selections pre-filled where available.

**E-3: Display Name Exactly at Length Limit**
A 30-character name is valid and must be accepted. A 31-character name must be rejected at input — the text field stops accepting characters at 30. No error message; the field simply stops.

**E-4: All-Whitespace Display Name**
A name composed entirely of spaces passes the non-empty check. At MVP: allow it. It affects only the player's own experience and is not harmful to other systems.

**E-5: Custom Pronoun Fields Left Partially Empty**
Player selects "Custom" and fills some but not all four fields. The confirm button remains disabled. On attempting to confirm, a hint indicates which specific fields are missing. The player cannot proceed until all four are populated.

**E-6: Profile Updated Mid-Session**
Player edits their name or pronouns via settings. The Profile is updated immediately on confirm. Scenes already in progress hold their current Yarn variable values for the duration of that scene. The next scene load binds the updated values.

**E-7: Appearance Category Value Not Found on Load**
An appearance selection references a category or value removed in the current build. The unknown selection is ignored; the category defaults to its first valid option. A warning is logged. No crash, no player-visible error.

**E-8: Player Names Themselves the Same as a Love Interest**
Allowed unconditionally. Writers must not assume the PC's name is distinct from any NPC's name.

## Dependencies

### Upstream Dependencies
None. Player Profile is a Foundation system with no upstream dependencies.

### Downstream Dependents

| System | How it depends |
|---|---|
| Character Creator (#4) | The only writer to Player Profile. Owns the appearance option list. Depends on Player Profile's schema being stable before Character Creator UI can be built. |
| Text Substitution System (#13) | Reads `display_name` and all four pronoun fields to resolve name and pronoun tokens in non-Yarn text. *(Provisional — GDD not yet authored.)* |
| Dialogue/Narrative Engine (#8) via MasksVariableStorage | MasksVariableStorage binds five Yarn variables from Player Profile fields. If Player Profile is unavailable at scene start, MasksVariableStorage must substitute fallback strings. |
| Save/Load System (#7) | Serializes `SerializablePlayerProfile` to `save.json` and calls `RestoreProfile()` on load. Save/Load owns the file I/O; Player Profile owns the schema. |

## Tuning Knobs

| Knob | Type | Default | Safe Range | Effect |
|---|---|---|---|---|
| `DISPLAY_NAME_MAX_LENGTH` | int | 30 | [10, 60] | Maximum characters in the display name. Increasing risks layout overflow in dialogue boxes. Decreasing frustrates players with longer names. |
| `DEFAULT_PRONOUN_PRESET` | string | `"they/them"` | Any valid preset key | The pronoun preset pre-selected on first load. "They/Them" is the most inclusive default. |
| Pronoun preset list | string[] | She/Her, He/Him, They/Them | Any set of valid 4-string tuples | Available named presets in the Character Creator. Adding presets requires UI space and localization coverage. |

## Visual/Audio Requirements

None — Player Profile is a data schema. It has no visual or audio output. The Character Creator GDD owns the presentation of profile-editing UI.

## UI Requirements

The Character Creator (GDD #4) is the sole UI surface for this system. Player Profile defines the data contract; Character Creator defines the screens.

Constraints this GDD places on that UI:
- Name field: max 30 characters, Unicode, confirm disabled when empty
- Pronoun UI: must offer the three named presets plus a Custom path that exposes all four fields individually; confirm disabled if any field is empty
- Appearance UI: a category-and-option picker for each appearance category; the full category/option list is owned by Character Creator GDD
- Re-edit access: a settings entry point must exist from the main menu that returns the player to the Character Creator without resetting existing values

> **📌 UX Flag — Player Profile**: This system has UI requirements. In Phase 4 (Pre-Production), run `/ux-design` to create a UX spec for the Character Creator screen before writing epics. Stories that reference Character Creator UI should cite `design/ux/character-creator.md`, not this GDD directly.

## Acceptance Criteria

Legend: **Logic** = Edit Mode automated | **Integration** = Play Mode automated | **Manual** = QA walkthrough

### Category 1: First Launch Behavior

| ID | Criterion | Classification |
|---|---|---|
| PP-001 | GIVEN no save file exists / WHEN the application starts / THEN the Character Creator is presented before any other game content | Integration / BLOCKING |
| PP-002 | GIVEN the Character Creator is presented on first launch / THEN the pronoun preset "They/Them" is pre-selected | Integration / BLOCKING |
| PP-003 | GIVEN a valid name is entered and all pronoun fields are populated / WHEN the player confirms / THEN the Profile transitions from Uninitialized to Active | Logic / BLOCKING |

### Category 2: Name Field Validation

| ID | Criterion | Classification |
|---|---|---|
| PP-010 | GIVEN the name field is empty / THEN the confirm button is disabled | Logic / BLOCKING |
| PP-011 | GIVEN the name field contains exactly 1 character / THEN the confirm button is enabled (assuming pronouns are valid) | Logic / BLOCKING |
| PP-012 | GIVEN the name field contains exactly 30 characters / THEN the name is accepted | Logic / BLOCKING |
| PP-013 | GIVEN the name field is at 30 characters / WHEN the player types another character / THEN the field does not accept the input | Logic / BLOCKING |
| PP-014 | GIVEN the name contains Unicode characters (e.g., "Ren渡") / THEN the name is accepted and stored without corruption | Logic / BLOCKING |

### Category 3: Pronoun Validation

| ID | Criterion | Classification |
|---|---|---|
| PP-020 | GIVEN the player selects the "She/Her" preset / THEN all four fields are populated: subject=she, object=her, possessive=her, reflexive=herself | Logic / BLOCKING |
| PP-021 | GIVEN the player selects the "He/Him" preset / THEN all four fields are populated: subject=he, object=him, possessive=his, reflexive=himself | Logic / BLOCKING |
| PP-022 | GIVEN the player selects the "They/Them" preset / THEN all four fields are populated: subject=they, object=them, possessive=their, reflexive=themselves | Logic / BLOCKING |
| PP-023 | GIVEN the player selects "Custom" and leaves one field empty / THEN the confirm button is disabled | Logic / BLOCKING |
| PP-024 | GIVEN the player enters custom values in all four fields / THEN all four values are stored exactly as entered | Logic / BLOCKING |
| PP-025 | GIVEN a preset is selected and then "Custom" is selected / THEN the four fields are cleared and editable | Logic / BLOCKING |

### Category 4: Yarn Variable Binding

| ID | Criterion | Classification |
|---|---|---|
| PP-030 | GIVEN display_name="Ren" / WHEN a Yarn scene renders `{$pc_name}` / THEN the output contains "Ren" | Integration / BLOCKING |
| PP-031 | GIVEN pronoun_subject="she" / WHEN a Yarn scene renders `{$pc_pronoun_subject}` / THEN the output contains "she" | Integration / BLOCKING |
| PP-032 | GIVEN pronoun_reflexive="themselves" / WHEN a Yarn scene renders `{$pc_pronoun_reflexive}` / THEN the output contains "themselves" | Integration / BLOCKING |

### Category 5: Save/Load Round-Trip

| ID | Criterion | Classification |
|---|---|---|
| PP-040 | GIVEN display_name="Ash" and pronoun_subject="they" / WHEN saved and the application restarts / THEN both values are restored exactly | Integration / BLOCKING |
| PP-041 | GIVEN custom appearance selections / WHEN saved and loaded / THEN all selections are restored exactly | Integration / BLOCKING |

### Category 6: Re-edit

| ID | Criterion | Classification |
|---|---|---|
| PP-050 | GIVEN the player updates display_name via settings / WHEN the next scene starts / THEN `$pc_name` reflects the updated name | Integration / BLOCKING |
| PP-051 | GIVEN the player updates pronouns / WHEN the current scene completes and a new scene starts / THEN `$pc_pronoun_subject` reflects the updated value | Integration / BLOCKING |

### Category 7: Edge Cases

| ID | Criterion | Classification |
|---|---|---|
| PP-060 | GIVEN a save file with a missing `display_name` field / WHEN loaded / THEN the Character Creator is presented on startup rather than crashing | Integration / BLOCKING |
| PP-061 | GIVEN a save file with an unknown appearance category / WHEN loaded / THEN the unknown selection is ignored, a warning is logged, and the game does not crash | Integration / BLOCKING |
| PP-062 | GIVEN the player names themselves the same name as a love interest / THEN the game does not crash or produce incorrect dialogue substitutions | Manual / BLOCKING |

## Open Questions

All design questions are resolved. Engineering items to address before implementation:

1. **Character Creator GDD** (#4, design order #14) owns the appearance category list and valid values per category — Player Profile's `AppearanceSelectionDto` schema is final, but the content it can hold depends on Character Creator's authored option set.
2. **Text Substitution System** (#13) interface is provisional — marked accordingly in Interactions. Confirm the substitution token format (`{pc_name}` vs. another scheme) when Text Substitution GDD is authored.
3. **MasksVariableStorage fallback** — if Player Profile is somehow unavailable when a scene starts (only possible if load sequence fails), MasksVariableStorage needs fallback strings for all five variables. Recommend: empty string for `$pc_name`, "they/them/their/themselves" for pronouns. This is an implementation detail for the Dialogue Engine programmer, not a GDD change.
