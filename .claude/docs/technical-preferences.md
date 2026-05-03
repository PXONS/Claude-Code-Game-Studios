# Technical Preferences

<!-- Populated by /setup-engine. Updated as the user makes decisions throughout development. -->
<!-- All agents reference this file for project-specific standards and conventions. -->

## Engine & Language

- **Engine**: Unity 6.3 LTS
- **Language**: C#
- **Rendering**: Universal Render Pipeline (URP)
- **Physics**: Unity Physics 2D (Physics2D for any UI/VN interactions)

## Input & Platform

- **Target Platforms**: PC (Steam)
- **Input Methods**: Keyboard/Mouse
- **Primary Input**: Keyboard/Mouse
- **Gamepad Support**: Partial (menu/dialogue navigation — Steam Deck compatibility)
- **Touch Support**: None
- **Platform Notes**: Steam Deck compatibility recommended — all menus and dialogue must support controller d-pad navigation. No hover-only interactions.

## Naming Conventions

- **Classes**: PascalCase (e.g., `CharacterProfile`, `DialogueManager`)
- **Public fields/properties**: PascalCase (e.g., `MoveSpeed`, `CharacterName`)
- **Private fields**: _camelCase (e.g., `_currentDepth`, `_isUnlocked`)
- **Methods**: PascalCase (e.g., `AdvanceDepth()`, `TriggerReveal()`)
- **Files**: PascalCase matching class (e.g., `CharacterProfile.cs`)
- **Scenes/Prefabs**: PascalCase matching root object (e.g., `DialoguePanel.prefab`)
- **Constants**: PascalCase or UPPER_SNAKE_CASE

## Performance Budgets

- **Target Framerate**: 60 fps
- **Frame Budget**: 16.6ms
- **Draw Calls**: ≤100 per frame (2D VN scenes are typically well under this)
- **Memory Ceiling**: 2GB RAM target (PC — visual novel asset sets are moderate)

## Testing

- **Framework**: NUnit via Unity Test Runner (Edit Mode + Play Mode tests)
- **Minimum Coverage**: Core game logic (depth system, state machine, save/load)
- **Required Tests**: Relationship depth progression, secret reveal state machine, save/load round-trip, character creator data integrity

## Forbidden Patterns

- [None configured yet — add as architectural decisions are made]

## Allowed Libraries / Addons

- **Newtonsoft.Json** (`com.unity.nuget.newtonsoft-json`) — JSON serialization for save data. Required because Unity's JsonUtility cannot serialize Dictionary or HashSet, which are used in CharacterState (narrative_flags, scenes_played). Approved: design/gdd/save-load-system.md.

## Architecture Decisions Log

- [No ADRs yet — use /architecture-decision to create one]

## Engine Specialists

- **Primary**: unity-specialist
- **Language/Code Specialist**: unity-specialist (C# review — primary covers it)
- **Shader Specialist**: unity-shader-specialist (Shader Graph, HLSL, URP/HDRP materials)
- **UI Specialist**: unity-ui-specialist (UI Toolkit UXML/USS, UGUI Canvas, runtime UI)
- **Additional Specialists**: unity-dots-specialist (ECS, Jobs system, Burst compiler), unity-addressables-specialist (asset loading, memory management, content catalogs)
- **Routing Notes**: Invoke primary for architecture and general C# code review. Invoke shader specialist for rendering and visual effects. Invoke UI specialist for all interface implementation (dialogue boxes, character creator, HUD). Invoke Addressables specialist for character asset loading and memory management.

### File Extension Routing

| File Extension / Type | Specialist to Spawn |
|-----------------------|---------------------|
| Game code (.cs files) | unity-specialist |
| Shader / material files (.shader, .shadergraph, .mat) | unity-shader-specialist |
| UI / screen files (.uxml, .uss, Canvas prefabs) | unity-ui-specialist |
| Scene / prefab / level files (.unity, .prefab) | unity-specialist |
| Native extension / plugin files (.dll, native plugins) | unity-specialist |
| General architecture review | unity-specialist |
