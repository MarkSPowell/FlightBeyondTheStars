using System.Collections.Generic;
using UnityEngine;

namespace FBTS
{
    /// <summary>
    /// Real-time, top-down ship combat that can be paused at any time.
    /// The captain flies the ship and picks targets; the Weapons Officer fires automatically at the target
    /// whenever a weapon is in range and loaded (unless told to hold fire). Crew skill decides hits:
    /// Weapons skill for accuracy and damage, Pilot skill for evasion. There is no arena edge: a ship gets away
    /// by opening a big enough gap from its pursuers and holding it, or (for us) by an emergency jump.
    /// </summary>
    public class CombatView : View
    {
        readonly Encounter enc;
        readonly Vector2 returnPos;          // where the ship goes back to in the system view

        Combatant me;
        readonly List<Combatant> enemies = new List<Combatant>();
        Combatant target;
        bool paused;
        bool weaponsFree = true;
        bool over;
        readonly List<Transform> stars = new List<Transform>();
        const float StarField = 70f;          // stars wrap around the camera in a square this size
        float jumpSpool = -1f;                // seconds left on an emergency jump; -1 = not spooling
        float elapsed;

        class Shot
        {
            public Combatant from, to;
            public WeaponRow weapon;
            public float damage;
            public bool hit;
            public Vector2 pos, missDir;
            public float speed, life;
            public SpriteRenderer sprite;
        }
        readonly List<Shot> shots = new List<Shot>();

        class Flash { public SpriteRenderer sprite; public float life, max; }

        readonly List<string> injuries = new List<string>();
        bool lost;   // the whole crew died: the game manager starts over, so don't go anywhere
        readonly List<Flash> flashes = new List<Flash>();

        public CombatView(Encounter encounter, Vector2 returnTo)
        {
            enc = encounter;
            returnPos = returnTo;
        }

        static Color RaceColor(string race)
        {
            switch (race)
            {
                case "Orrun Combine": return new Color(1f, 0.8f, 0.35f);
                case "Varkesh": return new Color(1f, 0.4f, 0.3f);
                case "Skarrow": return new Color(0.7f, 0.9f, 0.4f);
                case "Mother": return new Color(0.55f, 0.55f, 0.6f);
                default: return new Color(0.9f, 0.6f, 0.9f);
            }
        }

        public override void Enter()
        {
            Cam.orthographicSize = 12f;

            // Backdrop: open space. The stars wrap around the camera, so the fight can range anywhere.
            var rng = new System.Random(S.day * 31 + enc.row.id.GetHashCode());
            for (int i = 0; i < 260; i++)
            {
                var st = Draw.Dot("Star", root, new Vector2((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f) * StarField,
                                  0.06f + (float)rng.NextDouble() * 0.12f, new Color(1f, 1f, 1f, 0.25f + 0.35f * (float)rng.NextDouble()), 0);
                stars.Add(st.transform);
            }

            // Our ship
            me = new Combatant
            {
                name = "Your ship",
                isPlayer = true,
                hull = S.Hull,
                hullMax = S.HullMax,
                shieldMax = S.ShieldCapacity,
                recharge = S.ShieldRecharge * (1f + S.Trait("ShieldRecharge")),
                armor = S.Armor,
                speed = (S.hull != null ? S.hull.speed : 100f) / 100f * 5f * (1f + 0.15f * S.pilotSkill / GameData.T("MaxSkill", 250f))
                        * (1f + S.Trait("CombatSpeed")),
                pilotSkill = S.pilotSkill,
                weaponsSkill = S.weaponsSkill,
                pos = new Vector2(0f, -6f)
            };
            me.shields = me.shieldMax;
            me.Fit(S.weapons);
            Build(me, new Color(0.4f, 0.9f, 1f), 0.8f);

            // Theirs
            var r = enc.row;
            int n = Mathf.Max(1, r.count);
            for (int i = 0; i < n; i++)
            {
                var c = new Combatant
                {
                    name = n > 1 ? $"{r.name} {i + 1}" : r.name,
                    row = r,
                    hull = r.hullPoints,
                    hullMax = r.hullPoints,
                    shields = r.shields,
                    shieldMax = r.shields,
                    recharge = r.shieldRecharge,
                    armor = r.armor,
                    speed = r.speed / 100f * 5f,
                    pilotSkill = r.pilotSkill,
                    weaponsSkill = r.weaponsSkill,
                    pos = new Vector2((i - (n - 1) / 2f) * 3f, 6f)
                };
                c.Fit(r.WeaponList);
                Build(c, RaceColor(r.race), 0.7f);
                enemies.Add(c);
            }
            target = enemies[0];
            gm.Follow(Vector2.zero, 0f);
            gm.Say(enc.provoked ? "Weapons free. You fired first." : $"Battle stations! {r.name} engaging.");
        }

        void Build(Combatant c, Color color, float size)
        {
            string kind = c.isPlayer ? "player" : Art.ShipKind(c.row.race);
            c.body = Art.Ship(kind, c.name, root, color, size * 2f);
            c.body.localPosition = c.pos;
            c.rig = c.body.GetComponent<ShipRig>();
            c.bubble = Draw.Dot("Shield", c.body, Vector2.zero, size * 2.6f, new Color(0.4f, 0.7f, 1f, 0.25f), 8);
        }

        // ---------------------------------------------------------------- Update

        public override void Tick(float dt)
        {
            if (over) return;

            // Orders work while paused.
            if (Controls.Pause()) paused = !paused;
            if (Controls.NextTarget()) CycleTarget();
            if (Controls.Attack()) weaponsFree = !weaponsFree;
            if (Controls.Hail()) HailThem();
            if (Controls.EmergencyJump()) ToggleJump();
            if (Controls.Clicked(out var screen) && !OverPanel(screen)) ClickTarget(screen);
            if (paused) return;

            elapsed += dt;
            Crew.Gain(S, Roles.Pilot, GameData.T("XpPilotSecond", 0.08f) * dt);
            Crew.Gain(S, Roles.Engineer, GameData.T("XpEngineerSecond", 0.05f) * dt);
            Pilot(dt);
            if (over) return;
            foreach (var e in enemies) if (e.Active) Think(e, dt);

            foreach (var c in All())
            {
                if (!c.Active) continue;
                c.shields = Mathf.Min(c.shieldMax, c.shields + c.recharge * dt);
                Fire(c, c.isPlayer ? (weaponsFree ? target : null) : me, dt);
                if (c.rig != null)
                {
                    var aimAt = c.isPlayer ? target : me;
                    c.rig.aim = aimAt != null && aimAt.Active ? aimAt.pos : (Vector2?)null;
                }
                c.body.localPosition = c.pos;
                if (c.vel.sqrMagnitude > 0.01f)
                    c.body.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(c.vel.y, c.vel.x) * Mathf.Rad2Deg);
                c.hitFlash = Mathf.Max(0f, c.hitFlash - dt * 3f);
                Fx.SetAlpha(c.bubble, c.shieldMax > 0 ? 0.08f + 0.3f * c.shields / c.shieldMax + c.hitFlash * 0.4f : 0f);
            }
            UpdateShots(dt);
            UpdateFlashes(dt);

            // Keep the fight in frame: centre between us and our target.
            Vector2 focus = target != null && target.Active ? Vector2.Lerp(me.pos, target.pos, 0.5f) : me.pos;
            gm.Follow(focus, 3f);
            float spread = target != null && target.Active ? Vector2.Distance(me.pos, target.pos) : 0f;
            Cam.orthographicSize = Mathf.Lerp(Cam.orthographicSize, Mathf.Clamp(spread * 0.7f + 6f, 9f, 18f), 1f - Mathf.Exp(-2f * dt));
            WrapStars();

            CheckEnd();
        }

        void WrapStars()
        {
            Vector2 c = Cam.transform.position;
            float h = StarField / 2f;
            foreach (var st in stars)
            {
                Vector2 p = st.localPosition;
                if (p.x - c.x > h) p.x -= StarField; else if (c.x - p.x > h) p.x += StarField;
                if (p.y - c.y > h) p.y -= StarField; else if (c.y - p.y > h) p.y += StarField;
                st.localPosition = p;
            }
        }

        IEnumerable<Combatant> All()
        {
            yield return me;
            foreach (var e in enemies) yield return e;
        }

        void Pilot(float dt)
        {
            Vector2 input = Controls.Move();
            me.vel = Vector2.Lerp(me.vel, input * me.speed, 1f - Mathf.Exp(-4f * dt));
            me.pos += me.vel * dt;
            // We only "escape" from ships that are chasing us; if they're all running, they're the ones escaping.
            float gap = NearestPursuer();
            if (gap < float.MaxValue && Escaping(me, gap, dt)) { Flee(); return; }

            if (jumpSpool >= 0f)
            {
                jumpSpool -= dt;
                if (jumpSpool <= 0f) EmergencyJump();
            }
        }

        float EscapeDistance => GameData.T("EscapeDistance", 30f);
        float EscapeTime => GameData.T("EscapeTime", 3f);

        /// <summary>Distance from us to the closest enemy still fighting (not running away); infinity if none.</summary>
        float NearestPursuer()
        {
            float best = float.MaxValue;
            foreach (var e in enemies) if (e.Active && !e.fleeing) best = Mathf.Min(best, Vector2.Distance(me.pos, e.pos));
            return best;
        }

        /// <summary>A ship breaks away once the gap to its pursuers stays above EscapeDistance for EscapeTime
        /// seconds. Closing the gap resets the clock, so a faster ship can always run a slower one down.</summary>
        bool Escaping(Combatant c, float gap, float dt)
        {
            if (gap <= EscapeDistance) { c.escapeTimer = 0f; return false; }
            c.escapeTimer += dt;
            return c.escapeTimer >= EscapeTime;
        }

        float JumpSpoolTime => GameData.T("EmergencyJumpTime", 8f) * (1f - 0.5f * S.navigatorSkill / GameData.T("MaxSkill", 250f))
                                 * (1f - S.Trait("EmergencyJump"));
        float JumpFuel => GameData.T("EmergencyJumpFuel", 5f);

        /// <summary>The Navigator spools an emergency jump: we keep fighting (and taking fire) until it's ready.
        /// Fleeing is always possible this way, even from faster ships, at a price in fuel.</summary>
        void ToggleJump()
        {
            if (jumpSpool >= 0f) { jumpSpool = -1f; gm.Toast("Emergency jump cancelled", 1.5f); return; }
            if (S.fuel < JumpFuel) { gm.Say($"Not enough fuel for an emergency jump (needs {JumpFuel:0})."); return; }
            jumpSpool = JumpSpoolTime;
            gm.Toast("Navigator: spooling emergency jump!", 2f);
        }

        /// <summary>Enemy behaviour by type: hold at weapon range and circle; raiders and traders run.</summary>
        void Think(Combatant e, float dt)
        {
            string behavior = e.row.behavior;
            if (behavior == "Raider" && e.hull < e.hullMax * 0.4f) e.fleeing = true;
            if (behavior == "Trader") e.fleeing = true;

            Vector2 toMe = me.pos - e.pos;
            float dist = toMe.magnitude;
            Vector2 dir = dist > 0.01f ? toMe / dist : Vector2.up;
            Vector2 desired;
            if (e.fleeing)
            {
                // Run directly away, jinking a little.
                Vector2 jink = new Vector2(-dir.y, dir.x) * Mathf.Sin(elapsed * 1.3f + e.GetHashCode()) * 0.35f;
                desired = (-dir + jink).normalized * e.speed;
            }
            else
            {
                float hold = Mathf.Max(2f, e.MaxRange * (behavior == "Patrol" ? 0.45f : 0.65f));
                float radial = Mathf.Clamp((dist - hold) / 3f, -1f, 1f);
                Vector2 tangent = new Vector2(-dir.y, dir.x) * (e.GetHashCode() % 2 == 0 ? 1f : -1f);
                desired = (dir * radial + tangent * 0.7f).normalized * e.speed;
            }
            e.vel = Vector2.Lerp(e.vel, desired, 1f - Mathf.Exp(-2f * dt));
            e.pos += e.vel * dt;

            if (Escaping(e, dist, dt))
            {
                e.escaped = true;
                e.body.gameObject.SetActive(false);
                gm.Toast($"{e.name} escaped", 1.5f);
                if (target == e) CycleTarget();
            }
        }

        void Fire(Combatant c, Combatant at, float dt)
        {
            foreach (var m in c.mounts)
            {
                m.cooldown -= dt;
                if (at == null || !at.Active || m.cooldown > 0f) continue;
                if (Vector2.Distance(c.pos, at.pos) > m.Range) continue;
                // Our traits: Gunnery Drills (reload), Sharpshooter (hit), Heavy Hitter (damage); theirs vs our Evasive Flyer.
                m.cooldown = 1f / Mathf.Max(0.05f, m.row.shotsPerSecond) / (c.isPlayer ? 1f + S.Trait("FireRate") : 1f);
                float chance = CombatRules.HitChance(c.weaponsSkill, at.pilotSkill)
                               + (c.isPlayer ? S.Trait("HitChance") : 0f) - (at.isPlayer ? S.Trait("Evasion") : 0f);
                bool hit = Random.value < Mathf.Clamp(chance, GameData.T("MinHit", 0.05f), GameData.T("MaxHit", 0.95f));
                float dmg = CombatRules.DamagePerShot(m.row, c.weaponsSkill) * (c.isPlayer ? 1f + S.Trait("Damage") : 1f);
                if (c.isPlayer)
                    Crew.Gain(S, Roles.Weapons, GameData.T("XpWeaponsShot", 0.08f) + (hit ? GameData.T("XpWeaponsHit", 0.04f) : 0f));
                if (m.row.family == "Energy" || m.row.family == "Exotic")
                {
                    // Lasers land instantly
                    Vector2 end = hit ? at.pos : at.pos + Random.insideUnitCircle.normalized * 1.2f;
                    var col = c.isPlayer ? new Color(0.5f, 1f, 1f, 0.9f) : new Color(1f, 0.5f, 0.4f, 0.9f);
                    AddFlash(Draw.Line("Beam", root, c.pos, end, m.row.weapon == "Beam laser" ? 0.14f : 0.07f, col, 12), m.row.weapon == "Beam laser" ? 0.3f : 0.1f);
                    if (hit) Damage(at, m.row, dmg);
                }
                else
                {
                    // Slugs and missiles travel
                    float speed = m.row.family == "Missile" ? 7f : 22f;
                    var shot = new Shot
                    {
                        from = c, to = at, weapon = m.row, damage = dmg, hit = hit, pos = c.pos, speed = speed,
                        life = m.Range / speed + 1.5f,
                        missDir = Random.insideUnitCircle.normalized,
                        sprite = Draw.Dot("Shot", root, c.pos, m.row.family == "Missile" ? 0.3f : 0.16f,
                                          m.row.family == "Missile" ? new Color(1f, 0.85f, 0.3f) : new Color(0.9f, 0.9f, 0.8f), 13)
                    };
                    shots.Add(shot);
                }
            }
        }

        void UpdateShots(float dt)
        {
            for (int i = shots.Count - 1; i >= 0; i--)
            {
                var s = shots[i];
                s.life -= dt;
                Vector2 aim = s.to.pos + (s.hit ? Vector2.zero : s.missDir * 1.5f);
                Vector2 step = aim - s.pos;
                float move = s.speed * dt;
                bool arrived = step.magnitude <= move;
                s.pos = arrived ? aim : s.pos + step.normalized * move;
                s.sprite.transform.localPosition = s.pos;
                if (arrived && s.hit && s.to.Active) Damage(s.to, s.weapon, s.damage);
                if (arrived || s.life <= 0f || !s.to.Active)
                {
                    Object.Destroy(s.sprite.gameObject);
                    shots.RemoveAt(i);
                }
            }
        }

        void Damage(Combatant c, WeaponRow w, float dmg)
        {
            float before = c.hull;
            CombatRules.ApplyHit(c, w, dmg);
            if (c.isPlayer)
            {
                // Hits that get through to the hull can injure the crew.
                string hurt = Crew.InjureFromHit(S, before - c.hull);
                if (hurt != null) { gm.Toast(hurt, 2.5f); injuries.Add(hurt); }
            }
            c.hitFlash = 1f;
            if (c.hull > 0f || c.destroyed) return;
            c.destroyed = true;
            AddFlash(Draw.Dot("Explosion", root, c.pos, 3f, new Color(1f, 0.7f, 0.3f, 0.9f), 14), 0.6f);
            c.body.gameObject.SetActive(false);
            if (!c.isPlayer)
            {
                gm.Toast($"{c.name} destroyed", 1.5f);
                if (target == c) CycleTarget();
            }
        }

        void AddFlash(SpriteRenderer sr, float life) => flashes.Add(new Flash { sprite = sr, life = life, max = life });

        void UpdateFlashes(float dt)
        {
            for (int i = flashes.Count - 1; i >= 0; i--)
            {
                var f = flashes[i];
                f.life -= dt;
                if (f.life <= 0f) { Object.Destroy(f.sprite.gameObject); flashes.RemoveAt(i); continue; }
                Fx.SetAlpha(f.sprite, 0.9f * f.life / f.max);
            }
        }

        // ---------------------------------------------------------------- Orders

        void CycleTarget()
        {
            var live = enemies.FindAll(e => e.Active);
            if (live.Count == 0) { target = null; return; }
            int i = live.IndexOf(target);
            target = live[(i + 1) % live.Count];
        }

        void ClickTarget(Vector2 screen)
        {
            Vector2 world = Cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 10f));
            foreach (var e in enemies)
                if (e.Active && Vector2.Distance(world, e.pos) < 1.5f) { target = e; return; }
        }

        void HailThem()
        {
            if (!enc.CanTalk) { gm.Say("No response. Only a carrier tone."); return; }
            var vars = new Dictionary<string, object> { { "in_combat", true }, { "odds", OddsNow() } };
            gm.Hail(enc.Race, "encounter", enc.row.name, vars,
                onAttack: () => enc.hostile = true,
                onStandDown: () => enc.hostile = false,
                onClose: () => { if (!enc.hostile) BreakOff(); });
        }

        /// <summary>Who would win from here (100 = even); damage taken so far counts.</summary>
        int OddsNow()
        {
            float theirDur = 0f, theirDps = 0f, ourDps = 0f, theirRecharge = 0f;
            foreach (var e in enemies)
            {
                if (!e.Active) continue;
                theirDur += e.hull + e.shields;
                theirDps += CombatRules.Dps(e.row.WeaponList, e.weaponsSkill, me.pilotSkill, me.armor);
                ourDps = Mathf.Max(ourDps, CombatRules.Dps(S.weapons, me.weaponsSkill, e.pilotSkill, e.armor));
                theirRecharge = Mathf.Max(theirRecharge, e.recharge);
            }
            return CombatRules.Odds(me.hull + me.shields, ourDps, me.recharge, theirDur, theirDps, theirRecharge);
        }

        // ---------------------------------------------------------------- Endings

        void CheckEnd()
        {
            if (over) return;
            if (me.destroyed) { Defeat(); return; }
            foreach (var e in enemies) if (e.Active) return;
            Victory();
        }

        void Finish()
        {
            over = true;
            S.hullDamage = Mathf.Clamp(S.HullMax - me.hull, 0f, S.HullMax);
        }

        /// <summary>The crew's side of the fight: injuries, and the Medical Officer's fight for the critically
        /// wounded. Returns text for the result note (empty if nobody was hurt).</summary>
        string CrewAftermath()
        {
            var lines = new List<string>(injuries);
            lines.AddRange(Crew.ResolveCritical(S));
            return lines.Count > 0 ? "\n\nCrew:\n" + string.Join("\n", lines) : "";
        }

        /// <summary>Show the result, unless the whole crew is gone.</summary>
        void Report(string title, string body)
        {
            body += CrewAftermath();
            if (Crew.AllLost(S)) { lost = true; gm.GameOver(body); return; }
            gm.ShowNote(title, body);
        }

        void Victory()
        {
            Finish();
            var r = enc.row;
            int killed = 0;
            foreach (var e in enemies) if (e.destroyed) killed++;
            int escaped = enemies.Count - killed;
            bool routed = false;                      // they ran because we were beating them
            foreach (var e in enemies) if (e.escaped && e.hull < e.hullMax * 0.5f) routed = true;
            string title;
            var sb = new System.Text.StringBuilder();
            if (killed == 0)
            {
                title = routed ? "Driven off" : "They got away";
                sb.Append(routed
                    ? $"The {r.name} broke and ran before you could finish them. No salvage, but they won't be back today.\n"
                    : $"The {r.name} outran you. No salvage.\n");
            }
            else
            {
                title = escaped == 0 ? "Victory" : "Victory (some escaped)";
                float credits = 0f;
                int goods = 0, kept = 0;
                for (int i = 0; i < killed; i++)
                {
                    credits += r.lootCredits * Random.Range(0.75f, 1.25f);
                    if (!string.IsNullOrEmpty(r.lootGoods)) goods += Random.Range(1, 4);
                }
                S.credits += Mathf.Round(credits);
                if (goods > 0) kept = S.AddCargo(r.lootGoods, goods);
                sb.Append($"Destroyed {killed} of {enemies.Count}" + (escaped > 0 ? $"; {escaped} got away" : "") + $". Salvage: {credits:N0} cr");
                if (goods > 0) sb.Append($" and {kept} {r.lootGoods}" + (kept < goods ? $" ({goods - kept} left behind, hold full)" : ""));
                sb.Append(".\n");
                int rep = Mathf.RoundToInt(r.repOnKill * killed);
                if (rep != 0 && r.race != "Mother")
                {
                    S.AddRep(r.race, rep);
                    sb.Append(rep > 0
                        ? $"The {r.race} respect a fight well won (standing {rep:+0}).\n"
                        : $"The {r.race} will hear of this (standing {rep}).\n");
                }
            }
            sb.Append($"Hull {me.hull:0}/{me.hullMax:0}." + (me.hull < me.hullMax ? " Repairs are available at any starport." : ""));
            if (killed > 0 || routed) Crew.Morale(S, "FightWon");
            if (killed > 0) Crew.Morale(S, "Salvage");
            Report(title, sb.ToString());
            Leave();
        }

        void BreakOff()
        {
            if (over) return;
            Finish();
            Report("Ceasefire", $"The {enc.row.name} break off and leave.\nHull {me.hull:0}/{me.hullMax:0}.");
            Leave();
        }

        void Flee()
        {
            if (over) return;
            Finish();
            float fuel = GameData.T("FleeFuel", 2f);
            S.fuel = Mathf.Max(0f, S.fuel - fuel);
            if (enc.row.race == "Varkesh" && !enc.provoked) S.AddRep("Varkesh", -2);  // fleeing a fair fight is shameful to them
            Crew.Morale(S, "Fled");
            Report("Escaped", $"You broke away at full burn, burning {fuel:0} fuel.\nHull {me.hull:0}/{me.hullMax:0}.");
            Leave();
        }

        void EmergencyJump()
        {
            if (over) return;
            Finish();
            S.fuel = Mathf.Max(0f, S.fuel - JumpFuel);
            if (enc.row.race == "Varkesh" && !enc.provoked) S.AddRep("Varkesh", -2);
            Crew.Morale(S, "Fled");
            Report("Emergency jump", $"The Navigator threw the ship across the system, burning {JumpFuel:0} fuel.\nHull {me.hull:0}/{me.hullMax:0}.");
            gm.FadeColor = Color.white;
            gm.FadeAlpha = 1f;
            if (lost) return;
            Vector2 away = returnPos.sqrMagnitude > 0.01f ? -returnPos.normalized * Mathf.Max(6f, returnPos.magnitude) : Vector2.down * 6f;
            gm.Go(new SystemView(away));
            gm.FadeIn(0.8f);
        }

        /// <summary>Disabled: an Orrun salvage tug tows us to the nearest starport, for a price. Cargo is lost.</summary>
        void Defeat()
        {
            over = true;
            StarSystem port = null;
            float best = float.MaxValue;
            foreach (var sys in S.galaxy.systems)
            {
                if (sys.starportRace == null) continue;
                float d = Vector2.Distance(sys.pos, S.current.pos);
                if (d < best) { best = d; port = sys; }
            }
            if (port == null) port = S.galaxy.Home;
            float tow = GameData.T("TowCost", 2500f);
            int days = port == S.current ? 1 : Rules.TravelDays(S, S.current, port);
            S.credits -= tow;
            S.cargo.Clear();
            S.hullDamage = S.HullMax * 0.75f;
            S.AdvanceDays(days);
            S.current = port;
            port.visited = true;
            Crew.Morale(S, "FightLost");
            Report("Ship disabled",
                $"Your ship was crippled and drifting. {days} days later an Orrun salvage tug hauled you into {port.name}.\n" +
                $"Towing fee: {tow:N0} cr" + (S.credits < 0 ? " (you are now in debt)" : "") + ". Your cargo was \"recovered\" as salvage.\n" +
                "Hull at 25%. Repair at the starport before your next fight.");
            if (lost) return;
            gm.FadeColor = Color.black;
            gm.FadeAlpha = 1f;
            gm.Go(new SystemView(null));
            gm.FadeIn(1f);
        }

        void Leave()
        {
            if (lost) return;
            gm.FadeColor = Color.black;
            gm.FadeAlpha = 1f;
            gm.Go(new SystemView(returnPos));
            gm.FadeIn(0.8f);
        }

        // ---------------------------------------------------------------- GUI

        public override void WorldGui()
        {
            foreach (var c in All())
            {
                if (!c.Active) continue;
                string mark = c == target ? "[ " : "";
                string end = c == target ? " ]" : "";
                gm.WorldLabel(c.pos, $"{mark}{c.name}{end}  {100f * c.hull / c.hullMax:0}%", 18);
            }
            // Ships off-screen get a marker at the screen edge pointing their way.
            float sc = GameManager.UiScale;
            float sw = Screen.width / sc, sh = Screen.height / sc;
            float left = GameManager.PanelWidth + 30f;
            foreach (var e in enemies)
            {
                if (!e.Active) continue;
                Vector3 sp = Cam.WorldToScreenPoint(new Vector3(e.pos.x, e.pos.y, 0f));
                float x = sp.x / sc, y = sh - sp.y / sc;
                if (x >= left && x <= sw - 10f && y >= 50f && y <= sh - 70f) continue;
                var mk = new GUIStyle(gm.Label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                mk.normal.textColor = e == target ? new Color(1f, 0.9f, 0.4f) : new Color(1f, 0.55f, 0.45f);
                string arrow = x < left ? "<" : x > sw - 10f ? ">" : y < 50f ? "^" : "v";
                GUI.Label(new Rect(Mathf.Clamp(x, left, sw - 170f) - 0f, Mathf.Clamp(y, 50f, sh - 90f), 170f, 22f),
                          $"{arrow} {e.name} {Vector2.Distance(me.pos, e.pos):0}", mk);
            }

            // Getting away: show the gap to the nearest pursuer once it opens up, and the jump countdown.
            if (!over)
            {
                var ws = new GUIStyle(gm.Header) { alignment = TextAnchor.MiddleCenter, fontSize = 20 };
                ws.normal.textColor = new Color(1f, 0.75f, 0.35f);
                float gap = NearestPursuer();
                string msg = null;
                if (jumpSpool >= 0f) msg = $"Emergency jump in {jumpSpool:0.0}s  (J to cancel)";
                else if (me.escapeTimer > 0f) msg = $"Breaking away... {Mathf.Max(0f, EscapeTime - me.escapeTimer):0.0}s  (close in to stay and fight)";
                else if (gap < float.MaxValue && gap > EscapeDistance * 0.6f) msg = $"Pulling away: {gap:0} / {EscapeDistance:0}";
                if (msg != null) GUI.Label(new Rect(0, sh * 0.82f, sw, 30), msg, ws);
            }

            if (paused)
            {
                var st = new GUIStyle(gm.Header) { alignment = TextAnchor.MiddleCenter, fontSize = 26 };
                float s = GameManager.UiScale;
                GUI.Label(new Rect(0, Screen.height / s * 0.18f, Screen.width / s, 40), "PAUSED  -  P / Start to resume", st);
            }
        }

        void Bar(string label, float value, float max, Color color)
        {
            GUILayout.Label($"{label}: {value:0} / {max:0}", gm.Label);
            var r = GUILayoutUtility.GetRect(200f, 10f, GUILayout.ExpandWidth(true));
            var old = GUI.color;
            GUI.color = new Color(0.15f, 0.17f, 0.25f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = color;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(max > 0 ? value / max : 0f), r.height), Texture2D.whiteTexture);
            GUI.color = old;
        }

        public override void Gui()
        {
            GUILayout.Label(paused ? "Combat (paused)" : "Combat", gm.Header);
            GUILayout.Label($"Fly: WASD.  Target: Tab or click.  Hold/free fire: F.  Hail: H.  Pause: P.\nFlee: open a gap of {EscapeDistance:0} and hold it, or emergency jump (J).", gm.Hint);
            GUILayout.Space(6);

            Bar("Hull", me.hull, me.hullMax, new Color(0.9f, 0.5f, 0.3f));
            Bar("Shields", me.shields, me.shieldMax, new Color(0.4f, 0.7f, 1f));
            GUILayout.Space(6);

            GUILayout.Label(weaponsFree ? "Weapons free" : "Holding fire", gm.Header);
            foreach (var m in me.mounts)
            {
                string state;
                if (target == null || !target.Active) state = "no target";
                else if (Vector2.Distance(me.pos, target.pos) > m.Range) state = $"out of range ({m.Range:0.0})";
                else state = m.cooldown > 0f ? "reloading" : "ready";
                GUILayout.Label($"{m.row.weapon}: {state}", gm.Label);
            }
            if (gm.Button(weaponsFree ? "Hold fire (F)" : "Weapons free (F)")) weaponsFree = !weaponsFree;

            GUILayout.Space(8);
            if (target != null && target.Active)
            {
                GUILayout.Label("Target: " + target.name, gm.Header);
                Bar("Hull", target.hull, target.hullMax, new Color(0.9f, 0.5f, 0.3f));
                Bar("Shields", target.shields, target.shieldMax, new Color(0.4f, 0.7f, 1f));
                GUILayout.Label($"Hit chance {CombatRules.HitChance(me.weaponsSkill, target.pilotSkill):P0}" +
                                $"   Their hit chance {CombatRules.HitChance(target.weaponsSkill, me.pilotSkill):P0}", gm.Hint);
                if (target.escapeTimer > 0f)
                    GUILayout.Label($"Breaking away in {Mathf.Max(0f, EscapeTime - target.escapeTimer):0.0}s!", gm.Label);
                else if (target.fleeing)
                    GUILayout.Label($"Running! Gap {Vector2.Distance(me.pos, target.pos):0} / {EscapeDistance:0}. Stay close to stop them escaping.", gm.Hint);
            }

            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (gm.Button(paused ? "Resume (P)" : "Pause (P)")) paused = !paused;
            if (gm.Button("Hail (H)")) HailThem();
            GUILayout.EndHorizontal();
            if (gm.Button(jumpSpool >= 0f ? $"Cancel jump ({jumpSpool:0.0}s)" : $"Emergency jump (J): {JumpSpoolTime:0}s, {JumpFuel:0} fuel",
                          jumpSpool >= 0f || S.fuel >= JumpFuel)) ToggleJump();
        }
    }
}
