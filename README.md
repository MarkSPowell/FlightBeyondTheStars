# Flight Beyond the Stars - Stage 1 prototype

Press Play in any scene (or run: unity command editor.playmode start --project-path <this folder>). The game builds itself from code (Assets/FlightBeyondTheStars/Scripts/Core/GameManager.cs, Bootstrap).

## Controls
- Fly / drive / pan: WASD, arrow keys, or gamepad left stick
- Act (scan, land, collect, dock; at the lander: unload cargo, refuel rover): E, Space, or gamepad A
- Launch from a planet (at the lander): L or gamepad Y
- Galaxy map: M or gamepad View/Select
- Galaxy map: click a star to select it, mouse wheel to zoom
- Transitions (scan, land, launch, dock, undock, jump): skip with Space, E, Esc, click, or gamepad A/B.
  Turn them all off with the "Animations" button at the top right.

## Loop to test
Scan a planet near Haven -> land -> explore (the surface wraps around the planet and streams in sections) -> collect -> unload at the lander -> launch
-> fly to the Starport -> dock -> sell -> buy fuel -> open the galaxy map -> jump to Brightmarket (Orrun market, different prices).

## Tuning numbers
All numbers come from Design/Flight_Beyond_the_Stars_Systems_Values.xlsx.
After editing it in Excel (save it so formulas recalculate), re-export the JSON:

    python Tools/export_data.py Design/Flight_Beyond_the_Stars_Systems_Values.xlsx Assets/FlightBeyondTheStars/Resources/Data

(needs Python with openpyxl: pip install openpyxl)
