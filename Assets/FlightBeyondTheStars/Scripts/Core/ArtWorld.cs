using System.Collections.Generic;
using UnityEngine;

namespace FBTS
{
    /// <summary>
    /// Painted art for planet surfaces and starports: textured ground (FBTSGround shader), the lander,
    /// minerals, life, ruins and scattered rocks, and a station for each race in the style of its ships.
    /// Each falls back to the code-drawn shapes when its file or shader is missing.
    /// </summary>
    public static partial class Art
    {
        // ---------------------------------------------------------------- Ground

        static readonly Dictionary<PlanetType, Material> groundMats = new Dictionary<PlanetType, Material>();

        /// <summary>The ground material for a planet type: which painted detail goes on its land, rock, snow and
        /// sea, and whether the sea drifts (water, lava) or glows (lava). Null when the shader or art is missing.</summary>
        public static Material GroundMaterial(PlanetType type)
        {
            if (groundMats.TryGetValue(type, out var m)) return m;
            var sh = Shader("FBTSGround");
            string land = "regolith", sea = "water";
            float flow = 0.06f, glow = 0f;
            switch (type)
            {
                case PlanetType.Ocean: land = "moss"; break;
                case PlanetType.Ice: land = "snow"; sea = "seaice"; flow = 0f; break;
                case PlanetType.Molten: land = "basalt"; sea = "lava"; flow = 0.02f; glow = 1f; break;
            }
            var tLand = Texture("Ground/ground_" + land, repeat: true);
            var tRock = Texture("Ground/ground_bedrock", repeat: true);
            var tSnow = Texture("Ground/ground_snow", repeat: true);
            var tSea = Texture("Ground/ground_" + sea, repeat: true);
            if (sh == null || tLand == null || tRock == null || tSnow == null || tSea == null) { groundMats[type] = null; return null; }

            m = new Material(sh) { name = "Ground " + type };
            m.SetTexture("_LandTex", tLand);
            m.SetTexture("_RockTex", tRock);
            m.SetTexture("_SnowTex", tSnow);
            m.SetTexture("_SeaTex", tSea);
            m.SetFloat("_SeaFlow", flow);
            m.SetFloat("_SeaGlow", glow);
            groundMats[type] = m;
            return m;
        }

        // ---------------------------------------------------------------- Things on the ground

        /// <summary>A mineral deposit by value tier (0 common, 1 valuable, 2 rare), a life form or a ruin.
        /// Null if the art is missing (the caller draws its old marker).</summary>
        public static SpriteRenderer SiteSprite(SiteKind kind, int tier, Transform parent, Vector2 pos, float size, int order)
        {
            string file = kind == SiteKind.Ruin ? "ruin" : kind == SiteKind.Life ? "life" : tier >= 2 ? "ore_rare" : tier == 1 ? "ore_valuable" : "ore_common";
            var s = Sprite("Surface/" + file);
            if (s == null) return null;
            var sr = Draw.MakeSprite(file, parent, s, pos, Vector2.one * size, Color.white, order);
            return sr;
        }

        public static int RockCount => Sprite("Surface/rock_0") == null ? 0 : 6;

        /// <summary>A scattered boulder (decoration only), tinted to the ground around it.</summary>
        public static SpriteRenderer Rock(int index, Transform parent, Vector2 pos, float size, float angle, Color tint, int order)
        {
            var s = Sprite("Surface/rock_" + index);
            if (s == null) return null;
            var sr = Draw.MakeSprite("Rock", parent, s, pos, Vector2.one * size, tint, order);
            sr.transform.localRotation = Quaternion.Euler(0, 0, angle);
            return sr;
        }

        // ---------------------------------------------------------------- Settlements

        public static string SettlementKind(string race)
        {
            switch (race)
            {
                case "Orrun Combine": return "orrun";
                case "The Murmur": return "murmur";
                case null: return "humans";
                default: return race.ToLowerInvariant();
            }
        }

        /// <summary>A town of the race that lives on this world, in its own style. Falls back to a pale disc.</summary>
        public static SpriteRenderer Settlement(string race, Transform parent, Vector2 pos, float size)
        {
            var s = Sprite("Settlements/settlement_" + SettlementKind(race));
            if (s == null) return Draw.Dot("Settlement", parent, pos, size * 0.4f, new Color(1f, 1f, 0.8f, 0.9f), 9);
            return Draw.MakeSprite("Settlement", parent, s, pos, Vector2.one * size, Color.white, 9);
        }

        // ---------------------------------------------------------------- Lander

        /// <summary>The landing craft, art facing up, with engine glow, blinking pad lights and dust driven by a
        /// LanderRig (set its thrust). Falls back to the drawn shape (then rig is null).</summary>
        public static Transform Lander(Transform parent, float size, out LanderRig rig)
        {
            rig = null;
            var body = Sprite("Vehicles/lander");
            if (body == null)
            {
                var d = Draw.Ship("Lander", parent, new Color(0.4f, 0.9f, 1f), 1.2f);
                d.localRotation = Quaternion.Euler(0, 0, 90);
                return d;
            }
            var root = new GameObject("Lander").transform;
            root.SetParent(parent, false);
            var hull = new GameObject("Body").transform;
            hull.SetParent(root, false);
            hull.localScale = Vector3.one * size;
            var sr = hull.gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = body;
            sr.sortingOrder = 18;

            rig = root.gameObject.AddComponent<LanderRig>();
            rig.size = size;
            // Engines sit under the four corner pods; pad lights on the four feet (positions in the art, 0..1 wide)
            foreach (var e in new[] { new Vector2(-0.315f, 0.227f), new Vector2(0.315f, 0.227f), new Vector2(-0.315f, -0.184f), new Vector2(0.315f, -0.184f) })
                rig.engines.Add(Draw.Dot("Engine", hull, e, 0.2f, new Color(1f, 0.7f, 0.35f, 0f), 17));
            int i = 0;
            foreach (var l in new[] { new Vector2(-0.44f, 0.44f), new Vector2(0.44f, 0.44f), new Vector2(-0.44f, -0.44f), new Vector2(0.44f, -0.44f) })
            {
                rig.lights.Add(Draw.Dot("Light", hull, l, 0.045f, new Color(0.4f, 1f, 0.9f), 19));
                rig.lightPhase.Add(i++ * 0.25f);
            }
            return root;
        }

        // ---------------------------------------------------------------- Stations

        public static string StationKind(string race)
        {
            switch (race)
            {
                case "Orrun Combine": return "orrun";
                case "Varkesh": return "varkesh";
                case "Skarrow": return "skarrow";
                default: return "humans";
            }
        }

        static readonly Dictionary<string, (Vector2[] lights, Color color, float spin)> stationLayouts = new Dictionary<string, (Vector2[], Color, float)>
        {
            ["humans"] = (new[] { new Vector2(0.437f, 0f), new Vector2(0f, 0.46f), new Vector2(-0.44f, 0f), new Vector2(0f, -0.467f),
                                  new Vector2(0.29f, 0.29f), new Vector2(-0.29f, 0.29f), new Vector2(-0.29f, -0.29f), new Vector2(0.29f, -0.29f) },
                          new Color(0.4f, 1f, 0.9f), 3f),
            ["orrun"] = (new[] { new Vector2(0.467f, 0f), new Vector2(0f, 0.462f), new Vector2(-0.465f, 0f), new Vector2(0f, -0.452f),
                                 new Vector2(0.33f, 0.33f), new Vector2(-0.33f, 0.33f), new Vector2(-0.33f, -0.33f), new Vector2(0.33f, -0.33f) },
                         new Color(0.3f, 1f, 0.9f), 1.5f),
            ["varkesh"] = (new[] { new Vector2(0.43f, 0f), new Vector2(-0.43f, 0f), new Vector2(0f, 0.34f), new Vector2(0f, -0.31f),
                                   new Vector2(0.33f, 0.33f), new Vector2(-0.33f, 0.33f), new Vector2(-0.33f, -0.33f), new Vector2(0.33f, -0.33f) },
                           new Color(1f, 0.4f, 0.15f), 2f),
            ["skarrow"] = (new[] { new Vector2(0.45f, 0f), new Vector2(0.33f, 0.33f), new Vector2(0f, 0.3f), new Vector2(-0.31f, 0f),
                                   new Vector2(-0.3f, -0.3f), new Vector2(0f, -0.37f), new Vector2(0.33f, -0.33f) },
                           new Color(1f, 0.85f, 0.3f), 1f),
        };

        /// <summary>A starport in the style of its race's ships, turning slowly with blinking lights (StationRig).
        /// Falls back to the drawn diamond.</summary>
        public static Transform Station(string race, Transform parent, Vector2 pos, float size)
        {
            string kind = StationKind(race);
            var art = Sprite("Stations/station_" + kind);
            if (art == null)
            {
                var sp = Draw.MakeSprite("Starport", parent, Draw.Square, pos, new Vector2(0.9f, 0.9f), new Color(0.75f, 0.8f, 0.9f), 11);
                sp.transform.localRotation = Quaternion.Euler(0, 0, 45);
                return sp.transform;
            }
            var root = new GameObject("Starport").transform;
            root.SetParent(parent, false);
            root.localPosition = pos;
            var body = new GameObject("Body").transform;
            body.SetParent(root, false);
            body.localScale = Vector3.one * size;
            var sr = body.gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = art;
            sr.sortingOrder = 11;

            var rig = root.gameObject.AddComponent<StationRig>();
            rig.body = body;
            var lay = stationLayouts[kind];
            rig.spin = lay.spin;
            int i = 0;
            foreach (var p in lay.lights)
            {
                rig.lights.Add(Draw.Dot("Light", body, p, 0.022f, lay.color, 12));
                rig.lightPhase.Add((i++ * 0.37f) % 1f);
            }
            return root;
        }
    }

    /// <summary>Turns a station slowly and blinks its lights.</summary>
    public class StationRig : MonoBehaviour
    {
        public Transform body;
        public float spin = 2f;   // degrees per second
        public readonly List<SpriteRenderer> lights = new List<SpriteRenderer>();
        public readonly List<float> lightPhase = new List<float>();
        float t;

        void Update()
        {
            t += Time.deltaTime;
            if (body != null) body.localRotation = Quaternion.Euler(0, 0, t * spin);
            for (int i = 0; i < lights.Count; i++)
                Fx.SetAlpha(lights[i], Mathf.Repeat(t * 0.6f + lightPhase[i], 1f) < 0.15f ? 1f : 0.2f);
        }
    }

    /// <summary>Animates the lander: engine glow and a ring of dust while thrust is above zero, and blinking
    /// pad lights. Cutscenes set thrust (0 = engines off, 1 = full burn).</summary>
    public class LanderRig : MonoBehaviour
    {
        public float size = 3f;
        [System.NonSerialized] public float thrust;
        public readonly List<SpriteRenderer> engines = new List<SpriteRenderer>();
        public readonly List<SpriteRenderer> lights = new List<SpriteRenderer>();
        public readonly List<float> lightPhase = new List<float>();
        /// <summary>Ground colour at a point (in the parent's space), used to tint the dust.</summary>
        public System.Func<Vector2, Color> groundColor;
        float t, dustTimer;
        readonly List<(SpriteRenderer sr, Vector3 vel, float age)> dust = new List<(SpriteRenderer, Vector3, float)>();

        void Update()
        {
            float dt = Time.deltaTime;
            t += dt;
            for (int k = 0; k < engines.Count; k++)
            {
                float f = 0.8f + 0.2f * Mathf.PerlinNoise(t * 11f, k * 2.3f);
                engines[k].transform.localScale = Vector3.one * (0.12f + 0.16f * thrust) * f;
                Fx.SetAlpha(engines[k], Mathf.Clamp01(thrust * 1.3f) * 0.9f * f);
            }
            for (int i = 0; i < lights.Count; i++)
                Fx.SetAlpha(lights[i], Mathf.Repeat(t * 0.9f + lightPhase[i], 1f) < 0.14f ? 1f : 0.2f);

            // Blast dust pushed outward from under the craft (only near the ground: the craft shrinks as it climbs)
            float near = Mathf.InverseLerp(0.55f, 0.95f, transform.localScale.x);
            if (thrust > 0.15f && near > 0f && (dustTimer -= dt) <= 0f)
            {
                dustTimer = 0.04f;
                for (int n = 0; n < 2; n++)
                {
                    float a = Random.value * Mathf.PI * 2f;
                    var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                    Spawn(transform.position + dir * size * 0.3f, dir * size * Random.Range(0.9f, 1.6f) * thrust, near);
                }
            }
            for (int i = dust.Count - 1; i >= 0; i--)
            {
                var (sr, vel, age) = dust[i];
                age += dt;
                if (sr == null) { dust.RemoveAt(i); continue; }
                if (age >= 0.9f) { Destroy(sr.gameObject); dust.RemoveAt(i); continue; }
                float k = age / 0.9f;
                sr.transform.position += vel * dt * (1f - k);
                sr.transform.localScale = Vector3.one * size * Mathf.Lerp(0.12f, 0.35f, Mathf.Sqrt(k));
                var c = sr.color; c.a = 0.7f * (1f - k) * (1f - k); sr.color = c;
                dust[i] = (sr, vel, age);
            }
        }

        void Spawn(Vector3 at, Vector3 vel, float strength)
        {
            var go = new GameObject("Dust");
            go.transform.SetParent(transform.parent, true);
            go.transform.position = at;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Draw.Circle;
            Color g = groundColor != null && transform.parent != null ? groundColor(transform.parent.InverseTransformPoint(at)) : new Color(0.6f, 0.52f, 0.4f);
            float lum = 0.3f * g.r + 0.59f * g.g + 0.11f * g.b;
            Color c = lum < 0.6f ? Color.Lerp(g, Color.white, 0.45f) : Color.Lerp(g, g * 0.55f, 0.7f);
            c.a = 0.7f * strength;
            sr.color = c;
            sr.sortingOrder = 16;
            dust.Add((sr, vel, 0f));
        }

        void OnDestroy()
        {
            foreach (var d in dust) if (d.sr != null) Destroy(d.sr.gameObject);
        }
    }
}
