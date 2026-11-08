using System.Collections.Generic;
using UnityEngine;

namespace FBTS
{
    /// <summary>When and where a world event happened.</summary>
    public class WorldEvent
    {
        public int day;
        public int system;
    }

    /// <summary>
    /// Significant universe events (the spreadsheet's Events tab) that races can talk about.
    /// An event happens once. News of it then spreads from where it happened at NewsSpeed light-years
    /// per day, so a race far away hasn't heard yet; Broadcast events reach everyone at once.
    /// Dialogue asks heard("id"), so a comment unlocks only once that race actually knows.
    ///
    /// Stage events follow the threat clock and are filled in by Sync. Player and Story events are
    /// fired by game code with Fire (for example, finding the first artifact).
    /// </summary>
    public static class WorldEvents
    {
        public static bool Happened(GameState s, string id) => s.events.ContainsKey(id);

        /// <summary>Record an event. Returns false if it had already happened.</summary>
        public static bool Fire(GameState s, string id, int systemId, int? day = null)
        {
            if (s.events.ContainsKey(id)) return false;
            if (GameData.Event(id) == null) Debug.LogWarning($"[FBTS] Event '{id}' is not on the Events tab.");
            s.events[id] = new WorldEvent { day = day ?? s.day, system = systemId };
            return true;
        }

        /// <summary>Fire any threat-stage events whose start day has passed. Returns the newly fired rows.</summary>
        public static List<EventRow> Sync(GameState s)
        {
            var fired = new List<EventRow>();
            foreach (var e in GameData.Events)
            {
                if (e.trigger != "Stage" || s.events.ContainsKey(e.id)) continue;
                DeadlineStageRow stage = null;
                foreach (var d in GameData.Deadline) if (d.stage == e.stage) stage = d;
                if (stage == null || s.day < stage.baseStartDay) continue;
                // It happened on the stage's start day, out at the rim, even if we only notice now.
                Fire(s, e.id, RimSystem(s).id, Mathf.RoundToInt(stage.baseStartDay));
                fired.Add(e);
            }
            return fired;
        }

        /// <summary>True once news of the event has reached this race's home system.</summary>
        public static bool Heard(GameState s, string id, string race)
        {
            if (!s.events.TryGetValue(id, out var ev)) return false;
            var row = GameData.Event(id);
            if (row != null && row.spread == "Broadcast") return true;
            if (race == null || !s.galaxy.homeOf.TryGetValue(race, out var home)) return true;
            if (ev.system < 0 || ev.system >= s.galaxy.systems.Count) return true;
            float dist = Vector2.Distance(s.galaxy.systems[ev.system].pos, home.pos);
            float delay = dist / Mathf.Max(0.1f, GameData.T("NewsSpeed", 3f));
            return s.day - ev.day >= delay;
        }

        /// <summary>Days since the event happened, or -1 if it hasn't.</summary>
        public static int DaysSince(GameState s, string id) =>
            s.events.TryGetValue(id, out var ev) ? s.day - ev.day : -1;

        /// <summary>The system farthest from Haven, where the Anchors strike first.</summary>
        public static StarSystem RimSystem(GameState s)
        {
            StarSystem best = s.galaxy.Home;
            float bestD = -1f;
            foreach (var sys in s.galaxy.systems)
            {
                float d = Vector2.Distance(sys.pos, s.galaxy.Home.pos);
                if (d > bestD) { bestD = d; best = sys; }
            }
            return best;
        }
    }
}
