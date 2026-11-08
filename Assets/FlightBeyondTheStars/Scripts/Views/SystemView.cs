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
        float boundary;
        Vector2 scroll;

        public SystemView(Vector2? spawnAt) { spawn = spawnAt; }

        public override void Enter()
        {
            var sys = S.current;
            sys.visited = true;
            Cam.orthographicSize = 10f;

            Draw.Dot("Star", root, Vector2.zero, 3f, sys.color, 5);
            Draw.Dot("Glow", root, Vector2.zero, 5f, new Color(sys.color.r, sys.color.g, sys.color.b, 0.15f), 4);

            boundary = 8f;
            foreach (var p in sys.planets)
            {
                Draw.Dot(p.name, root, p.Position, p.size, PlanetColor(p.type), 10);
                boundary = Mathf.Max(boundary, p.orbitRadius + 6f);
            }

            hasStarport = sys.starportRace != null;
            if (hasStarport)
            {
                starportPos = sys.planets.Count > 0 ? sys.planets[0].Position + new Vector2(1.8f, 1.2f) : new Vector2(6f, 6f);
                var sp = Draw.MakeSprite("Starport", root, Draw.Square, starportPos, new Vector2(0.9f, 0.9f), new Color(0.75f, 0.8f, 0.9f), 11);
                sp.transform.localRotation = Quaternion.Euler(0, 0, 45);
            }

            ship = Draw.Ship("Ship", root, new Color(0.4f, 0.9f, 1f), 0.6f);
            ship.localPosition = spawn ?? new Vector2(0f, -4f);
            gm.Follow(ship.localPosition, 0f);
        }

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
            if (docked) return;

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
            if (Controls.Map()) gm.Go(new GalaxyView());
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
                    gm.Say($"Docked at {S.current.name} starport ({S.current.starportRace} market).");
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
                    gm.Say($"Scan of {p.name} complete: {p.type}, {p.gravity:0.0} g. Survey record added to cargo.");
                }
            });
        }

        void Land(Planet p)
        {
            if (!p.Landable) { gm.Say($"{p.name} is a gas giant. No landing possible."); return; }
            float cost = Rules.LandingFuel(p);
            if (S.fuel < cost) { gm.Say($"Landing needs {cost:0} fuel; you have {S.fuel:0.0}."); return; }

            Vector2 start = ship.localPosition;
            float startSize = Cam.orthographicSize;
            Vector2 dir = (p.Position - start).normalized;
            Vector2 back = p.Position + new Vector2(0f, -(p.size / 2f + 0.9f));
            velocity = Vector2.zero;

            gm.Play(new Cutscene
            {
                duration = 4f,
                caption = t => t < 0.75f ? $"Descending to {p.name} ({p.gravity:0.0} g)" : "Touchdown",
                onUpdate = (t, dt) =>
                {
                    float a = Cutscene.Smooth(Cutscene.Phase(t, 0f, 0.75f));
                    Vector2 pos = Vector2.Lerp(start, p.Position, a);
                    ship.localPosition = pos;
                    ship.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                    ship.localScale = Vector3.one * Mathf.Lerp(1f, 0.25f, a);
                    Cam.orthographicSize = Mathf.Lerp(startSize, p.size * 0.8f, a);
                    gm.Follow(pos, 0f);
                    gm.FadeColor = Color.black;
                    gm.FadeAlpha = Cutscene.Phase(t, 0.7f, 1f);
                },
                onComplete = () =>
                {
                    S.fuel -= cost;
                    S.day += 1;
                    gm.FadeColor = Color.black;
                    gm.FadeAlpha = 1f;
                    gm.Go(new SurfaceView(p, back));
                    gm.FadeIn(0.8f);
                }
            });
        }

        public override void WorldGui()
        {
            foreach (var p in S.current.planets)
                gm.WorldLabel(p.Position, p.scanned ? $"{p.name} ({p.type})" : p.name, 14 + p.size * 8);
            if (hasStarport) gm.WorldLabel(starportPos, "Starport", 16);
        }

        public override void Gui()
        {
            if (docked) { TradeGui(); return; }

            var sys = S.current;
            GUILayout.Label(sys.name + " system", gm.Header);
            GUILayout.Label(sys.starportRace != null ? $"Starport: {sys.starportRace} market" : "No starport", gm.Label);
            GUILayout.Label("Fly: WASD / arrows / left stick.  Act: E / Space / A button.  Map: M.", gm.Hint);
            GUILayout.Space(8);

            if (nearStarport)
            {
                GUILayout.Label("Starport in range.", gm.Label);
                if (gm.Button("Dock (E)")) Dock();
            }
            else if (near != null)
            {
                GUILayout.Label(near.name, gm.Header);
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
                        float cost = Rules.LandingFuel(near);
                        if (gm.Button($"Land (E) - {cost:0} fuel, 1 day", S.fuel >= cost)) Land(near);
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
            GUILayout.Space(6);

            scroll = GUILayout.BeginScrollView(scroll);
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

            GUILayout.Space(10);
            GUILayout.Label("Fuel", gm.Header);
            float fuelPrice = Rules.FuelPrice(S, sys);
            float space = S.hull.fuelTank - S.fuel;
            GUILayout.Label($"Drive crystal fuel @ {fuelPrice:N0} cr per unit. Tank {S.fuel:0.0}/{S.hull.fuelTank:0}.", gm.Label);
            int affordable = Mathf.FloorToInt(Mathf.Min(space, S.credits / Mathf.Max(1f, fuelPrice)));
            GUILayout.BeginHorizontal();
            if (gm.Button("Buy 1", affordable >= 1)) BuyFuel(1, fuelPrice);
            if (gm.Button($"Fill ({affordable})", affordable >= 1)) BuyFuel(affordable, fuelPrice);
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
            GUILayout.Label("Upgrades", gm.Header);
            if (S.landerAutopilot)
                GUILayout.Label("Lander Autopilot: installed.", gm.Label);
            else
            {
                GUILayout.Label("Lander Autopilot: call your lander to the rover anywhere on a planet (costs ship fuel each use).", gm.Label);
                if (gm.Button($"Buy ({GameState.LanderAutopilotCost:N0} cr)", S.credits >= GameState.LanderAutopilotCost))
                {
                    S.credits -= GameState.LanderAutopilotCost;
                    S.landerAutopilot = true;
                    gm.Say("Lander Autopilot installed.");
                }
            }
            GUILayout.EndScrollView();

            if (gm.Button("Undock")) Undock();
        }

        void Sell(string commodity, int qty, float price)
        {
            S.RemoveCargo(commodity, qty);
            S.credits += qty * price;
            gm.Say($"Sold {qty} {commodity} for {qty * price:N0} cr.");
        }

        void BuyFuel(int units, float price)
        {
            S.fuel += units;
            S.credits -= units * price;
            gm.Say($"Bought {units} fuel for {units * price:N0} cr.");
        }
    }
}
