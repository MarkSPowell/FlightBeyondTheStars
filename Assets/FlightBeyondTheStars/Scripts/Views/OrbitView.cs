using System.Collections.Generic;
using UnityEngine;

namespace FBTS
{
    /// <summary>
    /// Orbital landing map: the whole planet as a topographic map (equirectangular, wrapping east-west).
    /// Move the crosshair to a landing site and descend. Seas and lava can't be landed on.
    /// </summary>
    public class OrbitView : View
    {
        const float MapW = 50f;
        static readonly float MapH = MapW * 144f / 360f; // latitude span 144 degrees vs 360 of longitude
        static readonly Vector2 MapOrigin = new Vector2(-MapW / 2f, -MapH / 2f);

        readonly Planet planet;
        readonly Vector2 systemReturn;
        PlanetTerrain terrain;
        Texture2D tex;
        Vector2 cursor;                // world tile coordinates
        Transform crosshair;
        readonly List<Vector2> ruinAreas = new List<Vector2>();   // search-area centres
        float ruinRadius;                                          // tiles; 0 = ruins not detected

        int Skill => S.ScanSkill;   // Keen Sensors trait and sensor artifacts read planets better
        bool CanEstimate => Skill >= GameData.T("ScanEstimateSkill", 40f);
        bool Detailed => Skill >= GameData.T("ScanDetailSkill", 120f);
        bool DetectsRuins => Skill >= GameData.T("ScanRuinsSkill", 100f);

        /// <summary>Search-area radius shrinks from RuinAreaMaxRadius to RuinAreaMinRadius as skill rises.</summary>
        float RuinRadiusForSkill()
        {
            float t = Mathf.InverseLerp(GameData.T("ScanRuinsSkill", 100f), GameData.T("ScanPreciseSkill", 250f), Skill);
            float r = Mathf.Lerp(GameData.T("RuinAreaMaxRadius", 30f), GameData.T("RuinAreaMinRadius", 1.5f), t);
            return Mathf.Max(GameData.T("RuinAreaMinRadius", 1.5f), r * (1f - S.Trait("RuinRadius")));   // Archaeologist
        }

        public OrbitView(Planet p, Vector2 returnSpawn)
        {
            planet = p;
            systemReturn = returnSpawn;
        }

        Vector2 ToMap(Vector2 world) => MapOrigin + new Vector2(terrain.WrapX(world.x) / terrain.circumference * MapW, world.y / terrain.worldHeight * MapH);
        Vector2 ToWorld(Vector2 map) => new Vector2((map.x - MapOrigin.x) / MapW * terrain.circumference, Mathf.Clamp((map.y - MapOrigin.y) / MapH * terrain.worldHeight, 1f, terrain.worldHeight - 1f));

        public override void Enter()
        {
            terrain = new PlanetTerrain(planet);
            FitCamera();

            // Topographic texture: one pixel per 2 tiles.
            int tw = Mathf.CeilToInt(terrain.circumference / 2f), th = Mathf.CeilToInt(terrain.worldHeight / 2f);
            tex = new Texture2D(tw, th, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[tw * th];
            for (int y = 0; y < th; y++)
                for (int x = 0; x < tw; x++)
                    px[y * tw + x] = terrain.TopoColor(x * 2f + 1f, y * 2f + 1f, 2f, 2f);
            tex.SetPixels(px);
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, tw, th), Vector2.zero, tw / MapW);
            Draw.MakeSprite("Topographic map", root, sprite, MapOrigin, new Vector2(1f, MapH / (th * MapW / tw)), Color.white, 1);
            Draw.MakeSprite("Frame", root, Draw.Square, MapOrigin + new Vector2(MapW, MapH) / 2f, new Vector2(MapW + 0.4f, MapH + 0.4f), new Color(0.3f, 0.4f, 0.55f), 0);

            // Grid lines every 30 degrees of longitude and latitude.
            for (int i = 1; i < 12; i++)
            {
                float x = MapOrigin.x + MapW * i / 12f;
                Draw.Line("Meridian", root, new Vector2(x, MapOrigin.y), new Vector2(x, MapOrigin.y + MapH), 0.03f, new Color(1f, 1f, 1f, 0.18f), 2);
            }
            for (int lat = -60; lat <= 60; lat += 30)
            {
                float y = MapOrigin.y + (lat + 72f) / 144f * MapH;
                Draw.Line("Parallel", root, new Vector2(MapOrigin.x, y), new Vector2(MapOrigin.x + MapW, y), 0.03f, new Color(1f, 1f, 1f, lat == 0 ? 0.35f : 0.18f), 2);
            }

            // Ruins: only a good enough Science Officer detects them, as search areas that tighten with skill.
            float tilesToMap = MapW / terrain.circumference;
            if (DetectsRuins)
            {
                ruinRadius = RuinRadiusForSkill();
                bool precise = ruinRadius <= GameData.T("RuinAreaMinRadius", 1.5f) + 0.01f;
                for (int col = 0; col < terrain.around; col++)
                    for (int row = 0; row < Planet.SectionsTall; row++)
                        foreach (var site in terrain.SectionSites(col, row))
                        {
                            if (site.kind != SiteKind.Ruin || planet.minedDeposits.Contains(site.key)) continue;
                            Vector2 centre = precise ? site.pos : PlanetTerrain.SearchAreaCenter(site, ruinRadius);
                            ruinAreas.Add(centre);
                            if (precise)
                            {
                                var m = Draw.MakeSprite("Ruin", root, Draw.Square, ToMap(centre), new Vector2(0.45f, 0.45f), new Color(1f, 0.85f, 0.3f), 4);
                                m.transform.localRotation = Quaternion.Euler(0, 0, 45);
                            }
                            else
                            {
                                float d = Mathf.Max(0.5f, ruinRadius * 2f * tilesToMap);
                                Draw.Dot("Ruin area", root, ToMap(centre), d, new Color(1f, 0.85f, 0.3f, 0.28f), 3);
                            }
                        }
            }

            // City lights on inhabited worlds.
            if (planet.inhabitedBy != null && CanEstimate)
                foreach (var c in terrain.CitySites(10))
                    Draw.Dot("City", root, ToMap(c), 0.3f, new Color(1f, 1f, 0.8f, 0.95f), 5);

            // Crosshair
            crosshair = new GameObject("Crosshair").transform;
            crosshair.SetParent(root, false);
            var ring = Draw.Dot("Ring", crosshair, Vector2.zero, 1.1f, new Color(0.4f, 1f, 0.9f, 0.35f), 6);
            Draw.MakeSprite("H", crosshair, Draw.Square, Vector2.zero, new Vector2(1.8f, 0.06f), new Color(0.4f, 1f, 0.9f), 7);
            Draw.MakeSprite("V", crosshair, Draw.Square, Vector2.zero, new Vector2(0.06f, 1.8f), new Color(0.4f, 1f, 0.9f), 7);

            cursor = FindStartSite();
            crosshair.localPosition = ToMap(cursor);
        }

        /// <summary>Fit the map into the screen area right of the info panel, whatever the aspect ratio.</summary>
        void FitCamera()
        {
            float panelFrac = Mathf.Clamp01((GameManager.PanelWidth + 30f) * GameManager.UiScale / Screen.width);
            float usable = Mathf.Max(0.3f, 1f - panelFrac - 0.03f);
            float halfW = MapW / usable / 2f;
            float ortho = Mathf.Max(halfW / Cam.aspect, (MapH + 8f) / 2f);
            halfW = ortho * Cam.aspect;
            Cam.orthographicSize = ortho;
            float centerFrac = panelFrac + usable / 2f;
            gm.Follow(new Vector2(-(centerFrac - 0.5f) * 2f * halfW, 1f), 0f);
        }

        public override void Exit()
        {
            if (tex != null) Object.Destroy(tex);
            base.Exit();
        }

        /// <summary>Start the crosshair on land near the equator.</summary>
        Vector2 FindStartSite()
        {
            for (float x = 0; x < terrain.circumference; x += 6f)
            {
                var p = new Vector2(x, terrain.worldHeight / 2f);
                if (CanLand(p)) return p;
            }
            return new Vector2(0f, terrain.worldHeight / 2f);
        }

        bool CanLand(Vector2 p) => terrain.Passable(p.x, p.y) && (planet.type == PlanetType.Ice || terrain.ClassAt(p.x, p.y) != TerrainClass.Sea);

        public override void Tick(float dt)
        {
            Vector2 move = Controls.Move();
            if (move.sqrMagnitude > 0f)
            {
                float speed = terrain.circumference * 0.18f; // tiles per second
                cursor += move * speed * dt;
                cursor.x = terrain.WrapX(cursor.x);
                cursor.y = Mathf.Clamp(cursor.y, 1f, terrain.worldHeight - 1f);
            }
            if (Controls.Clicked(out var screen) && !OverPanel(screen))
            {
                Vector2 w = Cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 10f));
                if (w.x >= MapOrigin.x && w.x <= MapOrigin.x + MapW && w.y >= MapOrigin.y && w.y <= MapOrigin.y + MapH)
                    cursor = ToWorld(w);
            }
            crosshair.localPosition = ToMap(cursor);

            if (Controls.Action()) TryLand();
            if (Controls.Map() || Keyboard_Escape()) Cancel();
        }

        static bool Keyboard_Escape()
        {
            var k = UnityEngine.InputSystem.Keyboard.current;
            return k != null && k.escapeKey.wasPressedThisFrame;
        }

        void Cancel() => gm.Go(new SystemView(systemReturn));

        void TryLand()
        {
            if (!CanLand(cursor)) { gm.Say($"Can't land in a {terrain.SeaName}. Pick a spot on land."); return; }
            float cost = Rules.LandingFuel(S, planet);
            if (S.fuel < cost) { gm.Say($"Landing needs {cost:0} fuel; you have {S.fuel:0.0}."); return; }

            Vector2 site = cursor;
            Vector2 mapPoint = ToMap(site);
            float startSize = Cam.orthographicSize;
            Vector3 camStart = Cam.transform.position;

            gm.Play(new Cutscene
            {
                duration = 3.5f,
                caption = t => t < 0.75f ? $"Descending to {terrain.Describe(terrain.ClassAt(site.x, site.y))} at {LatLongText(site)}" : "Touchdown",
                onUpdate = (t, dt) =>
                {
                    float a = Cutscene.Smooth(Cutscene.Phase(t, 0f, 0.8f));
                    Cam.orthographicSize = Mathf.Lerp(startSize, 0.6f, a);
                    var target = new Vector3(mapPoint.x, mapPoint.y, camStart.z);
                    Cam.transform.position = Vector3.Lerp(camStart, target, a);
                    gm.FadeColor = Color.black;
                    gm.FadeAlpha = Cutscene.Phase(t, 0.7f, 1f);
                },
                onComplete = () =>
                {
                    S.fuel -= cost;
                    S.AdvanceDays(1);
                    Crew.Gain(S, Roles.Science, GameData.T("XpLanding", 1f));
                    gm.FadeColor = Color.black;
                    gm.FadeAlpha = 1f;
                    gm.Go(new SurfaceView(planet, systemReturn, site));
                    gm.FadeIn(0.8f);
                }
            });
        }

        string LatLongText(Vector2 p)
        {
            float lat = terrain.Latitude(p.y), lon = terrain.Longitude(p.x);
            return $"{Mathf.Abs(lat):0}°{(lat >= 0 ? "N" : "S")} {Mathf.Abs(lon):0}°{(lon >= 0 ? "E" : "W")}";
        }

        public override void WorldGui()
        {
            gm.WorldLabel(MapOrigin + new Vector2(MapW / 2f, MapH), $"{planet.name} - topographic survey", -26);
        }

        public override void Gui()
        {
            var cls = terrain.ClassAt(cursor.x, cursor.y);
            float e = terrain.Elevation(cursor.x, cursor.y);
            int metres = Mathf.RoundToInt((e - terrain.seaLevel) * 9000f);

            GUILayout.Label($"Landing site: {planet.name}", gm.Header);
            GUILayout.Label("Move the crosshair: WASD / stick, or click the map. Land: E / A. Back: Esc / M.", gm.Hint);
            GUILayout.Space(6);
            GUILayout.Label($"Position: {LatLongText(cursor)}", gm.Label);
            GUILayout.Label($"Terrain: {terrain.Describe(cls)}", gm.Label);
            GUILayout.Label(cls == TerrainClass.Sea ? $"Depth: {-metres:N0} m" : $"Elevation: {metres:N0} m", gm.Label);
            GUILayout.Space(6);

            GUILayout.Label($"Science Officer skill {Skill}.", gm.Hint);
            if (cls == TerrainClass.Sea) GUILayout.Label($"{terrain.SeaName}: no landing.", gm.Label);
            if (CanEstimate)
            {
                GUILayout.Label($"Mineral density: {PlanetTerrain.Level(terrain.MineralDensity(cursor.x, cursor.y), Detailed)}", gm.Label);
                GUILayout.Label($"Life signs: {PlanetTerrain.Level(terrain.LifeSigns(cursor.x, cursor.y), Detailed)}", gm.Label);
                if (!Detailed) GUILayout.Label($"Finer readings at skill {GameData.T("ScanDetailSkill", 120f):0}.", gm.Hint);
            }
            else GUILayout.Label($"Mineral and life readings need skill {GameData.T("ScanEstimateSkill", 40f):0}.", gm.Hint);

            if (planet.inhabitedBy != null)
                GUILayout.Label(CanEstimate ? $"Cities detected: {planet.inhabitedBy} civilization (the pale dots on the map)." : "Faint artificial signals detected.", gm.Label);

            if (!DetectsRuins)
                GUILayout.Label($"Ruin detection needs skill {GameData.T("ScanRuinsSkill", 100f):0}.", gm.Hint);
            else if (ruinAreas.Count == 0)
                GUILayout.Label("Scan detected no ruins.", gm.Label);
            else
            {
                float nearest = float.MaxValue;
                foreach (var r in ruinAreas)
                {
                    float dx = Mathf.Abs(r.x - cursor.x);
                    dx = Mathf.Min(dx, terrain.circumference - dx);
                    nearest = Mathf.Min(nearest, new Vector2(dx, r.y - cursor.y).magnitude);
                }
                bool precise = ruinRadius <= GameData.T("RuinAreaMinRadius", 1.5f) + 0.01f;
                GUILayout.Label(precise
                    ? $"Ruins located: {ruinAreas.Count}. Nearest: {nearest:0} km."
                    : $"Possible ruins: {ruinAreas.Count} search areas (~{ruinRadius:0} km radius). Nearest area: {nearest:0} km.", gm.Label);
            }
            if (S.current.territory != null) GUILayout.Label($"This system is {S.current.territory} territory.", gm.Hint);

            GUILayout.Space(6);
            float cost = Rules.LandingFuel(S, planet);
            if (gm.Button($"Land here (E) - {cost:0} fuel, 1 day", CanLand(cursor) && S.fuel >= cost)) TryLand();
            GUILayout.FlexibleSpace();
            if (gm.Button("Back to orbit (Esc)")) Cancel();
        }
    }
}
