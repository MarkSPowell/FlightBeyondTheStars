using System.Collections.Generic;
using UnityEngine;

namespace FBTS
{
    /// <summary>One member of the ship's crew.</summary>
    public class CrewMember
    {
        public string name, species, personality;
        public bool captain;
        public readonly float[] skills = new float[Roles.Names.Length];

        // Experience earned by doing things (training doesn't count), in total and per station. Levels come from this.
        public float earned;
        public readonly float[] earnedByRole = new float[Roles.Names.Length];
        public int level;
        public readonly List<string> traits = new List<string>();
        public int pendingTraits;               // levels reached but trait not yet chosen
        public string[] traitChoices;           // the 3 traits on offer for the next pending level

        public float health = 100f;
        public bool critical, dead;
        public float morale;
        public readonly List<string> moraleLog = new List<string>();   // most recent first

        public int Skill(int role) => Mathf.RoundToInt(skills[role]);
        public int Cap(int role) => GameData.SkillCap(species, role);
        public bool Fit => !dead && !critical;

        /// <summary>Sum of this member's own traits with an effect (for "Any" traits and personal effects).</summary>
        public float OwnTrait(string effect)
        {
            float t = 0f;
            foreach (var id in traits)
            {
                var row = GameData.Trait(id);
                if (row != null && row.effect == effect && row.role == "Any") t += row.amount;
            }
            return t;
        }

        public string MoraleState =>
            morale >= 80f ? "Inspired" : morale >= 50f ? "Steady" : morale >= 25f ? "Shaken" :
            morale >= GameData.T("LeaveMorale", 10f) ? "Breaking" : "Wants off the ship";

        /// <summary>How morale scales effectiveness: +10% inspired down to -25% breaking.</summary>
        public float MoraleFactor => morale >= 80f ? 1.1f : morale >= 50f ? 1f : morale >= 25f ? 0.9f : 0.75f;

        /// <summary>Injured crew work at reduced effectiveness: 50% at 0 health, 100% when healthy.</summary>
        public float HealthFactor => 0.5f + 0.5f * Mathf.Clamp01(health / 100f);

        public string HealthState => dead ? "Dead" : critical ? "CRITICAL" : health >= 99.5f ? "Healthy" : $"Injured ({health:0}%)";
    }

    /// <summary>
    /// Crew rules: who sits at each station, skill growth and levels, traits, morale, injuries.
    /// Every number comes from the spreadsheet (Tuning, Personalities, MoraleEvents, Traits).
    /// </summary>
    public static class Crew
    {
        static float T(string k, float f) => GameData.T(k, f);

        // ---------------------------------------------------------------- Stations

        public static int StationCount(GameState s, CrewMember m)
        {
            int n = 0;
            foreach (var h in s.stations) if (h == m) n++;
            return n;
        }

        /// <summary>The skill the ship actually gets from a station: the holder's skill, scaled by morale,
        /// health and double duty (DoubleDutyPenalty). An empty or incapacitated station counts as 0.</summary>
        public static int StationSkill(GameState s, int role)
        {
            var m = s.stations[role];
            if (m == null || !m.Fit) return 0;
            float v = m.skills[role] * m.MoraleFactor * m.HealthFactor;
            if (StationCount(s, m) > 1) v *= 1f - T("DoubleDutyPenalty", 0.25f);
            return Mathf.RoundToInt(v);
        }

        public static void Assign(GameState s, int role, CrewMember m)
        {
            s.stations[role] = m;
        }

        /// <summary>Sum of trait effects that apply right now: station traits held by whoever is at that station.</summary>
        public static float TraitBonus(GameState s, string effect)
        {
            float t = 0f;
            for (int role = 0; role < s.stations.Length; role++)
            {
                var m = s.stations[role];
                if (m == null || !m.Fit) continue;
                foreach (var id in m.traits)
                {
                    var row = GameData.Trait(id);
                    if (row != null && row.effect == effect && GameData.RoleIndex(row.role) == role) t += row.amount;
                }
            }
            return t;
        }

        // ---------------------------------------------------------------- Experience and levels

        /// <summary>Skill points needed for the next level: LevelFirst, then LevelStep more each time.</summary>
        public static float LevelCost(int levelReached) => T("LevelFirst", 30f) + T("LevelStep", 15f) * levelReached;

        public static float NextLevelAt(CrewMember m)
        {
            float total = 0f;
            for (int i = 0; i <= m.level; i++) total += LevelCost(i);
            return total;
        }

        /// <summary>Whoever is at a station learns from doing its job. Every point of experience counts toward
        /// levels; skill rises by the same amount scaled by 1 - (skill/cap)^2, so it slows near the species cap.
        /// Humans learn 25% faster (their species trait); Quick Learner adds more.</summary>
        /// <summary>Set by the game manager to show crew news (level-ups) as a toast.</summary>
        public static System.Action<string> Notice;

        public static void Gain(GameState s, int role, float amount)
        {
            var m = s.stations[role];
            if (m == null || !m.Fit || amount <= 0f) return;
            float xp = amount * (m.species == "Humans" ? 1.25f : 1f) * (1f + m.OwnTrait("XpGain"));

            // Experience always counts toward levels, so veterans at their species cap still grow (through traits).
            m.earned += xp;
            m.earnedByRole[role] += xp;

            // Skill itself grows more slowly as it nears the cap (gently at first, steeply at the end).
            float cap = m.Cap(role);
            float frac = Mathf.Clamp01(m.skills[role] / Mathf.Max(1f, cap));
            m.skills[role] = Mathf.Min(cap, m.skills[role] + xp * (1f - frac * frac));

            while (m.earned >= NextLevelAt(m))
            {
                m.level++;
                m.pendingTraits++;
                Morale(s, "LevelUp", m);
                if (m.traitChoices == null) m.traitChoices = OfferTraits(m, new System.Random(m.name.GetHashCode() ^ m.level));
                Notice?.Invoke($"{m.name} reached level {m.level}! Choose a trait on the Crew screen (C).");
            }
        }

        /// <summary>Three different traits the member doesn't have, weighted toward the stations where they've
        /// earned the most experience (and the one they hold now). "Any" traits are always possible.</summary>
        public static string[] OfferTraits(CrewMember m, System.Random rng)
        {
            var pool = new List<TraitRow>();
            var weights = new List<float>();
            foreach (var t in GameData.Traits)
            {
                if (m.traits.Contains(t.id)) continue;
                int role = GameData.RoleIndex(t.role);
                float w = role < 0 ? 2f : 0.5f + m.earnedByRole[role] / 5f;
                pool.Add(t);
                weights.Add(w);
            }
            var picks = new List<string>();
            while (picks.Count < 3 && pool.Count > 0)
            {
                float total = 0f;
                foreach (var w in weights) total += w;
                double r = rng.NextDouble() * total;
                int i = 0;
                for (; i < pool.Count - 1; i++) { r -= weights[i]; if (r <= 0) break; }
                picks.Add(pool[i].id);
                pool.RemoveAt(i);
                weights.RemoveAt(i);
            }
            return picks.ToArray();
        }

        public static void ChooseTrait(GameState s, CrewMember m, string id)
        {
            if (m.traitChoices == null || System.Array.IndexOf(m.traitChoices, id) < 0) return;
            m.traits.Add(id);
            m.pendingTraits = Mathf.Max(0, m.pendingTraits - 1);
            m.traitChoices = m.pendingTraits > 0 ? OfferTraits(m, new System.Random(m.name.GetHashCode() ^ (m.level * 7 + m.traits.Count))) : null;
        }

        // ---------------------------------------------------------------- Morale

        /// <summary>Apply a morale event (MoraleEvents tab) to the whole crew, or to one member.
        /// Personality multiplies it; Iron Nerves halves losses; the Varkesh feel no combat morale loss.</summary>
        public static void Morale(GameState s, string evt, CrewMember only = null, float scale = 1f)
        {
            var row = GameData.MoraleEvent(evt);
            if (row == null) return;
            foreach (var m in s.crew)
            {
                if (m.dead || (only != null && m != only)) continue;
                float d = row.@base * scale * GameData.MoraleMultiplier(m.personality, evt);
                if (d < 0f)
                {
                    if (m.species == "Varkesh" && (evt == "Fled" || evt == "FightLost" || evt == "Injured")) continue;
                    d *= 1f - Mathf.Clamp01(m.OwnTrait("MoraleLoss"));
                }
                if (Mathf.Abs(d) < 0.01f) continue;
                m.morale = Mathf.Clamp(m.morale + d, 0f, 100f);
                if (Mathf.Abs(d) >= 0.5f)
                {
                    // Merge repeats of the same reason (the long-voyage drip) into one running entry.
                    string reason = row.description;
                    if (m.moraleLog.Count > 0 && m.moraleLog[0].EndsWith(reason)) m.moraleLog.RemoveAt(0);
                    m.moraleLog.Insert(0, $"{(d > 0 ? "+" : "")}{d:0.#}: {reason}");
                    if (m.moraleLog.Count > 6) m.moraleLog.RemoveAt(m.moraleLog.Count - 1);
                }
            }
        }

        public static float MoraleBaseline(GameState s) => T("MoraleStart", 60f) + s.UpgradeBonus("MoraleBaseline");

        // ---------------------------------------------------------------- Time passing

        /// <summary>One day passes: morale drifts toward its baseline, long voyages wear on the crew,
        /// and the injured heal (faster with a good Medical Officer).</summary>
        public static void Day(GameState s)
        {
            float baseline = MoraleBaseline(s);
            float drift = T("MoraleDrift", 0.5f);
            bool longVoyage = s.day - s.lastPortDay > T("LongVoyageDays", 30f) + s.UpgradeBonus("VoyageTolerance");
            if (longVoyage) Morale(s, "LongVoyage");

            int med = StationSkill(s, Roles.Medical);
            float heal = T("HealPerDay", 4f) * (1f + GameData.RoleBonus("Medical Officer", med)) * (1f + TraitBonus(s, "Recovery"));
            foreach (var m in s.crew)
            {
                if (m.dead) continue;
                if (!longVoyage) m.morale = Mathf.MoveTowards(m.morale, baseline, drift);
                if (!m.critical && m.health < 100f)
                {
                    m.health = Mathf.Min(100f, m.health + heal);
                    Gain(s, Roles.Medical, T("XpMedicalDay", 0.5f));
                }
            }
        }

        /// <summary>Docking at a starport: resets the long-voyage clock. Anyone whose morale has fallen below
        /// LeaveMorale walks off the ship. Returns their names.</summary>
        public static List<string> ArriveAtPort(GameState s)
        {
            s.lastPortDay = s.day;
            var gone = new List<string>();
            float leave = T("LeaveMorale", 10f);
            for (int i = s.crew.Count - 1; i >= 0; i--)
            {
                var m = s.crew[i];
                if (m.dead || m.captain || m.morale >= leave) continue;
                Remove(s, m);
                gone.Add(m.name);
            }
            return gone;
        }

        public static void Remove(GameState s, CrewMember m)
        {
            for (int r = 0; r < s.stations.Length; r++) if (s.stations[r] == m) s.stations[r] = null;
            s.crew.Remove(m);
        }

        // ---------------------------------------------------------------- Injuries

        /// <summary>A hit on the hull may injure someone: chance InjuryPerDamage per point of damage.</summary>
        public static string InjureFromHit(GameState s, float hullDamage)
        {
            if (hullDamage <= 0f) return null;
            var candidates = s.crew.FindAll(c => c.Fit);
            if (candidates.Count == 0) return null;
            var m = candidates[Random.Range(0, candidates.Count)];
            float chance = hullDamage * T("InjuryPerDamage", 0.004f) * (1f - Mathf.Clamp01(m.OwnTrait("InjuryChance")));
            if (Random.value >= chance) return null;
            m.health -= hullDamage * T("InjuryHealthPerDamage", 1f);
            Morale(s, "Injured", m);
            if (m.health > 0f) return $"{m.name} is injured.";
            m.health = 0f;
            m.critical = true;
            return $"{m.name} is critically wounded!";
        }

        /// <summary>After a fight, the Medical Officer tries to save each critically wounded crewmate:
        /// SaveChanceBase rising to SaveChanceMax at full skill, plus Field Medic. Failure is death.</summary>
        public static List<string> ResolveCritical(GameState s)
        {
            var lines = new List<string>();
            foreach (var m in s.crew.ToArray())
            {
                if (!m.critical || m.dead) continue;
                var medic = s.stations[Roles.Medical];
                bool hasMedic = medic != null && medic.Fit && medic != m;
                float skill = hasMedic ? StationSkill(s, Roles.Medical) : 0f;
                float baseChance = T("SaveChanceBase", 0.25f), max = T("SaveChanceMax", 0.5f);
                float chance = (baseChance + (max - baseChance) * skill / T("MaxSkill", 250f) + (hasMedic ? TraitBonus(s, "SaveChance") : 0f))
                               * (hasMedic ? 1f : 0.5f);
                if (hasMedic) Gain(s, Roles.Medical, T("XpMedicalSave", 3f));
                if (Random.value < chance)
                {
                    m.critical = false;
                    m.health = 10f;
                    Morale(s, "CrewSaved");
                    lines.Add(hasMedic ? $"{medic.name} saved {m.name}'s life ({chance:P0} chance)."
                                       : $"With no Medical Officer, {m.name} pulled through anyway ({chance:P0} chance).");
                }
                else
                {
                    m.dead = true;
                    m.critical = false;
                    bool wasCaptain = m.captain;
                    Remove(s, m);
                    Morale(s, "CrewDeath");
                    lines.Add(hasMedic ? $"{medic.name} couldn't save {m.name}. ({chance:P0} chance)"
                                       : $"{m.name} died. No Medical Officer was at their station.");
                    if (wasCaptain)
                    {
                        var next = NextInCommand(s);
                        if (next != null)
                        {
                            next.captain = true;
                            lines.Add($"The captain is dead. {next.name} takes command.");
                        }
                    }
                }
            }
            return lines;
        }

        /// <summary>Highest level, then most experience, among the living.</summary>
        public static CrewMember NextInCommand(GameState s)
        {
            CrewMember best = null;
            foreach (var m in s.crew)
            {
                if (m.dead) continue;
                if (best == null || m.level > best.level || (m.level == best.level && m.earned > best.earned)) best = m;
            }
            return best;
        }

        public static bool AllLost(GameState s) => s.crew.TrueForAll(m => m.dead);

        // ---------------------------------------------------------------- Recruits

        static readonly string[] First =
        {
            "Ayla", "Marcus", "Ines", "Tomas", "Priya", "Kenji", "Zara", "Dmitri", "Lena", "Obi", "Mei", "Rafael",
            "Sana", "Elias", "Noor", "Kofi", "Yuki", "Ivan", "Leila", "Mateo", "Hana", "Arjun", "Freya", "Tariq"
        };
        static readonly string[] Last =
        {
            "Reyes", "Okafor", "Lindqvist", "Nakamura", "Haddad", "Petrova", "Chen", "Abara", "Moreau", "Singh",
            "Kowalski", "Mensah", "Torres", "Sato", "Varga", "Osei", "Novak", "Ibrahim", "Castillo", "Park"
        };

        public static string RandomName(System.Random rng) => $"{First[rng.Next(First.Length)]} {Last[rng.Next(Last.Length)]}";

        public static string RandomPersonality(System.Random rng) =>
            GameData.Personalities.Length > 0 ? GameData.Personalities[rng.Next(GameData.Personalities.Length)].personality : "Stoic";

        /// <summary>A recruit trained for one station: main skill high, one secondary skill, the rest basic.</summary>
        public static CrewMember Recruit(GameState s, System.Random rng, string species, int mainRole, int mainMin, int mainMax)
        {
            var m = new CrewMember
            {
                name = RandomName(rng),
                species = species,
                personality = RandomPersonality(rng),
                morale = MoraleBaseline(s)
            };
            for (int r = 0; r < m.skills.Length; r++) m.skills[r] = Mathf.Min(m.Cap(r), 10 + rng.Next(31));
            int second = rng.Next(m.skills.Length);
            if (second != mainRole) m.skills[second] = Mathf.Min(m.Cap(second), 35 + rng.Next(26));
            m.skills[mainRole] = Mathf.Min(m.Cap(mainRole), mainMin + rng.Next(mainMax - mainMin + 1));
            return m;
        }

        /// <summary>The starting crew: six humans for the six bridge stations. The Science Officer has some medical
        /// training, but Medical and Security start empty: put someone on double duty or hire for them.
        /// The Pilot is the captain.</summary>
        public static void StartingCrew(GameState s)
        {
            var rng = new System.Random(s.seed * 13 + 5);
            s.crew.Clear();
            for (int i = 0; i < s.stations.Length; i++) s.stations[i] = null;
            int[] roles = { Roles.Pilot, Roles.Navigator, Roles.Engineer, Roles.Weapons, Roles.Science, Roles.Comms };
            foreach (int role in roles)
            {
                var m = Recruit(s, rng, "Humans", role, 55, 70);
                if (role == Roles.Science) m.skills[Roles.Medical] = Mathf.Max(m.skills[Roles.Medical], 50 + rng.Next(11));
                s.crew.Add(m);
                s.stations[role] = m;
            }
            s.crew[0].captain = true;
        }

        /// <summary>Starport hiring fee: HireBase plus HirePerPoint per point of their best skill.</summary>
        public static float HireCost(CrewMember m)
        {
            float best = 0f;
            foreach (var v in m.skills) best = Mathf.Max(best, v);
            return T("HireBase", 1500f) + T("HirePerPoint", 20f) * best;
        }
    }
}
