using System.Collections.Generic;
using UnityEngine;

namespace FBTS
{
    /// <summary>
    /// Planet surface: a whole wrap-around world streamed in sections. Drive the terrain vehicle, collect deposits,
    /// unload and refuel at the lander, call the lander to you, and launch when done.
    /// </summary>
    public class SurfaceView : View
    {
        class Deposit
        {
            public long key;
            public Vector2 pos;
            public string commodity;
            public int amount;
            public SpriteRenderer sprite;
        }

        class Section
        {
            public int cx, cy;           // unwrapped column, row
            public GameObject go;
            public Texture2D tex;
            public readonly List<Deposit> deposits = new List<Deposit>();
        }

        const int SW = 48, SH = 30;              // tiles per section
        const float SecondsPerHour = 8f;         // rover fuel is in hours; 1 hour = 8 s of driving
        const float PickupCost = 500f;
        const float RoverRefuelShipFuel = 1f;    // ship fuel to refill the rover from empty

        readonly Planet planet;
        readonly Vector2 returnSpawn;
        readonly Dictionary<long, Section> sections = new Dictionary<long, Section>();
        readonly Dictionary<string, int> roverCargo = new Dictionary<string, int>();
        readonly Queue<Vector2Int> buildQueue = new Queue<Vector2Int>();

        Transform rover, lander;
        SpriteRenderer compass;
        Vector2 landing;                          // unwrapped world position of the lander
        float roverFuel, roverFuelMax;
        int roverCapacity;
        Deposit nearDeposit;
        bool nearLander;
        long lastSectionKey = long.MinValue;
        bool firstSection = true;
        float ox, oy;

        public SurfaceView(Planet p, Vector2 spawnBack)
        {
            planet = p;
            returnSpawn = spawnBack;
        }

        int Around => planet.SectionsAround;
        float Circumference => Around * SW;
        float WorldHeight => Planet.SectionsTall * SH;

        int RoverLoad
        {
            get { int n = 0; foreach (var kv in roverCargo) n += kv.Value; return n; }
        }

        // ---------------------------------------------------------------- Setup

        public override void Enter()
        {
            Cam.orthographicSize = 8f;
            ox = (planet.seed % 1000) * 0.37f;
            oy = (planet.seed / 1000 % 1000) * 0.53f;

            landing = new Vector2(SW * 0.5f, WorldHeight * 0.5f);

            lander = Draw.Ship("Lander", root, new Color(0.4f, 0.9f, 1f), 1.2f);
            lander.localPosition = landing;
            lander.localRotation = Quaternion.Euler(0, 0, 90);

            rover = Draw.Ship("Rover", root, new Color(1f, 0.9f, 0.3f), 0.6f);
            rover.localPosition = landing + new Vector2(0f, -1.5f);

            compass = Draw.Dot("Compass", root, rover.localPosition, 0.3f, new Color(0.4f, 0.9f, 1f, 0.9f), 25);

            var fuelRow = GameData.ShipSystem("Terrain vehicle", "Fuel");
            var cargoRow = GameData.ShipSystem("Terrain vehicle", "Cargo");
            roverFuelMax = fuelRow != null ? fuelRow.Value(S.terrainVehicleTier) : 6f;
            roverCapacity = cargoRow != null ? Mathf.RoundToInt(cargoRow.Value(S.terrainVehicleTier)) : 10;
            roverFuel = roverFuelMax;

            // Build the starting area immediately; everything else streams in as the rover moves.
            var start = SectionAt(rover.localPosition);
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    BuildSection(start.x + dx, start.y + dy);

            gm.Follow(rover.localPosition, 0f);
            gm.Say($"Landed on {planet.name}. Explore, collect deposits, and return to the lander to unload or refuel.");
        }

        public override void Exit()
        {
            foreach (var s in sections.Values) if (s.tex != null) Object.Destroy(s.tex);
            sections.Clear();
            base.Exit();
        }

        // ---------------------------------------------------------------- World coordinates

        int WrapCol(int cx) => ((cx % Around) + Around) % Around;
        float WrapDx(float dx) => dx - Mathf.Round(dx / Circumference) * Circumference;
        static long Key(int col, int row) => ((long)col << 16) | (uint)row;

        Vector2Int SectionAt(Vector2 p) => new Vector2Int(Mathf.FloorToInt(p.x / SW), Mathf.Clamp(Mathf.FloorToInt(p.y / SH), 0, Planet.SectionsTall - 1));

        /// <summary>Latitude in degrees (-72..72) and longitude (-180..180) for the panel readout.</summary>
        Vector2 LatLong(Vector2 p)
        {
            float lat = (p.y / WorldHeight - 0.5f) * 144f;
            float x = ((p.x % Circumference) + Circumference) % Circumference;
            float lon = x / Circumference * 360f - 180f;
            return new Vector2(lat, lon);
        }

        /// <summary>Seamless terrain value: samples noise on a cylinder so the planet wraps east-west.</summary>
        float TerrainValue(float x, float y)
        {
            float u = x / Circumference * Mathf.PI * 2f;
            float r = 0.12f * Circumference / (Mathf.PI * 2f);
            float a = Mathf.PerlinNoise(ox + Mathf.Cos(u) * r, oy + y * 0.12f);
            float b = Mathf.PerlinNoise(ox + 50f + Mathf.Sin(u) * r, oy + 50f + y * 0.12f);
            float fine = (a + b) * 0.5f;
            float c = Mathf.PerlinNoise(ox + 200f + Mathf.Cos(u) * r * 0.15f, oy + y * 0.02f);
            float d = Mathf.PerlinNoise(ox + 300f + Mathf.Sin(u) * r * 0.15f, oy + 300f + y * 0.02f);
            float broad = (c + d) * 0.5f;
            float n = fine * 0.65f + broad * 0.35f;
            return Mathf.Clamp01((n - 0.5f) * 2.2f + 0.5f);
        }

        Color TerrainColor(float n, float lat)
        {
            Color c;
            switch (planet.type)
            {
                case PlanetType.Molten:
                    c = n > 0.62f ? Color.Lerp(new Color(0.9f, 0.3f, 0.05f), new Color(1f, 0.7f, 0.2f), (n - 0.62f) * 2.5f)
                                  : Color.Lerp(new Color(0.15f, 0.08f, 0.07f), new Color(0.32f, 0.16f, 0.12f), n);
                    return c; // no ice caps on molten worlds
                case PlanetType.Ice:
                    c = Color.Lerp(new Color(0.55f, 0.68f, 0.82f), new Color(0.9f, 0.95f, 1f), n);
                    break;
                case PlanetType.Ocean:
                    c = n > 0.55f ? Color.Lerp(new Color(0.22f, 0.45f, 0.22f), new Color(0.4f, 0.6f, 0.3f), n)
                                  : Color.Lerp(new Color(0.05f, 0.18f, 0.45f), new Color(0.12f, 0.35f, 0.65f), n);
                    break;
                default:
                    c = Color.Lerp(new Color(0.35f, 0.29f, 0.24f), new Color(0.65f, 0.56f, 0.44f), n);
                    break;
            }
            float polar = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(48f, 70f, Mathf.Abs(lat)));
            return Color.Lerp(c, new Color(0.92f, 0.95f, 1f), polar * 0.85f);
        }

        // ---------------------------------------------------------------- Streaming

        void BuildSection(int cx, int cy)
        {
            if (cy < 0 || cy >= Planet.SectionsTall) return;
            long k = Key(cx, cy); // unwrapped key: a section on the far side of the wrap is a separate copy
            if (sections.ContainsKey(k)) return;

            var sec = new Section { cx = cx, cy = cy };
            sec.go = new GameObject($"Section {WrapCol(cx)},{cy}");
            sec.go.transform.SetParent(root, false);

            var tex = new Texture2D(SW, SH, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[SW * SH];
            for (int y = 0; y < SH; y++)
                for (int x = 0; x < SW; x++)
                {
                    float wx = cx * SW + x + 0.5f, wy = cy * SH + y + 0.5f;
                    px[y * SW + x] = TerrainColor(TerrainValue(wx, wy), LatLong(new Vector2(wx, wy)).x);
                }
            tex.SetPixels(px);
            tex.Apply();
            sec.tex = tex;
            var sprite = Sprite.Create(tex, new Rect(0, 0, SW, SH), Vector2.zero, 1f);
            Draw.MakeSprite("Terrain", sec.go.transform, sprite, new Vector2(cx * SW, cy * SH), Vector2.one, Color.white, 0);

            // Deposits are seeded by the wrapped section, so the same spot always holds the same thing.
            int col = WrapCol(cx);
            var rng = new System.Random(planet.seed ^ (col * 73856093) ^ (cy * 19349663));
            int count = 5 + rng.Next(5);
            for (int i = 0; i < count; i++)
            {
                var pos = new Vector2(cx * SW + 1 + (float)rng.NextDouble() * (SW - 2), cy * SH + 1 + (float)rng.NextDouble() * (SH - 2));
                double roll = rng.NextDouble();
                string commodity = planet.minerals.Count == 0 ? "Iron ore"
                    : roll < 0.5 ? planet.minerals[0]
                    : roll < 0.85 ? planet.minerals[Mathf.Min(1, planet.minerals.Count - 1)]
                    : planet.minerals[planet.minerals.Count - 1];
                int amount = 1 + rng.Next(4);
                long dkey = (Key(col, cy) << 8) | (uint)i;
                if (planet.minedDeposits.Contains(dkey)) continue;

                var c = GameData.Commodity(commodity);
                float value = c != null ? c.basePrice : 10f;
                Color colr = value < 20 ? new Color(0.75f, 0.75f, 0.75f) : value < 60 ? new Color(1f, 0.65f, 0.2f) : new Color(0.3f, 1f, 0.95f);
                var d = new Deposit { key = dkey, pos = pos, commodity = commodity, amount = amount };
                d.sprite = Draw.Dot("Deposit", sec.go.transform, pos, 0.6f, colr, 10);
                sec.deposits.Add(d);
            }
            sections[k] = sec;
        }

        void Stream(Vector2 roverPos)
        {
            var here = SectionAt(roverPos);

            // Announce sections the crew hasn't seen before.
            long wrappedKey = Key(WrapCol(here.x), here.y);
            if (wrappedKey != lastSectionKey)
            {
                lastSectionKey = wrappedKey;
                if (planet.visitedSections.Add(wrappedKey) && !firstSection)
                    gm.Toast("Scanning new terrain...", 1.6f);
                firstSection = false;
            }

            // Queue the 3x3 neighbourhood; build at most one section per frame to avoid hitches.
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int cx = here.x + dx, cy = here.y + dy;
                    if (cy < 0 || cy >= Planet.SectionsTall || sections.ContainsKey(Key(cx, cy))) continue;
                    var v = new Vector2Int(cx, cy);
                    if (!buildQueue.Contains(v)) buildQueue.Enqueue(v);
                }
            if (buildQueue.Count > 0)
            {
                var next = buildQueue.Dequeue();
                BuildSection(next.x, next.y);
            }

            // Unload sections more than two away.
            List<long> drop = null;
            foreach (var kv in sections)
            {
                var s = kv.Value;
                if (Mathf.Abs(s.cx - here.x) > 2 || Mathf.Abs(s.cy - here.y) > 2)
                    (drop ??= new List<long>()).Add(kv.Key);
            }
            if (drop != null)
                foreach (var k in drop)
                {
                    Object.Destroy(sections[k].go);
                    Object.Destroy(sections[k].tex);
                    sections.Remove(k);
                }
        }

        // ---------------------------------------------------------------- Frame update

        Vector2 LanderShown(Vector2 roverPos) => new Vector2(roverPos.x + WrapDx(landing.x - roverPos.x), landing.y);

        public override void Tick(float dt)
        {
            Vector2 move = Controls.Move();
            Vector2 pos = rover.localPosition;
            if (roverFuel > 0f && move.sqrMagnitude > 0f)
            {
                float speed = 4f / Mathf.Lerp(0.8f, 1.6f, Mathf.Clamp01(planet.gravity / 2f));
                pos += move * speed * dt;
                pos.y = Mathf.Clamp(pos.y, 0.5f, WorldHeight - 0.5f); // poles are impassable; east-west wraps
                rover.localPosition = pos;
                rover.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(move.y, move.x) * Mathf.Rad2Deg);
                roverFuel = Mathf.Max(0f, roverFuel - move.magnitude * dt / SecondsPerHour);
            }
            gm.Follow(pos, 6f);
            Stream(pos);

            // The lander is drawn at whichever wrapped copy is nearest the rover.
            Vector2 shown = LanderShown(pos);
            lander.localPosition = shown;
            Vector2 toLander = shown - pos;
            compass.enabled = toLander.magnitude > 6f;
            compass.transform.localPosition = pos + toLander.normalized * 1.3f;

            nearDeposit = null;
            float best = 0.9f;
            foreach (var s in sections.Values)
                foreach (var d in s.deposits)
                {
                    float dist = Vector2.Distance(pos, d.pos);
                    if (d.sprite != null) d.sprite.enabled = planet.scanned || dist < 4f;
                    if (dist < best) { best = dist; nearDeposit = d; }
                }
            nearLander = toLander.magnitude < 1.6f;

            if (Controls.Action())
            {
                if (nearDeposit != null) Collect(nearDeposit);
                else if (nearLander && RoverLoad > 0) Unload();
                else if (nearLander && roverFuel < roverFuelMax) RefuelRover();
            }
            if (nearLander && Controls.Secondary()) Launch();
        }

        // ---------------------------------------------------------------- Actions

        void Collect(Deposit d)
        {
            int space = roverCapacity - RoverLoad;
            if (space <= 0) { gm.Say("Rover hold is full. Unload at the lander."); return; }
            int take = Mathf.Min(space, d.amount);
            roverCargo.TryGetValue(d.commodity, out int have);
            roverCargo[d.commodity] = have + take;
            d.amount -= take;
            gm.Say($"Collected {take} {d.commodity}.");
            if (d.amount <= 0)
            {
                planet.minedDeposits.Add(d.key);
                Object.Destroy(d.sprite.gameObject);
                foreach (var s in sections.Values) s.deposits.Remove(d);
            }
        }

        void Unload()
        {
            int stored = 0;
            var full = new List<string>();
            foreach (var kv in new List<KeyValuePair<string, int>>(roverCargo))
            {
                int added = S.AddCargo(kv.Key, kv.Value);
                stored += added;
                if (added >= kv.Value) roverCargo.Remove(kv.Key);
                else { roverCargo[kv.Key] = kv.Value - added; full.Add(kv.Key); }
            }
            gm.Say(full.Count == 0
                ? $"Unloaded {stored} units into the ship's hold."
                : $"Unloaded {stored} units. Ship hold is full; {string.Join(", ", full)} stays in the rover.");
        }

        void RefuelRover()
        {
            float missing = (roverFuelMax - roverFuel) / roverFuelMax; // 0..1 of a tank
            float shipFuel = missing * RoverRefuelShipFuel;
            if (S.fuel < shipFuel) { gm.Say("Not enough ship fuel to refuel the rover."); return; }
            S.fuel -= shipFuel;
            roverFuel = roverFuelMax;
            gm.Say($"Rover refuelled using {shipFuel:0.00} ship fuel.");
        }

        float RelocateCost => Mathf.Ceil(planet.gravity);

        /// <summary>Fly the lander to the rover, for big planets or an empty rover tank.</summary>
        void CallLander(bool emergency)
        {
            if (!emergency && !S.landerAutopilot) { gm.Say("Calling the lander needs the Lander Autopilot upgrade."); return; }
            if (!emergency && S.fuel < RelocateCost) { gm.Say($"Moving the lander needs {RelocateCost:0} ship fuel."); return; }
            Vector2 roverPos = rover.localPosition;
            Vector2 from = LanderShown(roverPos);
            Vector2 to = roverPos + new Vector2(2f, 0f);
            SpriteRenderer flame = null;

            gm.Play(new Cutscene
            {
                duration = 3f,
                caption = t => emergency ? "Emergency pickup inbound" : "Lander relocating",
                onStart = r => flame = Draw.Dot("Flame", r, from, 0.5f, new Color(1f, 0.6f, 0.2f, 0.9f), 19),
                onUpdate = (t, dt) =>
                {
                    float a = Cutscene.Smooth(t);
                    float hop = Mathf.Sin(t * Mathf.PI);
                    Vector2 p = Vector2.Lerp(from, to, a);
                    lander.localPosition = p;
                    lander.localScale = Vector3.one * (1f - 0.4f * hop); // smaller = higher above the ground
                    flame.transform.localPosition = p + new Vector2(0f, -0.9f * lander.localScale.x);
                    flame.transform.localScale = Vector3.one * (0.4f + 0.2f * Mathf.Sin(t * 60f));
                    gm.Follow(Vector2.Lerp(roverPos, p, 0.5f), 0f);
                },
                onComplete = () =>
                {
                    if (emergency) S.credits -= PickupCost; else S.fuel -= RelocateCost;
                    landing = to;
                    lander.localPosition = to;
                    lander.localScale = Vector3.one;
                    if (emergency) roverFuel = roverFuelMax * 0.25f;
                    gm.Say(emergency ? $"Pickup complete ({PickupCost:N0} cr). Rover given a quarter tank." : "Lander has set down beside the rover.");
                }
            });
        }

        void Launch()
        {
            Vector2 roverStart = rover.localPosition;
            Vector2 site = LanderShown(roverStart);
            float startSize = Cam.orthographicSize;
            SpriteRenderer flame = null;

            gm.Play(new Cutscene
            {
                duration = 3.5f,
                caption = t => t < 0.25f ? "Rover docking with lander" : "Launching to orbit",
                onStart = r => flame = Draw.Dot("Flame", r, site, 0.5f, new Color(1f, 0.6f, 0.2f, 0.9f), 19),
                onUpdate = (t, dt) =>
                {
                    float a = Cutscene.Smooth(Cutscene.Phase(t, 0f, 0.25f));
                    rover.localPosition = Vector2.Lerp(roverStart, site, a);
                    rover.gameObject.SetActive(a < 1f);

                    float b = Cutscene.Smooth(Cutscene.Phase(t, 0.25f, 1f));
                    Vector2 lp = site + new Vector2(0f, b * 10f);
                    lander.localPosition = lp;
                    float scale = Mathf.Lerp(1f, 0.5f, b);
                    lander.localScale = Vector3.one * scale;
                    flame.transform.localPosition = lp + new Vector2(0f, -0.9f * scale);
                    flame.transform.localScale = Vector3.one * (b > 0f ? (0.5f + 0.25f * Mathf.Sin(t * 60f)) * scale * 1.4f : 0f);

                    Cam.orthographicSize = Mathf.Lerp(startSize, 14f, b);
                    gm.Follow(Vector2.Lerp(site, lp, 0.6f), 0f);
                    gm.FadeColor = Color.black;
                    gm.FadeAlpha = Cutscene.Phase(t, 0.8f, 1f);
                },
                onComplete = CompleteLaunch
            });
        }

        void CompleteLaunch()
        {
            if (RoverLoad > 0) Unload(); // anything still in the rover goes aboard on the way up
            gm.FadeColor = Color.black;
            gm.FadeAlpha = 1f;
            gm.Go(new SystemView(returnSpawn));
            gm.FadeIn(0.8f);
        }

        // ---------------------------------------------------------------- GUI

        public override void WorldGui()
        {
            gm.WorldLabel(lander.localPosition, "Lander", 22);
        }

        public override void Gui()
        {
            Vector2 pos = rover.localPosition;
            Vector2 ll = LatLong(pos);
            Vector2 toLander = LanderShown(pos) - pos;

            GUILayout.Label(planet.name, gm.Header);
            GUILayout.Label($"{planet.type}, {planet.gravity:0.0} g. Hazard: {planet.hazard}", gm.Label);
            GUILayout.Label($"Position: {Mathf.Abs(ll.x):0}°{(ll.x >= 0 ? "N" : "S")}  {Mathf.Abs(ll.y):0}°{(ll.y >= 0 ? "E" : "W")}   " +
                            $"Sections explored: {planet.visitedSections.Count}/{Around * Planet.SectionsTall}", gm.Hint);
            if (!planet.scanned) GUILayout.Label("Not scanned from orbit: deposits only show when the rover is close.", gm.Hint);
            GUILayout.Label("Drive: WASD / stick.  Collect, unload, refuel: E / A.  Launch at lander: L / Y.", gm.Hint);
            GUILayout.Space(6);

            GUILayout.Label($"Rover fuel: {roverFuel:0.0} / {roverFuelMax:0} hours", gm.Label);
            GUILayout.Label($"Rover hold: {RoverLoad} / {roverCapacity}", gm.Label);
            foreach (var kv in roverCargo) GUILayout.Label($"  {kv.Key} x{kv.Value}", gm.Label);
            GUILayout.Label($"Ship hold: {S.CargoUsed:0} / {S.CargoCapacity:0}", gm.Label);
            GUILayout.Label(nearLander ? "At the lander." : $"Lander: {toLander.magnitude:0} km {Heading(toLander)}", gm.Label);
            GUILayout.Space(6);

            if (nearDeposit != null)
            {
                GUILayout.Label($"Deposit: {nearDeposit.commodity} ({nearDeposit.amount})", gm.Label);
                if (gm.Button("Collect (E)")) Collect(nearDeposit);
            }

            if (nearLander)
            {
                if (gm.Button($"Unload cargo to ship ({RoverLoad})", RoverLoad > 0)) Unload();
                float need = (roverFuelMax - roverFuel) / roverFuelMax * RoverRefuelShipFuel;
                if (gm.Button($"Refuel rover ({need:0.00} ship fuel)", roverFuel < roverFuelMax && S.fuel >= need)) RefuelRover();
                if (gm.Button("Launch to orbit (L)")) Launch();
            }
            else if (S.landerAutopilot)
            {
                if (gm.Button($"Call lander here ({RelocateCost:0} ship fuel)", S.fuel >= RelocateCost)) CallLander(false);
            }
            else
            {
                GUILayout.Label("Tip: a Lander Autopilot upgrade (starports) lets you call the lander to you.", gm.Hint);
            }

            if (roverFuel <= 0f && !nearLander)
            {
                GUILayout.Label("Rover is out of fuel.", gm.Label);
                if (gm.Button($"Emergency pickup ({PickupCost:N0} cr, debt allowed)")) CallLander(true);
            }
        }

        static string Heading(Vector2 v)
        {
            float a = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
            string[] dirs = { "E", "NE", "N", "NW", "W", "SW", "S", "SE" };
            int i = Mathf.RoundToInt(((a % 360f) + 360f) % 360f / 45f) % 8;
            return dirs[i];
        }
    }
}
