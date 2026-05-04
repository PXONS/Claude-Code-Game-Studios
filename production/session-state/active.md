# Session State

**Task**: System GDD authoring — UI System (design order #12, next)
**Status**: Secret Reveal System GDD complete (design/gdd/secret-reveal-system.md); all 8 sections + optional sections written
**File**: design/gdd/secret-reveal-system.md

## Completed This Session
- [x] Secret Reveal System GDD (design/gdd/secret-reveal-system.md) — all sections written, registry updated

## Progress
- [x] Art bible complete (design/art/art-bible.md — all 9 sections)
- [x] Systems index created (design/gdd/systems-index.md — 19 systems, dependency map, design order)
- [ ] System GDDs (3/19)
  - [x] Dialogue/Narrative Engine (design/gdd/dialogue-narrative-engine.md)
  - [x] Character State Manager (design/gdd/character-state-manager.md)
  - [x] Save/Load System (design/gdd/save-load-system.md)
  - [x] Player Profile / PC Data (design/gdd/player-profile.md)
  - [x] Relationship Depth System (design/gdd/relationship-depth-system.md)
  - [x] Relationship Depth System (design/gdd/relationship-depth-system.md)
  - [x] Text Substitution System (design/gdd/text-substitution-system.md)
  - [x] Scene Management / Flow Controller (design/gdd/scene-management.md)
  - [x] The Dig / Depth Gate (design/gdd/the-dig.md)
  - [x] Scene & Activity Loop (design/gdd/scene-activity-loop.md)
  - [x] Audio System / Reactive Music (design/gdd/audio-system.md)
  - [ ] Secret Reveal System — design order #11
  - [ ] ... (12 remaining)

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
- **Serialization library: Newtonsoft.Json** (`com.unity.nuget.newtonsoft-json`) — added to Allowed Libraries; required for Dictionary/HashSet in CSM schema
- **Save triggers**: scene exit (Scene Management) + depth tier change (Relationship Depth / The Dig) + session launch checkpoint
- **Single save slot**: `Application.persistentDataPath/save.json`; no save UI at MVP
- **CSM RestoreState() bulk bypass** — Save/Load uses this to skip invariant guards on restore; pre-restore repair corrects depth_tier/secret_revealed mismatches before RestoreState() is called
- **Corrupt save handling**: renamed to `save_corrupt_[timestamp].json`, default state initialized, no crash
- **Depth tier semantics**: 0=Public Face, 1=Texture, 2=Gap, 3=Shape, 4=Threshold, 5=Truth
- **Dig outcomes**: DIG_PASS (advance), DIG_PARTIAL (cooldown 1 scene), DIG_FAIL (closed_off), DIG_CRITICAL_FAIL (closed_off + flag)
- **SCENES_REQUIRED defaults**: [2, 3, 3, 4, 1] for tiers 0→1 through 4→5; per-character overrides supported
- **closed_off**: recoverable via recovery arc; The Dig writes it; no tier regression on recovery
- **engagementType taxonomy**: CORE, DEPTH (count toward threshold), AMBIENT (do not count), RECOVERY
- **dig_partial_cooldown**: reserved narrative_flag key; cleared by next scene completion (any type)
- **Scene Registry**: owned by Scene Management (#9); maps sceneId → {tier, charId, engagementType}

## QA Engineering Flags
### From Dialogue Engine GDD
1. AC-040 frame budget requires Profiler on minimum-spec hardware
2. Variable naming has no authoring-time enforcement — consider linter
3. AC-012/AC-015 pause tests must be Play Mode only
4. Rule 19 (CSM write isolation) enforced by convention only — consider interface boundary
5. F-3 stagger on DialogueOptionCollection — verify YarnSpinner 3.0 API before implementing

### From Save/Load GDD
1. SL-023 (30-character load) should be run as a performance check — flag if >200ms on min-spec hardware
2. SL-032 (write permission denied) requires manual environment setup — cannot fully automate
3. Newtonsoft.Json must be added before SL-020, SL-021, SL-023 can run
