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
Testing aids (editor only): F10 skips 30 days ahead. F7 (in a star system, not docked) spawns the next kind of ship from
the Encounters tab - trader, Varkesh patrol, Skarrow, silent ship - replacing any current contact.
The top of orrun.ink lists the functions and variables the game provides.
