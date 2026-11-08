using UnityEngine;

namespace FBTS
{
    /// <summary>Galaxy map: pick a destination and jump. Routes are cheap; free flight costs far more fuel and time.</summary>
    public class GalaxyView : View
    {
        StarSystem selected;

        public override void Enter()
        {
            Cam.orthographicSize = 24f;
            gm.Follow(S.current.pos, 0f);

            var g = S.galaxy;
            foreach (var s in g.systems)
                foreach (int id in s.links)
                    if (id > s.id)
                        Draw.Line("Route", root, s.pos, g.systems[id].pos, 0.12f, new Color(0.4f, 0.55f, 0.8f, 0.5f), 1);

            foreach (var s in g.systems)
            {
                Color c = s.visited ? s.color : Color.Lerp(s.color, new Color(0.3f, 0.3f, 0.4f), 0.5f);
                Draw.Dot(s.name, root, s.pos, s == S.current ? 1.4f : 1.0f, c, 5);
                if (s.starportRace != null)
                    Draw.MakeSprite("Port", root, Draw.Square, s.pos + new Vector2(0.9f, 0.9f), new Vector2(0.35f, 0.35f), new Color(0.75f, 0.8f, 0.9f), 6);
            }
            Draw.Dot("You are here", root, S.current.pos, 2.4f, new Color(0.4f, 0.9f, 1f, 0.3f), 3);
        }

        public override void Tick(float dt)
        {
            float scroll = Controls.Scroll();
            if (Mathf.Abs(scroll) > 0.01f)
                Cam.orthographicSize = Mathf.Clamp(Cam.orthographicSize - Mathf.Sign(scroll) * 2f, 8f, 45f);

            Vector2 pan = Controls.Move();
            if (pan.sqrMagnitude > 0f)
                Cam.transform.position += (Vector3)(pan * Cam.orthographicSize * dt);

            if (Controls.Clicked(out var screen) && !OverPanel(screen))
            {
                Vector2 world = Cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 10f));
                StarSystem pick = null;
                float best = 2f;
                foreach (var s in S.galaxy.systems)
                {
                    float d = Vector2.Distance(world, s.pos);
                    if (d < best) { best = d; pick = s; }
                }
                if (pick != null) selected = pick;
            }

            if (Controls.Map()) gm.Go(new SystemView(null));
        }

        void Jump(StarSystem target, float fuel, int days, bool route)
        {
            const int N = 140;
            var streaks = new SpriteRenderer[N];
            var ang = new float[N];
            var rad = new float[N];
            var spd = new float[N];
            Vector2 c = new Vector2(5000f, 5000f); // empty space far from the map
            Color tint = route ? new Color(0.6f, 0.8f, 1f, 0.9f) : new Color(1f, 0.75f, 0.45f, 0.8f);
            var from = S.current;

            gm.Play(new Cutscene
            {
                duration = route ? 4.5f : 6f,
                caption = t => (route ? $"Jumping to {target.name}" : $"Free flight to {target.name}") + $"  -  {days} days",
                onStart = r =>
                {
                    var rng = new System.Random(target.id * 7919 + S.day);
                    for (int i = 0; i < N; i++)
                    {
                        ang[i] = (float)(rng.NextDouble() * Mathf.PI * 2);
                        rad[i] = 0.5f + (float)rng.NextDouble() * 15f;
                        spd[i] = 0.6f + (float)rng.NextDouble() * 0.8f;
                        streaks[i] = Draw.Line("Streak", r, c, c + Vector2.right * 0.1f, 0.05f, tint, 2);
                    }
                    var ship = Draw.Ship("JumpShip", r, new Color(0.4f, 0.9f, 1f), 0.9f);
                    ship.localPosition = c;
                    ship.localRotation = Quaternion.Euler(0, 0, 90);
                    Cam.transform.position = new Vector3(c.x, c.y, -10f);
                    Cam.orthographicSize = 8f;
                },
                onUpdate = (t, dt) =>
                {
                    float speed = route
                        ? Mathf.Lerp(2f, 40f, Cutscene.Smooth(Cutscene.Phase(t, 0f, 0.5f))) * (1f - 0.7f * Cutscene.Phase(t, 0.8f, 1f))
                        : Mathf.Lerp(1f, 12f, Cutscene.Smooth(Cutscene.Phase(t, 0f, 0.4f)));
                    for (int i = 0; i < N; i++)
                    {
                        rad[i] += speed * spd[i] * dt;
                        if (rad[i] > 16f) rad[i] -= 15.5f;
                        Vector2 dir = new Vector2(Mathf.Cos(ang[i]), Mathf.Sin(ang[i]));
                        float len = Mathf.Clamp(speed * spd[i] * 0.06f, 0.05f, 6f);
                        Vector2 a = c + dir * rad[i];
                        Fx.SetLine(streaks[i], a, a + dir * len, 0.05f);
                    }
                    gm.FadeColor = route ? Color.white : Color.black;
                    gm.FadeAlpha = Cutscene.Phase(t, 0.85f, 1f);
                },
                onComplete = () =>
                {
                    S.fuel -= fuel;
                    S.day += days;
                    S.current = target;
                    gm.Say($"Arrived at {target.name} from {from.name} after {days} days, using {fuel:0.0} fuel.");
                    gm.FadeColor = route ? Color.white : Color.black;
                    gm.FadeAlpha = 1f;
                    gm.Go(new SystemView(null));
                    gm.FadeIn(1f);
                }
            });
        }

        public override void WorldGui()
        {
            foreach (var s in S.galaxy.systems)
            {
                string label = s == selected ? $"> {s.name} <" : s.name;
                gm.WorldLabel(s.pos, label, 14);
            }
        }

        public override void Gui()
        {
            GUILayout.Label("Galaxy map", gm.Header);
            GUILayout.Label("Click a star to select it. Pan: WASD. Zoom: mouse wheel. Back: M.", gm.Hint);
            GUILayout.Label($"Current system: {S.current.name} ({S.current.territory ?? "unclaimed"})", gm.Label);
            GUILayout.Space(8);

            if (selected != null && selected != S.current)
            {
                bool route = S.galaxy.HasRoute(S.current, selected);
                float dist = Vector2.Distance(S.current.pos, selected.pos);
                float fuel = Rules.JumpFuel(S, S.current, selected);
                int days = Rules.TravelDays(S, S.current, selected);

                GUILayout.Label(selected.name, gm.Header);
                GUILayout.Label($"Distance: {dist:0.0} ly\n" +
                                (route ? "Jump route: open" : "No jump route: free flight (slow, fuel-hungry)") +
                                $"\nFuel needed: {fuel:0.0} (you have {S.fuel:0.0})\nTravel time: {days} days" +
                                (selected.starportRace != null ? $"\nStarport: {selected.starportRace}" : "") +
                                $"\nTerritory: {(selected.territory ?? "unclaimed")}", gm.Label);
                GUILayout.Label($"Navigator skill {S.navigatorSkill} saves {-GameData.RoleBonus("Navigator", S.navigatorSkill):P0} fuel.", gm.Hint);

                if (gm.Button(route ? "Jump" : "Free flight", S.fuel >= fuel))
                    Jump(selected, fuel, days, route);
                if (S.fuel < fuel) GUILayout.Label("Not enough fuel.", gm.Hint);
            }
            else
            {
                GUILayout.Label("Select a destination.", gm.Label);
            }

            GUILayout.FlexibleSpace();
            if (gm.Button("Return to system (M)")) gm.Go(new SystemView(null));
        }
    }
}
