# Backlog

Ideas and deferred work, roughly grouped. Move items into a stage when we pick them up.

## Combat (first version is in; still to do)
- Missile ammo and point defence that can shoot missiles down; exotic weapons (EMP, shield siphon, tractor).
- Power shunting between engines, shields and weapons (Engineer); raise/lower shields.
- Science Officer weakness scan; Navigator emergency jump-out; Engineer mid-fight repairs.
- Crew injuries after fights (Medical Officer), once the crew roster exists.
- Encounters that follow you through a save, and encounters mid-jump on the galaxy map.
- Anchor stations and Mother's fleets as escalating fights; races' allies/enemies reacting to kills.

## Conversations
- Animated face of the alien you're talking to: idle loop, talking, and reactions to posture and mood
  (angry, pleased, nervous). Shown in the conversation window beside the transcript.
- Audio for conversations: per-race voice (alien babble or processed speech under the subtitles), channel-open
  and channel-close sounds, ambience of their bridge or hall. Ink lines could carry a mood tag to pick the clip.
- A fourth posture, Curious (needs a response for each topic, per race).
- Selling artifacts: the Orrun (and Varkesh, for weapons) asked to see them first.

## Story hooks already written, waiting for story code
- Fire the "mother-signal" world event (Mother's first "Return to me" broadcast).
- Fire the "first-anchor" world event when the first Anchor severs a route; routes then actually disappear
  (course plotting already routes around missing links).

## Controls
- Gamepad selection of stars on the galaxy map (currently mouse only).

## Crew (first version is in; still to do)
- Species tensions on board (Varkesh and Skarrow, Noeti and Ophane) once other species can be recruited.
- Recruiting other species: Orrun brokers, the Zhatt, uplifting the Glimmerkin, Tikka Brann stowing away.
- Crew members speaking up in alien conversations, by personality.
- More morale modules and events (crew quarters, a chapel or garden, birthdays, letters from home).
- "Breaking" crew asking to leave in a conversation, with a chance to talk them round.

## Art and audio (planned order)
1. Space art style test: a sun, three planet types, the player's frigate, one ship each for Orrun, Varkesh, Skarrow
   (top-down, transparent backgrounds) - approve the look, then the full set.
2. Planet scenes: terrain tiles per planet type, the lander, the terrain vehicle.
3. Portraits for the rest of the cast, using Factor Venn (A) and Clanmaster Ashquill (A) as style references.
   Portrait files go in Resources/Portraits named after the speaker (factor-venn.jpg).
4. Animated portraits (layered: mouth/beak, eyes, throat sac, quills) driven by voice audio (2D Animation + PSD Importer).
5. Voice groundwork, built so actors can replace AI placeholders: a permanent ID tag on every voiced Ink line,
   audio files named by ID, a Voice tab (ID, character, text, actor context, status none/AI/actor - also the record for
   Steam's AI disclosure), race babble when a line has no audio, and lines with variables split into fixed pieces.
6. Full-motion video only for key moments (Mother's broadcast, first Anchor, first contacts, endings).

### Art: to check full screen
- Player ship scale in the system view: it may be too small to see its two turrets. Check full screen; if needed,
  enlarge it (Art.Ship size 1.1 in SystemView) or zoom the system camera.
- Possible: show turrets only for fitted weapons (empty mount ring for an unused slot).

### Artifacts and settlements (first version is in; still to do)
- Settlement dialogue for the Noeti, Murmur, Ophane and Thuum (their towns exist but can't talk yet), and Ink files for them.
- More quests for artifacts; uses for the clue keywords (warp-lattice, route-hum, void-chart, exodus, burial,
  they-made-it) in conversations, especially the Noeti (Act 2).
- Research stations (design doc) that analyse artifacts; minor artifacts as small curios with their own names.
- Station art for the Varkesh and Skarrow is ready but neither race has a starport yet.

