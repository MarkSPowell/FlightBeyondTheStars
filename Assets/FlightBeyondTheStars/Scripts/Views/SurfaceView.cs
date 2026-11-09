using System.Collections.Generic;
using UnityEngine;

namespace FBTS
{
    /// <summary>
    /// Planet surface: a wrap-around world streamed in sections from the shared PlanetTerrain.
    /// Drive the rover, collect minerals and life forms, search ruins, unload and refuel at the lander, then launch.
    /// </summary>
    public class SurfaceView : View
    {
        class Site
        {
            public SiteSpec spec;
            public Vector2 pos;   // unwrapped world position
            public int amount;
            public SpriteRenderer sprite;
        }

        class Section
        {
            public int cx, cy;    // unwrapped column, row
            public GameObject go;
            public Texture2D tex, mask;
            public readonly List<Vector2> towns = new List<Vector2>();
            public readonly List<Site> sites = new List<Site>();
        }

        const int SW = PlanetTerrain.SW, SH = PlanetTerrain.SH;
        const float SecondsPerHour = 8f;

        static float PickupCost => GameData.T("EmergencyPickupCost", 500f);
        static float RoverRefuelShipFuel => GameData.T("RoverRefuelShipFuel", 1f);

        static readonly string[] RuinGoods = { "Consumer goods", "Machinery", "Electronics", "Medical supplies", "Alloy plate" };

        readonly Planet planet;
        readonly Vector2 returnSpawn;
        readonly Dictionary<long, Section> sections = new Dictionary<long, Section>();
        readonly Dictionary<string, int> roverCargo = new Dictionary<string, int>();
        readonly Queue<Vector2Int> buildQueue = new Queue<Vector2Int>();

        PlanetTerrain terrain;
        Transform rover, lander;
        LanderRig landerRig;
        float touchdown = 1.6f;   // seconds of engine wash when the scene opens
        SpriteRenderer compass;
        Vector2 landing;
        float roverFuel, roverFuelMax;
        int roverCapacity;
        Site nearSite;
        bool nearLander;
        long lastSectionKey = long.MinValue;
        bool firstSection = true;

        public SurfaceView(Planet p, Vector2 spawnBack, Vector2 landingSite)
        {
            planet = p;
            returnSpawn = spawnBack;
            landing = landingSite;
        }

        float Circumference => terrain.circumference;
        float WorldHeight => terrain.worldHeight;

        int RoverLoad
        {
            get { int n = 0; foreach (var kv in roverCargo) n += kv.Value; return n; }
        }

        // ---------------------------------------------------------------- Setup

        public override void Enter()
        {
            terrain = new PlanetTerrain(planet);
            Cam.orthographicSize = 6.5f;

            System.Func<Vector2, Color> ground = p => terrain.SurfaceColor(terrain.WrapX(p.x), Mathf.Clamp(p.y, 0f, terrain.worldHeight));
            lander = Art.Lander(root, 2.8f, out landerRig);
            lander.localPosition = landing;
            if (landerRig != null) landerRig.groundColor = ground;

            rover = Art.Rover(root, 1.7f);
            var rig = rover.GetComponent<RoverRig>();
            if (rig != null) rig.groundColor = ground;
            Vector2 start = landing + new Vector2(0f, -1.5f);
            if (!terrain.Passable(start.x, start.y)) start = landing;
            rover.localPosition = start;

            compass = Draw.Dot("Compass", root, start, 0.3f, new Color(0.4f, 0.9f, 1f, 0.9f), 25);

            var fuelRow = GameData.ShipSystem("Terrain vehicle", "Fuel");
            var cargoRow = GameData.ShipSystem("Terrain vehicle", "Cargo");
            roverFuelMax = fuelRow != null ? fuelRow.Value(S.terrainVehicleTier) : 6f;
            roverCapacity = cargoRow != null ? Mathf.RoundToInt(cargoRow.Value(S.terrainVehicleTier)) : 10;
            roverFuel = roverFuelMax;

            // Settlements of the race that lives here: the same places the orbital map shows as city lights
            if (planet.inhabitedBy != null) townSites = terrain.CitySites(10);

            var here = SectionAt(start);
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    BuildSection(here.x + dx, here.y + dy);

            gm.Follow(start, 0f);
            gm.Say($"Landed on {planet.name}. Minerals favour mountains; life and ruins favour coasts.");
        }

        public override void Exit()
        {
            foreach (var s in sections.Values) { if (s.tex != null) Object.Destroy(s.tex); if (s.mask != null) Object.Destroy(s.mask); }
            sections.Clear();
            base.Exit();
        }

        // ---------------------------------------------------------------- World coordinates

        int WrapCol(int cx) => terrain.WrapCol(cx);
        float WrapDx(float dx) => dx - Mathf.Round(dx / Circumference) * Circumference;
        static long Key(int col, int row) => PlanetTerrain.SectionKey(col, row);
        Vector2Int SectionAt(Vector2 p) => new Vector2Int(Mathf.FloorToInt(p.x / SW), Mathf.Clamp(Mathf.FloorToInt(p.y / SH), 0, Planet.SectionsTall - 1));

        // ---------------------------------------------------------------- Streaming

        void BuildSection(int cx, int cy)
        {
            if (cy < 0 || cy >= Planet.SectionsTall) return;
            long k = Key(cx, cy);
            if (sections.ContainsKey(k)) return;

            var sec = new Section { cx = cx, cy = cy };
            int col = WrapCol(cx);
            sec.go = new GameObject($"Section {col},{cy}");
            sec.go.transform.SetParent(root, false);

            // Colour map (one texel per km) with a one-texel border taken from the neighbours, so the smooth
            // filtering joins sections seamlessly. With the painted ground, a mask of the same size says
            // where the sea, rock and polar snow are, and carries elevation for hill shading.
            var mat = groundMat;
            int tw = SW + 2, th = SH + 2;
            var tex = new Texture2D(tw, th, TextureFormat.RGBA32, false)
                { filterMode = mat != null ? FilterMode.Bilinear : FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[tw * th];
            Color[] mk = mat != null ? new Color[tw * th] : null;
            for (int y = 0; y < th; y++)
                for (int x = 0; x < tw; x++)
                {
                    float wx = cx * SW + x - 1 + 0.5f, wy = cy * SH + y - 1 + 0.5f;
                    px[y * tw + x] = terrain.SurfaceColor(wx, wy);
                    if (mk != null) mk[y * tw + x] = terrain.GroundMask(wx, wy);
                }
            tex.SetPixels(px);
            tex.Apply();
            sec.tex = tex;
            var sprite = Sprite.Create(tex, new Rect(1, 1, SW, SH), Vector2.zero, 1f, 0, SpriteMeshType.FullRect);
            var ground = Draw.MakeSprite("Terrain", sec.go.transform, sprite, new Vector2(cx * SW, cy * SH), Vector2.one, Color.white, 0);
            if (mat != null)
            {
                var mask = new Texture2D(tw, th, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                mask.SetPixels(mk);
                mask.Apply();
                sec.mask = mask;
                ground.sharedMaterial = mat;
                var block = new MaterialPropertyBlock();
                ground.GetPropertyBlock(block);
                block.SetTexture("_MaskTex", mask);
                ground.SetPropertyBlock(block);
            }
            ScatterRocks(sec, cx, cy, col);
            if (townSites != null)
                foreach (var t in townSites)
                {
                    if (Mathf.FloorToInt(t.x / SW) != col || Mathf.Clamp(Mathf.FloorToInt(t.y / SH), 0, Planet.SectionsTall - 1) != cy) continue;
                    var p = t + new Vector2(shiftTowns(cx, col), 0f);
                    Art.Settlement(planet.inhabitedBy, sec.go.transform, p, TownSize);
                    sec.towns.Add(p);
                }

            float shift = (cx - col) * SW; // sites are generated at the wrapped column; move them to this copy
            foreach (var spec in terrain.SectionSites(col, cy))
            {
                bool done = planet.minedDeposits.Contains(spec.key);
                if (done && spec.kind != SiteKind.Ruin) continue;
                // Geologist / Xenobiologist find more in each deposit or life form
                float yield = spec.kind == SiteKind.Life ? S.Trait("LifeYield") : spec.kind == SiteKind.Ruin ? 0f : S.Trait("MineralYield");
                var site = new Site { spec = spec, pos = spec.pos + new Vector2(shift, 0f), amount = Mathf.Max(1, Mathf.RoundToInt(spec.amount * (1f + yield))) };
                site.sprite = MakeSiteSprite(site, sec.go.transform, done);
                sec.sites.Add(site);
            }
            sections[k] = sec;
        }

        static float shiftTowns(int cx, int col) => (cx - col) * SW;

        // ---------------------------------------------------------------- Settlements

        const float TownSize = 4.5f;
        List<Vector2> townSites;
        Vector2? nearTown;
        bool marketOpen;
        Vector2 marketScroll;
        string Locals => planet.inhabitedBy;
        float TownPrices => GameData.T("SettlementPriceFactor", 0.85f);

        bool TownTalks => Locals != null && Conversation.HasKnot(Locals, "settlement");

        void TalkToTown()
        {
            if (TownTalks) gm.Hail(Locals, "settlement", $"{Locals} settlement, {planet.name}");
            else gm.Say($"The {Locals} settlement answers, but in a language your Comms Officer can't follow yet. Trading is done by gesture.");
        }

        /// <summary>The settlement's market: buys from the rover and the ship's hold (shuttled down by the lander)
        /// at the local race's prices, a little below a starport's, and sells rover fuel.</summary>
        void TownMarketGui()
        {
            GUILayout.Label($"{Locals} settlement market", gm.Header);
            GUILayout.Label($"Locals pay {TownPrices * 100f:0}% of what a {Locals} starport would. Comms Officer skill {S.commsSkill} helps.", gm.Hint);
            GUILayout.Label($"Standing with the {Locals}: {S.Rep(Locals):+0;-0;0}.  Credits: {S.credits:N0}", gm.Hint);
            if (gm.Button("Close market")) marketOpen = false;
            marketScroll = GUILayout.BeginScrollView(marketScroll);

            GUILayout.Label("From the rover", gm.Header);
            if (roverCargo.Count == 0) GUILayout.Label("Rover hold is empty.", gm.Label);
            foreach (var kv in new List<KeyValuePair<string, int>>(roverCargo))
                SellRow(kv.Key, kv.Value, n =>
                {
                    roverCargo[kv.Key] -= n;
                    if (roverCargo[kv.Key] <= 0) roverCargo.Remove(kv.Key);
                });

            GUILayout.Space(8);
            GUILayout.Label("From the ship's hold", gm.Header);
            if (S.cargo.Count == 0) GUILayout.Label("Ship hold is empty.", gm.Label);
            foreach (var kv in new List<KeyValuePair<string, int>>(S.cargo))
                SellRow(kv.Key, kv.Value, n => S.RemoveCargo(kv.Key, n));

            Market.ArtifactsGui(gm, S, Locals, TownPrices);

            GUILayout.Space(8);
            GUILayout.Label("Rover fuel", gm.Header);
            float hours = roverFuelMax - roverFuel;
            float cost = Mathf.Ceil(hours * GameData.T("SettlementRoverFuelPrice", 40f));
            if (gm.Button($"Fill the rover ({hours:0.0} hours, {cost:N0} cr)", hours > 0.05f && S.credits >= cost))
            {
                S.credits -= cost;
                roverFuel = roverFuelMax;
                gm.Say($"Rover refuelled by the {Locals} for {cost:N0} cr.");
            }
            GUILayout.EndScrollView();
        }

        void SellRow(string commodity, int qty, System.Action<int> remove)
        {
            float price = Rules.SellPrice(S, S.current, commodity, Locals) * TownPrices;
            GUILayout.Label($"{commodity} x{qty}  @ {price:N0} cr", gm.Label);
            GUILayout.BeginHorizontal();
            bool buys = price > 0f;
            if (gm.Button("Sell 1", buys)) { remove(1); Market.Sold(gm, S, Locals, $"1 {commodity}", price); }
            if (gm.Button("Sell all", buys)) { remove(qty); Market.Sold(gm, S, Locals, $"{qty} {commodity}", qty * price); }
            GUILayout.EndHorizontal();
            if (!buys) GUILayout.Label("They won't buy it.", gm.Hint);
        }

        Material groundMat => groundMatCache ??= Art.GroundMaterial(planet.type);
        Material groundMatCache;

        /// <summary>Decorative boulders, the same every visit (seeded by the section), more on high ground,
        /// none in the sea; tinted to the ground they sit on.</summary>
        void ScatterRocks(Section sec, int cx, int cy, int col)
        {
            int kinds = Art.RockCount;
            if (kinds == 0) return;
            var rng = new System.Random(planet.seed * 7919 + col * 104729 + cy * 1299709);
            int tries = 26;
            for (int i = 0; i < tries; i++)
            {
                float x = cx * SW + (float)rng.NextDouble() * SW, y = cy * SH + (float)rng.NextDouble() * SH;
                var cls = terrain.ClassAt(x, y);
                if (cls == TerrainClass.Sea) continue;
                float keep = cls == TerrainClass.Mountain ? 1f : cls == TerrainClass.Highland ? 0.7f : 0.3f;
                if (rng.NextDouble() > keep) continue;
                float size = Mathf.Lerp(0.35f, cls == TerrainClass.Mountain ? 1.6f : 1f, (float)(rng.NextDouble() * rng.NextDouble()));
                Color tint = terrain.SurfaceColor(x, y);
                tint = Color.Lerp(tint, Color.white, 0.35f) * 1.05f;
                tint.a = 1f;
                Art.Rock(rng.Next(kinds), sec.go.transform, new Vector2(x, y), size, (float)rng.NextDouble() * 360f, tint, 5);
            }
        }

        SpriteRenderer MakeSiteSprite(Site s, Transform parent, bool searched)
        {
            var painted = PaintedSite(s, parent, searched);
            if (painted != null) return painted;
            switch (s.spec.kind)
            {
                case SiteKind.Ruin:
                    var r = Draw.MakeSprite("Ruin", parent, Draw.Square, s.pos, new Vector2(0.9f, 0.9f),
                        searched ? new Color(0.35f, 0.33f, 0.3f) : new Color(0.75f, 0.68f, 0.5f), 10);
                    r.transform.localRotation = Quaternion.Euler(0, 0, 45);
                    return r;
                case SiteKind.Life:
                    return Draw.Dot("Life", parent, s.pos, 0.55f, new Color(0.45f, 1f, 0.45f), 10);
                default:
                    var c = GameData.Commodity(s.spec.commodity);
                    float value = c != null ? c.basePrice : 10f;
                    Color col = value < 20 ? new Color(0.75f, 0.75f, 0.75f) : value < 60 ? new Color(1f, 0.65f, 0.2f) : new Color(0.3f, 1f, 0.95f);
                    return Draw.Dot("Mineral", parent, s.pos, 0.6f, col, 10);
            }
        }

        SpriteRenderer PaintedSite(Site s, Transform parent, bool searched)
        {
            int tier = 0;
            if (s.spec.kind == SiteKind.Mineral)
            {
                var c = GameData.Commodity(s.spec.commodity);
                float value = c != null ? c.basePrice : 10f;
                tier = value < 20 ? 0 : value < 60 ? 1 : 2;
            }
            float size = s.spec.kind == SiteKind.Ruin ? 2.6f : s.spec.kind == SiteKind.Life ? 1.2f : 1.1f;
            var sr = Art.SiteSprite(s.spec.kind, tier, parent, s.pos, size, 10);
            if (sr != null && searched) sr.color = new Color(0.55f, 0.53f, 0.5f);
            return sr;
        }

        void Stream(Vector2 roverPos)
        {
            var here = SectionAt(roverPos);
            long wrappedKey = Key(WrapCol(here.x), here.y);
            if (wrappedKey != lastSectionKey)
            {
                lastSectionKey = wrappedKey;
                if (planet.visitedSections.Add(wrappedKey) && !firstSection)
                    gm.Toast("Scanning new terrain...", 1.6f);
                firstSection = false;
            }

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

            List<long> drop = null;
            foreach (var kv in sections)
                if (Mathf.Abs(kv.Value.cx - here.x) > 2 || Mathf.Abs(kv.Value.cy - here.y) > 2)
                    (drop ??= new List<long>()).Add(kv.Key);
            if (drop != null)
                foreach (var k in drop)
                {
                    Object.Destroy(sections[k].go);
                    Object.Destroy(sections[k].tex);
                    if (sections[k].mask != null) Object.Destroy(sections[k].mask);
                    sections.Remove(k);
                }
        }

        // ---------------------------------------------------------------- Frame update

        Vector2 LanderShown(Vector2 roverPos) => new Vector2(roverPos.x + WrapDx(landing.x - roverPos.x), landing.y);

        public override void Tick(float dt)
        {
            // Planet map: M / gamepad View toggles it; it pauses driving while open.
            if (Controls.Map()) { mapOpen = !mapOpen; if (mapOpen) BuildMap(); }
            if (mapOpen)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null && kb.escapeKey.wasPressedThisFrame) mapOpen = false;
                return;
            }

            if (touchdown > 0f && landerRig != null)
            {
                touchdown -= dt;
                landerRig.thrust = Mathf.Clamp01(touchdown / 1.6f);
            }

            Vector2 move = Controls.Move();
            Vector2 pos = rover.localPosition;
            if (roverFuel > 0f && move.sqrMagnitude > 0f)
            {
                float speed = 4f / Mathf.Lerp(0.8f, 1.6f, Mathf.Clamp01(planet.gravity / 2f));
                Vector2 step = move * speed * dt;
                // Seas (and lava) block the rover; slide along the shore when moving diagonally.
                Vector2 next = pos + step;
                if (!terrain.Passable(next.x, next.y))
                {
                    if (terrain.Passable(pos.x + step.x, pos.y)) next = new Vector2(pos.x + step.x, pos.y);
                    else if (terrain.Passable(pos.x, pos.y + step.y)) next = new Vector2(pos.x, pos.y + step.y);
                    else next = pos;
                }
                next.y = Mathf.Clamp(next.y, 0.5f, WorldHeight - 0.5f);
                if (next != pos)
                {
                    rover.localPosition = next;
                    rover.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(move.y, move.x) * Mathf.Rad2Deg);
                    roverFuel = Mathf.Max(0f, roverFuel - move.magnitude * dt / SecondsPerHour);
                    pos = next;
                }
            }
            gm.Follow(pos, 6f);
            Stream(pos);

            Vector2 shown = LanderShown(pos);
            lander.localPosition = shown;
            Vector2 toLander = shown - pos;
            compass.enabled = toLander.magnitude > 6f;
            compass.transform.localPosition = pos + toLander.normalized * 1.3f;

            nearSite = null;
            float best = 1.0f;
            float reveal = S.RoverRevealRadius;
            foreach (var s in sections.Values)
                foreach (var site in s.sites)
                {
                    float dist = Vector2.Distance(pos, site.pos);
                    if (site.sprite != null && site.spec.kind != SiteKind.Ruin)
                        site.sprite.enabled = planet.scanned || dist < reveal;
                    bool usable = site.spec.kind != SiteKind.Ruin || !planet.minedDeposits.Contains(site.spec.key);
                    if (usable && dist < best) { best = dist; nearSite = site; }
                }
            nearLander = toLander.magnitude < 1.6f;

            nearTown = null;
            float townBest = TownSize * 0.5f + 0.8f;
            foreach (var s in sections.Values)
                foreach (var t in s.towns)
                {
                    float d = Vector2.Distance(pos, t);
                    if (d < townBest) { townBest = d; nearTown = t; }
                }
            if (nearTown == null) marketOpen = false;
            if (nearTown != null && Controls.Hail()) TalkToTown();

            if (Controls.Action())
            {
                if (nearSite != null) Use(nearSite);
                else if (nearLander && RoverLoad > 0) Unload();
                else if (nearLander && roverFuel < roverFuelMax) RefuelRover();
            }
            if (nearLander && Controls.Secondary()) Launch();
        }

        // ---------------------------------------------------------------- Sites

        void Use(Site s)
        {
            if (s.spec.kind == SiteKind.Ruin) SearchRuin(s);
            else Collect(s);
        }

        int AddToRover(string commodity, int amount)
        {
            int take = Mathf.Min(roverCapacity - RoverLoad, amount);
            if (take <= 0) return 0;
            roverCargo.TryGetValue(commodity, out int have);
            roverCargo[commodity] = have + take;
            return take;
        }

        void RemoveSite(Site s)
        {
            planet.minedDeposits.Add(s.spec.key);
            if (s.sprite != null) Object.Destroy(s.sprite.gameObject);
            foreach (var sec in sections.Values) sec.sites.Remove(s);
        }

        void Collect(Site s)
        {
            if (RoverLoad >= roverCapacity) { gm.Say("Rover hold is full. Unload at the lander."); return; }
            int take = AddToRover(s.spec.commodity, s.amount);
            s.amount -= take;
            gm.Say(s.spec.kind == SiteKind.Life ? $"Collected {take} {s.spec.commodity} (stored in stasis)." : $"Collected {take} {s.spec.commodity}.");
            if (s.amount <= 0) RemoveSite(s);
        }

        /// <summary>Ruins hold trade goods, sometimes fuel, rarely artifacts, and in a race's territory, lore.</summary>
        void SearchRuin(Site s)
        {
            if (planet.minedDeposits.Contains(s.spec.key)) { gm.Say("These ruins have already been searched."); return; }
            if (RoverLoad >= roverCapacity) { gm.Say("Rover hold is full. Unload at the lander before searching."); return; }

            var rng = new System.Random((int)(s.spec.key ^ (s.spec.key >> 32)) ^ planet.seed);
            double roll = rng.NextDouble();
            string found; int amount;
            // Major artifacts only turn up from Act 2; before that the same ruins hold a minor artifact instead.
            float major = S.Act >= 2 ? GameData.T("MajorArtifactChance", 0.06f) : 0f;
            float minor = GameData.T("MinorArtifactChance", 0.08f) + (S.Act >= 2 ? 0f : GameData.T("MajorArtifactChance", 0.06f));
            if (roll < major)
            {
                var art = Artifacts.Find(S, rng);
                if (art != null)
                {
                    planet.minedDeposits.Add(s.spec.key);
                    if (s.sprite != null) s.sprite.color = new Color(0.55f, 0.53f, 0.5f);
                    WorldEvents.Fire(S, "first-artifact", S.current.id);
                    Crew.Morale(S, "Artifact");
                    Crew.Gain(S, Roles.Science, GameData.T("XpArtifactFind", 2f));
                    gm.Say(art.identified
                        ? $"Searched the ruins: found a major artifact, the {Artifacts.Name(art)}! See the Artifacts screen (I)."
                        : "Searched the ruins: found a major artifact! Nobody aboard knows what it is yet; the Science Officer will study it as you travel (Artifacts screen, I).");
                    RuinLore(rng);
                    return;
                }
                found = "Major artifact"; amount = 1;   // every unique artifact already found
            }
            else if (roll < major + minor) { found = "Minor artifact"; amount = 1; }
            else if (roll < 0.30) { found = "Drive crystal fuel"; amount = 2 + rng.Next(3); }
            else { found = RuinGoods[rng.Next(RuinGoods.Length)]; amount = 2 + rng.Next(4); }

            int took = AddToRover(found, amount);
            planet.minedDeposits.Add(s.spec.key);
            if (s.sprite != null) s.sprite.color = new Color(0.55f, 0.53f, 0.5f);

            string msg = $"Searched the ruins: found {amount} {found}" + (took < amount ? $" (room for {took})." : ".");
            if (found.Contains("artifact"))
            {
                msg += " A rare find!";
                WorldEvents.Fire(S, "first-artifact", S.current.id);
                Crew.Morale(S, "Artifact");
            }
            gm.Say(msg);

            RuinLore(rng);
        }

        /// <summary>Lore, if this planet is in a race's territory and that race has records left to find.</summary>
        void RuinLore(System.Random rng)
        {
            string race = S.current.territory;
            if (race != null && rng.NextDouble() < 0.7)
            {
                foreach (var l in GameData.Lore)
                {
                    if (l.race != race || S.loreFound.Contains(l.id)) continue;
                    S.loreFound.Add(l.id);
                    Crew.Morale(S, "Lore");
                    gm.ShowNote($"{l.title}  ({l.race} record)", l.text + "\n\nAdded to the captain's log.");
                    break;
                }
            }
        }

        // ---------------------------------------------------------------- Lander actions

        void Unload()
        {
            int stored = 0, fuelUnits = 0;
            var full = new List<string>();
            foreach (var kv in new List<KeyValuePair<string, int>>(roverCargo))
            {
                int remaining = kv.Value;
                if (kv.Key == "Drive crystal fuel")
                {
                    // Recovered fuel tops up the ship's tank first; any surplus goes into the hold as cargo.
                    int toTank = Mathf.Min(remaining, Mathf.FloorToInt(S.FuelTank - S.fuel));
                    S.fuel += toTank;
                    fuelUnits += toTank;
                    remaining -= toTank;
                }
                int added = remaining > 0 ? S.AddCargo(kv.Key, remaining) : 0;
                stored += added;
                remaining -= added;
                if (remaining <= 0) roverCargo.Remove(kv.Key);
                else { roverCargo[kv.Key] = remaining; full.Add(kv.Key); }
            }
            string msg = $"Unloaded {stored} units into the hold" + (fuelUnits > 0 ? $" and {fuelUnits} fuel into the tank." : ".");
            if (full.Count > 0) msg += $" Hold is full; {string.Join(", ", full)} stays in the rover.";
            gm.Say(msg);
        }

        void RefuelRover()
        {
            float missing = (roverFuelMax - roverFuel) / roverFuelMax;
            float shipFuel = missing * RoverRefuelShipFuel;
            if (S.fuel < shipFuel) { gm.Say("Not enough ship fuel to refuel the rover."); return; }
            S.fuel -= shipFuel;
            roverFuel = roverFuelMax;
            gm.Say($"Rover refuelled using {shipFuel:0.00} ship fuel.");
        }

        float RelocateCost => Mathf.Ceil(planet.gravity);

        void CallLander(bool emergency)
        {
            if (!emergency && !S.LanderAutopilot) { gm.Say("Calling the lander needs the Lander Autopilot upgrade."); return; }
            if (!emergency && S.fuel < RelocateCost) { gm.Say($"Moving the lander needs {RelocateCost:0} ship fuel."); return; }
            Vector2 roverPos = rover.localPosition;
            Vector2 from = LanderShown(roverPos);
            Vector2 to = roverPos + new Vector2(2f, 0f);
            if (!terrain.Passable(to.x, to.y)) to = roverPos;
            SpriteRenderer flame = null;

            gm.Play(new Cutscene
            {
                duration = 3f,
                caption = t => emergency ? "Emergency pickup inbound" : "Lander relocating",
                onStart = r => { if (landerRig == null) flame = Draw.Dot("Flame", r, from, 0.5f, new Color(1f, 0.6f, 0.2f, 0.9f), 19); },
                onUpdate = (t, dt) =>
                {
                    float a = Cutscene.Smooth(t);
                    float hop = Mathf.Sin(t * Mathf.PI);
                    Vector2 p = Vector2.Lerp(from, to, a);
                    lander.localPosition = p;
                    lander.localScale = Vector3.one * (1f - 0.4f * hop);
                    if (landerRig != null) landerRig.thrust = 0.6f + 0.4f * Mathf.Abs(Mathf.Cos(t * Mathf.PI));
                    if (flame != null)
                    {
                        flame.transform.localPosition = p + new Vector2(0f, -0.9f * lander.localScale.x);
                        flame.transform.localScale = Vector3.one * (0.4f + 0.2f * Mathf.Sin(t * 60f));
                    }
                    gm.Follow(Vector2.Lerp(roverPos, p, 0.5f), 0f);
                },
                onComplete = () =>
                {
                    if (emergency) S.credits -= PickupCost; else S.fuel -= RelocateCost;
                    landing = to;
                    lander.localPosition = to;
                    lander.localScale = Vector3.one;
                    if (landerRig != null) landerRig.thrust = 0f;
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
                onStart = r => { if (landerRig == null) flame = Draw.Dot("Flame", r, site, 0.5f, new Color(1f, 0.6f, 0.2f, 0.9f), 19); },
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
                    if (landerRig != null) landerRig.thrust = t < 0.2f ? 0f : Mathf.Clamp01((t - 0.2f) * 8f);
                    if (flame != null)
                    {
                        flame.transform.localPosition = lp + new Vector2(0f, -0.9f * scale);
                        flame.transform.localScale = Vector3.one * (b > 0f ? (0.5f + 0.25f * Mathf.Sin(t * 60f)) * scale * 1.4f : 0f);
                    }

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
            if (RoverLoad > 0) Unload();
            gm.FadeColor = Color.black;
            gm.FadeAlpha = 1f;
            gm.Go(new SystemView(returnSpawn));
            gm.FadeIn(0.8f);
        }

        // ---------------------------------------------------------------- Planet map

        bool mapOpen;
        Texture2D mapTex;
        readonly List<Vector2> mapRuinAreas = new List<Vector2>();
        float mapRuinRadius;
        bool mapRuinsPrecise;

        float Science => S.ScanSkill;

        /// <summary>The whole-planet topographic map (as on the landing screen), built once per landing,
        /// plus the ruin search areas the Science Officer can make out from orbit.</summary>
        void BuildMap()
        {
            if (mapTex != null) return;
            int tw = Mathf.CeilToInt(terrain.circumference / 2f), th = Mathf.CeilToInt(terrain.worldHeight / 2f);
            mapTex = new Texture2D(tw, th, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[tw * th];
            for (int y = 0; y < th; y++)
                for (int x = 0; x < tw; x++)
                    px[y * tw + x] = terrain.TopoColor(x * 2f + 1f, y * 2f + 1f, 2f, 2f);
            mapTex.SetPixels(px);
            mapTex.Apply();

            mapRuinAreas.Clear();
            if (Science >= GameData.T("ScanRuinsSkill", 100f))
            {
                float t = Mathf.InverseLerp(GameData.T("ScanRuinsSkill", 100f), GameData.T("ScanPreciseSkill", 250f), Science);
                float minR = GameData.T("RuinAreaMinRadius", 1.5f);
                mapRuinRadius = Mathf.Max(minR, Mathf.Lerp(GameData.T("RuinAreaMaxRadius", 30f), minR, t) * (1f - S.Trait("RuinRadius")));
                mapRuinsPrecise = mapRuinRadius <= minR + 0.01f;
                for (int col = 0; col < terrain.around; col++)
                    for (int row = 0; row < Planet.SectionsTall; row++)
                        foreach (var site in terrain.SectionSites(col, row))
                            if (site.kind == SiteKind.Ruin && !planet.minedDeposits.Contains(site.key))
                                mapRuinAreas.Add(mapRuinsPrecise ? site.pos : PlanetTerrain.SearchAreaCenter(site, mapRuinRadius));
            }
        }

        void MapGui()
        {
            float s = GameManager.UiScale;
            float sw = Screen.width / s, sh = Screen.height / s;
            float left = GameManager.PanelWidth + 30f;
            float availW = sw - left - 20f, availH = sh - 130f;
            float aspect = terrain.circumference / terrain.worldHeight;
            float w = Mathf.Min(availW, availH * aspect), h = w / aspect;
            var r = new Rect(left + (availW - w) / 2f, 50f + (availH - h) / 2f, w, h);

            GUI.Box(new Rect(r.x - 8, r.y - 34, r.width + 16, r.height + 70), GUIContent.none);
            GUI.Label(new Rect(r.x, r.y - 30, r.width, 26), $"{planet.name} - planetary map   (M or Esc to close)", gm.Header);
            GUI.DrawTexture(r, mapTex, ScaleMode.StretchToFill);

            Vector2 ToScreen(Vector2 world) => new Vector2(
                r.x + terrain.WrapX(world.x) / terrain.circumference * r.width,
                r.y + r.height - Mathf.Clamp(world.y, 0f, terrain.worldHeight) / terrain.worldHeight * r.height);

            var old = GUI.color;

            // Unexplored sections are dimmed: the map fills in as you drive
            float cw = r.width / terrain.around, ch = r.height / Planet.SectionsTall;
            GUI.color = new Color(0f, 0f, 0f, 0.45f);
            for (int col = 0; col < terrain.around; col++)
                for (int row = 0; row < Planet.SectionsTall; row++)
                    if (!planet.visitedSections.Contains(PlanetTerrain.SectionKey(col, row)))
                        GUI.DrawTexture(new Rect(r.x + col * cw, r.y + r.height - (row + 1) * ch, cw, ch), Texture2D.whiteTexture);

            // Ruin search areas (or exact spots at top Science skill)
            GUI.color = new Color(1f, 0.85f, 0.3f, mapRuinsPrecise ? 1f : 0.35f);
            float rad = mapRuinsPrecise ? 5f : Mathf.Max(5f, mapRuinRadius / terrain.circumference * r.width);
            foreach (var c in mapRuinAreas)
            {
                var p = ToScreen(c);
                GUI.DrawTexture(new Rect(p.x - rad, p.y - rad, rad * 2f, rad * 2f), Draw.Circle.texture);
            }

            // Lander and rover
            Vector2 lp = ToScreen(landing), rp = ToScreen(rover.localPosition);
            GUI.color = new Color(0.4f, 0.9f, 1f);
            GUI.DrawTexture(new Rect(lp.x - 7, lp.y - 7, 14, 14), Draw.Circle.texture);
            GUI.color = new Color(1f, 0.9f, 0.3f);
            GUI.DrawTexture(new Rect(rp.x - 5, rp.y - 5, 10, 10), Draw.Circle.texture);
            GUI.color = old;
            GUI.Label(new Rect(lp.x + 9, lp.y - 11, 120, 22), "Lander", gm.Label);
            GUI.Label(new Rect(rp.x + 7, rp.y + 2, 120, 22), "You", gm.Label);

            GUI.Label(new Rect(r.x, r.yMax + 6, r.width, 24),
                (mapRuinAreas.Count > 0 ? (mapRuinsPrecise ? "Gold: ruins.  " : "Gold: ruin search areas.  ") : "") +
                "Dimmed: not yet explored.  Blue: lander.  Yellow: you.", gm.Hint);
        }

        // ---------------------------------------------------------------- GUI

        public override void WorldGui()
        {
            if (mapOpen) { MapGui(); return; }
            gm.WorldLabel(lander.localPosition, "Lander", 22);
            if (nearSite != null && nearSite.spec.kind == SiteKind.Ruin) gm.WorldLabel(nearSite.pos, "Ruins", 16);
        }

        public override void Gui()
        {
            Vector2 pos = rover.localPosition;
            float lat = terrain.Latitude(pos.y), lon = terrain.Longitude(pos.x);
            Vector2 toLander = LanderShown(pos) - pos;

            GUILayout.Label(planet.name, gm.Header);
            GUILayout.Label($"{planet.type}, {planet.gravity:0.0} g. Hazard: {planet.hazard}", gm.Label);
            GUILayout.Label($"{Mathf.Abs(lat):0}°{(lat >= 0 ? "N" : "S")} {Mathf.Abs(lon):0}°{(lon >= 0 ? "E" : "W")}, " +
                            $"{terrain.Describe(terrain.ClassAt(pos.x, pos.y))}.  Sections explored: {planet.visitedSections.Count}/{terrain.around * Planet.SectionsTall}", gm.Hint);
            GUILayout.Label("Drive: WASD / stick.  Collect, search, unload, refuel: E / A.  Launch at lander: L / Y.  Map: M.", gm.Hint);
            if (gm.Button(mapOpen ? "Close map (M)" : "Planet map (M)")) { mapOpen = !mapOpen; if (mapOpen) BuildMap(); }
            GUILayout.Space(6);

            GUILayout.Label($"Rover fuel: {roverFuel:0.0} / {roverFuelMax:0} hours", gm.Label);
            GUILayout.Label($"Rover hold: {RoverLoad} / {roverCapacity}", gm.Label);
            foreach (var kv in roverCargo) GUILayout.Label($"  {kv.Key} x{kv.Value}", gm.Label);
            GUILayout.Label($"Ship hold: {S.CargoUsed:0} / {S.CargoCapacity:0}", gm.Label);
            GUILayout.Label(nearLander ? "At the lander." : $"Lander: {toLander.magnitude:0} km {Heading(toLander)}", gm.Label);
            GUILayout.Space(6);

            if (nearTown != null)
            {
                if (marketOpen) { TownMarketGui(); return; }
                GUILayout.Label($"A {Locals} settlement.", gm.Label);
                if (gm.Button(TownTalks ? "Talk to the locals (H)" : "Hail the settlement (H)")) TalkToTown();
                if (gm.Button("Trade")) { marketOpen = true; marketScroll = Vector2.zero; }
                GUILayout.Space(6);
            }

            if (nearSite != null)
            {
                switch (nearSite.spec.kind)
                {
                    case SiteKind.Ruin:
                        GUILayout.Label("Ancient ruins.", gm.Label);
                        if (gm.Button("Search ruins (E)")) SearchRuin(nearSite);
                        break;
                    case SiteKind.Life:
                        GUILayout.Label($"Life form: {nearSite.spec.commodity} ({nearSite.amount})", gm.Label);
                        if (gm.Button("Collect specimen (E)")) Collect(nearSite);
                        break;
                    default:
                        GUILayout.Label($"Deposit: {nearSite.spec.commodity} ({nearSite.amount})", gm.Label);
                        if (gm.Button("Collect (E)")) Collect(nearSite);
                        break;
                }
            }

            if (nearLander)
            {
                if (gm.Button($"Unload cargo to ship ({RoverLoad})", RoverLoad > 0)) Unload();
                float need = (roverFuelMax - roverFuel) / roverFuelMax * RoverRefuelShipFuel;
                if (gm.Button($"Refuel rover ({need:0.00} ship fuel)", roverFuel < roverFuelMax && S.fuel >= need)) RefuelRover();
                if (gm.Button("Launch to orbit (L)")) Launch();
            }
            else if (S.LanderAutopilot)
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
