# Flight Beyond the Stars — Game Design Document

Nov 6, 2026 · @Mark

## Overview

The player captains a single starship through an open galaxy, exploring, trading, fighting and talking their way toward uncovering a galaxy-scale mystery — while building a home economy of outposts and colonies that feeds back into the ship.

**Inspiration.** Spiritual successor to Binary Systems' *Starflight* (1986): open-ended exploration, planet landings with a terrain vehicle, conversational diplomacy with distinct alien personalities, and a hidden central mystery. New in this design: an outpost/colony layer, a deep multi-species crew system, and a living trade economy. No names, races, plot or assets are taken from the original.

**Design pillars**

1. **Curiosity is rewarded.** Every system, planet and conversation can hold something useful — a clue, a coordinate, a rare mineral, a recruit.
2. **Talk is as powerful as weapons.** Most problems can be solved by diplomacy, trade or knowledge instead of combat.
3. **Your crew is your character.** Who you recruit, from which species, shapes what your ship is good at.
4. **The galaxy remembers.** Reputation, colonies and choices persist and change how the galaxy responds.
5. **Depth without busywork.** Management layers (outposts, colonies) run on their own once set up; the captain makes decisions, not clicks.

**Target.** Single-player on PC (Windows first; Mac/Linux if cheap) and Xbox Series X|S, designed controller-first so the same UI works at a desk and on a TV. Session length 30–90 minutes; full campaign 25–40 hours with an open-ended post-game. Starts as a solo project; the commercial goal is release through ID@Xbox and a place in Game Pass.

**Art direction.** Modern stylized 2D: clean shapes, strong silhouettes, rich lighting and color, readable on a TV.

**Homages and easter eggs.** The game is seeded with nods to classic space games and sci-fi series: a hidden scanner filter in a retro 16-color palette, derelicts shaped like famous ship silhouettes, planets and stars named for sci-fi authors, crew lines that riff on famous dialogue, and a nod to the original game's starting coordinates. Rule for a commercial release: homage, never copy — no trademarked names, logos, characters or direct quotes.

**Audience.** Fans of *Starflight*, *Star Control II*, *FTL*, *Starsector* and *Elite*-style sandboxes who want exploration and story over twitch skill.

## Working title candidates

**Working title: Flight Beyond the Stars** (chosen 2026-11-06, open to change). It has a warm, retro pulp-sci-fi feel, keeps the "flight" nod to the original, and echoes classic sci-fi film titles — fitting for a game full of homages.

**Name checks.** Farflight was dropped because a Steam game called [Far Flight](https://store.steampowered.com/app/3183930), a rogue-lite top-down space shooter, is too close. A quick search for "Flight Beyond the Stars" found no game with that name; the nearest titles are films (*Beyond the Stars*, 1989, and *Battle Beyond the Stars*, 1980). That is not trademark clearance — run a proper check before a store page or announcement. The title is long, so plan a short logo form (e.g. a stacked "FLIGHT / beyond the stars" lockup).

Other candidates considered:

| Title | Why it works | Watch out for |
| --- | --- | --- |
| Flight of the Stars | Reverses "Starflight"; the clearest signal to veteran fans | Close to the original's name; generic words are harder to search and trademark |
| Farflight | Short, ownable, works as a logo and store tile | Less obvious nod |
| Starward Flight | Keeps "flight", adds aspiration and motion | Slightly soft |
| The Long Flight | Fits the long voyage and the soft deadline | Generic |
| Flight Beyond the Stars | Retro pulp-sci-fi feel, a warm nod | Long for a logo |
| Starwake | The trail a ship leaves; can tie to a story threat "waking" | No direct nod to the original |
| Farflight: Captains of the Void | Distinct title plus a role-play subtitle | Two parts to market |

Before committing: search the USPTO trademark database, Steam, the Xbox store, and domain/social handle availability. A quick web search found no existing game called "Flight of the Stars", but that is not trademark clearance.

## Core gameplay loop

The player explores, gathers, trades and talks, then reinvests in the ship, crew and colonies so each expedition can go further than the last.

&#91;embedded content: core gameplay loop · 6 steps\]

The story runs across this loop: clues from conversations and ruins point to new destinations, so curiosity drives the player outward as much as profit does. Colonies close the loop late in the game by supplying fuel, ammo and upgrades directly.

## Starship

The ship is the player's home base and main progression track: every system has tiers (1–5, plus rare alien "exotic" variants) bought, built at colonies, or salvaged.

| System | What it does | Key stat | Upgraded by |
| --- | --- | --- | --- |
| Hull / armor | Absorbs damage after shields | Hit points, armor class | Starport, colony shipyards, salvage |
| Shields | Regenerating damage buffer | Capacity, recharge rate | Purchase, alien tech |
| Engines (in-system) | Speed and maneuvering | Thrust, turn rate | Purchase, Engineer tuning |
| Hyperdrive | Interstellar jumps | Jump range, fuel efficiency | Rare tech, artifacts |
| Weapons | Lasers, missiles, exotic | Damage, range, ammo | Purchase, colony munitions plants |
| Sensors | Scan planets, ships, anomalies | Range, detail level | Science tech, artifacts |
| Cargo hold | Minerals, goods, artifacts, specimens | Cargo units | Hull modules |
| Crew quarters | Max crew and passengers | Berths | Hull modules |
| Terrain vehicle bay | Planet surface exploration | Vehicle tier | Purchase, colony factories |
| Module slots | Optional fittings (lab, brig, cloak, mining laser, stasis pods) | Slot count | Hull class |

**Hull classes.** The player starts in a light survey frigate and can buy or earn larger hulls (corvette, explorer, freighter, cruiser). Each trades speed and fuel cost against slots, cargo and berths, so there is no single "best" ship.

**Fuel.** A single fuel resource (working name: "drive crystal") powers the hyperdrive and emergency shield boosts. Fuel can be bought, mined on certain worlds, or refined at outposts. Running low is a real tension, not a punishment: an emergency distress beacon exists at a steep cost.

**Damage and repair.** Systems take individual damage. Damaged systems underperform until the Engineer repairs them in flight (slow, uses spare parts) or a starport/colony repairs them (fast, costs credits).

## Crew

The ship has six bridge stations, and the crew member assigned to each sets that station's bonus; the strongest specialists can only be recruited from other species.

| Station | Core effect | Example skill unlocks |
| --- | --- | --- |
| Navigator | Jump fuel cost, jump accuracy, map reveal radius | Plot shortcuts through nebulae; detect hidden jump routes |
| Pilot | In-system speed, combat evasion, landing safety | Evasive maneuvers; safe landings on hostile worlds |
| Engineer | Repair speed, power output, system efficiency | Overcharge a system in combat; field-repair without parts |
| Weapons Officer | Accuracy, damage, reload, target subsystems | Called shots on enemy engines/weapons |
| Science Officer | Scan detail, artifact identification, life-form analysis | Reveal planet value from orbit; analyze artifacts faster |
| Communications Officer | Translation speed, persuasion, trade prices, hail range | Calm a hostile hail; unlock hidden dialogue topics |

**Captain and succession.** The captain is the player's avatar and also a crew member, with a species, skills and a Leadership bonus (morale and XP gain). The captain can die. The next officer in a player-set chain of command then takes command and play continues with the same ship, colonies and log; the new captain brings their own species traits and unlocks different story dialogue (a captain's species changes how hard story actions are, but never blocks an ending). **The game ends only when the entire crew is dead.**

**Specialist roles 7 and 8 (decided): away-team specialists.** Instead of more bridge stations, two roles that matter off the bridge:

- **Medical Officer** — treats injuries, stabilizes dying crew (a short window to save someone), cures alien diseases, boosts life-form research. A strong fit now that losing the whole crew is the fail state.
- **Security Chief** — leads away-team skirmishes and boarding actions, repels boarders, runs the brig for prisoners and bounties.

**Double duty.** A crew member can hold one bridge station plus one specialist role at a skill penalty, so a crew thinned by deaths stays playable.

**Skills and training.** Each crew member has a skill rating per role (0–250 as a reference scale). Training happens at starports or at a colony Academy and costs credits and time. Each species has a **max cap per role** — a species built for piloting might cap Pilot at 250 but Science at 100.

**Species traits.** Beyond caps, each species brings one trait: e.g. needs a special atmosphere (life-support cost), immune to radiation (safe on hot worlds), telepathic (bonus in diplomacy), or culturally offended by certain other species (morale clash).

**Experience.** Crew gain small amounts of XP by doing their job, so a veteran Navigator slowly outgrows their trained level up to the species cap.

**Recruitment.** In a standard game the captain and all starting crew are human: generalists with moderate caps, available everywhere. Every other species must be earned or bought; New Game+ unlocks all species as starting crew and captain. The best specialists come from other species, unlocked by reputation, quests, rescue or trade. Some legendary crew are unique named characters with personal quests.

**Morale and loyalty (suggested).** Simple morale meter per crew member affected by pay, species mix, combat losses and choices. Low morale reduces bonuses; very low morale can mean desertion at port.

## Travel and navigation

Travel has three scales — galaxy, system and orbit — and fuel, time and danger make every route a decision.

**Galaxy map (interstellar).** Real-time flight across a 2D starfield, as in *Starflight*, with the option to plot a jump. Jumps cost fuel based on distance and Navigator skill. Hazards between stars: nebulae (slow, block sensors, hide secrets), ion storms (damage shields), gravity flux zones (shortcuts for a skilled Navigator). Encounters with other ships can trigger mid-route.

**System view (in-system).** Fly between planets, moons, asteroid belts, stations and derelicts. Sensors reveal points of interest; each body can be scanned from range for a rough reading.

**Orbit.** Enter orbit to run a full scan (minerals, life, atmosphere, gravity, temperature, hazards), then decide whether to land, place an outpost, or flag for colonization.

**Starmap and logbook.** The map starts mostly unknown. It fills in by exploration, purchased star charts, and coordinates learned in conversation. The captain's log auto-records clues, rumors and coordinates with a searchable notes system — critical for a mystery-driven game. Players can pin their own notes to stars.

**Time and the soft deadline.** In-game time (stardate) advances with travel, landings and repairs. A story threat runs on a soft deadline: as it nears, the galaxy escalates (wars spread, regions turn dangerous, prices spike, allies grow desperate). Completing main missions and certain side tasks pushes the deadline back — for example by delaying the threat, winning an ally's help or rescuing a key specialist — so side content earns time rather than wasting it. The clock is shown in-world through news, crew warnings and visible changes, not as a raw countdown.

**Story tie-in.** The threat behind the deadline is Mother, an AI whose Anchors sever jump routes from the galaxy's rim inward. Act 3 adds a small second map: 10–15 systems in the Milky Way around Mother's core. Details in Flight Beyond the Stars — Storyline.

## Galaxy layout

The main galaxy has about 300 star systems: about 20 hand-crafted story systems and roughly 280 generated from a seed under design rules, so each new game feels fresh while the story stays intact.

**Scale.** At an average of 3–4 bodies per system, that is roughly 1,000 planets, moons and belts, comparable to the original's 800 planets. The Milky Way map adds 10–15 mostly hand-crafted systems for Act 3.

**Seeds.** Each new game rolls a galaxy seed. The same seed always builds the same galaxy, which helps bug reports, balancing and players sharing favorite galaxies. Hand-crafted systems also vary slightly per seed: position within their region, minor planets, mineral mix, encounter tables.

**Regions.** The galaxy divides into about 10 regions: one home territory per major race, the human home region, contested borderlands, a lawless frontier and the outer rim where Mother's Anchors first appear. Region sets star types, minerals, life chance, encounter tables and danger level.

**Jump routes.** Systems are linked by a network of jump routes: fast, fuel-efficient lanes. Free flight between stars is always possible but slow and fuel-hungry. This network is what Anchors sever, so the generator must create it explicitly.

**Hand-crafted systems (about 20)**

| Type | Count | Purpose |
| --- | --- | --- |
| Human home system and starport | 1 | Start, main hub |
| Major race homeworlds | 7 | Diplomacy, trade, recruitment, story |
| Firstborn vault sites | 4 | Act 2 discoveries, Anchor-jamming relics, the buried weapon |
| Noeti archive | 1 | Act 2 history (alternative: the hidden Firstborn society) |
| Hidden Firstborn society base | 1 | Alternative Act 2 path |
| First Anchor encountered | 1 | Act 1 revelation |
| Orrun Combine frontier station | 1 | Major trade hub and social space |
| Void-edge launch point | 1 | Act 3 crossing to the Milky Way |
| Signature anomalies (living ocean world, forbidden Zone, ancient supercomputer) | 3 | Lore, rare rewards, homages |

Smaller easter eggs (the refugee convoy, the towel, the arcade cabinet) are placed as rare events or seeded into generated systems rather than fixed locations.

**Generator rules**

- Every system is reachable from the start, and the route network has redundant paths so a few severed routes never strand the player permanently.
- Hand-crafted systems keep minimum distances from each other and sit in their intended regions.
- Difficulty and rarity rise with distance from the human home region.
- Fuel sources appear within reach of every region, so no generated galaxy is unwinnable.
- Star type drives planet types; planet type drives minerals, life and hazards.
- A validation pass rejects and rerolls galaxies that break these rules.

**Tooling.** Build a galaxy viewer early in development: generate seeds, inspect routes and regions, and check that every ending stays reachable.

## Planetary exploration

Landing sends a terrain vehicle onto a procedurally generated surface map, where the player gathers minerals, artifacts and life forms against the clock of fuel, cargo space and hazards.

**Landing.** Pilot skill and planet conditions (gravity, atmosphere, storms) set landing risk. Heavy-gravity or hostile worlds may need a better vehicle or a specific species on the away team.

**Terrain vehicle.** Upgradable rover with fuel, cargo, armor and a scanner. Away team of 1–3 crew can leave the vehicle on foot for ruins or caves.

**What can be found**

- **Minerals** — common to rare, sold or used to build outposts and upgrades. Distribution follows planet type (molten, rocky, ice, ocean, gas-moon).
- **Artifacts** — alien relics that are either sold, used as ship upgrades, traded to specific races, or are story clues. The Science Officer identifies them.
- **Life forms** — scanned or stunned and captured in stasis pods for research credits, colony farming stock, or trade to collectors. Some are dangerous.
- **Ruins and anomalies** — hand-placed story sites with puzzles, lore and unique rewards.
- **Derelicts and crash sites** — salvage, logs, and occasional survivors to recruit.

**Hazards.** Weather, quakes, hostile fauna, radiation, extreme heat/cold. Hazards damage the vehicle or injure crew, pushing the player to return to the ship in time.

**Survey value.** Every scanned planet adds to a survey record that can be sold to a cartography guild — a steady income for pure explorers.

## Communication and diplomacy

Conversation is a primary verb: every race can be hailed, and what you say, your posture and your reputation decide whether you get information, coordinates, trade or a fight.

**Where it happens.** Ship-to-ship hails, planetary hails from orbit, and face-to-face on inhabited worlds and stations.

**Posture.** As in *Starflight*, the captain picks a stance before speaking — Friendly, Obsequious, Hostile, plus a suggested **Curious** (asks more, slower to trust). Each race responds differently: one respects aggression, another flees from it.

**Topics.** The player can ask about: themselves, other races, the region, specific planets, artifacts, rumors, and story keywords learned elsewhere. Keywords picked up in one conversation unlock new questions elsewhere — the main driver of the mystery.

**Translation.** First contact starts garbled. Science/Comms skill and repeated contact improve translation from fragments to full text, which is itself a satisfying progression.

**Mother's transmissions.** From Act 1, rare unexplained messages ("Return to me.") arrive through damaged Anchors, derelict computers or crew nightmares. Logging and answering them is a dialogue thread that shapes which endings open up later.

**Reputation.** Each race tracks attitude toward the player (Hostile → Allied) and remembers events: who you attacked, what you traded, which of their enemies you helped.

**Outcomes.** Information and rumors, star map data, trade access, crew recruitment, quests, treaties (safe passage, colony rights), or combat.

**Writing note.** Each race needs a distinct voice, sentence style and sense of humor. This is where much of the game's personality will come from and should get dedicated writing effort.

## Combat

Combat is real-time top-down ship-to-ship that can be paused at any time to issue orders; crew skill matters more than reflexes, so a well-staffed ship can win even for a less twitchy player.

**Flow.** Encounter → hail or scan (optional) → fight, flee, or talk down. Fleeing is always possible but costs fuel and may damage engines.

**Player controls.** Pilot the ship, fire weapons, raise/lower shields, shunt power between engines/shields/weapons (Engineer amplifies this). Pause to issue orders.

**Crew effects.** Pilot = evasion and turn rate; Weapons = accuracy and subsystem targeting; Engineer = power and mid-fight repairs; Science = enemy weakness scan; Navigator = emergency jump-out.

**Weapon families.** Energy (no ammo, uses power), missiles (ammo, high burst), and exotic alien weapons with unique effects (EMP, tractor, drain shields).

**Enemy design.** Each race fights differently — swarms of small ships, a single fortress ship, cloaked ambushers, or ships that refuse to fight unless provoked. Mother's Anchors and drone fleets are the escalating threat across the campaign.

**Aftermath.** Salvage debris (cargo, components, occasionally survivors), reputation change with that race and its allies/enemies, and crew injuries that need medical time.

**Suggested: ground skirmishes.** Small, optional away-team encounters on planets (hostile fauna, guardians at ruins) resolved with simple tactics, so crew combat skills matter beyond the ship.

## Trade and economy

Prices differ by race, region and supply, so knowing who wants what is itself a kind of treasure the player earns through exploration and conversation.

**Markets.** Starports (the player's home port plus alien stations) buy and sell minerals, goods, fuel, ammo, components, crew and information. Each race has strong preferences — a race of collectors pays triple for artifacts, another refuses to buy anything from their rivals.

**Trade goods.** Raw minerals, refined materials, food and biologicals (from farming outposts), manufactured goods (from colonies), specimens, artifacts, star charts and survey data.

**Barter.** Some races don't use credits and trade only by barter or for favors, information or specific items.

**Dynamic supply (light touch).** Selling a lot of one good in one market lowers its price there for a while; events (wars, plagues, discoveries) shift demand. Simple enough to read, deep enough to reward route planning.

**Route collapse (story-driven).** As the story's threat severs jump routes, the economy worsens: fewer markets, longer hauls, higher prices and higher fuel use. Colonies, destroying Anchors and allies all counteract it — see Flight Beyond the Stars — Storyline.

**Licenses and contracts.** Optional guild contracts — delivery, survey, bounty, rescue — give steady goals between story beats.

**Currency sinks.** Ship upgrades, crew training and wages, repairs, fuel, colony construction. Late game needs sinks too: exotic tech, colony megaprojects, legendary recruits.

## Outposts and colonies

Outposts turn single planets into passive income, and colonies grow into production centers that supply the ship with fuel, ammo and upgrades — the player's long-term home economy.

**Outposts (early to mid game).** Built from orbit with a prefab kit bought at a starport.

| Outpost type | Placed on | Produces | Needs |
| --- | --- | --- | --- |
| Mining | Mineral-rich world or asteroid | Raw minerals | Power, periodic pickup |
| Farming / bio | World with compatible soil or water | Food, biologicals, specimens | Suitable climate, crew caretaker |
| Fuel refinery | World with fuel deposits | Drive fuel | Power |
| Research station | Anomaly or ruin site | Research points, artifact analysis | Science staff |
| Listening post | Strategic system | Sensor coverage, early warning, trade rumors | Power |

Outputs accumulate in storage until collected, or are shipped automatically once the player has a freighter route (mid game).

**Colonization (mid to late game).**

1. **Flag** a habitable world after a full scan (atmosphere, gravity, temperature, biosphere, hazards set its habitability score).
2. **Seed** it: deliver colonists, supplies and a colony kit; some worlds need terraforming tech first.
3. **Grow** it: population grows over in-game time based on habitability, food and safety.
4. **Build** it: spend materials on facilities — shipyard (ship upgrades), munitions plant (ammo), refinery (fuel), academy (crew training), trade hub (goods to sell), defense grid.
5. **Specialize** it: each colony picks a focus, so a network of colonies matters more than one big one.

**Risks.** Colonies and outposts can be raided, struck by disease or disaster, or contested by a race that claims the system — giving the player reasons to return and defend or negotiate.

**Management load.** A single colony screen with a few clear choices; no city-builder micromanagement. Colonies send messages when they need attention.

## Alien races framework

Plan for 8–10 major races plus a few minor ones; every race is defined on the same template so they plug into crew, trade, diplomacy and combat consistently. Names and lore will be set during storyline work — the archetypes below are placeholders.

**Race template:** appearance and biology · home region · personality and speech style · what they value / what offends them · preferred posture · trade wants and offers · combat style · crew role strengths and caps · species trait · relationship to other races · secret they know about the central mystery.

| Race (archetype) | Personality | Crew strength | Trade | Combat |
| --- | --- | --- | --- | --- |
| Orrun Combine (merchants) | Polite, mercenary | Comms, Navigator | Buys everything, pays for info | Avoids fights, hires escorts |
| Varkesh (warrior clans) | Honor-bound, blunt | Weapons, Pilot | Wants weapons, sells ore | Aggressive, respects hostility |
| Noeti (ancient scholars) | Cryptic, slow | Science (very high cap) | Collect artifacts | Rarely fight, very strong if they do |
| The Murmur (hive) | Alien logic, collective voice | Engineer | Barter only | Swarms of small ships |
| Skarrow (nomad scavengers) | Jumpy, funny | Engineer, Pilot | Sell salvage, black market | Hit and run |
| Ophane (isolationist theocracy) | Paranoid, formal | Navigator | Trade only when Allied | Defend borders hard |
| Thuum (gentle giants) | Slow, kind | Pilot on heavy-gravity worlds | Food, biologicals | Will not fight |
| Mother (antagonist AI) | Calm, patient, reasonable | Not recruitable | Bargains through proxy factions | Anchors, elegant drone fleets, repurposed alien hulks |

**Major race names (decided).** Orrun Combine, Varkesh, the Murmur, Skarrow, Ophane, Thuum and the **Noeti** (from the Greek for "mind"; homage to Master of Orion's research race). The Noeti homeworld, **Silpon Prime**, named for their legendary founding scientist Silpon, is the easter egg for fans. Full details live in Flight Beyond the Stars — Race Guide.

**Minor species.** Primitive natives on inhabited worlds (protect, uplift or exploit — a moral choice), plus wildlife that become specimens or hazards.

## Suggested additional features

These are additions beyond the original brief, ordered roughly by how much value they add per unit of work.

| Feature | Why it helps | Priority |
| --- | --- | --- |
| Captain's log with auto-recorded clues and searchable notes | Essential for a mystery; *Starflight* players kept paper notes | Must have |
| Communications/Diplomat crew role | Conversation is a core pillar and deserves its own stat | Must have |
| Research / tech tree fed by artifacts, specimens and survey data | Gives science-focused play a progression path | Must have |
| Reputation and faction relationships web | Makes choices matter; enables alliances and wars | Must have |
| Crew morale, loyalty and personal quests | Turns crew from stat sticks into characters | Should have |
| Escort / wingman ships (hire or build at colonies) | Late-game power growth without one giant ship | Should have |
| Galactic events (wars, plagues, migrations, solar flares) | Keeps the galaxy feeling alive between story beats | Should have |
| Derelicts, salvage and boarding | Adds mid-scale adventure between landing and combat | Should have |
| Multiple difficulty settings and an "explorer mode" with gentle combat | Widens audience | Should have |
| Ironman / permadeath option | Replayability for veterans | Could have |
| Procedural galaxy seed with hand-placed story anchors | Replay without losing the authored mystery | Could have |
| Codex / xenopedia of races, life forms and minerals | Rewards collecting and doubles as a tutorial | Could have |
| Mod support for races, ships and items | Long tail for a niche audience | Later |
| New Game+ | Unlock every species as starting crew and captain, and carry knowledge (not items) into a new run, like the original's notes-driven replays | Later |

## Progression, scope and open questions

**Progression arc**

- **Early game (hours 0–5):** small ship, human crew, local region. Learn landing, scanning, trading, first contact. First outpost.
- **Mid game (5–20):** first alien recruits, better hull, outposts network, first colony, the mystery's shape emerges, regional conflicts.
- **Late game (20–40):** specialist crew near caps, exotic tech, multiple colonies, escorts, full access to the galaxy, climax of the story, including the crossing to the Milky Way map.
- **Post-game:** open sandbox — finish colonies, legendary crew, uncharted regions.

**UI screens.** Bridge (hub with station buttons, like the original's crew-station menu) · galaxy map · system view · orbit/scan · surface · combat · comms · trade · crew roster/training · cargo · colony management · captain's log · codex.

### Platforms and technology

**Recommendation: Unity 6 with C#, on a free Unity Personal license through PC development, upgrading to Unity Pro when console work starts.** Personal covers individuals and small teams under $200K in revenue and funding over the last 12 months. Moving to Pro is a license change on the same project, while switching engines later would be close to a rewrite — so the starting engine is effectively the console plan.

| Engine | Xbox console path | Cost for a solo dev | Fit |
| --- | --- | --- | --- |
| **Unity 6 (chosen)** | Official Xbox support; same project from PC to console | Personal is free during development; [Unity Pro required](https://unity.com/products/unity-personal%20) to build for Xbox (12-month commitment, no indie discount, check current price) | Smoothest console path; more Xbox shipping experience, tutorials and assets |
| Godot 4 (C#) | No first-party console export; licensed through [W4 Consoles](https://www.w4games.com/w4consoles) or a porting partner | Engine free; W4 is a paid subscription, and letting it lapse blocks publishing updates | Lighter for 2D and fully open source, but console porting depends on a third party |

**Trade-offs accepted.** Unity's 2023 per-install fee attempt (later withdrawn) shows its license terms can change, unlike Godot's MIT license; Pro is a recurring cost once on console; and Unity is heavier than Godot for pure 2D. Mitigation: keep content in data files and game logic in plain C# where practical, so the project isn't more locked in than it needs to be.

**Build for console from day one**

- Controller-first UI: radial menus for bridge stations, no small mouse-only targets, text readable at TV distance.
- Captain's log notes through tags, pins and an on-screen keyboard rather than free typing alone.
- Performance budget set by Xbox Series S, the weaker console.
- Save system ready for cloud saves; safe to suspend and resume at any moment.
- Achievements planned early: exploration, diplomacy, colonies, crew.
- Accessibility per Microsoft's Xbox Accessibility Guidelines: remapping, text size, colorblind palettes, subtitles, adjustable combat speed.
- Data-driven content (races, items, planets in JSON/YAML) and dialogue in Ink or Yarn Spinner.

**Path to Game Pass**

1. Ship a polished vertical slice on PC: Steam page, demo, festival builds, wishlists.
2. Register with ID@Xbox and submit a concept with its submission template; [Game Pass, business development and ID@Xbox teams review these together](https://news.xbox.com/en-us/2023/03/21/id-xbox-10-year-anniversary/).
3. With a deal or traction, upgrade to Unity Pro and go through Xbox certification.
4. Consider Xbox Play Anywhere so one purchase covers PC and console.

**MVP roadmap (vertical slice first)**

1. **Slice 1 — Fly and land (with a first galaxy generator):** galaxy map, one system, orbit scan, terrain vehicle collecting minerals, sell at starport.
2. **Slice 2 — Talk and fight:** two alien races with conversation, posture, reputation and basic combat.
3. **Slice 3 — Crew:** six stations, three species, training and caps affecting the systems above.
4. **Slice 4 — Build:** outposts, then one colony with two facilities.
5. **Slice 5 — Story:** captain's log, keyword system and the first act of the mystery.
6. **Content scale-up:** remaining races, regions, artifacts, events; balance; polish.

**Decided**

- Platforms: PC and Xbox Series X|S; goal of ID@Xbox release and Game Pass.
- Soft deadline, extended by completing missions and side tasks.
- Six bridge stations, including Communications, plus two away-team specialists: Medical Officer and Security Chief.
- The captain can die; the game is over only when the whole crew is dead.
- Real-time combat, pausable at any time.
- Engine: Unity 6 with C# (Personal now, Pro for console).
- Modern stylized 2D art with homages and easter eggs.
- Solo project first, commercial later.
- Working title: Flight Beyond the Stars.
- Story: an AI named Mother hunts humanity's ancestors, the Firstborn, by shutting down routes with Anchors; six endings, all at launch.

**Open questions**

- Confirm Flight Beyond the Stars after a trademark check (see Working title candidates).
- Add Medical Officer and Security Chief as away-team specialists?
- Budget and timing for the Unity Pro upgrade.
- Story: in progress in Flight Beyond the Stars — Storyline.
- Default difficulty and whether ironman is an option or a mode.
- Mac/Linux support at launch?
