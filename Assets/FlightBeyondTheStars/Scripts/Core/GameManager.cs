using UnityEngine;

namespace FBTS
{
    /// <summary>Starts the prototype automatically in any scene when you press Play — no scene setup needed.</summary>
    static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            if (Object.FindAnyObjectByType<GameManager>() != null) return;
            new GameObject("FlightBeyondTheStars").AddComponent<GameManager>();
        }
    }

    /// <summary>A screen of the game (galaxy map, star system, planet surface).</summary>
    public abstract class View
    {
        protected GameManager gm;
        protected Transform root;
        protected GameState S => gm.State;
        protected Camera Cam => gm.Cam;

        public void Attach(GameManager owner)
        {
            gm = owner;
            root = new GameObject(GetType().Name).transform;
            root.SetParent(owner.transform, false);
        }

        public abstract void Enter();
        public virtual void Exit() { if (root != null) Object.Destroy(root.gameObject); }
        public abstract void Tick(float dt);
        public abstract void Gui();
        /// <summary>Labels drawn in world space (names under stars and planets), outside the panel.</summary>
        public virtual void WorldGui() { }

        /// <summary>True when a screen position is over the left-hand info panel.</summary>
        protected bool OverPanel(Vector2 screenPos) => screenPos.x < (GameManager.PanelWidth + 20) * GameManager.UiScale;
    }

    public class GameManager : MonoBehaviour
    {
        public const float PanelWidth = 330f;
        public static float UiScale => Mathf.Max(1f, Screen.height / 720f);

        public GameState State { get; private set; }
        public Camera Cam { get; private set; }

        View view;
        View pending;

        // Transitions
        Cutscene active;
        float cutsceneTime;
        public bool AnimationsOn = true;
        public float FadeAlpha;              // 0 = clear, 1 = fully covered
        public Color FadeColor = Color.black;
        float fadeInSpeed;
        public bool PlayingCutscene => active != null;
        string message = "";
        string toast = "";
        float toastUntil;
        float messageTime = -100f;

        GUIStyle labelStyle, headerStyle, worldLabelStyle, hintStyle;
        bool showLoadMenu;

        // Note pop-up (lore, captain's log)
        bool noteOpen;
        string noteTitle = "", noteBody = "";
        float noteShownAt;
        Vector2 noteScroll;

        public void ShowNote(string title, string body)
        {
            noteTitle = title;
            noteBody = body;
            noteOpen = true;
            noteShownAt = Time.time;
            noteScroll = Vector2.zero;
        }

        public void ShowLog()
        {
            var sb = new System.Text.StringBuilder();
            if (State.loreFound.Count == 0) sb.Append("No entries yet. Search ruins to find records of the galaxy's past.");
            foreach (var id in State.loreFound)
            {
                var l = GameData.LoreEntry(id);
                if (l != null) sb.Append($"{l.title} ({l.race})\n{l.text}\n\n");
            }
            ShowNote($"Captain's log - {State.loreFound.Count} of {GameData.Lore.Length} records", sb.ToString());
        }

        void Awake()
        {
            GameData.Load();
            int seed = System.Environment.TickCount & 0xffff;
            State = GameState.NewGame(seed);
            SetupCamera();
            Go(new SystemView(null));
            ApplyPending();
            Say($"Welcome, Captain. Galaxy seed {seed}. Survey planets, land, collect minerals and sell them at the starport.");
        }

        void SetupCamera()
        {
            Cam = Camera.main;
            if (Cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                Cam = go.AddComponent<Camera>();
            }
            Cam.orthographic = true;
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = new Color(0.02f, 0.02f, 0.06f);
            Cam.transform.position = new Vector3(0, 0, -10);
        }

        /// <summary>Switch screens at the start of the next frame (safe to call from GUI buttons).</summary>
        public void Go(View next) { pending = next; }

        void ApplyPending()
        {
            if (pending == null) return;
            view?.Exit();
            view = pending;
            pending = null;
            view.Attach(this);
            view.Enter();
        }

        void NoteGui(float w, float h)
        {
            var r = new Rect(w / 2 - 260, h / 2 - 170, 520, 340);
            GUI.Box(r, GUIContent.none);
            GUI.Box(r, GUIContent.none); // doubled for a more opaque backing
            GUILayout.BeginArea(new Rect(r.x + 14, r.y + 12, r.width - 28, r.height - 24));
            GUILayout.Label(noteTitle, headerStyle);
            noteScroll = GUILayout.BeginScrollView(noteScroll);
            GUILayout.Label(noteBody, labelStyle);
            GUILayout.EndScrollView();
            if (Button("Close (Space / E / Esc)")) noteOpen = false;
            GUILayout.EndArea();
        }

        void LoadMenuGui(float w, float h)
        {
            var r = new Rect(w / 2 - 190, h / 2 - 110, 380, 220);
            GUI.Box(r, GUIContent.none);
            GUILayout.BeginArea(new Rect(r.x + 12, r.y + 10, r.width - 24, r.height - 20));
            GUILayout.Label("Load game", headerStyle);
            foreach (var slot in new[] { SaveSystem.QuickSlot, SaveSystem.AutoSlot })
            {
                var info = SaveSystem.Info(slot);
                GUILayout.Label($"{(slot == SaveSystem.QuickSlot ? "Quick save (F5 / F9)" : "Autosave")}: {info.summary}", labelStyle);
                if (Button("Load " + (slot == SaveSystem.QuickSlot ? "quick save" : "autosave"), info.exists)) LoadSlot(slot);
            }
            GUILayout.FlexibleSpace();
            if (Button("Cancel")) showLoadMenu = false;
            GUILayout.EndArea();
        }

        /// <summary>A brief status line at the top centre of the screen that doesn't pause play.</summary>
        public void Toast(string text, float seconds)
        {
            toast = text;
            toastUntil = Time.time + seconds;
        }

        public void Say(string text)
        {
            message = text;
            messageTime = Time.time;
            Debug.Log("[FBTS] " + text);
        }

        public void Follow(Vector2 target, float sharpness)
        {
            var p = Cam.transform.position;
            var t = new Vector3(target.x, target.y, p.z);
            Cam.transform.position = sharpness <= 0 ? t : Vector3.Lerp(p, t, 1f - Mathf.Exp(-sharpness * Time.deltaTime));
        }

        // ---------------------------------------------------------------- Save / load

        public bool CanSave(out string why)
        {
            why = null;
            if (active != null || pending != null) why = "Can't save during a transition.";
            else if (view is SurfaceView) why = "Can't save on a planet surface. Launch to orbit first.";
            return why == null;
        }

        public void QuickSave() => SaveTo(SaveSystem.QuickSlot, false);

        /// <summary>Called at safe moments (docking, arriving in a system). Silent unless it fails.</summary>
        public void AutoSave() => SaveTo(SaveSystem.AutoSlot, true);

        void SaveTo(string slot, bool quiet)
        {
            if (!CanSave(out var why)) { if (!quiet) Say(why); return; }
            if (SaveSystem.Save(State, slot, out var err)) { if (!quiet) Toast("Game saved", 1.5f); }
            else Say("Save failed: " + err);
        }

        public void LoadSlot(string slot)
        {
            if (active != null) return;
            var loaded = SaveSystem.Load(slot, out var err);
            if (loaded == null) { Say("Load failed: " + err); return; }
            showLoadMenu = false;
            State = loaded;
            FadeColor = Color.black;
            FadeAlpha = 1f;
            Go(new SystemView(null));
            FadeIn(0.6f);
            Say($"Loaded {slot}: day {State.day} at {State.current.name}.");
        }

        void Update()
        {
            ApplyPending();

            if (fadeInSpeed > 0f && active == null)
            {
                FadeAlpha = Mathf.Max(0f, FadeAlpha - fadeInSpeed * Time.deltaTime);
                if (FadeAlpha <= 0f) fadeInSpeed = 0f;
            }

            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && active == null)
            {
                if (kb.f5Key.wasPressedThisFrame) QuickSave();
                if (kb.f9Key.wasPressedThisFrame) LoadSlot(SaveSystem.QuickSlot);
            }

            if (noteOpen)
            {
                if (Time.time - noteShownAt > 0.3f && Controls.Skip()) noteOpen = false;
                return; // reading pauses the game
            }

            if (active != null)
            {
                cutsceneTime += Time.deltaTime;
                float t = Mathf.Clamp01(cutsceneTime / Mathf.Max(0.01f, active.duration));
                active.onUpdate?.Invoke(t, Time.deltaTime);
                if (t >= 1f || (cutsceneTime > 0.2f && Controls.Skip())) FinishCutscene();
                return; // the screen underneath is paused while a transition plays
            }

            view?.Tick(Time.deltaTime);
        }

        /// <summary>Play a transition, or apply its result instantly if animations are off.</summary>
        public void Play(Cutscene c)
        {
            if (active != null) FinishCutscene();
            if (!AnimationsOn)
            {
                c.onComplete?.Invoke();
                return;
            }
            c.root = new GameObject("Cutscene").transform;
            c.root.SetParent(transform, false);
            active = c;
            cutsceneTime = 0f;
            c.onStart?.Invoke(c.root);
            c.onUpdate?.Invoke(0f, 0f);
        }

        void FinishCutscene()
        {
            var c = active;
            active = null;
            if (c.root != null) Destroy(c.root.gameObject);
            c.onComplete?.Invoke();
        }

        /// <summary>Fade the screen in from the current FadeAlpha over the given seconds.</summary>
        public void FadeIn(float seconds)
        {
            if (!AnimationsOn) { FadeAlpha = 0f; return; }
            fadeInSpeed = FadeAlpha > 0f ? FadeAlpha / Mathf.Max(0.05f, seconds) : 0f;
        }

        // ---------------------------------------------------------------- GUI helpers

        void EnsureStyles()
        {
            if (labelStyle != null) return;
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
            labelStyle.normal.textColor = new Color(0.9f, 0.92f, 1f);
            headerStyle = new GUIStyle(labelStyle) { fontSize = 18, fontStyle = FontStyle.Bold };
            hintStyle = new GUIStyle(labelStyle) { fontSize = 12 };
            hintStyle.normal.textColor = new Color(0.65f, 0.7f, 0.8f);
            worldLabelStyle = new GUIStyle(labelStyle) { alignment = TextAnchor.UpperCenter, fontSize = 12, wordWrap = false };
        }

        public GUIStyle Label { get { EnsureStyles(); return labelStyle; } }
        public GUIStyle Header { get { EnsureStyles(); return headerStyle; } }
        public GUIStyle Hint { get { EnsureStyles(); return hintStyle; } }

        public void WorldLabel(Vector2 world, string text, float yOffset = 0f)
        {
            EnsureStyles();
            Vector3 sp = Cam.WorldToScreenPoint(new Vector3(world.x, world.y, 0));
            if (sp.z < 0) return;
            float s = UiScale;
            GUI.Label(new Rect(sp.x / s - 80, (Screen.height - sp.y) / s + yOffset, 160, 22), text, worldLabelStyle);
        }

        public bool Button(string text, bool enabled = true)
        {
            bool was = GUI.enabled;
            GUI.enabled = was && enabled;
            bool clicked = GUILayout.Button(text, GUILayout.Height(28));
            GUI.enabled = was;
            return clicked;
        }

        void OnGUI()
        {
            EnsureStyles();
            float s = UiScale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1));
            float w = Screen.width / s, h = Screen.height / s;

            // Top status bar
            var st = State;
            var stage = st.Stage;
            GUI.Box(new Rect(0, 0, w, 34), GUIContent.none);
            string bar = $"Credits {st.credits:N0} cr    Fuel {st.fuel:0.0}/{st.FuelTank:0}    " +
                         $"Cargo {st.CargoUsed:0}/{st.CargoCapacity:0}    Day {st.day}    " +
                         $"Threat: {(stage != null ? stage.name : "-")}    System: {st.current.name}";
            GUI.Label(new Rect(10, 7, w - 370, 24), bar, labelStyle);
            bool wasOn = GUI.enabled;
            GUI.enabled = active == null;
            if (GUI.Button(new Rect(w - 356, 5, 66, 24), "Log")) ShowLog();
            if (GUI.Button(new Rect(w - 284, 5, 66, 24), "Save")) QuickSave();
            if (GUI.Button(new Rect(w - 212, 5, 66, 24), "Load")) showLoadMenu = !showLoadMenu;
            GUI.enabled = wasOn;
            if (GUI.Button(new Rect(w - 140, 5, 130, 24), AnimationsOn ? "Animations: On" : "Animations: Off"))
                AnimationsOn = !AnimationsOn;

            view?.WorldGui();

            // Left info panel, filled by the active view (read-only while a transition plays)
            bool was = GUI.enabled;
            GUI.enabled = active == null;
            GUILayout.BeginArea(new Rect(10, 44, PanelWidth, h - 110), GUI.skin.box);
            view?.Gui();
            GUILayout.EndArea();
            GUI.enabled = was;

            if (showLoadMenu) LoadMenuGui(w, h);
            if (noteOpen) NoteGui(w, h);

            // Fade overlay
            if (FadeAlpha > 0.001f)
            {
                var old = GUI.color;
                GUI.color = new Color(FadeColor.r, FadeColor.g, FadeColor.b, FadeAlpha);
                GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
                GUI.color = old;
            }

            // Transition caption and skip hint
            if (active != null)
            {
                float t = Mathf.Clamp01(cutsceneTime / Mathf.Max(0.01f, active.duration));
                string cap = active.caption != null ? active.caption(t) : "";
                var capStyle = new GUIStyle(headerStyle) { alignment = TextAnchor.MiddleCenter, fontSize = 22 };
                GUI.Label(new Rect(0, h * 0.72f, w, 34), cap, capStyle);
                var hint = new GUIStyle(hintStyle) { alignment = TextAnchor.MiddleCenter };
                GUI.Label(new Rect(0, h * 0.72f + 34, w, 22), "Skip: Space / E / Esc / click / gamepad A or B", hint);
            }

            if (Time.time < toastUntil)
            {
                var ts = new GUIStyle(headerStyle) { alignment = TextAnchor.MiddleCenter, fontSize = 18 };
                GUI.Label(new Rect(0, 46, w, 28), toast, ts);
            }

            // Message line at the bottom
            if (Time.time - messageTime < 8f)
                GUI.Box(new Rect(10, h - 56, w - 20, 46), message, labelStyle);
        }

    }
}
