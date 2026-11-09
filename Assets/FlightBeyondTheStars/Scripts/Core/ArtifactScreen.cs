using UnityEngine;

namespace FBTS
{
    /// <summary>
    /// The artifacts screen (I): every major artifact aboard, how long until the Science Officer identifies the one
    /// being studied, and Install / Remove for upgrades. Artifacts are sold at markets and handed over in
    /// conversations. Pauses the game while open.
    /// </summary>
    public class ArtifactScreen
    {
        readonly GameManager gm;
        OwnedArtifact selected;
        Vector2 listScroll, detailScroll;
        System.Action pending;
        public bool Open { get; private set; }
        float openedAt;

        GameState S => gm.State;

        public ArtifactScreen(GameManager owner) { gm = owner; }

        public void Show()
        {
            Open = true;
            openedAt = Time.time;
            if (selected == null || !S.artifacts.Contains(selected)) selected = S.artifacts.Count > 0 ? S.artifacts[0] : null;
        }

        public void Tick()
        {
            var act = pending;
            pending = null;
            act?.Invoke();
            var kb = UnityEngine.InputSystem.Keyboard.current;
            var pad = UnityEngine.InputSystem.Gamepad.current;
            if (Time.time - openedAt > 0.2f &&
                ((kb != null && (kb.escapeKey.wasPressedThisFrame || kb.iKey.wasPressedThisFrame)) ||
                 (pad != null && pad.buttonEast.wasPressedThisFrame)))
                Open = false;
        }

        string Status(OwnedArtifact a)
        {
            if (a.identified) return a.installed ? "Installed" : "Identified";
            if (Artifacts.Studying(S) != a) return "Waiting to be studied";
            int d = Artifacts.DaysLeft(S, a);
            return d < 0 ? "No Science Officer to study it" : $"Being studied: about {d} day{(d == 1 ? "" : "s")} left";
        }

        public void Gui(float w, float h)
        {
            float ww = Mathf.Min(900f, w - 30f), wh = Mathf.Min(560f, h - 70f);
            var r = new Rect((w - ww) / 2f, 42f, ww, wh);
            GUI.Box(r, GUIContent.none);
            GUI.Box(r, GUIContent.none);
            GUI.Box(r, GUIContent.none);
            GUILayout.BeginArea(new Rect(r.x + 12, r.y + 10, r.width - 24, r.height - 20));
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Artifacts  ({S.artifacts.Count} aboard, {S.artifactsFound.Count} of {GameData.Artifacts.Length} found this voyage)", gm.Header);
            GUILayout.FlexibleSpace();
            if (gm.Button("Close (I / Esc)")) pending = () => Open = false;
            GUILayout.EndHorizontal();

            if (S.artifacts.Count == 0)
            {
                GUILayout.Label("No artifacts aboard. Major artifacts turn up, rarely, in ruins, once the rim has gone dark (Act 2). The Science Officer studies " +
                                "each one as the days pass; a Noeti aboard can identify them on sight.", gm.Label);
                GUILayout.EndArea();
                return;
            }

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(300));
            listScroll = GUILayout.BeginScrollView(listScroll);
            foreach (var a in S.artifacts)
            {
                var style = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft, wordWrap = true, fontSize = 13 };
                if (a == selected) style.normal = style.active;
                if (GUILayout.Button($"{Artifacts.Name(a)}\n{Status(a)}", style, GUILayout.Height(48))) { var pick = a; pending = () => selected = pick; }
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            detailScroll = GUILayout.BeginScrollView(detailScroll);
            if (selected != null && S.artifacts.Contains(selected)) Details(selected);
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        void Details(OwnedArtifact a)
        {
            var r = a.Row;
            GUILayout.Label(Artifacts.Name(a), gm.Header);
            GUILayout.Label(Status(a), gm.Hint);
            if (r == null) return;
            if (!a.identified)
            {
                GUILayout.Label("An object of unknown make, recovered from ruins. Until the Science Officer works out what it is, " +
                                "it can only be sold as an unidentified curiosity.", gm.Label);
                GUILayout.Label($"Study progress: {a.study:0.0} of {r.studyDays:0} study-days (one study-day per day at skill 100). " +
                                $"Science Officer skill {S.scienceSkill}.", gm.Hint);
                return;
            }
            GUILayout.Label(r.description, gm.Label);
            if (!string.IsNullOrEmpty(r.clue)) { GUILayout.Space(4); GUILayout.Label(r.clue, gm.Label); }
            GUILayout.Space(6);
            GUILayout.Label(Artifacts.Uses(r), gm.Hint);
            GUILayout.Label($"Listed value {r.value:N0} cr.", gm.Hint);

            if (r.Is("Upgrade"))
            {
                GUILayout.Space(6);
                if (!a.installed)
                {
                    if (gm.Button($"Install ({Artifacts.EffectText(r)})")) pending = () => { a.installed = true; gm.Say($"{r.name} installed: {Artifacts.EffectText(r)}."); };
                }
                else if (gm.Button("Remove (so it can be sold or given away)")) pending = () => { a.installed = false; gm.Say($"{r.name} removed."); };
            }
        }
    }
}
