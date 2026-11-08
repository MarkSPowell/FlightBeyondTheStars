using System;
using System.Collections.Generic;
using UnityEngine;

namespace FBTS
{
    // Row types mirror the JSON exported from Flight_Beyond_the_Stars_Systems_Values.xlsx
    // (Tools/export_data.py). Field names must match the JSON keys exactly.

    [Serializable] public class TuningRow { public string key; public float value; }

    [Serializable]
    public class CommodityRow
    {
        public string commodity;
        public string category;
        public float basePrice;
        public float volatility;
        public float minPrice;
        public float maxPrice;
        public float cargoUnitsPerUnit;
        public string mainSource;
    }

    [Serializable] public class RacePriceRow { public string race; public string category; public float multiplier; }

    [Serializable]
    public class HullRow
    {
        public string hullClass;
        public float hullPoints, armor, moduleSlots, cargoUnits, crewBerths, fuelTank, speed, turnRate, jumpFuelMultiplier, cost;
    }

    [Serializable]
    public class ShipSystemRow
    {
        public string system, stat, unit;
        public float t1Value, t2Value, t3Value, t4Value, t5Value;
        public float t1Cost, t2Cost, t3Cost, t4Cost, t5Cost;

        public float Value(int tier)
        {
            switch (Mathf.Clamp(tier, 1, 5))
            {
                case 1: return t1Value;
                case 2: return t2Value;
                case 3: return t3Value;
                case 4: return t4Value;
                default: return t5Value;
            }
        }
    }

    [Serializable]
    public class UpgradeRow
    {
        public string id;
        public string name;
        public float cost;
        public string effect;   // Unlock, FuelTank, Cargo, RevealRadius
        public float amount;
        public string description;
    }

    [Serializable]
    public class SpeciesRow
    {
        public string name;
        public string majorOrMinor;
        public int[] caps;   // Navigator, Pilot, Engineer, Weapons, Science, Comms, Medical, Security
        public string howToGet;
        public string trait;
    }

    public static class Roles
    {
        public const int Navigator = 0, Pilot = 1, Engineer = 2, Weapons = 3, Science = 4, Comms = 5, Medical = 6, Security = 7;
        public static readonly string[] Names = { "Navigator", "Pilot", "Engineer", "Weapons Officer", "Science Officer", "Communications Officer", "Medical Officer", "Security Chief" };
    }

    [Serializable] public class LoreRow { public string id; public string race; public string title; public string text; }

    [Serializable] public class CrewRoleRow { public string role; public string mainEffect; public float maxBonusAtSkill250; public string secondaryEffect; }

    [Serializable]
    public class DeadlineStageRow
    {
        public int stage;
        public string name;
        public float baseStartDay;
        public float fuelPriceMultiplier;
        public float routesSevered;
        public float marketsLost;
        public string whatThePlayerSees;
    }

    [Serializable] class Table<T> { public T[] items; }

    /// <summary>Loads every data table once from Assets/Resources/Data/*.json.</summary>
    public static class GameData
    {
        public static Dictionary<string, float> Tuning = new Dictionary<string, float>();
        public static CommodityRow[] Commodities = new CommodityRow[0];
        public static RacePriceRow[] RacePrices = new RacePriceRow[0];
        public static HullRow[] Hulls = new HullRow[0];
        public static ShipSystemRow[] ShipSystems = new ShipSystemRow[0];
        public static CrewRoleRow[] CrewRoles = new CrewRoleRow[0];
        public static DeadlineStageRow[] Deadline = new DeadlineStageRow[0];
        public static UpgradeRow[] Upgrades = new UpgradeRow[0];
        public static LoreRow[] Lore = new LoreRow[0];
        public static SpeciesRow[] Species = new SpeciesRow[0];

        public static bool Loaded { get; private set; }

        public static void Load()
        {
            if (Loaded) return;
            Tuning.Clear();
            foreach (var t in LoadTable<TuningRow>("Tuning")) Tuning[t.key] = t.value;
            Commodities = LoadTable<CommodityRow>("Commodities");
            RacePrices = LoadTable<RacePriceRow>("RacePrices");
            Hulls = LoadTable<HullRow>("Hulls");
            ShipSystems = LoadTable<ShipSystemRow>("ShipSystems");
            CrewRoles = LoadTable<CrewRoleRow>("CrewRoles");
            Deadline = LoadTable<DeadlineStageRow>("DeadlineStages");
            Upgrades = LoadTable<UpgradeRow>("Upgrades");
            Lore = LoadTable<LoreRow>("Lore");
            Species = LoadTable<SpeciesRow>("Species");
            Loaded = true;
            Debug.Log($"[FBTS] Data loaded: {Commodities.Length} commodities, {Hulls.Length} hulls, {Tuning.Count} tuning levers.");
        }

        static T[] LoadTable<T>(string name)
        {
            var asset = Resources.Load<TextAsset>("Data/" + name);
            if (asset == null)
            {
                Debug.LogError($"[FBTS] Missing data file Resources/Data/{name}.json");
                return new T[0];
            }
            var table = JsonUtility.FromJson<Table<T>>(asset.text);
            return table != null && table.items != null ? table.items : new T[0];
        }

        public static float T(string key, float fallback)
        {
            return Tuning.TryGetValue(key, out var v) ? v : fallback;
        }

        public static CommodityRow Commodity(string name)
        {
            foreach (var c in Commodities) if (c.commodity == name) return c;
            return null;
        }

        public static HullRow Hull(string name)
        {
            foreach (var h in Hulls) if (h.hullClass == name) return h;
            return Hulls.Length > 0 ? Hulls[0] : null;
        }

        public static float RaceMultiplier(string race, string category)
        {
            foreach (var r in RacePrices) if (r.race == race && r.category == category) return r.multiplier;
            return 1f;
        }

        /// <summary>Bonus at a given skill, scaled linearly from the role's max bonus at MaxSkill.</summary>
        public static float RoleBonus(string role, int skill)
        {
            float max = 0f;
            foreach (var r in CrewRoles) if (r.role == role) max = r.maxBonusAtSkill250;
            return skill / T("MaxSkill", 250f) * max;
        }

        /// <summary>Highest skill a species can reach in a role (see Roles for indices).</summary>
        public static int SkillCap(string species, int role)
        {
            foreach (var sp in Species)
                if (sp.name == species && sp.caps != null && role < sp.caps.Length) return sp.caps[role];
            return (int)T("MaxSkill", 250f);
        }

        public static LoreRow LoreEntry(string id)
        {
            foreach (var l in Lore) if (l.id == id) return l;
            return null;
        }

        public static UpgradeRow Upgrade(string id)
        {
            foreach (var u in Upgrades) if (u.id == id) return u;
            return null;
        }

        public static ShipSystemRow ShipSystem(string system, string stat)
        {
            foreach (var s in ShipSystems) if (s.system == system && s.stat == stat) return s;
            return null;
        }

        public static DeadlineStageRow StageForDay(int day)
        {
            DeadlineStageRow current = Deadline.Length > 0 ? Deadline[0] : null;
            foreach (var s in Deadline) if (day >= s.baseStartDay) current = s;
            return current;
        }
    }
}
