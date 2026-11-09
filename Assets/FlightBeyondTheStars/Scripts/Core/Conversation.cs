using System.Collections.Generic;
using System.Text.RegularExpressions;
using Ink.Runtime;
using Ink.UnityIntegration;
using UnityEngine;

namespace FBTS
{
    /// <summary>
    /// A conversation with an alien race, driven by an Ink story in Resources/Dialogue/&lt;file&gt;.ink.
    /// The game owns the facts (credits, reputation, keywords, lore); Ink owns the words and branching.
    /// The story's own memory (visit counts, its variables) is kept per race in GameState.inkStates,
    /// so the Orrun remember the captain between conversations and across saves.
    /// </summary>
    public class Conversation
    {
        public static readonly string[] Postures = { "friendly", "obsequious", "hostile" };

        readonly GameManager gm;
        readonly Story story;
        public readonly string race;
        readonly string where;               // shown in the window title
        public string posture;
        public bool Finished { get; private set; }

        public class Line { public string speaker; public string text; public bool captain; }
        public readonly List<Line> transcript = new List<Line>();
        public readonly List<string> choices = new List<string>();
        public int selected;
        /// <summary>The alien speaking most recently (for the portrait), or null before anyone has spoken.</summary>
        public string LastSpeaker { get; private set; }

        GameState S => gm.State;

        /// <summary>File name for a race's dialogue: "Orrun Combine" -> "orrun".</summary>
        public static string FileFor(string race) =>
            string.IsNullOrEmpty(race) ? null : race.Split(' ')[0].ToLowerInvariant();

        static Dictionary<string, InkFile> stories;

        /// <summary>The story for a race. The Ink plugin (2.0) compiles each .ink file when Unity imports it
        /// and stores the result inside the file's InkFile asset; there is no separate .json.</summary>
        static InkFile StoryFor(string race)
        {
            if (stories == null)
            {
                stories = new Dictionary<string, InkFile>();
                foreach (var f in Resources.LoadAll<InkFile>("Dialogue"))
                {
                    if (f.hasErrors)
                        foreach (var e in f.errors) Debug.LogError($"[FBTS] Ink error in {f.name}.ink line {e.lineNumber}: {e.content}");
                    if (f.isCompiled) stories[f.name.ToLowerInvariant()] = f;
                }
            }
            var file = FileFor(race);
            return file != null && stories.TryGetValue(file, out var a) ? a : null;
        }

        /// <summary>True if a race has a dialogue file (so the game can offer a Hail button).</summary>
        public static bool Has(string race) => StoryFor(race) != null;

        static readonly Dictionary<string, Story> probes = new Dictionary<string, Story>();

        /// <summary>True if a race's dialogue has a given entry point (knot), such as "encounter".</summary>
        public static bool HasKnot(string race, string knot)
        {
            var asset = StoryFor(race);
            if (asset == null) return false;
            if (!probes.TryGetValue(race, out var probe))
            {
                try { probe = new Story(asset.storyJson); } catch { probe = null; }
                probes[race] = probe;
            }
            return probe != null && probe.KnotContainerWithName(knot) != null;
        }

        /// <summary>Open a channel. Entry is the Ink knot to start at, such as "station" or "homeworld".
        /// Extra variables (such as in_combat) are set if the story declares them; attack and stand_down
        /// let a ship encounter's dialogue start or end a fight.</summary>
        public static Conversation Open(GameManager gm, string race, string entry, string where,
            Dictionary<string, object> vars = null, System.Action onAttack = null, System.Action onStandDown = null)
        {
            var asset = StoryFor(race);
            if (asset == null) { gm.Say($"No one answers. ({race} has no dialogue yet.)"); return null; }
            if (!HasKnot(race, entry)) { gm.Say("No response. Only a carrier tone."); return null; }
            try { return new Conversation(gm, asset, race, entry, where, vars, onAttack, onStandDown); }
            catch (System.Exception e)
            {
                Debug.LogError("[FBTS] Conversation failed to start: " + e);
                gm.Say("The channel fills with static. (Dialogue error: see Console.)");
                return null;
            }
        }

        readonly Dictionary<string, object> extraVars;
        readonly System.Action onAttack, onStandDown;

        Conversation(GameManager gm, InkFile asset, string race, string entry, string where,
            Dictionary<string, object> vars, System.Action onAttack, System.Action onStandDown)
        {
            extraVars = vars;
            this.onAttack = onAttack;
            this.onStandDown = onStandDown;
            this.gm = gm;
            this.race = race;
            this.where = where;
            posture = string.IsNullOrEmpty(S.posture) ? "friendly" : S.posture;  // set before the greeting plays
            story = new Story(asset.storyJson) { allowExternalFunctionFallbacks = true };

            // Restore what this race remembers of us. If the dialogue file changed shape since the save,
            // Ink may refuse the old state; then they simply meet us fresh.
            if (S.inkStates.TryGetValue(race, out var saved) && !string.IsNullOrEmpty(saved))
            {
                try { story.state.LoadJson(saved); }
                catch (System.Exception e) { Debug.LogWarning("[FBTS] Old dialogue state discarded: " + e.Message); }
            }

            WorldEvents.Sync(S);
            Bind();
            PushVariables();
            story.ChoosePathString(entry);
            Advance();
        }

        public string Title => $"{where} - {race}";
        public int Reputation => S.Rep(race);

        void Bind()
        {
            story.BindExternalFunction("rep", (int delta) => S.AddRep(race, delta));
            story.BindExternalFunction("pay", (int amount) =>
            {
                if (S.credits < amount) return false;
                S.credits -= amount;
                SetVar("credits", Mathf.FloorToInt(S.credits));
                return true;
            });
            story.BindExternalFunction("receive", (int amount) =>
            {
                S.credits += amount;
                SetVar("credits", Mathf.FloorToInt(S.credits));
            });
            story.BindExternalFunction("learn", (string word) => { S.keywords.Add(word); });
            story.BindExternalFunction("knows", (string word) => S.keywords.Contains(word) || S.loreFound.Contains(word), true);
            story.BindExternalFunction("give_lore", (string forRace) => GiveLore(forRace));
            story.BindExternalFunction("survey_data", () => SurveyNearest());
            // World events: true once news has reached this race; days since it happened (-1 = not yet)
            story.BindExternalFunction("heard", (string id) => WorldEvents.Heard(S, id, race), true);
            story.BindExternalFunction("days_since", (string id) => WorldEvents.DaysSince(S, id), true);
            // Ship encounters: the other ship opens fire, or backs off
            story.BindExternalFunction("attack", () => { onAttack?.Invoke(); });
            story.BindExternalFunction("stand_down", () => { onStandDown?.Invoke(); });
            // The crew reacts to what the captain just did (a MoraleEvents id, such as "Mercy")
            story.BindExternalFunction("crew_reacts", (string evt) => { Crew.Morale(S, evt); });
            // Major artifacts: aboard and identified? hand one over (quests)
            story.BindExternalFunction("has_artifact", (string id) => Artifacts.Owns(S, id), true);
            story.BindExternalFunction("give_artifact", (string id) => Artifacts.Give(S, id));
        }

        void SetVar(string name, object value)
        {
            if (story.variablesState.GlobalVariableExistsWithName(name)) story.variablesState[name] = value;
        }

        void PushVariables()
        {
            SetVar("posture", posture);
            SetVar("comms", S.commsSkill + Mathf.RoundToInt(S.Trait("DialogueSkill")));   // Linguist
            SetVar("credits", Mathf.FloorToInt(S.credits));
            SetVar("reputation", S.Rep(race));
            if (extraVars != null) foreach (var kv in extraVars) SetVar(kv.Key, kv.Value);
        }

        public void SetPosture(string p)
        {
            posture = p;
            S.posture = p;
            SetVar("posture", p);
        }

        static readonly Regex SpeakerLine = new Regex(@"^([A-Z][A-Za-z' .-]{1,30}):\s+(.+)$");

        void Advance()
        {
            while (story.canContinue)
            {
                string text = story.Continue().Trim();
                if (text.Length == 0) continue;
                var m = SpeakerLine.Match(text);
                transcript.Add(m.Success
                    ? new Line { speaker = m.Groups[1].Value, text = m.Groups[2].Value }
                    : new Line { text = text });
                if (m.Success && m.Groups[1].Value != "Comms Officer") LastSpeaker = m.Groups[1].Value;
            }
            choices.Clear();
            foreach (var c in story.currentChoices) choices.Add(c.text);
            selected = 0;
            if (choices.Count == 0) Finished = true;
        }

        public void Choose(int index)
        {
            if (index < 0 || index >= choices.Count) return;
            transcript.Add(new Line { speaker = "Captain", text = choices[index], captain = true });
            Crew.Gain(S, Roles.Comms, GameData.T("XpCommsLine", 0.3f));
            PushVariables();   // credits or skills may have changed since the last line
            story.ChooseChoiceIndex(index);
            Advance();
        }

        /// <summary>Remember this conversation in the save. Called when the channel closes.</summary>
        public void Close()
        {
            try { S.inkStates[race] = story.state.ToJson(); }
            catch (System.Exception e) { Debug.LogWarning("[FBTS] Could not store dialogue state: " + e.Message); }
        }

        // ---------------------------------------------------------------- Effects Ink can call

        string GiveLore(string forRace)
        {
            foreach (var l in GameData.Lore)
            {
                if (l.race != forRace || S.loreFound.Contains(l.id)) continue;
                S.loreFound.Add(l.id);
                Crew.Morale(S, "Lore");
                gm.Toast($"Captain's log: {l.title}", 3f);
                return l.title;
            }
            return "";
        }

        /// <summary>Survey every planet of the nearest system we haven't explored. Returns its name, or "".</summary>
        string SurveyNearest()
        {
            StarSystem best = null;
            float bestD = float.MaxValue;
            foreach (var sys in S.galaxy.systems)
            {
                if (sys == S.current || sys.visited) continue;
                bool anyUnscanned = false;
                foreach (var p in sys.planets) if (!p.scanned) anyUnscanned = true;
                if (!anyUnscanned) continue;
                float d = Vector2.Distance(sys.pos, S.current.pos);
                if (d < bestD) { bestD = d; best = sys; }
            }
            if (best == null) return "";
            foreach (var p in best.planets) p.scanned = true;
            gm.Toast($"Survey data received: {best.name}", 3f);
            return best.name;
        }
    }
}
