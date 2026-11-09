using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FBTS
{
    /// <summary>
    /// JSON save files in Application.persistentDataPath/saves. The galaxy itself is rebuilt from its seed,
    /// so a save only stores what the player changed: money, ship, cargo, time, position and planet progress.
    /// </summary>
    public static class SaveSystem
    {
        public const int Version = 4;
        public const string QuickSlot = "quicksave";
        public const string AutoSlot = "autosave";

        [Serializable] class CargoEntry { public string commodity; public int amount; }
        [Serializable] class ArtifactEntry { public string id; public bool identified, installed; public float study; }

        [Serializable]
        class CrewEntry
        {
            public string name, species, personality;
            public bool captain, critical;
            public float[] skills;
            public float earned;
            public float[] earnedByRole;
            public int level, pendingTraits;
            public string[] traits, traitChoices;
            public float health, morale;
            public string[] moraleLog;
        }

        [Serializable]
        class PlanetEntry
        {
            public int system;
            public int index;
            public bool scanned;
            public long[] mined;
            public long[] visited;
        }

        [Serializable]
        class SaveData
        {
            public int version;
            public string savedAt;
            public int seed;
            public int day;
            public float credits;
            public float fuel;
            public string hull;
            public int currentSystem;
            public int terrainVehicleTier;
            public int navigatorSkill, pilotSkill, scienceSkill, commsSkill;   // version 1-2 only (now per crew member)
            public string[] upgrades;
            public string[] lore;
            public CargoEntry[] cargo;
            public int[] visitedSystems;
            public PlanetEntry[] planets;
            // Version 2: diplomacy
            public string[] repRaces;
            public int[] repValues;
            public string[] keywords;
            public string[] inkRaces;
            public string[] inkStates;
            // World events (also version 2; older saves simply have none)
            public string[] eventIds;
            public int[] eventDays;
            public int[] eventSystems;
            public string posture;
            // Combat (version 2 as well; missing in older saves means the defaults)
            public int weaponsSkill;   // version 2 only
            // Version 3: crew (stations hold indexes into crew; -1 = empty)
            public CrewEntry[] crew;
            public int[] stations;
            public int lastPortDay, lastShoreLeaveDay;
            public string[] weapons;
            public int shieldTier;
            public float hullDamage;
            // Version 4: major artifacts carried, and every one ever found (each turns up once per game)
            public ArtifactEntry[] artifacts;
            public string[] artifactsFound;
        }

        public class SlotInfo
        {
            public string slot;
            public bool exists;
            public string summary;
        }

        static string Dir => Path.Combine(Application.persistentDataPath, "saves");
        static string PathFor(string slot) => Path.Combine(Dir, slot + ".json");

        public static bool Save(GameState s, string slot, out string error)
        {
            error = null;
            try
            {
                var d = new SaveData
                {
                    version = Version,
                    savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                    seed = s.seed,
                    day = s.day,
                    credits = s.credits,
                    fuel = s.fuel,
                    hull = s.hull != null ? s.hull.hullClass : null,
                    currentSystem = s.current.id,
                    terrainVehicleTier = s.terrainVehicleTier,
                    upgrades = new List<string>(s.upgrades).ToArray(),
                    lore = s.loreFound.ToArray()
                };

                var cargo = new List<CargoEntry>();
                foreach (var kv in s.cargo) cargo.Add(new CargoEntry { commodity = kv.Key, amount = kv.Value });
                d.cargo = cargo.ToArray();

                d.repRaces = new List<string>(s.reputation.Keys).ToArray();
                d.repValues = new int[d.repRaces.Length];
                for (int i = 0; i < d.repRaces.Length; i++) d.repValues[i] = s.reputation[d.repRaces[i]];
                d.keywords = new List<string>(s.keywords).ToArray();
                d.inkRaces = new List<string>(s.inkStates.Keys).ToArray();
                d.inkStates = new string[d.inkRaces.Length];
                for (int i = 0; i < d.inkRaces.Length; i++) d.inkStates[i] = s.inkStates[d.inkRaces[i]];
                d.posture = s.posture;
                var crew = new List<CrewEntry>();
                foreach (var m in s.crew)
                    crew.Add(new CrewEntry
                    {
                        name = m.name, species = m.species, personality = m.personality,
                        captain = m.captain, critical = m.critical,
                        skills = (float[])m.skills.Clone(), earned = m.earned, earnedByRole = (float[])m.earnedByRole.Clone(),
                        level = m.level, pendingTraits = m.pendingTraits,
                        traits = m.traits.ToArray(), traitChoices = m.traitChoices,
                        health = m.health, morale = m.morale, moraleLog = m.moraleLog.ToArray()
                    });
                d.crew = crew.ToArray();
                d.stations = new int[s.stations.Length];
                for (int i = 0; i < s.stations.Length; i++) d.stations[i] = s.stations[i] != null ? s.crew.IndexOf(s.stations[i]) : -1;
                d.lastPortDay = s.lastPortDay;
                d.lastShoreLeaveDay = s.lastShoreLeaveDay;
                d.weapons = s.weapons.ToArray();
                d.shieldTier = s.shieldTier;
                d.hullDamage = s.hullDamage;
                var arts = new List<ArtifactEntry>();
                foreach (var a in s.artifacts) arts.Add(new ArtifactEntry { id = a.id, identified = a.identified, installed = a.installed, study = a.study });
                d.artifacts = arts.ToArray();
                d.artifactsFound = new List<string>(s.artifactsFound).ToArray();
                d.eventIds = new List<string>(s.events.Keys).ToArray();
                d.eventDays = new int[d.eventIds.Length];
                d.eventSystems = new int[d.eventIds.Length];
                for (int i = 0; i < d.eventIds.Length; i++) { d.eventDays[i] = s.events[d.eventIds[i]].day; d.eventSystems[i] = s.events[d.eventIds[i]].system; }

                var visited = new List<int>();
                var planets = new List<PlanetEntry>();
                foreach (var sys in s.galaxy.systems)
                {
                    if (sys.visited) visited.Add(sys.id);
                    for (int i = 0; i < sys.planets.Count; i++)
                    {
                        var p = sys.planets[i];
                        if (!p.scanned && p.minedDeposits.Count == 0 && p.visitedSections.Count == 0) continue;
                        planets.Add(new PlanetEntry
                        {
                            system = sys.id,
                            index = i,
                            scanned = p.scanned,
                            mined = new List<long>(p.minedDeposits).ToArray(),
                            visited = new List<long>(p.visitedSections).ToArray()
                        });
                    }
                }
                d.visitedSystems = visited.ToArray();
                d.planets = planets.ToArray();

                Directory.CreateDirectory(Dir);
                string tmp = PathFor(slot) + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(d, true));
                if (File.Exists(PathFor(slot))) File.Delete(PathFor(slot));
                File.Move(tmp, PathFor(slot)); // write-then-rename so a crash never leaves a half-written save
                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                Debug.LogError("[FBTS] Save failed: " + e);
                return false;
            }
        }

        public static GameState Load(string slot, out string error)
        {
            error = null;
            try
            {
                if (!File.Exists(PathFor(slot))) { error = "No save in that slot."; return null; }
                var d = JsonUtility.FromJson<SaveData>(File.ReadAllText(PathFor(slot)));
                if (d == null) { error = "Save file is unreadable."; return null; }
                if (d.version > Version) { error = "Save was made by a newer version of the game."; return null; }

                var s = GameState.NewGame(d.seed);
                s.day = d.day;
                s.credits = d.credits;
                s.hull = GameData.Hull(d.hull);
                s.terrainVehicleTier = Mathf.Clamp(d.terrainVehicleTier, 1, 5);
                if (d.upgrades != null) foreach (var u in d.upgrades) s.upgrades.Add(u);
                if (d.lore != null) s.loreFound.AddRange(d.lore);
                if (d.cargo != null) foreach (var c in d.cargo) s.cargo[c.commodity] = c.amount;
                s.fuel = Mathf.Min(d.fuel, s.FuelTank);
                // Version 1 saves have no diplomacy; those arrays are simply missing.
                if (d.repRaces != null && d.repValues != null)
                    for (int i = 0; i < Mathf.Min(d.repRaces.Length, d.repValues.Length); i++) s.reputation[d.repRaces[i]] = d.repValues[i];
                if (d.keywords != null) foreach (var k in d.keywords) s.keywords.Add(k);
                if (d.inkRaces != null && d.inkStates != null)
                    for (int i = 0; i < Mathf.Min(d.inkRaces.Length, d.inkStates.Length); i++) s.inkStates[d.inkRaces[i]] = d.inkStates[i];
                if (!string.IsNullOrEmpty(d.posture)) s.posture = d.posture;
                LoadCrew(s, d);
                if (d.weapons != null && d.weapons.Length > 0) { s.weapons.Clear(); s.weapons.AddRange(d.weapons); }
                if (d.shieldTier > 0) s.shieldTier = Mathf.Clamp(d.shieldTier, 1, 5);
                s.hullDamage = Mathf.Max(0f, d.hullDamage);
                if (d.artifacts != null)
                    foreach (var a in d.artifacts)
                        if (GameData.Artifact(a.id) != null)
                            s.artifacts.Add(new OwnedArtifact { id = a.id, identified = a.identified, installed = a.installed, study = a.study });
                if (d.artifactsFound != null) foreach (var id in d.artifactsFound) s.artifactsFound.Add(id);
                if (d.eventIds != null && d.eventDays != null && d.eventSystems != null)
                    for (int i = 0; i < d.eventIds.Length && i < d.eventDays.Length && i < d.eventSystems.Length; i++)
                        s.events[d.eventIds[i]] = new WorldEvent { day = d.eventDays[i], system = d.eventSystems[i] };

                if (d.currentSystem >= 0 && d.currentSystem < s.galaxy.systems.Count)
                    s.current = s.galaxy.systems[d.currentSystem];
                if (d.visitedSystems != null)
                    foreach (int id in d.visitedSystems)
                        if (id >= 0 && id < s.galaxy.systems.Count) s.galaxy.systems[id].visited = true;
                if (d.planets != null)
                    foreach (var pe in d.planets)
                    {
                        if (pe.system < 0 || pe.system >= s.galaxy.systems.Count) continue;
                        var sys = s.galaxy.systems[pe.system];
                        if (pe.index < 0 || pe.index >= sys.planets.Count) continue;
                        var p = sys.planets[pe.index];
                        p.scanned = pe.scanned;
                        if (pe.mined != null) foreach (var k in pe.mined) p.minedDeposits.Add(k);
                        if (pe.visited != null) foreach (var k in pe.visited) p.visitedSections.Add(k);
                    }
                return s;
            }
            catch (Exception e)
            {
                error = e.Message;
                Debug.LogError("[FBTS] Load failed: " + e);
                return null;
            }
        }

        static void LoadCrew(GameState s, SaveData d)
        {
            if (d.crew == null || d.crew.Length == 0)
            {
                // An older save: keep the new starting crew, but give them the skills that save had.
                SetHolder(s, Roles.Navigator, d.navigatorSkill);
                SetHolder(s, Roles.Pilot, d.pilotSkill);
                SetHolder(s, Roles.Science, d.scienceSkill);
                SetHolder(s, Roles.Comms, d.commsSkill);
                SetHolder(s, Roles.Weapons, d.weaponsSkill);
                return;
            }
            s.crew.Clear();
            foreach (var e in d.crew)
            {
                var m = new CrewMember
                {
                    name = e.name, species = e.species, personality = e.personality,
                    captain = e.captain, critical = e.critical,
                    earned = e.earned, level = e.level, pendingTraits = e.pendingTraits,
                    traitChoices = e.traitChoices != null && e.traitChoices.Length > 0 ? e.traitChoices : null,
                    health = e.health, morale = e.morale
                };
                if (e.skills != null) for (int i = 0; i < Mathf.Min(e.skills.Length, m.skills.Length); i++) m.skills[i] = e.skills[i];
                if (e.earnedByRole != null) for (int i = 0; i < Mathf.Min(e.earnedByRole.Length, m.earnedByRole.Length); i++) m.earnedByRole[i] = e.earnedByRole[i];
                if (e.traits != null) m.traits.AddRange(e.traits);
                if (e.moraleLog != null) m.moraleLog.AddRange(e.moraleLog);
                s.crew.Add(m);
            }
            for (int i = 0; i < s.stations.Length; i++)
            {
                int idx = d.stations != null && i < d.stations.Length ? d.stations[i] : -1;
                s.stations[i] = idx >= 0 && idx < s.crew.Count ? s.crew[idx] : null;
            }
            s.lastPortDay = d.lastPortDay;
            s.lastShoreLeaveDay = d.lastShoreLeaveDay;
        }

        static void SetHolder(GameState s, int role, int skill)
        {
            var m = s.stations[role];
            if (m != null && skill > 0) m.skills[role] = skill;
        }

        public static SlotInfo Info(string slot)
        {
            var info = new SlotInfo { slot = slot, exists = File.Exists(PathFor(slot)) };
            if (!info.exists) { info.summary = "Empty"; return info; }
            try
            {
                var d = JsonUtility.FromJson<SaveData>(File.ReadAllText(PathFor(slot)));
                info.summary = $"Day {d.day}, {d.credits:N0} cr, saved {d.savedAt}";
            }
            catch { info.summary = "Unreadable"; }
            return info;
        }
    }
}
