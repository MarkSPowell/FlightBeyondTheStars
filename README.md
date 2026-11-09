# Flight Beyond the Stars - prototype (stage 2 in progress)

Press Play in any scene (or run: unity command editor.playmode start --project-path <this folder>). The game builds itself from code (Assets/FlightBeyondTheStars/Scripts/Core/GameManager.cs, Bootstrap).

## Controls
- Fly / drive / pan: WASD, arrow keys, or gamepad left stick
- Act (scan, land, collect, dock; at the lander: unload cargo, refuel rover): E, Space, or gamepad A
- Launch from a planet (at the lander): L or gamepad Y
- Galaxy map: M or gamepad View/Select
- Galaxy map: click a star to select it, mouse wheel to zoom
- Transitions (scan, land, launch, dock, undock, jump): skip with Space, E, Esc, click, or gamepad A/B.
  Turn them all off with the "Animations" button at the top right.

- Hail: H or gamepad X. Docked at a starport with a dialogue (Brightmarket), or flying near an inhabited world
  (Orrun homeworld at Brightmarket; Varkesh homeworld in their home system).
  In a conversation: choose with 1-9, click, or up/down plus E / Space / A. Change posture with Tab (gamepad LB/RB); the next hail opens with the same posture.
  Close the channel with Esc or gamepad B.

- Ship encounters: arriving by jump may bring a contact (traders, patrols, scavengers, and later silent ships).
  Close in to hail (H) or attack (F). Some ships hail you first.
- Combat (real time, pausable): fly with WASD; your Weapons Officer fires automatically at the target.
  Tab or click to change target, F to hold or free fire, H to hail (ransom, yield, truce), P or gamepad Start to pause.
  To flee, open a gap of 30 from every ship chasing you and hold it for 3 seconds, or press J (gamepad left trigger)
  for an emergency jump: it takes a few seconds to spool (faster with a better Navigator) and burns fuel. Repairs, weapons, shields and Weapons Officer training are at starports.

- Crew: C or the Crew button opens the crew screen (pauses the game). Assign people to stations (one person can
  cover two, at 25% less effectiveness each), choose a trait when someone levels up, see health and morale.
  Crew learn by doing their station's job; every level (30 experience, then 15 more per level) offers 3 traits.
  At starports: hire crew (humans at Haven), shore leave (+morale, once a week), training, and morale modules
  (Ship's Galley, Recreation Deck). Crew whose morale falls too low leave at the next port; the critically
  wounded may be saved by the Medical Officer after a fight. The game ends only if the whole crew is lost.

- Artifacts: I or the Artifacts button. Major artifacts turn up (rarely) in ruins, unidentified; the Science Officer
  studies one at a time as days pass (Study days x 100 / Science skill), and a Noeti aboard identifies them on sight.
  Major artifacts appear from Act 2 (for now: once the rim goes dark, threat stage 2); before that, ruins give
  minor artifacts instead. Identified, they can be installed (ship upgrades), add captain's log entries and keywords (clues), sell for more to
  the races that prize them, or be handed over in conversations (the Founding Seal to the Orrun Ledger Council, the
  Sigil of the First Watch to Clanmaster Ashquill). Data: the spreadsheet's Artifacts tab.

- Settlements: home worlds have towns of their race, at the city lights the landing map shows (planet map: M).
  Drive up to one to talk to the locals (H) or trade: they buy from the rover and the ship's hold at 85% of a
  starport's prices, buy artifacts, and sell rover fuel. Humans, Orrun and Varkesh locals talk; other races
  answer in languages your Comms Officer can't follow yet.

- Save: F5 or the Save button (quick save). Load: F9 (asks first) or the Load button. Autosaves on docking and on arriving in a system.
  Saves live in Unity's persistentDataPath/saves (on Windows: %USERPROFILE%\AppData\LocalLow\<company>\FlightBeyondTheStars\saves).

## Loop to test
Scan a planet near Haven -> land -> explore (the surface wraps around the planet and streams in sections) -> collect -> unload at the lander -> launch
-> fly to the Starport -> dock -> sell -> buy fuel -> open the galaxy map -> jump to Brightmarket (Orrun market, different prices).

## Tuning numbers
All numbers come from Design/Flight_Beyond_the_Stars_Systems_Values.xlsx.
After editing it in Excel (save it so formulas recalculate), re-export the JSON:

    python Tools/export_data.py Design/Flight_Beyond_the_Stars_Systems_Values.xlsx Assets/FlightBeyondTheStars/Resources/Data

(needs Python with openpyxl: pip install openpyxl)

## Dialogue
Conversations are written in Ink (https://www.inklestudios.com/ink/), installed as the
com.inkle.ink-unity-integration package. Each race has one file in Assets/FlightBeyondTheStars/Resources/Dialogue,
named after the first word of the race (orrun.ink). The Ink plugin (2.0) compiles it whenever Unity imports it
and keeps the result inside the .ink asset; there are no separate .json files. Compile errors show in the Console. You can also test a story without running the game in the Ink Player window that the plugin adds.

World events (spreadsheet Events tab) are things races can comment on once they've heard of them: heard("stage-2")
in Ink. News spreads from where the event happened at NewsSpeed ly/day; Broadcast events reach everyone at once.
Threat-stage events fire automatically by day; game code fires the rest with WorldEvents.Fire.
Testing aids (editor only): F10 skips 30 days ahead. F8 brings aboard a random unfound artifact; Shift+F8 identifies
everything aboard. F7 (in a star system, not docked) spawns the next kind of ship from
the Encounters tab - trader, Varkesh patrol, Skarrow, silent ship - replacing any current contact.
The top of orrun.ink lists the functions and variables the game provides.
