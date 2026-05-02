# Game Concept: Masks

*Created: 2026-05-02*
*Status: Draft*

---

## Elevator Pitch

> A romance visual novel where you choose from 20–30 unique characters spanning
> every personality type and orientation — each with a polished public face hiding
> a secret private self. Spend time with anyone you want, dig as deep as you dare,
> and discover whether the person you fell for is who you thought they were.

---

## Core Identity

| Aspect | Detail |
| ---- | ---- |
| **Genre** | Visual Novel / Romance — Dark / Otome-inspired |
| **Platform** | PC (Steam) |
| **Target Audience** | 18–30 romance/story players; Explorers and Socializers |
| **Player Count** | Single-player |
| **Session Length** | 30–60 minutes |
| **Monetization** | Premium (TBD — DLC characters viable post-launch) |
| **Estimated Scope** | Large (12–18 months solo / 6–9 months small team) |
| **Comparable Titles** | Mystic Messenger, Dangerous Fellows, Collar×Malice |

---

## Core Fantasy

> "This character is everything I shouldn't want — and I can't stop."

You meet people who seem like one thing. You spend time with them. You think you
know them. Then the mask slips — and everything shifts. Sometimes the secret is
dark. Sometimes it's heartbreaking. Sometimes it's surprisingly good.

The fantasy isn't "date the perfect person." It's the sharper, more honest fantasy:
*understanding why you stayed, why you pushed, and who you found underneath.*

---

## Unique Hook

It's like a dating sim — AND ALSO every character has a hidden layer that the
player actively chooses to uncover (or not). The depth of the relationship is
controlled entirely by the player's curiosity — and curiosity has consequences.

No other game in this genre makes the *discovery process itself* the primary
mechanical tension rather than romance route progression.

---

## Visual Identity Anchor

*Note: Art style is TBD — to be defined in `/art-bible`. Recommended direction for
review: Korean webtoon / manhwa style. This genre has massive overlap with Tapas,
Webtoon, and social sharing audiences. Highly expressive character art, strong silhouettes,
and shareability built into the format. Unique in the Western VN market.*

**Placeholder anchor until `/art-bible` is run:**
- **Named direction**: Modern Webtoon
- **Visual rule**: Every character must be instantly recognizable in silhouette and
  read as both attractive and slightly unknowable at first glance
- **Color philosophy**: Each character has a personal palette that subtly shifts
  when their secret layer begins to emerge
- **Design test**: If a character's art could appear on a Webtoon cover and generate
  comments — it passes. If it looks generic or interchangeable — redraw.

---

## Player Experience Analysis (MDA Framework)

### Target Aesthetics (What the player FEELS)

| Aesthetic | Priority | How We Deliver It |
| ---- | ---- | ---- |
| **Fantasy** (make-believe, role-playing) | 1 | Player inhabits a protagonist who can pursue anyone; identity/gender fully customizable |
| **Narrative** (drama, story arc) | 2 | Each character has a structured arc with a secret revelation as climax |
| **Discovery** (exploration, secrets) | 3 | Secrets are earned through depth progression; no two players discover the same things at the same pace |
| **Sensation** (sensory pleasure) | 4 | High-quality character art, expressive writing, scene-level audio design |
| **Expression** (self-expression) | 5 | Character creator lets players define their own identity before pursuing others |
| **Fellowship** (social connection) | 6 | Designed-to-share moments drive community and fan content |
| **Challenge** (mastery) | N/A | — |
| **Submission** (relaxation) | N/A | — |

### Key Dynamics (Emergent player behaviors)

- Players will pursue multiple characters simultaneously, comparing secrets
- Players will screenshot pivotal reveal moments and share them online
- Players will debate in communities which character's secret is "worst" or "best"
- Players will replay routes after knowing the secret to catch foreshadowing they missed
- Players will invest in character creator to express their own identity

### Core Mechanics (Systems we build)

1. **Relationship Depth System** — each character has 5 depth tiers; advancing requires sustained attention and correct read of their signals
2. **Scene + Activity Loop** — dialogue scenes and date-style activities interleave; activities unlock scenes; scenes advance depth
3. **The Dig** — at each depth tier, the player chooses: push deeper (risk / reward) or maintain (safe but slow); wrong pushes can close a character off
4. **Character Creator** — gender identity, pronouns, appearance; no locked routes based on identity choices
5. **Secret Reveal System** — each character's secret surfaces through a dedicated arc at depth tier 4–5; tone and content vary widely across the cast

---

## Player Motivation Profile

### Primary Psychological Needs Served

| Need | How This Game Satisfies It | Strength |
| ---- | ---- | ---- |
| **Autonomy** | Player chooses who to pursue, how fast, how deep; no forced route order | Core |
| **Competence** | Reading character signals correctly and advancing depth creates mastery feeling | Supporting |
| **Relatedness** | Attachment to characters (and comparison/discussion with other players) | Core |

### Player Type Appeal (Bartle Taxonomy)

- [x] **Explorers** — Discovering secrets, finding foreshadowing, understanding each character's full truth
- [x] **Socializers** — Fan communities, ship debates, sharing screenshots of shocking reveals
- [x] **Achievers** — Collecting all character endings, unlocking full depth on every character

### Flow State Design

- **Onboarding curve**: First character is guided — depth tiers are explained naturally through early scenes. No tutorial pop-ups.
- **Difficulty scaling**: Early characters have more obvious signals; deeper into the roster, characters are more guarded
- **Feedback clarity**: Subtle UI shift (color palette change, music) signals depth tier advancement
- **Recovery from failure**: A "closed off" character can be re-approached after in-game time; nothing is permanently locked

---

## Core Loop

### Moment-to-Moment (30 seconds)
Read a scene or activity description → make a dialogue choice → observe the
character's reaction. The reaction contains signal: does their mask slip slightly,
hold firm, or tighten? Every choice is intrinsically satisfying because the writing
rewards attention.

### Short-Term (5–15 minutes)
Complete an activity or scene set → relationship depth meter advances → new scene
or Depth Gate unlocks. "One more scene" psychology: each scene ends on a small
emotional hook — a look, a pause, a door closed too fast.

### Session-Level (30–60 minutes)
Advance one or two characters' depth tiers. Hit a Depth Gate — a moment where the
character notices your attention shifting and the dynamic changes. Decide: push
deeper or pull back. Natural session endpoint. Strong re-entry hook: the player
knows exactly what's waiting if they come back.

### Long-Term Progression
Unlock each character's secret. Collect unique endings. Re-read earlier scenes
with new knowledge to find the foreshadowing. Eventually: all 20–30 characters
revealed, every ending seen, full cast "known."

### Retention Hooks

- **Curiosity**: Every character's secret is unknown until depth tier 4–5. The
  question "what are they hiding?" is always live
- **Investment**: Relationship depth progress, attachment to specific characters
- **Social**: Fan community discussions, secret rankings, ship debates — players
  return to contribute and stay current
- **Mastery**: Learning to read character signals faster; first playthrough vs.
  second playthrough feel genuinely different

---

## Game Pillars

### Pillar 1: Every Face Hides a Face
Every character has a public self and a private self. No character is exactly what
they seem at first glance — not as a twist mechanic but as a worldview.

*Design test*: If a new character's secret could be guessed from their public
persona alone — make the gap bigger.

### Pillar 2: You Choose How Deep
Players control the pace and depth of every relationship. Secrets are earned through
deliberate attention, not time-gating. The player's curiosity shapes what they find.

*Design test*: If a scene or mechanic forces the player forward without a choice —
redesign it to offer a meaningful decision.

### Pillar 3: Desire Drives Discovery
Players engage because they're attracted to the character — emotionally, aesthetically,
or intellectually — not because a quest marker told them to. Character writing and
art must do this work.

*Design test*: If we're using game mechanics to push the player toward a character
instead of the character themselves generating interest — the character needs work.

### Pillar 4: Designed to Be Shared
Every pivotal scene, every secret reveal, every surprising moment should be a
shareable artifact. Writing, art, and composition should be screenshot-worthy.

*Design test*: Would a player post this to social media without prompting? If not —
punch it up.

### Pillar 5: No Wrong Type
Character creation and romantic options respect all orientations and identities
without forcing labels or locking content behind identity choices.

*Design test*: Can any player build a version of themselves and pursue any character
they want? If not — remove the gate.

### Anti-Pillars (What This Game Is NOT)

- **NOT a horror game**: The dark secrets are character texture, not genre definition. The tone is "romantic with edge," not unsettling or traumatic.
- **NOT a pure idealized dating sim**: Love interests have genuine flaws. The edge is not optional — it's the point.
- **NOT all-dark secrets**: Some secrets should be surprising in a good or funny way. Variety in revelation tone is essential.
- **NOT a grind**: Depth is earned through attention and good reads, not time-gated resource accumulation.

---

## Inspiration and References

| Reference | What We Take From It | What We Do Differently | Why It Matters |
| ---- | ---- | ---- | ---- |
| Mystic Messenger | Large character cast, dedicated routes, community-driven | Secrets not telegraphed; player-driven depth system | Proves large casts sustain long-term player investment |
| Dangerous Fellows | Dark romantic interests, mobile success | PC-first; secret variety (not all villains) | Proves "toxic romantic" appeal has real audience |
| Doki Doki Literature Club | Subverted expectations, viral word-of-mouth | Not horror; tone is "romantic with edge" not psychological horror | Proves shareable subversion drives organic growth |

**Non-game inspirations**: Webtoon/manhwa romance series (e.g., *True Beauty*, *I Love Yoo*);
psychological thriller character writing; the social media phenomenon of parasocial attachment
to fictional "red flag" characters.

---

## Target Player Profile

| Attribute | Detail |
| ---- | ---- |
| **Age range** | 18–30 |
| **Gaming experience** | Casual to mid-core — comfortable with VNs; not hardcore gamers |
| **Time availability** | 30–60 minute sessions; consistent but not marathon |
| **Platform preference** | PC (Steam); active in fan communities on Twitter/TikTok/Reddit |
| **Current games they play** | Mystic Messenger, Stardew Valley, Life is Strange |
| **What they're looking for** | Characters to fall for that feel real and complicated — not wish fulfillment |
| **What would turn them away** | Overly dark/traumatic tone; forced routes; characters with no warmth |

---

## Technical Considerations

| Consideration | Assessment |
| ---- | ---- |
| **Recommended Engine** | Unity — developer's existing expertise; strong 2D pipeline; good Steam integration |
| **Key Technical Challenges** | Relationship depth state tracking across 20–30 characters; branching narrative management at scale |
| **Art Style** | TBD — Korean webtoon / manhwa recommended (see Visual Identity Anchor above) |
| **Art Pipeline Complexity** | High — 20–30 characters × multiple expressions × background art × key CG scenes |
| **Audio Needs** | Music-heavy — each character needs distinct theme; scene music reacts to depth tier |
| **Networking** | None |
| **Content Volume** | Est. 20–30 characters × ~6 scenes + 2 activities + 1 secret arc + 1 ending each |
| **Procedural Systems** | None — fully handcrafted narrative |

---

## Risks and Open Questions

### Design Risks
- Tone balance is fragile — too dark tips into uncomfortable; too light and the edge disappears
- Secret variety may feel inconsistent if some secrets are much better written than others
- Player may not push deep enough on any character to hit the reveal — pacing must guide without forcing

### Technical Risks
- Narrative state management at scale (20–30 characters with branching depth) — needs a robust dialogue/state system (e.g., Ink, Yarn Spinner)
- Art volume is the single biggest production risk — character expressions + CGs + backgrounds is substantial

### Market Risks
- Genre has established mobile competitors (Dangerous Fellows, Obey Me!) — PC positioning must be clearly distinct
- Adult content policy on Steam may constrain some creative directions — needs early platform clarity

### Scope Risks
- 20–30 characters is full-vision scope — MVP must be validated at 3–5 characters before expanding
- Writing quality must remain consistent across the full cast; quality decay is a real risk at scale

### Open Questions
- What narrative tool handles the depth/branching system best? (Ink vs Yarn Spinner vs custom — needs spike)
- What is the correct ratio of "dark" to "surprising-good" secrets across the cast?
- Does the "Dig" mechanic feel satisfying to players or frustrating? (Needs playtest at MVP)

---

## MVP Definition

**Core hypothesis**: Players find a character compelling enough to push through all
5 depth tiers, and the secret reveal is satisfying and shareable.

**Required for MVP**:
1. 3–5 fully written characters with complete depth arcs (all 5 tiers, full secret, unique ending)
2. Scene + Activity loop implemented and functional
3. The Dig mechanic — depth tier advancement with player choice gates
4. Basic character creator (appearance + gender/pronouns)
5. Secret reveal moment for each character — screenshot-worthy presentation

**Explicitly NOT in MVP** (defer to later):
- Full 20–30 character cast
- Advanced audio (character themes, reactive music) — placeholder audio only
- Achievement system, save file management polish, Steam integration beyond basic

### Scope Tiers

| Tier | Content | Features | Timeline |
| ---- | ---- | ---- | ---- |
| **MVP** | 3–5 characters, full depth arcs | Core loop, Dig mechanic, basic character creator | 2–4 months |
| **Early Access** | 8–10 characters, all systems polished | Full character creator, Steam page, community features | 5–8 months |
| **Full Release** | 20–30 characters | All secrets, polish pass, DLC hooks | 12–18 months solo |

---

## Next Steps

- [ ] Run `/setup-engine` to configure Unity and populate version-aware reference docs
- [ ] Run `/art-bible` to define visual identity before any asset production
- [ ] Run `/design-review design/gdd/game-concept.md` to validate completeness
- [ ] Decompose into systems with `/map-systems`
- [ ] Author per-system GDDs with `/design-system`
- [ ] Plan technical architecture with `/create-architecture`
- [ ] Prototype the Dig mechanic with `/prototype dig-mechanic`
- [ ] Validate with `/playtest-report` after MVP prototype
- [ ] Plan first sprint with `/sprint-plan new`
