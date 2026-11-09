using System.Collections.Generic;
using UnityEngine;

namespace FBTS
{
    /// <summary>Combat formulas, matching the spreadsheet's Combat calculator.</summary>
    public static class CombatRules
    {
        static float MaxSkill => GameData.T("MaxSkill", 250f);

        public static float Accuracy(float weaponsSkill) => weaponsSkill / MaxSkill * Mathf.Abs(RoleMax("Weapons Officer", 0.3f));
        public static float DamageBonus(float weaponsSkill) => weaponsSkill / MaxSkill * 0.2f;
        public static float Evasion(float pilotSkill) => pilotSkill / MaxSkill * Mathf.Abs(RoleMax("Pilot", 0.3f));

        static float RoleMax(string role, float fallback)
        {
            foreach (var r in GameData.CrewRoles) if (r.role == role) return r.maxBonusAtSkill250;
            return fallback;
        }

        /// <summary>BaseHit + attacker accuracy - defender evasion, clamped to MinHit..MaxHit.</summary>
        public static float HitChance(float attackerWeaponsSkill, float defenderPilotSkill)
        {
            float h = GameData.T("BaseHit", 0.7f) + Accuracy(attackerWeaponsSkill) - Evasion(defenderPilotSkill);
            return Mathf.Clamp(h, GameData.T("MinHit", 0.05f), GameData.T("MaxHit", 0.95f));
        }

        public static float DamagePerShot(WeaponRow w, float weaponsSkill) => w.damagePerShot * (1f + DamageBonus(weaponsSkill));

        /// <summary>Shields absorb first (beams hit shields 50% harder). Whatever gets through hits the hull,
        /// reduced by armor (kinetic weapons ignore armor), and every hull hit does at least 1 damage.</summary>
        public static void ApplyHit(Combatant target, WeaponRow w, float damage)
        {
            bool beam = w.weapon == "Beam laser";
            bool kinetic = w.family == "Kinetic";
            if (target.shields > 0f)
            {
                float vsShields = damage * (beam ? 1.5f : 1f);
                if (vsShields <= target.shields) { target.shields -= vsShields; return; }
                // Overflow carries through to the hull at the weapon's normal strength.
                damage *= 1f - target.shields / vsShields;
                target.shields = 0f;
            }
            float toHull = kinetic ? damage : Mathf.Max(1f, damage - target.armor);
            target.hull = Mathf.Max(0f, target.hull - toHull);
        }

        /// <summary>Expected hull-or-shield damage per second from a set of weapons against a defender,
        /// after hit chance and armor (kinetic weapons ignore armor).</summary>
        public static float Dps(IEnumerable<string> weapons, float weaponsSkill, float defenderPilot, float defenderArmor)
        {
            float dps = 0f;
            foreach (var n in weapons)
            {
                var w = GameData.Weapon(n);
                if (w == null) continue;
                float dmg = DamagePerShot(w, weaponsSkill);
                if (w.family != "Kinetic") dmg = Mathf.Max(1f, dmg - defenderArmor);
                dps += dmg * w.shotsPerSecond;
            }
            return dps * HitChance(weaponsSkill, defenderPilot);
        }

        /// <summary>Who would win a stand-up fight, as a percentage (100 = even, 200 = we're twice as strong).
        /// Durability x damage on each side, with shield recharge eating into incoming damage. Totalled over
        /// every ship, so numbers count twice, as they do in a real fight.</summary>
        public static int Odds(float ourDurability, float ourDps, float ourRecharge,
                               float theirDurability, float theirDps, float theirRecharge)
        {
            float ours = Mathf.Max(ourDps * 0.1f, ourDps - theirRecharge);
            float theirs = Mathf.Max(theirDps * 0.1f, theirDps - ourRecharge);
            return Mathf.RoundToInt(100f * ourDurability * ours / Mathf.Max(1f, theirDurability * theirs));
        }
    }

    /// <summary>A weapon fitted to a ship in combat, with its reload timer.</summary>
    public class Mount
    {
        public WeaponRow row;
        public float cooldown;
        public float Range => row.range * GameData.T("RangeScale", 0.01f);
    }

    /// <summary>One ship in a fight: the player's, or one of an encounter's.</summary>
    public class Combatant
    {
        public string name;
        public bool isPlayer;
        public EncounterRow row;          // null for the player
        public float hull, hullMax, shields, shieldMax, recharge, armor;
        public float speed, pilotSkill, weaponsSkill;
        public readonly List<Mount> mounts = new List<Mount>();
        public Vector2 pos, vel;
        public bool destroyed, escaped, fleeing;
        public float escapeTimer;         // seconds spent beyond the arena edge; escape at EscapeTime
        public Transform body;
        public SpriteRenderer bubble;
        public ShipRig rig;               // painted ships: aims the turrets (null for code-drawn ships)
        public float hitFlash;

        public bool Active => !destroyed && !escaped;

        public float MaxRange
        {
            get { float r = 0f; foreach (var m in mounts) r = Mathf.Max(r, m.Range); return r; }
        }

        public void Fit(IEnumerable<string> weaponNames)
        {
            foreach (var n in weaponNames)
            {
                var w = GameData.Weapon(n);
                if (w != null) mounts.Add(new Mount { row = w, cooldown = Random.Range(0.2f, 1f) / Mathf.Max(0.05f, w.shotsPerSecond) });
            }
        }
    }

    /// <summary>Ships met in a system. Lives in the system view until it leaves, is fought, or we jump away.</summary>
    public class Encounter
    {
        public EncounterRow row;
        public bool hostile;
        public bool provoked;      // we fired first
        public bool hailed;        // they have already spoken to us (or tried)
        public bool leaving;
        public Vector2 pos;
        public Transform marker;

        public string Race => row.race;
        public bool CanTalk => Conversation.HasKnot(row.race, "encounter");

        /// <summary>Extra encounter chance as the threat clock advances (0 at stage 1).</summary>
        public static float StageBonus(GameState s) =>
            GameData.T("EncounterStageBonus", 0.05f) * ((s.Stage != null ? s.Stage.stage : 1) - 1);

        /// <summary>Chance of meeting ships on arriving in a system by jump.</summary>
        public static float ArrivalChance(GameState s) => GameData.T("EncounterChance", 0.4f) + StageBonus(s);

        /// <summary>Chance of being intercepted at a waypoint partway through a chained course.</summary>
        public static float WaypointChance(GameState s) => GameData.T("EncounterWaypointChance", 0.2f) + StageBonus(s);

        /// <summary>Roll for an encounter in a system with the given chance. Returns null if nobody is there.</summary>
        public static Encounter Roll(GameState s, StarSystem sys, System.Random rng, float chance)
        {
            int stage = s.Stage != null ? s.Stage.stage : 1;
            if (rng.NextDouble() >= chance) return null;

            bool home = sys == s.galaxy.Home;
            bool rimward = Vector2.Distance(sys.pos, s.galaxy.Home.pos) >
                           0.6f * Vector2.Distance(WorldEvents.RimSystem(s).pos, s.galaxy.Home.pos);

            var pool = new List<EncounterRow>();
            float total = 0f;
            foreach (var e in GameData.Encounters)
            {
                if (stage < e.minStage || e.weight <= 0f) continue;
                bool ok;
                switch (e.behavior)
                {
                    case "Trader": ok = true; break;                                  // Orrun traders go everywhere
                    case "Raider": ok = !home; break;                                 // scavengers roam, but not at Haven
                    case "Drone": ok = !home && rimward; break;                       // Mother's ships, from the rim inward
                    default: ok = !home && sys.territory == e.race; break;            // patrols guard their own space
                }
                if (!ok) continue;
                pool.Add(e);
                total += e.weight;
            }
            if (pool.Count == 0) return null;

            double pick = rng.NextDouble() * total;
            EncounterRow row = pool[pool.Count - 1];
            foreach (var e in pool) { pick -= e.weight; if (pick <= 0) { row = e; break; } }

            return Create(s, row);
        }

        /// <summary>Ships of a given kind, with their starting attitude toward us.</summary>
        public static Encounter Create(GameState s, EncounterRow row)
        {
            var enc = new Encounter { row = row };
            switch (row.behavior)
            {
                case "Trader": enc.hostile = false; break;
                case "Patrol": enc.hostile = s.Rep(row.race) < 0; break;
                default: enc.hostile = true; break;
            }
            return enc;
        }

        /// <summary>Who would win a stand-up fight right now (100 = even). Dialogue sees this as "odds".</summary>
        public int Odds(GameState s)
        {
            int n = Mathf.Max(1, row.count);
            float ourDps = CombatRules.Dps(s.weapons, s.weaponsSkill, row.pilotSkill, row.armor);
            float theirDps = CombatRules.Dps(row.WeaponList, row.weaponsSkill, s.pilotSkill, s.Armor) * n;
            return CombatRules.Odds(s.Hull + s.ShieldCapacity, ourDps, s.ShieldRecharge,
                                    (row.hullPoints + row.shields) * n, theirDps, row.shieldRecharge);
        }
    }
}
