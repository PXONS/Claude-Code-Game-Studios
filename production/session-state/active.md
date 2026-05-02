# Session State

**Task**: System GDD authoring
**Status**: Character State Manager GDD complete (2/19)
**File**: design/gdd/character-state-manager.md
**Next**: Design Save/Load System GDD — `/design-system save-load-system`

## Progress
- [x] Art bible complete (design/art/art-bible.md — all 9 sections)
- [x] Systems index created (design/gdd/systems-index.md — 19 systems, dependency map, design order)
- [ ] System GDDs (1/19)
  - [x] Dialogue/Narrative Engine (design/gdd/dialogue-narrative-engine.md)
  - [ ] Character State Manager — design order #2, 8 dependents (highest risk remaining)
  - [ ] Save/Load System — design order #3
  - [ ] Scene Management / Flow Controller — design order #4
  - [ ] ... (15 remaining)

## Key Decisions
- Review mode: lean
- 19 systems total: 14 MVP, 3 Vertical Slice, 2 Alpha
- **Dialogue engine: YarnSpinner 3.0** (over Ink) — live state binding, no flush gap, better multi-character scaling, visual editor for writers
- **MasksVariableStorage** — `VariableStorageBehaviour` subclass; all Yarn↔CharacterStateManager reads/writes go through it; no direct CSM access from scripts
- **Variable naming**: `$char_{CharacterID}_{variable}` + fixed `$pc_name`, `$pc_pronoun_subject/object/possessive/reflexive`
- **DialogueEngineController** — façade over YarnSpinner DialogueRunner; Scene Management calls StartScene/EndScene only
- **<<notify_state_change event_type char_id>>** — notification-only; subscribers query CSM directly
- **<<pause duration_ms>>** — MVP built-in, clamped to [0, 5000ms], default 750ms
- **Text reveal**: 45.0 * speed_multiplier chars/sec; speed_multiplier ∈ [0.0, 10.0]
- **Choice stagger**: i * 60ms; max 4 choices per set

## QA Engineering Flags (from Dialogue Engine GDD)
1. AC-040 frame budget requires Profiler on minimum-spec hardware
2. Variable naming has no authoring-time enforcement — consider linter
3. AC-012/AC-015 pause tests must be Play Mode only
4. Rule 19 (CSM write isolation) enforced by convention only — consider interface boundary
5. F-3 stagger on DialogueOptionCollection — verify YarnSpinner 3.0 API before implementing
