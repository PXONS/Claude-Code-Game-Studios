# Scene Management / Flow Controller

> **Status**: In Design
> **Author**: Design session + agents
> **Last Updated**: 2026-05-03
> **Implements Pillar**: Pillar 2 (You Choose How Deep), Pillar 1 (Every Face Hides a Face)

## Overview

The Scene Management / Flow Controller is the sequencing layer that governs every scene transition in Masks. It decides which scene to run next, loads the required assets, injects the player's identity into the dialogue engine, starts execution, handles completion, and signals the downstream systems that need to respond. Every moment of content the player sees passes through this system — it is the single point of authority between the player's choices and the dialogue engine's execution.

From the player's perspective, Scene Management is invisible. What they experience is a character select screen that shows the right options at the right time: scenes they haven't played, scenes they're eligible for at their current depth tier, the Dig available when they've earned it. The system enforces the access rules of Pillar 2 (You Choose How Deep) — the player controls pacing, but the system enforces the preconditions that make depth meaningful. A scene at tier 3 does not appear until the relationship has earned it. The player never sees the gate; they only experience the sense that the game is keeping up with them.

Mechanically, Scene Management owns three responsibilities: scene availability (which scenes can a player access right now, for which character), scene execution (the full StartScene → SceneComplete → EndScene lifecycle), and scene accounting (recording completion, signaling the Relationship Depth System, triggering saves). It does not generate content, author scenes, or make creative decisions — it sequences, tracks, and reports.

## Player Fantasy

You open the character select screen and feel it before you choose: a pull, quiet but specific, toward one face among many. You click, and the scene that opens is exactly the one you didn't know you were ready for — not a door you've already walked through, not one locked against you, but the next room in a house you've been slowly learning. The game doesn't ask you to remember where you left off with them. It remembers for you, and it has been waiting.

This is Scene Management working correctly: no friction between wanting and having. The friction belongs in the relationships — in the gap before the right word, in the moment before someone decides whether to tell you something true. It does not belong in the act of reaching them. When the system is invisible, the player is thinking about the character. When it surfaces — a wrong scene appearing, a scene locked without reason, the game failing to find them where they left off — the spell breaks, and what was intimacy becomes navigation.

**Design north star**: Friction belongs in the relationships, not in reaching them.

## Detailed Design

### Core Rules

[To be designed]

### States and Transitions

[To be designed]

### Interactions with Other Systems

[To be designed]

## Formulas

[To be designed]

## Edge Cases

[To be designed]

## Dependencies

[To be designed]

## Tuning Knobs

[To be designed]

## Visual/Audio Requirements

[To be designed]

## UI Requirements

[To be designed]

## Acceptance Criteria

[To be designed]

## Open Questions

[To be designed]
