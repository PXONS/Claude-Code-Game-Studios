// PROTOTYPE - NOT FOR PRODUCTION
// Question: Can Ink represent Masks' depth state, drive The Dig, and handle PC pronouns?
// Date: 2026-05-02

// ── External state (set by Unity C# before running this knot) ──────────────
VAR depth_tier = 0          // 0–5; read from CharacterStateManager
VAR closed_off = false      // true if player was rejected at a Dig gate
VAR pc_name = "Alex"        // set from PlayerProfile at session start
VAR pc_pronoun_sub = "they" // "he" | "she" | "they" — used inline

// ── Tunnel variables (written back to Unity after knot ends) ───────────────
// Unity reads these via story.variablesState after the knot completes.
// No special Ink mechanism needed — they're just VAR writes.

// ─────────────────────────────────────────────────────────────────────────────
// KNOT: surface_meeting
// Used at depth_tier 0–1. Demonstrates conditional branching on depth state.
// ─────────────────────────────────────────────────────────────────────────────

=== surface_meeting ===

{ closed_off:
    Rei glances up when {pc_name} enters, then looks away.
    -> END
}

{ depth_tier >= 3:
    "You're here." Rei doesn't smile, but {pc_pronoun_sub} doesn't look away either.
    -> deeper_beat
- else:
    "Oh — {pc_name}." Rei's smile is precise. Practiced.
    -> surface_beat
}

= surface_beat
"I wasn't sure you'd come back."
The line lands perfectly. It always does.

+ ["I almost didn't."]
    Something shifts — almost nothing. One brow, a fraction.
    "Almost." {pc_pronoun_sub} repeats it. "Interesting."
    -> END
+ ["Of course I came back."]
    Rei nods. Warm. Correct.
    -> END

= deeper_beat
There's no performed smile today. Just Rei, looking at {pc_name} like
{pc_pronoun_sub}'s trying to work something out.

-> END


// ─────────────────────────────────────────────────────────────────────────────
// KNOT: the_dig_gate
// THE DIG — the primary mechanical tension. Player chooses to push or hold.
// On wrong push: closed_off = true (written back to Unity state manager).
// ─────────────────────────────────────────────────────────────────────────────

=== the_dig_gate ===

{ depth_tier < 2:
    // Guard: Dig shouldn't be reachable this early. Unity should gate this.
    -> END
}

"You always do that," {pc_name} says.

Rei goes still. Not the practiced stillness — the other kind.

"Do what?"

* (push) ["Look away right when things get real."]
    -> dig_push
* (hold) ["Nothing. Forget it."]
    -> dig_hold
* (read) [Say nothing. Wait.]
    -> dig_read


= dig_push
// WRONG PUSH — too direct, too early at tier 2.
{ depth_tier <= 2:
    Something closes in Rei's expression. Not anger. Worse.
    "I think I need to go."
    ~ closed_off = true    // ← Unity reads this; CharacterStateManager.SetClosedOff("rei", true)
    -> END
}
// Correct push at tier 3+
"You noticed that." Rei's voice is careful. Flat. But {pc_pronoun_sub} doesn't leave.
~ depth_tier = depth_tier + 1   // ← Unity reads this; CharacterStateManager.SetDepthTier("rei", depth_tier)
-> END

= dig_hold
// Safe choice — no progress, no damage.
"Right." Rei exhales. The stillness passes.
"Do you want to get out of here?"
-> END

= dig_read
// Skilled read — waiting is correct at tier 2.
{ depth_tier >= 2:
    The silence stretches. Rei looks at {pc_name}.
    "You're not going to push." It isn't a question.
    "No."
    Something in {pc_pronoun_sub} shoulders drops. Just slightly.
    ~ depth_tier = depth_tier + 1
    -> END
}
-> dig_hold


// ─────────────────────────────────────────────────────────────────────────────
// KNOT: revelation_surface
// Depth tier 4–5 scene. Private palette active. Looser register.
// ─────────────────────────────────────────────────────────────────────────────

=== revelation_surface ===

{ depth_tier < 4:
    -> END
}

It's late. The room is different at this hour — or maybe Rei is different.

{pc_pronoun_sub} is sitting on the floor with {pc_pronoun_sub} back against the
couch, not the cushions. Hair not quite right. Jacket somewhere else.

{pc_name} has never seen Rei sit like that.

"I've been thinking about what you said."

+ ["Which part?"]
    "All of it." A pause. "I don't — I'm not good at this."
    -> END
+ [Say nothing.]
    Rei looks up. The expression doesn't have a name.
    -> END

-> END
