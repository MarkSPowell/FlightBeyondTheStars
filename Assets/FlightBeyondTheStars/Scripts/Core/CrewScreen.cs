using UnityEngine;

namespace FBTS
{
    /// <summary>
    /// The crew screen (C): everyone aboard, their stations, skills, health, morale and traits.
    /// Assign people to stations (one person can cover two, at a penalty), pick traits on level-up,
    /// and dismiss crew while docked. Pauses the game while open.
    /// </summary>
    public class CrewScreen
    {
        readonly GameManager gm;
        CrewMember selected;
        Vector2 listScroll, detailScroll;
        System.Action pending;      // button actions run at the start of the next Update (stable IMGUI layout)
        public bool Open { get; private set; }
        float openedAt;

        GameState S => gm.State;

        public CrewScreen(GameManager owner) { gm = owner; }

        public void Show()
        {
            Open = true;
            openedAt = Time.time;
            if (selected == null || !S.crew.Contains(selected))
                selected = S.crew.Find(m => m.pendingTraits > 0) ?? (S.crew.Count > 0 ? S.crew[0] : null);
        }

        public void Tick()
        {
            var act = pending;
            pending = null;
            act?.Invoke();
            var kb = UnityEngine.InputSystem.Keyboard.current;
            var pad = UnityEngine.InputSystem.Gamepad.current;
            if (Time.time - openedAt > 0.2f &&
                ((kb != null && (kb.escapeKey.wasPressedThisFrame || kb.cKey.wasPressedThisFrame)) ||
                 (pad != null && pad.buttonEast.wasPressedThisFrame)))
                Open = false;
        }

        static string Stations(GameState s, CrewMember m)
        {
            var list = new System.Collections.Generic.List<string>();
            for (int r = 0; r < s.stations.Length; r++) if (s.stations[r] == m) list.Add(Roles.Names[r]);
            return list.Count == 0 ? "Unassigned" : string.Join(" + ", list);
        }

        public void Gui(float w, float h)
        {
            float ww = Mathf.Min(980f, w - 30f), wh = Mathf.Min(620f, h - 70f);
            var r = new Rect((w - ww) / 2f, 42f, ww, wh);
            GUI.Box(r, GUIContent.none);
            GUI.Box(r, GUIContent.none);
            GUI.Box(r, GUIContent.none);
            GUILayout.BeginArea(new Rect(r.x + 12, r.y + 10, r.width - 24, r.height - 20));
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Crew  ({S.crew.FindAll(c => !c.dead).Count} aboard)", gm.Header);
            GUILayout.FlexibleSpace();
            if (gm.Button("Close (C / Esc)")) pending = () => Open = false;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            // ---- Roster
            GUILayout.BeginVertical(GUILayout.Width(330));
            listScroll = GUILayout.BeginScrollView(listScroll);
            foreach (var m in S.crew)
            {
                if (m.dead) continue;
                string flag = m.pendingTraits > 0 ? "  [choose trait!]" : "";
                string cap = m.captain ? " (Captain)" : "";
                string label = $"{m.name}{cap}  L{m.level}{flag}\n{Stations(S, m)}\n{m.HealthState}, {m.MoraleState} ({m.morale:0})";
                var style = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft, wordWrap = true, fontSize = 13 };
                if (m == selected) style.normal = style.active;
                if (GUILayout.Button(label, style, GUILayout.Height(58))) { var pick = m; pending = () => selected = pick; }
            }
            GUILayout.EndScrollView();

            // Empty stations warning
            var empty = new System.Collections.Generic.List<string>();
            for (int i = 0; i < S.stations.Length; i++) if (S.stations[i] == null || !S.stations[i].Fit) empty.Add(Roles.Names[i]);
            if (empty.Count > 0) GUILayout.Label("Unmanned: " + string.Join(", ", empty), gm.Hint);
            GUILayout.EndVertical();

            // ---- Details
            GUILayout.BeginVertical();
            detailScroll = GUILayout.BeginScrollView(detailScroll);
            if (selected != null && S.crew.Contains(selected)) Details(selected);
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        void Details(CrewMember m)
        {
            var pers = GameData.Personality(m.personality);
            GUILayout.Label($"{m.name}{(m.captain ? ", Captain" : "")}", gm.Header);
            GUILayout.Label($"{m.species}.  {m.personality}: {(pers != null ? pers.description : "")}", gm.Label);
            float next = Crew.NextLevelAt(m);
            GUILayout.Label($"Level {m.level}.  Experience {m.earned:0} / {next:0} for level {m.level + 1}.", gm.Label);
            GUILayout.Label($"Health: {m.HealthState}.  Morale: {m.MoraleState} ({m.morale:0}, {(m.MoraleFactor - 1f) * 100f:+0;-0;0}% effectiveness)", gm.Label);
            if (m.moraleLog.Count > 0) GUILayout.Label("Recently: " + string.Join("   ", m.moraleLog), gm.Hint);

            // Trait choice
            if (m.pendingTraits > 0 && m.traitChoices != null)
            {
                GUILayout.Space(6);
                GUILayout.Label($"Level up! Choose a trait ({m.pendingTraits} to choose):", gm.Header);
                foreach (var id in m.traitChoices)
                {
                    var t = GameData.Trait(id);
                    if (t == null) continue;
                    string where = t.role == "Any" ? "always" : $"as {t.role}";
                    if (gm.Button($"{t.name} ({where}): {t.description}"))
                    {
                        var pick = id;
                        pending = () => { Crew.ChooseTrait(S, m, pick); gm.Say($"{m.name} learned {t.name}."); };
                    }
                }
            }

            GUILayout.Space(6);
            GUILayout.Label("Traits", gm.Header);
            if (m.traits.Count == 0) GUILayout.Label("None yet. One is chosen at every level.", gm.Hint);
            foreach (var id in m.traits)
            {
                var t = GameData.Trait(id);
                if (t != null) GUILayout.Label($"{t.name} ({(t.role == "Any" ? "always" : t.role)}): {t.description}", gm.Label);
            }

            GUILayout.Space(6);
            GUILayout.Label("Skills and stations", gm.Header);
            GUILayout.Label("Assign someone to a station by clicking it. Covering two stations costs " +
                            $"{GameData.T("DoubleDutyPenalty", 0.25f):P0} effectiveness at both.", gm.Hint);
            for (int role = 0; role < Roles.Names.Length; role++)
            {
                var holder = S.stations[role];
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{Roles.Names[role]}: {m.Skill(role)} / {m.Cap(role)}", gm.Label, GUILayout.Width(250));
                int rr = role;
                if (holder == m)
                {
                    if (gm.Button("Assigned - remove", true)) pending = () => S.stations[rr] = null;
                }
                else
                {
                    string who = holder != null ? $"Replace {holder.name.Split(' ')[0]}" : "Assign";
                    if (gm.Button(who, m.Fit)) pending = () => Crew.Assign(S, rr, m);
                }
                GUILayout.EndHorizontal();
            }

            // Dismiss (only in port, never the captain)
            if (!m.captain && gm.DockedAtStarport)
            {
                GUILayout.Space(8);
                if (gm.Button($"Dismiss {m.name} (leaves at this starport)"))
                    pending = () => { Crew.Remove(S, m); selected = null; gm.Say($"{m.name} has left the ship."); };
            }
        }
    }
}
