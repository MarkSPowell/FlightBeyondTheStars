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
        public int weaponsSkill = 60;

        // Combat: fitted weapons (names from the Weapons tab), shield generator tier, and hull damage
        // carried between fights until repaired at a starport.
        public readonly List<string> weapons = new List<string> { "Pulse laser", "Mass driver" };
        public int shieldTier = 1;
        public float hullDamage;

        public float HullMax => hull != null ? hull.hullPoints : 400f;
        public float Hull => Mathf.Max(0f, HullMax - hullDamage);
        public float Armor => hull != null ? hull.armor : 2f;
        public int WeaponSlots => Mathf.Max(1, Mathf.RoundToInt((hull != null ? hull.moduleSlots : 3f) - 1f));
        public float ShieldCapacity { get { var r = GameData.ShipSystem("Shields", "Capacity"); return r != null ? r.Value(shieldTier) : 200f; } }
        public float ShieldRecharge { get { var r = GameData.ShipSystem("Shields", "Recharge"); return r != null ? r.Value(shieldTier) : 10f; } }

        // Set by the galaxy map when we arrive by jump, so the system view rolls for an encounter once.
        public bool justArrived;
        // Ships that intercepted a chained course at a waypoint, waiting for the system view (not saved)
        public Encounter pendingEncounter;

        public int terrainVehicleTier = 1;

        // Lore fragments found in ruins, in the order found (the captain's log)
        public readonly List<string> loreFound = new List<string>();

        // Modules bought at starports (ids from the spreadsheet's Upgrades tab)
        public readonly HashSet<string> upgrades = new HashSet<string>();

        public bool Has(string upgradeId) => upgrades.Contains(upgradeId);

        // Diplomacy: each race's opinion of us (-100 to 100), topics we've learned in conversation,
        // and each race's Ink story state (what they remember of past conversations).
        public readonly Dictionary<string, int> reputation = new Dictionary<string, int>();
        public string posture = "friendly";   // the captain's last conversation posture; the next hail opens with it
        public readonly HashSet<string> keywords = new HashSet<string>();
        public readonly Dictionary<string, string> inkStates = new Dictionary<string, string>();

        // Significant universe events that have happened: id -> when and where (see WorldEvents).
        public readonly Dictionary<string, WorldEvent> events = new Dictionary<string, WorldEvent>();

        public int Rep(string race) => race != null && reputation.TryGetValue(race, out int r) ? r : 0;

        public int AddRep(string race, int delta)
        {
            if (race == null) return 0;
            int r = Mathf.Clamp(Rep(race) + delta, -100, 100);
            reputation[race] = r;
            return r;
        }

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
        // Travel speed in light-years per day (Tuning tab): jump routes are fast; free flight is slow.
        public static float JumpLyPerDay => GameData.T("RouteLyPerDay", 5f);
        public static float FreeFlightLyPerDay => GameData.T("FreeFlightLyPerDay", 1f);

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
            return c.basePrice * race * MarketSwing(s, sys, c) * (1f + comms) * RepFactor(s, sys);
        }

        public static float FuelPrice(GameState s, StarSystem sys)
        {
            var c = GameData.Commodity("Drive crystal fuel");
            float basePrice = c != null ? c.basePrice : GameData.T("FuelBasePrice", 60f);
            float swing = c != null ? MarketSwing(s, sys, c) : 1f;
            float comms = GameData.RoleBonus("Communications Officer", s.commsSkill);
            float stage = s.Stage != null ? s.Stage.fuelPriceMultiplier : 1f;
            return basePrice * swing * stage * (1f - comms) / RepFactor(s, sys);
        }

        /// <summary>How the starport race's opinion of us moves prices: above 1 = better deals.</summary>
        public static float RepFactor(GameState s, StarSystem sys)
        {
            if (sys.starportRace == null) return 1f;
            return Mathf.Max(0.5f, 1f + s.Rep(sys.starportRace) * GameData.T("RepPriceEffect", 0.002f));
        }

        public static float LandingFuel(Planet p) => Mathf.Ceil(p.gravity * 2f);

        /// <summary>One hop of a course: a jump along a route, or a free flight.</summary>
        public class Leg
        {
            public StarSystem from, to;
            public float fuel;
            public int days;
            public bool route;
        }

        public static Leg MakeLeg(GameState s, StarSystem from, StarSystem to) => new Leg
        {
            from = from,
            to = to,
            fuel = JumpFuel(s, from, to),
            days = TravelDays(s, from, to),
            route = s.galaxy.HasRoute(from, to)
        };

        /// <summary>The cheapest chain of route jumps from one system to another (by fuel), or null if
        /// the routes don't connect them. Dijkstra over the jump-route graph.</summary>
        public static List<Leg> PlotCourse(GameState s, StarSystem from, StarSystem to)
        {
            var sys = s.galaxy.systems;
            int n = sys.Count;
            var cost = new float[n];
            var prev = new int[n];
            var done = new bool[n];
            for (int i = 0; i < n; i++) { cost[i] = float.MaxValue; prev[i] = -1; }
            cost[from.id] = 0f;
            for (int step = 0; step < n; step++)
            {
                int u = -1;
                for (int i = 0; i < n; i++) if (!done[i] && cost[i] < float.MaxValue && (u < 0 || cost[i] < cost[u])) u = i;
                if (u < 0 || u == to.id) break;
                done[u] = true;
                foreach (int v in sys[u].links)
                {
                    float c = cost[u] + JumpFuel(s, sys[u], sys[v]);
                    if (c < cost[v]) { cost[v] = c; prev[v] = u; }
                }
            }
            if (cost[to.id] == float.MaxValue) return null;

            var legs = new List<Leg>();
            for (int v = to.id; prev[v] >= 0; v = prev[v]) legs.Insert(0, MakeLeg(s, sys[prev[v]], sys[v]));
            return legs;
        }
    }
}
