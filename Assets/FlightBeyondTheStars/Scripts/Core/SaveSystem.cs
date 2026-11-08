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
        public const int Version = 1;
        public const string QuickSlot = "quicksave";
        public const string AutoSlot = "autosave";

        [Serializable] class CargoEntry { public string commodity; public int amount; }

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
            public int navigatorSkill, pilotSkill, scienceSkill, commsSkill;
            public string[] upgrades;
            public string[] lore;
            public CargoEntry[] cargo;
            public int[] visitedSystems;
            public PlanetEntry[] planets;
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
                    navigatorSkill = s.navigatorSkill,
                    pilotSkill = s.pilotSkill,
                    scienceSkill = s.scienceSkill,
                    commsSkill = s.commsSkill,
                    upgrades = new List<string>(s.upgrades).ToArray(),
                    lore = s.loreFound.ToArray()
                };

                var cargo = new List<CargoEntry>();
                foreach (var kv in s.cargo) cargo.Add(new CargoEntry { commodity = kv.Key, amount = kv.Value });
                d.cargo = cargo.ToArray();

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
                s.navigatorSkill = d.navigatorSkill;
                s.pilotSkill = d.pilotSkill;
                s.scienceSkill = d.scienceSkill;
                s.commsSkill = d.commsSkill;
                if (d.upgrades != null) foreach (var u in d.upgrades) s.upgrades.Add(u);
                if (d.lore != null) s.loreFound.AddRange(d.lore);
                if (d.cargo != null) foreach (var c in d.cargo) s.cargo[c.commodity] = c.amount;
                s.fuel = Mathf.Min(d.fuel, s.FuelTank);

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
