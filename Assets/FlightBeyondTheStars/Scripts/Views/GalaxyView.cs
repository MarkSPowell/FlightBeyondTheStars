using System.Collections.Generic;
using UnityEngine;

namespace FBTS
{
    /// <summary>Galaxy map: pick a destination and travel. Routes are cheap; free flight costs far more fuel and time.
    /// Clicking a distant star plots a course of chained route jumps to it.</summary>
    public class GalaxyView : View
    {
        StarSystem selected;
        List<Rules.Leg> course;      // cheapest chain of route jumps to the selected star (null = none)
        Rules.Leg direct;            // straight-line free flight, offered when there's no direct route
        Transform courseRoot;

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
                if (pick != null) Select(pick);
            }

            if (Controls.Map()) gm.Go(new SystemView(null));
        }

        void Select(StarSystem target)
        {
            selected = target;
            course = null;
            direct = null;
            if (target != S.current)
            {
                course = Rules.PlotCourse(S, S.current, target);
                if (!S.galaxy.HasRoute(S.current, target)) direct = Rules.MakeLeg(S, S.current, target);
            }
            DrawCourse();
        }

        void DrawCourse()
        {
            if (courseRoot != null) Object.Destroy(courseRoot.gameObject);
            courseRoot = new GameObject("Course").transform;
            courseRoot.SetParent(root, false);
            if (course == null) return;
            var col = new Color(0.4f, 1f, 0.9f, 0.9f);
            foreach (var leg in course)
            {
                Draw.Line("Leg", courseRoot, leg.from.pos, leg.to.pos, 0.3f, col, 2);
                if (leg.to != selected) Draw.Dot("Waypoint", courseRoot, leg.to.pos, 1.8f, new Color(col.r, col.g, col.b, 0.35f), 4);
            }
        }

        static float TotalFuel(List<Rules.Leg> legs) { float f = 0f; foreach (var l in legs) f += l.fuel; return f; }
        static int TotalDays(List<Rules.Leg> legs) { int d = 0; foreach (var l in legs) d += l.days; return d; }

        /// <summary>The longest start of the course the fuel tank can cover.</summary>
        List<Rules.Leg> AffordablePart(List<Rules.Leg> legs)
        {
            var part = new List<Rules.Leg>();
            float f = 0f;
            foreach (var l in legs)
            {
                if (f + l.fuel > S.fuel + 0.0001f) break;
                f += l.fuel;
                part.Add(l);
            }
            return part;
        }

        /// <summary>Travel one or more legs as a single trip. Each leg's fuel and days are applied as the
        /// animation passes it (or all at once if skipped), so intermediate systems are really visited.</summary>
        void Travel(List<Rules.Leg> legs)
        {
            if (legs == null || legs.Count == 0) return;

            // Chained courses can be intercepted at a waypoint: the trip stops there, ships waiting.
            var rng = new System.Random(System.Environment.TickCount);
            for (int i = 0; i < legs.Count - 1; i++)
            {
                var met = Encounter.Roll(S, legs[i].to, rng, Encounter.WaypointChance(S));
                if (met == null) continue;
                legs = legs.GetRange(0, i + 1);
                S.pendingEncounter = met;
                break;
            }

            bool route = legs[0].route;
            var start = S.current;
            int applied = 0;
            float fuelUsed = 0f;
            int daysUsed = 0;
            System.Action<int> applyUpTo = count =>
            {
                for (; applied < count && applied < legs.Count; applied++)
                {
                    var l = legs[applied];
                    S.fuel = Mathf.Max(0f, S.fuel - l.fuel);
                    S.AdvanceDays(l.days);
                    S.current = l.to;
                    if (!l.to.visited) Crew.Morale(S, "NewSystem");
                    l.to.visited = true;
                    Crew.Gain(S, Roles.Navigator, GameData.T("XpJump", 1.5f));
                    Crew.Gain(S, Roles.Engineer, GameData.T("XpEngineerJump", 0.5f));
                    fuelUsed += l.fuel;
                    daysUsed += l.days;
                }
            };
            int n = legs.Count;
            Jump(legs[n - 1].to, route, n, n == 1 ? (route ? 4.5f : 6f) : 3.5f + 1.4f * n,
                t =>
                {
                    int leg = Mathf.Min(n - 1, Mathf.FloorToInt(t * n));
                    string where = n > 1 ? $"Jump {leg + 1} of {n}: {legs[leg].from.name} to {legs[leg].to.name}" :
                        (route ? $"Jumping to {legs[0].to.name}" : $"Free flight to {legs[0].to.name}");
                    return $"{where}  -  day {S.day}";
                },
                t => applyUpTo(Mathf.FloorToInt(t * n)),
                () =>
                {
                    applyUpTo(n);
                    S.justArrived = true;   // the system view rolls for an encounter
                    string via = n > 1 ? $" via {n - 1} waypoint{(n > 2 ? "s" : "")}" : "";
                    gm.Say(S.pendingEncounter != null
                        ? $"Course interrupted at {S.current.name}: ships dropped out of hyperspace beside you! ({daysUsed} days, {fuelUsed:0.0} fuel)"
                        : $"Arrived at {S.current.name} from {start.name}{via} after {daysUsed} days, using {fuelUsed:0.0} fuel.");
                });
        }

        void Jump(StarSystem target, bool route, int legCount, float duration, System.Func<float, string> caption,
                  System.Action<float> onProgress, System.Action onArrive)
        {
            const int N = 140;
            var streaks = new SpriteRenderer[N];
            var ang = new float[N];
            var rad = new float[N];
            var spd = new float[N];
            Vector2 c = new Vector2(5000f, 5000f); // empty space far from the map
            Color tint = route ? new Color(0.6f, 0.8f, 1f, 0.9f) : new Color(1f, 0.75f, 0.45f, 0.8f);

            gm.Play(new Cutscene
            {
                duration = duration,
                caption = caption,
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
                    var ship = Art.Ship("player", "JumpShip", r, new Color(0.4f, 0.9f, 1f), 1.4f);
                    ship.localPosition = c;
                    ship.localRotation = Quaternion.Euler(0, 0, 90);
                    Cam.transform.position = new Vector3(c.x, c.y, -10f);
                    Cam.orthographicSize = 8f;
                },
                onUpdate = (t, dt) =>
                {
                    // On a chained course the streaks surge and ease once per jump.
                    float lt = legCount > 1 ? Mathf.Repeat(t * legCount, 1f) : t;
                    float speed = route
                        ? Mathf.Lerp(2f, 40f, Cutscene.Smooth(Cutscene.Phase(lt, 0f, 0.5f))) * (1f - 0.7f * Cutscene.Phase(lt, 0.8f, 1f))
                        : Mathf.Lerp(1f, 12f, Cutscene.Smooth(Cutscene.Phase(lt, 0f, 0.4f)));
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
                    onProgress(t);
                },
                onComplete = () =>
                {
                    onArrive();
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

        void CourseGui()
        {
            float fuel = TotalFuel(course);
            int days = TotalDays(course);
            int n = course.Count;
            GUILayout.Label(n == 1 ? "Jump route" : $"Plotted course: {n} jumps", gm.Header);

            var names = new List<string> { S.current.name };
            var ports = new List<string>();
            foreach (var l in course)
            {
                names.Add(l.to.name);
                if (l.to != selected && l.to.starportRace != null) ports.Add(l.to.name);
            }
            if (n > 1) GUILayout.Label(string.Join(" > ", names), gm.Label);
            GUILayout.Label($"Fuel needed: {fuel:0.0} (you have {S.fuel:0.0})\nTravel time: {days} days", gm.Label);
            if (ports.Count > 0) GUILayout.Label("Refuel en route at: " + string.Join(", ", ports), gm.Hint);

            if (S.fuel >= fuel)
            {
                if (gm.Button(n == 1 ? "Jump" : $"Engage course ({n} jumps)")) Travel(course);
                return;
            }
            GUILayout.Label($"Not enough fuel for the whole course (short by {fuel - S.fuel:0.0}).", gm.Hint);
            var part = AffordablePart(course);
            if (part.Count > 0 && n > 1)
            {
                var stop = part[part.Count - 1].to;
                if (gm.Button($"Go as far as fuel allows: {stop.name} ({part.Count} of {n})")) Travel(part);
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
                float dist = Vector2.Distance(S.current.pos, selected.pos);
                GUILayout.Label(selected.name, gm.Header);
                GUILayout.Label($"Distance: {dist:0.0} ly" +
                                (selected.starportRace != null ? $"\nStarport: {selected.starportRace}" : "") +
                                $"\nTerritory: {(selected.territory ?? "unclaimed")}", gm.Label);
                GUILayout.Label($"Navigator skill {S.navigatorSkill} saves {-GameData.RoleBonus("Navigator", S.navigatorSkill):P0} fuel.", gm.Hint);
                GUILayout.Space(6);

                if (course != null) CourseGui();
                else GUILayout.Label("No jump routes connect these systems.", gm.Label);

                if (direct != null)
                {
                    GUILayout.Space(8);
                    GUILayout.Label("Free flight (direct)", gm.Header);
                    GUILayout.Label($"Slow and fuel-hungry: {direct.fuel:0.0} fuel, {direct.days} days.", gm.Label);
                    if (gm.Button("Free flight", S.fuel >= direct.fuel)) Travel(new List<Rules.Leg> { direct });
                    if (S.fuel < direct.fuel) GUILayout.Label("Not enough fuel.", gm.Hint);
                }
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
