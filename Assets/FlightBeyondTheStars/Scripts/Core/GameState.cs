using System.Collections.Generic;
using UnityEngine;

namespace FBTS
{
    /// <summary>Everything about the current run: ship, crew skills, money, cargo, time and position.</summary>
    public class GameState
    {
        public int seed;
        public Galaxy galaxy;
        public StarSystem current;
        public HullRow hull;
        public float credits;
        public float fuel;
        public int day;
        public readonly Dictionary<string, int> cargo = new Dictionary<string, int>();

        // Stage 1 prototype: one all-human starting crew with fixed skills (crew system comes in stage 3).
        public int navigatorSkill = 60;
        public int pilotSkill = 60;
        public int scienceSkill = 60;
        public int commsSkill = 60;

        public int terrainVehicleTier = 1;

        // Lore fragments found in ruins, in the order found (the captain's log)
        public readonly List<string> loreFound = new List<string>();

        // Modules bought at starports (ids from the spreadsheet's Upgrades tab)
        public readonly HashSet<string> upgrades = new HashSet<string>();

        public bool Has(string upgradeId) => upgrades.Contains(upgradeId);

        /// <summary>Sum of the amounts of owned modules with this effect code.</summary>
        public float UpgradeBonus(string effect)
        {
            float total = 0f;
            foreach (var id in upgrades)
            {
                var u = GameData.Upgrade(id);
                if (u != null && u.effect == effect) total += u.amount;
            }
            return total;
        }

        public float FuelTank => (hull != null ? hull.fuelTank : 40f) + UpgradeBonus("FuelTank");
        public float CargoCapacity => (hull != null ? hull.cargoUnits : 50f) + UpgradeBonus("Cargo");
        public float RoverRevealRadius => GameData.T("RoverRevealRadius", 4f) + UpgradeBonus("RevealRadius");
        public bool LanderAutopilot => Has("LanderAutopilot");

        public float CargoUsed
        {
            get
            {
                float used = 0f;
                foreach (var kv in cargo)
                {
                    var c = GameData.Commodity(kv.Key);
                    used += kv.Value * (c != null ? c.cargoUnitsPerUnit : 1f);
                }
                return used;
            }
        }

        public int AddCargo(string commodity, int amount)
        {
            var c = GameData.Commodity(commodity);
            float per = c != null ? c.cargoUnitsPerUnit : 1f;
            int fits = per <= 0f ? amount : Mathf.Min(amount, Mathf.FloorToInt((CargoCapacity - CargoUsed) / per));
            if (fits <= 0) return 0;
            cargo.TryGetValue(commodity, out int have);
            cargo[commodity] = have + fits;
            return fits;
        }

        public void RemoveCargo(string commodity, int amount)
        {
            if (!cargo.TryGetValue(commodity, out int have)) return;
            have -= amount;
            if (have <= 0) cargo.Remove(commodity); else cargo[commodity] = have;
        }

        public DeadlineStageRow Stage => GameData.StageForDay(day);

        public static GameState NewGame(int seed)
        {
            GameData.Load();
            var s = new GameState { seed = seed };
            s.galaxy = Galaxy.Generate(seed, 12, 32f);
            s.current = s.galaxy.Home;
            s.hull = GameData.Hull("Light survey frigate");
            s.credits = GameData.T("StartCredits", 10000f);
            s.fuel = s.FuelTank;
            return s;
        }
    }

    /// <summary>Travel, fuel and pricing formulas, matching the spreadsheet's calculators.</summary>
    public static class Rules
    {
        public const float JumpLyPerDay = 2f;       // open route
        public const float FreeFlightLyPerDay = 0.5f;

        public static float JumpFuel(GameState s, StarSystem from, StarSystem to)
        {
            float dist = Vector2.Distance(from.pos, to.pos);
            float hullMult = s.hull != null ? s.hull.jumpFuelMultiplier : 1f;
            float navBonus = GameData.RoleBonus("Navigator", s.navigatorSkill); // negative = saves fuel
            float route = s.galaxy.HasRoute(from, to) ? 1f : GameData.T("FreeFlightMult", 3f);
            return dist * GameData.T("FuelPerLy", 1f) * hullMult * (1f + navBonus) * route;
        }

        public static int TravelDays(GameState s, StarSystem from, StarSystem to)
        {
            float dist = Vector2.Distance(from.pos, to.pos);
            float speed = s.galaxy.HasRoute(from, to) ? JumpLyPerDay : FreeFlightLyPerDay;
            return Mathf.Max(1, Mathf.CeilToInt(dist / speed));
        }

        /// <summary>Deterministic market swing for a commodity at a system, within its volatility.</summary>
        static float MarketSwing(GameState s, StarSystem sys, CommodityRow c)
        {
            int h = (s.seed * 31 + sys.id * 977 + c.commodity.GetHashCode()) & 0x7fffffff;
            float t = (h % 1000) / 999f; // 0..1
            return 1f + (t - 0.5f) * c.volatility;
        }

        public static float SellPrice(GameState s, StarSystem sys, string commodity)
        {
            var c = GameData.Commodity(commodity);
            if (c == null || sys.starportRace == null) return 0f;
            float race = GameData.RaceMultiplier(sys.starportRace, c.category);
            float comms = GameData.RoleBonus("Communications Officer", s.commsSkill);
            return c.basePrice * race * MarketSwing(s, sys, c) * (1f + comms);
        }

        public static float FuelPrice(GameState s, StarSystem sys)
        {
            var c = GameData.Commodity("Drive crystal fuel");
            float basePrice = c != null ? c.basePrice : GameData.T("FuelBasePrice", 60f);
            float swing = c != null ? MarketSwing(s, sys, c) : 1f;
            float comms = GameData.RoleBonus("Communications Officer", s.commsSkill);
            float stage = s.Stage != null ? s.Stage.fuelPriceMultiplier : 1f;
            return basePrice * swing * stage * (1f - comms);
        }

        public static float LandingFuel(Planet p) => Mathf.Ceil(p.gravity * 2f);
    }
}
