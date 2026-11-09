using System.Collections.Generic;
using UnityEngine;

namespace FBTS
{
    /// <summary>In-system flight: fly to planets, scan them, land, and dock at the starport to trade.</summary>
    public class SystemView : View
    {
        readonly Vector2? spawn;
        Transform ship;
        Vector2 velocity;
        Vector2 starportPos;
        bool hasStarport;
        Planet near;
        bool nearStarport;
        bool docked;
        public bool Docked => docked;
        float boundary;
        Vector2 scroll;

        // Ships met here (at most one group at a time)
        Encounter enc;
        float encTimer;
        float sinceContact;      // seconds flown in this system since the last contact left
#if UNITY_EDITOR
        static int debugEncounter;   // F7 cycles through the Encounters tab
#endif
        const float EncTalkRange = 5f;

        public SystemView(Vector2? spawnAt) { spawn = spawnAt; }

        public override void Enter()
        {
            var sys = S.current;
            sys.visited = true;
            Cam.orthographicSize = 10f;

            Art.Sun(root, Vector2.zero, 3f, sys.color);

            boundary = 8f;
            foreach (var p in sys.planets)
            {
                Art.Planet(root, p, Vector2.zero, PlanetColor(p.type));
                boundary = Mathf.Max(boundary, p.orbitRadius + 6f);
            }

            hasStarport = sys.starportRace != null;
            if (hasStarport)
            {
                starportPos = sys.planets.Count > 0 ? sys.planets[0].Position + new Vector2(1.8f, 1.2f) : new Vector2(6f, 6f);
                Art.Station(sys.starportRace, root, starportPos, 1.7f);
            }

            ship = Art.Ship("player", "Ship", root, new Color(0.4f, 0.9f, 1f), 1.1f);
            ship.localPosition = spawn ?? new Vector2(0f, -4f);
            gm.Follow(ship.localPosition, 0f);

            // Autosave on arrival (after a jump, a launch or a load). Day 0 is skipped so starting
            // a new game never overwrites the previous autosave.
            // The threat clock may have moved on while we travelled.
            foreach (var e in WorldEvents.Sync(S)) gm.Say($"News on the open band: {e.name}.");

            if (S.day > 0) gm.AutoSave();

            // Arriving by jump: is anyone here? (Rolled after the autosave, so loading can't skip it.)
            if (S.pendingEncounter != null)
            {
                // Intercepted at a waypoint on a chained course
                S.justArrived = false;
                var e = S.pendingEncounter;
                S.pendingEncounter = null;
                SpawnEncounter(e);
            }
            else if (S.justArrived)
            {
                S.justArrived = false;
                var e = Encounter.Roll(S, S.current, new System.Random(System.Environment.TickCount), Encounter.ArrivalChance(S));
                if (e != null) SpawnEncounter(e);
            }
        }

        // ---------------------------------------------------------------- Encounters

        public static Color RaceColor(string race)
        {
            switch (race)
            {
                case "Orrun Combine": return new Color(1f, 0.8f, 0.35f);
                case "Varkesh": return new Color(1f, 0.4f, 0.3f);
                case "Skarrow": return new Color(0.7f, 0.9f, 0.4f);
                case "Mother": return new Color(0.55f, 0.55f, 0.6f);
                default: return new Color(0.9f, 0.6f, 0.9f);
            }
        }

        void SpawnEncounter(Encounter e)
        {
            enc = e;
            float ang = Random.value * Mathf.PI * 2f;
            e.pos = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (boundary - 1f);
            e.marker = new GameObject("Encounter").transform;
            e.marker.SetParent(root, false);
            int n = Mathf.Max(1, e.row.count);
            for (int i = 0; i < n; i++)
            {
                var sh = Art.Ship(Art.ShipKind(e.row.race), e.row.name, e.marker, RaceColor(e.row.race), 0.85f);
                sh.localPosition = new Vector2((i - (n - 1) / 2f) * 1.0f, -Mathf.Abs(i - (n - 1) / 2f) * 0.6f);
            }
            e.marker.localPosition = e.pos;
            encTimer = 0f;
            string what = e.hostile ? "on an intercept course!" : e.row.behavior == "Patrol" ? "approaching." : "in the system.";
            gm.Toast($"Contact: {e.row.name}", 2.5f);
            gm.Say($"Sensors: {e.row.name}{(n > 1 ? $" ({n} ships)" : "")} {what}");
        }

        /// <summary>While we fly around a system, ships can drop in: on average one every
        /// InSystemEncounterInterval seconds of flight, never sooner than EncounterCooldown after the last.</summary>
        void MaybeSpawnInSystem(float dt)
        {
            if (enc != null) return;
            sinceContact += dt;
            if (sinceContact < GameData.T("EncounterCooldown", 30f)) return;
            float interval = GameData.T("InSystemEncounterInterval", 90f) / (1f + Encounter.StageBonus(S) * 4f);
            if (Random.value >= dt / Mathf.Max(5f, interval)) return;
            var e = Encounter.Roll(S, S.current, new System.Random(System.Environment.TickCount), 1f);
            if (e == null) { sinceContact = 0f; return; }
            SpawnEncounter(e);
            gm.Toast($"{e.row.name} dropping out of hyperspace", 2.5f);
        }

        /// <summary>Testing aid (editor only): F7 spawns the next kind of ship from the Encounters tab,
        /// replacing any current contact. Ignores territory and threat-stage rules.</summary>
        void DebugSpawn()
        {
#if UNITY_EDITOR
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null || !kb.f7Key.wasPressedThisFrame || GameData.Encounters.Length == 0) return;
            var row = GameData.Encounters[debugEncounter % GameData.Encounters.Length];
            debugEncounter++;
            RemoveEncounter();
            SpawnEncounter(Encounter.Create(S, row));
            gm.Toast($"Debug: spawned {row.name} (F7 for next)", 2f);
#endif
        }

        void TickEncounter(float dt, Vector2 shipPos)
        {
            DebugSpawn();
            MaybeSpawnInSystem(dt);
            if (enc == null) return;
            encTimer += dt;
            var r = enc.row;
            float speed = r.speed / 100f * 6f * 0.8f;
            Vector2 toShip = shipPos - enc.pos;
            float dist = toShip.magnitude;
            Vector2 move;

            if (enc.leaving)
            {
                move = (enc.pos.sqrMagnitude > 0.01f ? enc.pos.normalized : Vector2.up) * speed;
                if (enc.pos.magnitude > boundary + 2f) { RemoveEncounter(); return; }
            }
            else if (enc.hostile || r.behavior == "Patrol")
            {
                // Hostiles close in; friendly patrols come over to ask our business.
                bool talkFirst = !enc.hailed && enc.CanTalk && (r.behavior == "Patrol" || r.behavior == "Raider");
                float stopAt = enc.hostile && !talkFirst ? 0f : 3f;
                move = dist > stopAt ? toShip.normalized * speed : Vector2.zero;

                if (talkFirst && dist < 4f) { TheyHail(); return; }
                if (enc.hostile && dist < 1.6f) { StartCombat(); return; }
            }
            else
            {
                // Traders drift through and move on after a while.
                move = new Vector2(-enc.pos.y, enc.pos.x).normalized * speed * 0.4f;
                if (encTimer > 45f) enc.leaving = true;
            }
            enc.pos += move * dt;
            enc.marker.localPosition = enc.pos;
            if (move.sqrMagnitude > 0.001f)
                enc.marker.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(move.y, move.x) * Mathf.Rad2Deg);
        }

        void RemoveEncounter()
        {
            if (enc?.marker != null) Object.Destroy(enc.marker.gameObject);
            enc = null;
            sinceContact = 0f;
        }

        System.Collections.Generic.Dictionary<string, object> EncVars(bool theyHailed) =>
            new System.Collections.Generic.Dictionary<string, object>
            {
                { "in_combat", false }, { "odds", enc.Odds(S) }, { "they_hailed", theyHailed }
            };

        /// <summary>They open a channel to us (patrols asking our business, raiders making demands).</summary>
        void TheyHail()
        {
            enc.hailed = true;
            velocity = Vector2.zero;
            var e = enc;
            gm.Hail(e.Race, "encounter", e.row.name, EncVars(true),
                onAttack: () => e.hostile = true,
                onStandDown: () => { if (e.hostile) Crew.Morale(S, "TalkedDown"); e.hostile = false; },
                onClose: () => AfterTalk(e));
        }

        void HailEncounter()
        {
            if (enc == null) return;
            enc.hailed = true;
            velocity = Vector2.zero;
            if (!enc.CanTalk) { gm.Say("No response. Only a carrier tone."); return; }
            var e = enc;
            gm.Hail(e.Race, "encounter", e.row.name, EncVars(false),
                onAttack: () => e.hostile = true,
                onStandDown: () => { if (e.hostile) Crew.Morale(S, "TalkedDown"); e.hostile = false; },
                onClose: () => AfterTalk(e));
        }

        void AfterTalk(Encounter e)
        {
            if (e != enc) return;
            if (!e.hostile && e.row.behavior != "Trader") e.leaving = true;   // they've said their piece
        }

        void AttackEncounter()
        {
            if (enc == null) return;
            if (!enc.hostile && enc.row.race != "Mother")
            {
                int pen = Mathf.RoundToInt(GameData.T("AttackRepPenalty", 10f));
                S.AddRep(enc.row.race, -pen);
                gm.Say($"You opened fire without provocation. The {enc.row.race} will remember (standing -{pen}).");
                Crew.Morale(S, "Unprovoked");
            }
            enc.provoked = !enc.hostile;
            enc.hostile = true;
            StartCombat();
        }

        void StartCombat()
        {
            var e = enc;
            velocity = Vector2.zero;
            gm.FadeColor = new Color(0.45f, 0f, 0f);
            gm.FadeAlpha = 1f;
            gm.Go(new CombatView(e, ship.localPosition));
            gm.FadeIn(0.6f);
        }

        bool EncounterInRange => enc != null && !enc.leaving && Vector2.Distance(ship.localPosition, enc.pos) < EncTalkRange;

        public static Color PlanetColor(PlanetType t)
        {
            switch (t)
            {
                case PlanetType.Rocky: return new Color(0.6f, 0.5f, 0.4f);
                case PlanetType.Molten: return new Color(0.9f, 0.35f, 0.15f);
                case PlanetType.Ice: return new Color(0.8f, 0.9f, 1f);
                case PlanetType.Ocean: return new Color(0.2f, 0.45f, 0.85f);
                default: return new Color(0.85f, 0.7f, 0.5f);
            }
        }

        public override void Tick(float dt)
        {
            if (docked)
            {
                if (Controls.Hail()) HailStation();
                return;
            }

            float topSpeed = (S.hull != null ? S.hull.speed : 100f) / 100f * 6f;
            Vector2 input = Controls.Move();
            velocity = Vector2.Lerp(velocity, input * topSpeed, 1f - Mathf.Exp(-4f * dt));
            Vector2 pos = (Vector2)ship.localPosition + velocity * dt;
            if (pos.magnitude > boundary) pos = pos.normalized * boundary;
            ship.localPosition = pos;
            if (velocity.sqrMagnitude > 0.01f)
                ship.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg);
            gm.Follow(pos, 5f);

            near = null;
            float best = float.MaxValue;
            foreach (var p in S.current.planets)
            {
                float d = Vector2.Distance(pos, p.Position);
                if (d < p.size / 2f + 1.3f && d < best) { best = d; near = p; }
            }
            nearStarport = hasStarport && Vector2.Distance(pos, starportPos) < 1.6f;

            if (Controls.Action())
            {
                if (nearStarport) Dock();
                else if (near != null && !near.scanned) Scan(near);
                else if (near != null && near.Landable) Land(near);
            }
            TickEncounter(dt, pos);
            if (Controls.Map()) gm.Go(new GalaxyView());
            if (Controls.Hail())
            {
                if (EncounterInRange) HailEncounter();
                else if (near != null && CanHail(near)) HailPlanet(near);
            }
            if (Controls.Attack() && EncounterInRange) AttackEncounter();
        }

        // ---------------------------------------------------------------- Hailing

        bool StationTalks => Conversation.HasKnot(S.current.starportRace, "station");
        static bool CanHail(Planet p) => p.inhabitedBy != null && Conversation.HasKnot(p.inhabitedBy, "homeworld");

        void HailStation()
        {
            if (!StationTalks) return;
            gm.Hail(S.current.starportRace, "station", $"{S.current.name} starport");
        }

        void HailPlanet(Planet p)
        {
            velocity = Vector2.zero;
            gm.Hail(p.inhabitedBy, "homeworld", p.name);
        }

        void PostureHint() =>
            GUILayout.Label($"You'll open with a {S.posture} posture (change it during a conversation with Tab).", gm.Hint);

        void StandingGui(string race)
        {
            int r = S.Rep(race);
            GUILayout.Label($"Standing with the {race}: {r:+0;-0;0}. Better standing means better prices.", gm.Hint);
        }

        // ---------------------------------------------------------------- Transitions

        void Dock()
        {
            Vector2 start = ship.localPosition;
            Vector2 end = starportPos + new Vector2(0f, -0.75f);
            float startSize = Cam.orthographicSize;
            Quaternion startRot = ship.localRotation;
            SpriteRenderer ring = null;
            velocity = Vector2.zero;

            gm.Play(new Cutscene
            {
                duration = 3f,
                caption = t => t < 0.8f ? $"Docking at {S.current.name} starport" : "Docking clamps engaged",
                onStart = r => ring = Draw.Dot("DockRing", r, starportPos, 0.1f, new Color(0.6f, 0.9f, 1f, 0.4f), 9),
                onUpdate = (t, dt) =>
                {
                    float a = Cutscene.Smooth(Cutscene.Phase(t, 0f, 0.8f));
                    Vector2 pos = Vector2.Lerp(start, end, a);
                    ship.localPosition = pos;
                    ship.localRotation = Quaternion.Slerp(startRot, Quaternion.Euler(0, 0, 90), Cutscene.Smooth(Cutscene.Phase(t, 0f, 0.5f)));
                    Cam.orthographicSize = Mathf.Lerp(startSize, 5f, Cutscene.Smooth(t));
                    gm.Follow(Vector2.Lerp(pos, starportPos, 0.5f), 0f);
                    float pulse = (t * 2.5f) % 1f;
                    ring.transform.localScale = Vector3.one * (0.5f + pulse * 2.5f);
                    Fx.SetAlpha(ring, 0.5f * (1f - pulse));
                },
                onComplete = () =>
                {
                    ship.localPosition = end;
                    ship.localRotation = Quaternion.Euler(0, 0, 90);
                    docked = true;
                    var gone = Crew.ArriveAtPort(S);
                    gm.Say($"Docked at {S.current.name} starport ({S.current.starportRace} market).");
                    if (gone.Count > 0)
                        gm.ShowNote("Crew left the ship", string.Join(", ", gone) +
                            (gone.Count == 1 ? " has" : " have") + " had enough and walked off at the dock. Morale had fallen too low.\n\n" +
                            "Hire replacements or reassign stations on the Crew screen (C).");
                    gm.AutoSave();
                }
            });
        }

        void Undock()
        {
            Vector2 start = ship.localPosition;
            Vector2 end = starportPos + new Vector2(0f, -2.6f);
            float startSize = Cam.orthographicSize;

            gm.Play(new Cutscene
            {
                duration = 1.5f,
                caption = t => "Undocking",
                onUpdate = (t, dt) =>
                {
                    float a = Cutscene.Smooth(t);
                    ship.localPosition = Vector2.Lerp(start, end, a);
                    Cam.orthographicSize = Mathf.Lerp(startSize, 10f, a);
                    gm.Follow(ship.localPosition, 0f);
                },
                onComplete = () =>
                {
                    docked = false;
                    ship.localPosition = end;
                    Cam.orthographicSize = 10f;
                    gm.Follow(end, 0f);
                }
            });
        }

        void Scan(Planet p)
        {
            Vector2 shipPos = ship.localPosition;
            SpriteRenderer beam = null, ring1 = null, ring2 = null, sweep = null;
            var scanColor = new Color(0.4f, 1f, 0.8f, 0.8f);
            velocity = Vector2.zero;

            gm.Play(new Cutscene
            {
                duration = 3f,
                caption = t => $"Scanning {p.name}... {Mathf.RoundToInt(t * 100f)}%",
                onStart = r =>
                {
                    beam = Draw.Line("Beam", r, shipPos, p.Position, 0.08f, scanColor, 15);
                    ring1 = Draw.Dot("Pulse1", r, p.Position, 0.1f, scanColor, 14);
                    ring2 = Draw.Dot("Pulse2", r, p.Position, 0.1f, scanColor, 14);
                    sweep = Draw.Line("Sweep", r, p.Position, p.Position + Vector2.right * p.size, 0.06f, Color.white, 16);
                },
                onUpdate = (t, dt) =>
                {
                    float max = p.size * 2.2f;
                    float p1 = (t * 3f) % 1f, p2 = (t * 3f + 0.5f) % 1f;
                    ring1.transform.localScale = Vector3.one * (0.2f + p1 * max);
                    ring2.transform.localScale = Vector3.one * (0.2f + p2 * max);
                    Fx.SetAlpha(ring1, 0.35f * (1f - p1));
                    Fx.SetAlpha(ring2, 0.35f * (1f - p2));
                    float ang = t * Mathf.PI * 6f;
                    Fx.SetLine(sweep, p.Position, p.Position + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * p.size * 0.6f, 0.06f);
                    Fx.SetAlpha(beam, 0.45f + 0.35f * Mathf.Sin(t * 40f));
                },
                onComplete = () =>
                {
                    p.scanned = true;
                    S.AddCargo("Survey record (per planet)", 1);
                    Crew.Gain(S, Roles.Science, GameData.T("XpScan", 2f));
                    gm.Say($"Scan of {p.name} complete: {p.type}, {p.gravity:0.0} g. Survey record added to cargo.");
                }
            });
        }

        /// <summary>Open the orbital landing map. Fuel and time are spent when the lander actually descends.</summary>
        void Land(Planet p)
        {
            if (!p.Landable) { gm.Say($"{p.name} is a gas giant. No landing possible."); return; }
            if (!p.scanned) { gm.Say("Scan the planet first to map landing sites."); return; }
            Vector2 back = p.Position + new Vector2(0f, -(p.size / 2f + 0.9f));
            velocity = Vector2.zero;
            gm.FadeColor = Color.black;
            gm.FadeAlpha = 1f;
            gm.Go(new OrbitView(p, back));
            gm.FadeIn(0.5f);
        }

        public override void WorldGui()
        {
            foreach (var p in S.current.planets)
                gm.WorldLabel(p.Position, p.scanned ? $"{p.name} ({p.type})" : p.name, 14 + p.size * 8);
            if (hasStarport) gm.WorldLabel(starportPos, "Starport", 16);
            if (enc != null && !enc.leaving) gm.WorldLabel(enc.pos, enc.row.name, 16);
        }

        public override void Gui()
        {
            if (docked) { TradeGui(); return; }

            var sys = S.current;
            GUILayout.Label(sys.name + " system", gm.Header);
            GUILayout.Label(sys.starportRace != null ? $"Starport: {sys.starportRace} market" : "No starport", gm.Label);
            GUILayout.Label(sys.territory != null ? $"Territory: {sys.territory}" : "Unclaimed space", gm.Label);
            GUILayout.Label("Fly: WASD / arrows / left stick.  Act: E / Space / A button.  Map: M.", gm.Hint);
            GUILayout.Space(8);

            if (enc != null && !enc.leaving)
            {
                var r = enc.row;
                float d = Vector2.Distance(ship.localPosition, enc.pos);
                GUILayout.Label($"Contact: {r.name}{(r.count > 1 ? $" x{r.count}" : "")}", gm.Header);
                GUILayout.Label((enc.hostile ? "Hostile, closing in." : r.behavior == "Patrol" ? "Approaching to hail." : "Not hostile.") +
                                $"  Range {d:0.0}", gm.Label);
                if (r.race != "Mother") GUILayout.Label($"Standing with the {r.race}: {S.Rep(r.race):+0;-0;0}", gm.Hint);
                if (d < EncTalkRange)
                {
                    GUILayout.BeginHorizontal();
                    if (gm.Button("Hail (H)")) HailEncounter();
                    if (gm.Button("Attack (F)")) AttackEncounter();
                    GUILayout.EndHorizontal();
                }
                else GUILayout.Label("Close to hail or attack. Or fly to the galaxy map (M) to jump away.", gm.Hint);
                GUILayout.Space(8);
            }

            if (nearStarport)
            {
                GUILayout.Label("Starport in range.", gm.Label);
                if (gm.Button("Dock (E)")) Dock();
            }
            else if (near != null)
            {
                GUILayout.Label(near.name, gm.Header);
                if (near.inhabitedBy != null)
                {
                    GUILayout.Label($"Inhabited: {near.inhabitedBy} cities.", gm.Label);
                    if (CanHail(near))
                    {
                        if (gm.Button("Hail the planet (H)")) HailPlanet(near);
                        PostureHint();
                    }
                }
                if (!near.scanned)
                {
                    GUILayout.Label("Unknown world. Scan to learn more.", gm.Label);
                    if (gm.Button("Scan (E)")) Scan(near);
                }
                else
                {
                    GUILayout.Label($"Type: {near.type}\nGravity: {near.gravity:0.0} g\nHazard: {near.hazard}", gm.Label);
                    if (near.minerals.Count > 0) GUILayout.Label("Resources: " + string.Join(", ", near.minerals), gm.Label);
                    if (near.Landable)
                    {
                        if (gm.Button("Choose landing site (E)")) Land(near);
                    }
                    else GUILayout.Label("Too dense to land.", gm.Label);
                }
            }
            else
            {
                GUILayout.Label("Fly close to a planet or the starport.", gm.Label);
            }

            GUILayout.FlexibleSpace();
            if (gm.Button("Galaxy map (M)")) gm.Go(new GalaxyView());
        }

        void TradeGui()
        {
            var sys = S.current;
            GUILayout.Label($"{sys.name} starport", gm.Header);
            GUILayout.Label($"{sys.starportRace} market. Comms Officer skill {S.commsSkill} improves prices.", gm.Hint);
            StandingGui(sys.starportRace);
            if (StationTalks)
            {
                if (gm.Button("Talk to the station (H)")) HailStation();
                PostureHint();
            }
            GUILayout.Space(6);

            int tab = GUILayout.Toolbar(portTab, PortTabs, GUILayout.Height(28));
            if (tab != portTab) { portTab = tab; scroll = Vector2.zero; }
            GUILayout.Space(4);
            scroll = GUILayout.BeginScrollView(scroll);
            if (portTab == 0) MarketGui(sys);
            else if (portTab == 1) ShipyardTabGui(sys);
            else CrewTabGui(sys);
            GUILayout.EndScrollView();

            if (gm.Button("Undock")) Undock();
        }

        static readonly string[] PortTabs = { "Market", "Shipyard", "Crew" };
        static int portTab;   // remembered between visits

        void ShipyardTabGui(StarSystem sys)
        {
            RepairGui();
            WeaponsGui();
            ShieldsGui();
            ModulesGui();
            RoverGui();
            if (sys.starportRace == "Humans") ShipyardGui();
            else
            {
                GUILayout.Space(10);
                GUILayout.Label("New hulls are sold only at Haven's shipyard.", gm.Hint);
            }
        }

        void CrewTabGui(StarSystem sys)
        {
            if (gm.Button("Open the crew screen (C)")) gm.ShowCrew();
            ShoreLeaveGui();
            if (sys.starportRace == "Humans") HiringGui();
            else
            {
                GUILayout.Space(10);
                GUILayout.Label("Recruits for hire are found at Haven.", gm.Hint);
            }
            TrainingGui();
        }

        void MarketGui(StarSystem sys)
        {
            GUILayout.Label("Sell cargo", gm.Header);
            if (S.cargo.Count == 0) GUILayout.Label("Cargo hold is empty.", gm.Label);
            foreach (var kv in new List<KeyValuePair<string, int>>(S.cargo))
            {
                float price = Rules.SellPrice(S, sys, kv.Key);
                GUILayout.Label($"{kv.Key} x{kv.Value}  @ {price:N0} cr", gm.Label);
                GUILayout.BeginHorizontal();
                bool buys = price > 0f;
                if (gm.Button("Sell 1", buys)) Sell(kv.Key, 1, price);
                if (gm.Button("Sell all", buys)) Sell(kv.Key, kv.Value, price);
                GUILayout.EndHorizontal();
                if (!buys) GUILayout.Label("This market won't buy it.", gm.Hint);
            }
            Market.ArtifactsGui(gm, S, sys.starportRace, 1f);

            GUILayout.Space(10);
            GUILayout.Label("Fuel", gm.Header);
            float fuelPrice = Rules.FuelPrice(S, sys);
            float space = S.FuelTank - S.fuel;
            GUILayout.Label($"Drive crystal fuel @ {fuelPrice:N0} cr per unit. Tank {S.fuel:0.0}/{S.FuelTank:0}.", gm.Label);
            int affordable = Mathf.FloorToInt(Mathf.Min(space, S.credits / Mathf.Max(1f, fuelPrice)));
            GUILayout.BeginHorizontal();
            if (gm.Button("Buy 1", affordable >= 1)) BuyFuel(1, fuelPrice);
            if (gm.Button($"Fill ({affordable})", affordable >= 1)) BuyFuel(affordable, fuelPrice);
            GUILayout.EndHorizontal();
        }

        // ---------------------------------------------------------------- Shop

        /// <summary>Starport training: each crew member can train in the station(s) they hold, up to their
        /// species' cap. Training raises skill but doesn't count toward levels (only experience does).</summary>
        void TrainingGui()
        {
            GUILayout.Space(10);
            GUILayout.Label("Crew training", gm.Header);
            int points = Mathf.RoundToInt(GameData.T("TrainPointsPerSession", 10f));
            bool any = false;
            foreach (var m in S.crew)
            {
                if (!m.Fit) continue;
                for (int role = 0; role < S.stations.Length; role++)
                {
                    if (S.stations[role] != m) continue;
                    any = true;
                    TrainRow(m, role, points);
                }
            }
            if (!any) GUILayout.Label("Nobody is assigned to a station.", gm.Hint);
        }

        void TrainRow(CrewMember m, int role, int points)
        {
            int cap = m.Cap(role);
            int from = m.Skill(role);
            int to = Mathf.Min(cap, from + points);
            string label = $"{m.name}, {Roles.Names[role]}";
            if (to <= from)
            {
                GUILayout.Label($"{label}: {from} ({m.species} maximum).", gm.Label);
                return;
            }
            float tb = GameData.T("TrainBase", 20f), ts = GameData.T("TrainScale", 100f);
            float cost = tb * ((to - from) + (to * to - from * from) / (2f * ts));
            int days = Mathf.Max(1, Mathf.CeilToInt((to - from) * GameData.T("TrainDaysPerPoint", 0.5f)));
            GUILayout.Label($"{label}: {from} / {cap}", gm.Label);
            if (gm.Button($"Train to {to} ({cost:N0} cr, {days} days)", S.credits >= cost))
            {
                S.credits -= cost;
                S.AdvanceDays(days);
                m.skills[role] = to;
                gm.Say($"{m.name} trained to {to} as {Roles.Names[role]}.");
            }
        }

        /// <summary>A day ashore lifts morale (once a week).</summary>
        void ShoreLeaveGui()
        {
            GUILayout.Space(10);
            GUILayout.Label("Shore leave", gm.Header);
            int wait = 7 - (S.day - S.lastShoreLeaveDay);
            if (wait > 0) { GUILayout.Label($"The crew had leave recently. Available again in {wait} days.", gm.Hint); return; }
            if (gm.Button("Give the crew a day's shore leave (+morale)"))
            {
                S.AdvanceDays(1);
                S.lastShoreLeaveDay = S.day;
                Crew.Morale(S, "ShoreLeave");
                gm.Say("The crew enjoyed a day on the station. Morale is up.");
            }
        }

        /// <summary>Haven's hiring hall: three human recruits each week.</summary>
        void HiringGui()
        {
            GUILayout.Space(10);
            int berths = S.hull != null ? Mathf.RoundToInt(S.hull.crewBerths) : 6;
            int alive = S.crew.FindAll(c => !c.dead).Count;
            GUILayout.Label($"Hiring hall ({alive} / {berths} berths)", gm.Header);
            var rng = new System.Random(S.seed * 7 + S.day / 7 * 31 + 3);
            for (int i = 0; i < 3; i++)
            {
                var r = Crew.Recruit(S, rng, "Humans", rng.Next(Roles.Names.Length), 45, 85);
                int main = 0;
                for (int k = 1; k < r.skills.Length; k++) if (r.skills[k] > r.skills[main]) main = k;
                if (S.crew.Exists(c => c.name == r.name)) continue;
                float cost = Crew.HireCost(r);
                GUILayout.Label($"{r.name}: {Roles.Names[main]} {r.Skill(main)}, {r.personality}", gm.Label);
                if (gm.Button($"Hire ({cost:N0} cr)", alive < berths && S.credits >= cost))
                {
                    S.credits -= cost;
                    S.crew.Add(r);
                    if (S.stations[main] == null) S.stations[main] = r;
                    gm.Say($"{r.name} joins the crew" + (S.stations[main] == r ? $" as {Roles.Names[main]}." : ". Assign a station on the Crew screen (C)."));
                }
            }
            if (alive >= berths) GUILayout.Label("No free berths. A bigger hull holds more crew.", gm.Hint);
            GUILayout.Label("New recruits arrive every week.", gm.Hint);
        }

        void RepairGui()
        {
            if (S.hullDamage <= 0.5f) return;
            GUILayout.Space(10);
            GUILayout.Label("Repairs", gm.Header);
            float per = GameData.T("RepairCostPerPoint", 12f) * (1f - S.Trait("RepairCost"));
            float full = Mathf.Ceil(S.hullDamage) * per;
            GUILayout.Label($"Hull {S.Hull:0} / {S.HullMax:0}. Full repair {full:N0} cr.", gm.Label);
            int affordable = Mathf.FloorToInt(Mathf.Min(S.hullDamage, Mathf.Max(0f, S.credits) / per));
            GUILayout.BeginHorizontal();
            if (gm.Button("Repair all", S.credits >= full)) { S.credits -= full; S.hullDamage = 0f; gm.Say("Hull fully repaired."); }
            if (affordable >= 1 && S.credits < full && gm.Button($"Repair {affordable}"))
            {
                S.credits -= affordable * per;
                S.hullDamage -= affordable;
                gm.Say($"Repaired {affordable} hull points.");
            }
            GUILayout.EndHorizontal();
        }

        /// <summary>Weapon fittings. Energy, kinetic and missile weapons for now; exotic alien weapons come later.</summary>
        void WeaponsGui()
        {
            GUILayout.Space(10);
            GUILayout.Label($"Weapons ({S.weapons.Count} / {S.WeaponSlots} hardpoints)", gm.Header);
            for (int i = 0; i < S.weapons.Count; i++)
            {
                var w = GameData.Weapon(S.weapons[i]);
                float resale = w != null ? w.cost * 0.5f : 0f;
                GUILayout.BeginHorizontal();
                GUILayout.Label(S.weapons[i], gm.Label);
                if (gm.Button($"Sell ({resale:N0})", S.weapons.Count > 1))
                {
                    S.credits += resale;
                    gm.Say($"Sold the {S.weapons[i]}.");
                    S.weapons.RemoveAt(i);
                    GUILayout.EndHorizontal();
                    break;
                }
                GUILayout.EndHorizontal();
            }
            bool room = S.weapons.Count < S.WeaponSlots;
            foreach (var w in GameData.Weapons)
            {
                if (w.family == "Exotic") continue;
                GUILayout.Label($"{w.weapon}: {w.damagePerShot:0} dmg x {w.shotsPerSecond:0.##}/s, range {w.range:0}" +
                                (string.IsNullOrEmpty(w.special) ? "" : $". {w.special}"), gm.Hint);
                if (gm.Button($"Fit {w.weapon} ({w.cost:N0} cr)", room && S.credits >= w.cost))
                {
                    S.credits -= w.cost;
                    S.weapons.Add(w.weapon);
                    gm.Say($"{w.weapon} fitted.");
                }
            }
            if (!room) GUILayout.Label("All hardpoints in use. Sell a weapon to fit another, or buy a bigger hull.", gm.Hint);
        }

        void ShieldsGui()
        {
            var cap = GameData.ShipSystem("Shields", "Capacity");
            var rech = GameData.ShipSystem("Shields", "Recharge");
            if (cap == null) return;
            GUILayout.Space(10);
            GUILayout.Label("Shield generator", gm.Header);
            int tier = S.shieldTier;
            GUILayout.Label($"Tier {tier}: {cap.Value(tier):0} points, recharge {(rech != null ? rech.Value(tier) : 0):0}/s.", gm.Label);
            if (tier >= 5) { GUILayout.Label("Fully upgraded.", gm.Hint); return; }
            int next = tier + 1;
            float cost = TierCost(cap, next);
            GUILayout.Label($"Tier {next}: {cap.Value(next):0} points, recharge {(rech != null ? rech.Value(next) : 0):0}/s.", gm.Hint);
            if (gm.Button($"Upgrade to tier {next} ({cost:N0} cr)", S.credits >= cost))
            {
                S.credits -= cost;
                S.shieldTier = next;
                gm.Say($"Shield generator upgraded to tier {next}.");
            }
        }

        void ModulesGui()
        {
            GUILayout.Space(10);
            GUILayout.Label("Ship modules", gm.Header);
            foreach (var u in GameData.Upgrades)
            {
                GUILayout.Label($"{u.name}: {u.description}", gm.Label);
                if (S.Has(u.id)) { GUILayout.Label("Installed.", gm.Hint); continue; }
                if (gm.Button($"Buy ({u.cost:N0} cr)", S.credits >= u.cost))
                {
                    S.credits -= u.cost;
                    S.upgrades.Add(u.id);
                    gm.Say($"{u.name} installed.");
                }
            }
        }

        void RoverGui()
        {
            var cargoRow = GameData.ShipSystem("Terrain vehicle", "Cargo");
            var fuelRow = GameData.ShipSystem("Terrain vehicle", "Fuel");
            if (cargoRow == null) return;

            GUILayout.Space(10);
            GUILayout.Label("Terrain vehicle", gm.Header);
            int tier = S.terrainVehicleTier;
            GUILayout.Label($"Tier {tier}: hold {cargoRow.Value(tier):0}, fuel {(fuelRow != null ? fuelRow.Value(tier) : 0):0} hours.", gm.Label);
            if (tier >= 5) { GUILayout.Label("Fully upgraded.", gm.Hint); return; }

            int next = tier + 1;
            float cost = TierCost(cargoRow, next);
            GUILayout.Label($"Tier {next}: hold {cargoRow.Value(next):0}, fuel {(fuelRow != null ? fuelRow.Value(next) : 0):0} hours.", gm.Hint);
            if (gm.Button($"Upgrade to tier {next} ({cost:N0} cr)", S.credits >= cost))
            {
                S.credits -= cost;
                S.terrainVehicleTier = next;
                gm.Say($"Terrain vehicle upgraded to tier {next}.");
            }
        }

        static float TierCost(ShipSystemRow row, int tier)
        {
            switch (tier)
            {
                case 2: return row.t2Cost;
                case 3: return row.t3Cost;
                case 4: return row.t4Cost;
                default: return row.t5Cost;
            }
        }

        void ShipyardGui()
        {
            GUILayout.Space(10);
            GUILayout.Label("Shipyard", gm.Header);
            float tradeIn = S.hull.cost * GameData.T("HullTradeInRate", 0.5f);
            GUILayout.Label($"Current hull: {S.hull.hullClass}. Trade-in value {tradeIn:N0} cr.", gm.Hint);
            foreach (var h in GameData.Hulls)
            {
                if (h.hullClass == S.hull.hullClass) continue;
                float price = Mathf.Max(0f, h.cost - tradeIn);
                float cargoAfter = h.cargoUnits + S.UpgradeBonus("Cargo");
                GUILayout.Label($"{h.hullClass}: hull {h.hullPoints:0}, cargo {h.cargoUnits:0}, berths {h.crewBerths:0}, tank {h.fuelTank:0}, " +
                                $"jump fuel x{h.jumpFuelMultiplier:0.00}, speed {h.speed:0}", gm.Label);
                bool fits = S.CargoUsed <= cargoAfter;
                if (gm.Button($"Buy ({price:N0} cr after trade-in)", fits && S.credits >= price))
                {
                    S.credits -= price;
                    S.hull = h;
                    S.hullDamage = 0f;   // a new hull comes undamaged
                    S.fuel = Mathf.Min(S.fuel, S.FuelTank);
                    gm.Say($"Welcome aboard your new {h.hullClass}.");
                }
                if (!fits) GUILayout.Label("Too much cargo aboard to fit this hull. Sell some first.", gm.Hint);
            }
        }

        void Sell(string commodity, int qty, float price)
        {
            S.RemoveCargo(commodity, qty);
            Market.Sold(gm, S, S.current.starportRace, $"{qty} {commodity}", qty * price);
        }

        void BuyFuel(int units, float price)
        {
            S.fuel += units;
            S.credits -= units * price;
            gm.Say($"Bought {units} fuel for {units * price:N0} cr.");
        }
    }
}
