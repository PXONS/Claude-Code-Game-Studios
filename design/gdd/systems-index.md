# Systems Index: Masks

> **Status**: Draft
> **Created**: 2026-05-02
> **Last Updated**: 2026-05-02
> **Source Concept**: design/gdd/game-concept.md

---

## Overview

Masks is a romance visual novel where players pursue 20–30 characters, each with a
polished public face concealing a private secret. The mechanical spine is the
Relationship Depth System — a five-tier progression the player advances through
sustained attention, correct signal reading, and deliberate choices at Depth Gates
(The Dig). All other systems serve this core: the dialogue engine runs the scenes,
the character state manager tracks per-character progress, and the UI makes the
invisible depth of a relationship visible without announcing it.

The system architecture is dominated by two high-risk bottlenecks: the
Dialogue/Narrative Engine (the Ink vs. YarnSpinner open question gates the entire
content pipeline) and the Character State Manager (8 systems depend on it). These
two must be designed and decided before the rest of the pipeline can be specified.
The game has no networking, no procedural generation, and no economy — the
complexity is all in narrative state management at scale.

---

## Systems Enumeration

| # | System Name | Category | Priority | Status | Design Doc | Depends On |
|---|---|---|---|---|---|---|
| 1 | Relationship Depth System | Gameplay | MVP | Not Started | — | #6, #7 |
| 2 | Scene & Activity Loop | Gameplay | MVP | Not Started | — | #9, #1, #17 |
| 3 | The Dig (Depth Gate) | Gameplay | MVP | Not Started | — | #1, #9 |
| 4 | Character Creator | Gameplay | MVP | Not Started | — | #12, #13 |
| 5 | Secret Reveal System | Gameplay | MVP | Not Started | — | #1, #3 |
| 6 | Character State Manager | Core | MVP | Designed | `design/gdd/character-state-manager.md` | 8-field schema; write ownership table; quarantine map for cut chars |
| 7 | Save/Load System | Persistence | MVP | Not Started | — | — |
| 8 | Dialogue/Narrative Engine | Narrative | MVP | Designed | `design/gdd/dialogue-narrative-engine.md` | YarnSpinner 3.0; MasksVariableStorage pattern established |
| 9 | Scene Management / Flow Controller | Core | MVP | Not Started | — | #6, #8 |
| 10 | Activity System *(inferred)* | Gameplay | Vertical Slice | Not Started | — | #2, #9 |
| 11 | Signal Reading Mechanic *(inferred)* | Gameplay | Vertical Slice | Not Started | — | #3, #1 |
| 12 | Player Profile / PC Data *(inferred)* | Core | MVP | Not Started | — | — |
| 13 | Text Substitution System *(inferred)* | Narrative | MVP | Not Started | — | #12, #8 |
| 14 | Revelation Presentation System *(inferred)* | UI | MVP | Not Started | — | #5, #16, #15 |
| 15 | Audio System / Reactive Music *(inferred)* | Audio | MVP | Not Started | — | #8 |
| 16 | UI System *(inferred)* | UI | MVP | Not Started | — | #1, #3, #2, #8 |
| 17 | Character Roster / Availability *(inferred)* | Core | Vertical Slice | Not Started | — | #6, #9 |
| 18 | Endings / Route Completion *(inferred)* | Progression | Alpha | Not Started | — | #5, #1, #7 |
| 19 | Accessibility System *(inferred)* | Meta | Alpha | Not Started | — | #16, #8 |

> **Accessibility note**: System #19 is Alpha *build* priority, but its GDD should
> be authored at Vertical Slice — designing accessibility into the UI from the start
> is significantly less costly than retrofitting.

---

## Categories

| Category | Description |
|---|---|
| **Core** | Foundation systems everything else depends on |
| **Gameplay** | The systems that make the game mechanically distinct |
| **Narrative** | Script running, dialogue delivery, text processing |
| **Persistence** | Save state and session continuity |
| **UI** | Player-facing displays and interaction surfaces |
| **Audio** | Music and reactive audio systems |
| **Progression** | Tracking completion state over the full run |
| **Meta** | Accessibility, onboarding, and platform integration |

---

## Priority Tiers

| Tier | Definition | Target Milestone |
|---|---|---|
| **MVP** | Required to test the core hypothesis: "Is the Dig satisfying, and is the reveal shareable?" | 3–5 characters playable end-to-end |
| **Vertical Slice** | Required for a complete, polished experience with one character | Pre-Early Access demo |
| **Alpha** | All mechanical scope present; content placeholder OK | Steam Early Access candidate |
| **Full Vision** | Polish, edge cases, full cast | Full Release |

---

## Dependency Map

### Foundation Layer (no dependencies)
These must be designed first — all other systems read or write their interfaces.

1. **Character State Manager** (#6) — all depth, scene, and roster logic reads from this; without a stable state schema, nothing else can be specified
2. **Save/Load System** (#7) — persistence layer; the state manager's data goes nowhere without this
3. **Dialogue/Narrative Engine** (#8) — all scene content passes through this runner; the Ink/YarnSpinner architectural decision gates the entire content pipeline
4. **Player Profile / PC Data** (#12) — pronoun and name data must exist before any dialogue is meaningful

### Core Layer (depends on Foundation only)

1. **Relationship Depth System** (#1) — depends on: #6 (state), #7 (save)
2. **Text Substitution System** (#13) — depends on: #12 (player profile), #8 (dialogue engine)
3. **Scene Management / Flow Controller** (#9) — depends on: #6 (state), #8 (dialogue engine)
4. **Audio System / Reactive Music** (#15) — depends on: #8 (dialogue engine) — trigger architecture must be established before depth-reactive music can be wired
5. **Character Roster / Availability** (#17) — depends on: #6 (state), #9 (scene management)

### Feature Layer (depends on Core)

1. **The Dig (Depth Gate)** (#3) — depends on: #1 (depth), #9 (scene management)
2. **Scene & Activity Loop** (#2) — depends on: #9 (scene management), #1 (depth), #17 (roster)
3. **Signal Reading Mechanic** (#11) — depends on: #3 (dig), #1 (depth)
4. **Activity System** (#10) — depends on: #2 (scene loop), #9 (scene management)
5. **Secret Reveal System** (#5) — depends on: #1 (depth), #3 (dig)
6. **Character Creator** (#4) — depends on: #12 (player profile), #13 (text substitution)
7. **Endings / Route Completion** (#18) — depends on: #5 (secret reveal), #1 (depth), #7 (save/load)

### Presentation Layer (wraps gameplay systems)

1. **UI System** (#16) — depends on: #1 (depth), #3 (dig), #2 (scene loop), #8 (dialogue engine)
2. **Revelation Presentation System** (#14) — depends on: #5 (secret reveal), #16 (UI), #15 (audio)
3. **Accessibility System** (#19) — depends on: #16 (UI), #8 (dialogue engine)

---

## Recommended Design Order

Design these systems in order. Foundation systems must be completed before Core; Core before Feature; Feature before Presentation. Systems within the same layer are independent and may be designed in parallel.

| Order | System | Priority | Layer | Est. Effort |
|---|---|---|---|---|
| 1 | Dialogue/Narrative Engine | MVP | Foundation | L — architectural decision (Ink/YarnSpinner) + full spec |
| 2 | Character State Manager | MVP | Foundation | M — schema design; most of the game's complexity lives here |
| 3 | Save/Load System | MVP | Foundation | M — serialization design, session recovery |
| 4 | Player Profile / PC Data | MVP | Foundation | S — small schema; complexity is in integration |
| 5 | Relationship Depth System | MVP | Core | M — 5-tier design, tier advancement rules, cooldown after close-off |
| 6 | Text Substitution System | MVP | Core | S — pronoun/name injection rules |
| 7 | Scene Management / Flow Controller | MVP | Core | M — scene sequencing, unlock logic, session entry/exit |
| 8 | The Dig (Depth Gate) | MVP | Feature | M — the primary mechanical tension; needs careful design |
| 9 | Scene & Activity Loop | MVP | Feature | M — the moment-to-moment loop structure |
| 10 | Audio System / Reactive Music | MVP | Core | S — trigger architecture; audio content is placeholder in MVP |
| 11 | Secret Reveal System | MVP | Feature | M — reveal arc structure, tone variety rules |
| 12 | UI System | MVP | Presentation | L — dialogue box, depth meter, choice overlay; art bible has detailed specs |
| 13 | Revelation Presentation System | MVP | Presentation | M — coordinated sprite + audio + UI behavior at reveal |
| 14 | Character Creator | MVP | Feature | S — appearance + pronouns; basic scope for MVP |
| 15 | Signal Reading Mechanic | Vertical Slice | Feature | S — formalizes what "reading a character" means |
| 16 | Character Roster / Availability | Vertical Slice | Core | S — session availability logic |
| 17 | Activity System | Vertical Slice | Feature | S — activity structure, distinct from scene |
| 18 | Accessibility System | Alpha* | Presentation | M — text speed, scaling, reduced motion, colorblind mode |
| 19 | Endings / Route Completion | Alpha | Feature | S — ending state tracking, unlock presentation |

*Accessibility GDD should be written at Vertical Slice phase even though the build is Alpha priority.

---

## Circular Dependencies

None found. The dependency graph is a clean directed acyclic graph.

---

## High-Risk Systems

| System | Risk Type | Risk Description | Mitigation |
|---|---|---|---|
| Dialogue/Narrative Engine (#8) | Technical | Ink vs. YarnSpinner vs. custom is unresolved — this decision gates the entire content pipeline. Wrong choice = expensive rewrite. | Spike session first: prototype the Dig mechanic in both tools; choose before any other system is specified. |
| Character State Manager (#6) | Technical + Scope | 20–30 characters × 5 tiers × branching scenes = significant state complexity. Schema decisions made here ripple across 8 dependent systems. | Design the state schema at MVP scope (3–5 chars); validate before expanding. Make the schema extension-safe. |
| Relationship Depth System (#1) | Design | The pacing of depth tier advancement is the game's core feel. Too fast = no tension. Too slow = players stop. | Playtest at MVP. Build tuning knobs for advancement rate before locking values. |
| Scene & Activity Loop (#2) | Scope | Content volume is the single biggest production risk. The loop design determines how much writing is needed per character. | Define the minimum viable scene count per tier before designing the loop structure. |
| UI System (#16) | Design | The depth meter and revelation presentation must feel non-intrusive in public states and deeply satisfying at tier breaks. The art bible has detailed specs — implementation must honor them precisely. | Design GDD references art bible section 3.4 and 7 directly. Build a prototype of the depth meter behavior early. |

---

## Progress Tracker

| Metric | Count |
|---|---|
| Total systems identified | 19 |
| Design docs started | 0 |
| Design docs reviewed | 0 |
| Design docs approved | 0 |
| MVP systems designed | 0 / 14 |
| Vertical Slice systems designed | 0 / 3 |
| Alpha systems designed | 0 / 2 |

---

## Next Steps

- [x] **First**: Spike the Dialogue/Narrative Engine decision — YarnSpinner chosen. See `prototypes/dialogue-engine-spike/REPORT.md`
- [x] Design system GDDs — Dialogue/Narrative Engine complete (`design/gdd/dialogue-narrative-engine.md`)
- [x] Design system GDDs — Character State Manager complete (`design/gdd/character-state-manager.md`)
- [ ] Design system GDDs in recommended order — next: `/design-system save-load-system` (design order #3)
- [ ] Run `/design-review` on each completed GDD
- [ ] Run `/gate-check pre-production` when all MVP systems are designed
