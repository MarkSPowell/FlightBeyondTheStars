using System.Collections.Generic;
using UnityEngine;

namespace FBTS
{
    /// <summary>A major artifact aboard: unidentified until the Science Officer has studied it long enough.</summary>
    public class OwnedArtifact
    {
        public string id;
        public bool identified, installed;
        public float study;   // days of study done (at skill 100 one day counts as one)
        public ArtifactRow Row => GameData.Artifact(id);
    }

    /// <summary>
    /// Major artifacts (Artifacts tab): found in ruins, each at most once per game. They come aboard unidentified;
    /// the Science Officer studies one at a time as days pass (faster with skill), and a Noeti Archivist aboard
    /// identifies them on the spot. Identified, an artifact can be installed (Upgrade), adds a captain's log
    /// entry and a conversation keyword (Clue), sells for more to the races that prize it (Trade), or is wanted
    /// by someone in a conversation (Quest; see has_artifact / give_artifact in the Ink files).
    /// </summary>
    public static class Artifacts
    {
        /// <summary>Called when an artifact is identified (title, text), so the game can show it.</summary>
        public static System.Action<string, string> OnIdentified;

        public static bool Owns(GameState s, string id, bool identifiedOnly = true)
        {
            foreach (var a in s.artifacts) if (a.id == id && (a.identified || !identifiedOnly)) return true;
            return false;
        }

        /// <summary>Pick an artifact not yet found this game (by weight) and bring it aboard. Null when every
        /// artifact has already turned up (the caller then gives an ordinary "Major artifact" trade good).</summary>
        public static OwnedArtifact Find(GameState s, System.Random rng)
        {
            float total = 0f;
            foreach (var r in GameData.Artifacts) if (!s.artifactsFound.Contains(r.id)) total += Mathf.Max(0.01f, r.weight);
            if (total <= 0f) return null;
            double pick = rng.NextDouble() * total;
            foreach (var r in GameData.Artifacts)
            {
                if (s.artifactsFound.Contains(r.id)) continue;
                pick -= Mathf.Max(0.01f, r.weight);
                if (pick > 0) continue;
                var a = new OwnedArtifact { id = r.id };
                s.artifacts.Add(a);
                s.artifactsFound.Add(r.id);
                if (HasArchivist(s)) Identify(s, a, "The Noeti aboard recognises it at once.");
                return a;
            }
            return null;
        }

        /// <summary>A Noeti crew member (Archivist trait) identifies artifacts on pickup.</summary>
        public static bool HasArchivist(GameState s)
        {
            foreach (var m in s.crew) if (!m.dead && m.species == "Noeti") return true;
            return false;
        }

        /// <summary>Days of study left for an artifact at the current Science Officer's skill (-1 = no one to study it).</summary>
        public static int DaysLeft(GameState s, OwnedArtifact a)
        {
            var r = a.Row;
            if (r == null || a.identified) return 0;
            float rate = StudyRate(s);
            if (rate <= 0f) return -1;
            return Mathf.Max(1, Mathf.CeilToInt((r.studyDays - a.study) / rate));
        }

        /// <summary>Study-days earned per day: Science Officer skill / 100, plus study traits.</summary>
        public static float StudyRate(GameState s)
        {
            var holder = s.stations[Roles.Science];
            if (holder == null || !holder.Fit) return 0f;
            return Mathf.Max(0.1f, s.scienceSkill / 100f) * (1f + s.Trait("ArtifactStudy"));
        }

        /// <summary>The oldest unidentified artifact (the one being studied), or null.</summary>
        public static OwnedArtifact Studying(GameState s)
        {
            foreach (var a in s.artifacts) if (!a.identified) return a;
            return null;
        }

        /// <summary>One day passes: the Science Officer works on the next unidentified artifact.</summary>
        public static void Day(GameState s)
        {
            var a = Studying(s);
            if (a == null || a.Row == null) return;
            float rate = StudyRate(s);
            if (rate <= 0f) return;
            a.study += rate;
            Crew.Gain(s, Roles.Science, GameData.T("XpArtifactStudyDay", 0.3f));
            if (a.study >= a.Row.studyDays) Identify(s, a, "The Science Officer has finished studying it.");
        }

        public static void Identify(GameState s, OwnedArtifact a, string how)
        {
            var r = a.Row;
            if (r == null || a.identified) return;
            a.identified = true;
            a.study = r.studyDays;
            if (!string.IsNullOrEmpty(r.clue) && !s.loreFound.Contains("artifact:" + r.id)) s.loreFound.Add("artifact:" + r.id);
            if (!string.IsNullOrEmpty(r.keyword)) s.keywords.Add(r.keyword);

            var sb = new System.Text.StringBuilder();
            sb.Append(how).Append("\n\n").Append(r.description);
            if (!string.IsNullOrEmpty(r.clue)) sb.Append("\n\n").Append(r.clue).Append("\n\n(Added to the captain's log.)");
            sb.Append("\n\n").Append(Uses(r));
            OnIdentified?.Invoke($"Artifact identified: {r.name}", sb.ToString());
        }

        /// <summary>A short line on what the artifact is good for.</summary>
        public static string Uses(ArtifactRow r)
        {
            var parts = new List<string>();
            if (r.Is("Upgrade")) parts.Add("Install it on the Artifacts screen: " + EffectText(r) + ".");
            if (r.Is("Trade"))
            {
                string who = BestBuyers(r);
                parts.Add(who != null ? $"Collectors will pay well for it, the {who} most of all." : "Collectors will pay well for it.");
            }
            if (r.Is("Quest")) parts.Add("Someone, somewhere, will want this. It may be worth showing it around.");
            return string.Join(" ", parts);
        }

        static string BestBuyers(ArtifactRow r)
        {
            string best = null; float m = 1f;
            if (string.IsNullOrEmpty(r.racePrices)) return null;
            foreach (var part in r.racePrices.Split(';'))
            {
                int c = part.LastIndexOf(':');
                if (c <= 0) continue;
                string race = part.Substring(0, c).Trim();
                float v = r.PriceFor(race);
                if (v > m) { m = v; best = race; }
            }
            return best;
        }

        public static string EffectText(ArtifactRow r)
        {
            switch (r.effect)
            {
                case "JumpFuel": return $"jumps use {r.amount * 100f:0}% less fuel";
                case "TravelSpeed": return $"travel {r.amount * 100f:0}% faster along routes";
                case "ScanSkill": return $"+{r.amount:0} to the Science Officer's skill when scanning planets";
                case "Armor": return $"+{r.amount:0} hull armour";
                case "ShieldCapacity": return $"shields hold {r.amount * 100f:0}% more";
                case "Cargo": return $"+{r.amount:0} cargo space";
                case "FuelTank": return $"+{r.amount:0} fuel tank";
                default: return "no effect";
            }
        }

        /// <summary>What a race's market pays for an artifact. Unidentified ones fetch the ordinary
        /// "Major artifact" price; identified ones their value times the race's interest.</summary>
        public static float SellPrice(GameState s, StarSystem sys, OwnedArtifact a, string race)
        {
            if (race == null) return 0f;
            if (!a.identified || a.Row == null) return Rules.SellPrice(s, sys, "Major artifact", race);
            return a.Row.value * a.Row.PriceFor(race) * Rules.Haggle(s) * Rules.RepFactor(s, race);
        }

        public static void Remove(GameState s, OwnedArtifact a) => s.artifacts.Remove(a);

        /// <summary>Quest hand-over from a conversation. False if the artifact isn't aboard and identified.</summary>
        public static bool Give(GameState s, string id)
        {
            foreach (var a in s.artifacts)
                if (a.id == id && a.identified) { s.artifacts.Remove(a); return true; }
            return false;
        }

        public static string Name(OwnedArtifact a) => a.identified && a.Row != null ? a.Row.name : "Unidentified artifact";
    }
}
