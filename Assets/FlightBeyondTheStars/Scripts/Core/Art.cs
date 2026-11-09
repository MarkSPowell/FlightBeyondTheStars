using System.Collections.Generic;
using UnityEngine;

namespace FBTS
{
    /// <summary>
    /// Painted art, loaded from Resources/Art, with a fallback to the code-drawn shapes whenever a file or
    /// shader is missing (so the game always runs). Space art is built from still pieces that the engine
    /// animates: a boiling sun shader, spinning planet globes, ships with aiming turrets, blinking lights and
    /// engine glow, and a terrain vehicle with moving treads.
    /// Every art file is listed in Art/ArtManifest.csv with its source and status.
    /// </summary>
    public static partial class Art
    {
        static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, Shader> shaders = new Dictionary<string, Shader>();

        public static Texture2D Texture(string path, bool repeat = false)
        {
            if (!textures.TryGetValue(path, out var t))
            {
                t = Resources.Load<Texture2D>("Art/" + path);
                if (t != null && repeat) t.wrapMode = TextureWrapMode.Repeat;
                textures[path] = t;
            }
            return t;
        }

        /// <summary>A sprite one world unit wide, pivot in the centre. FullRect so it can be tiled.</summary>
        public static Sprite Sprite(string path)
        {
            if (sprites.TryGetValue(path, out var s)) return s;
            var t = Texture(path);
            s = t == null ? null : UnityEngine.Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f),
                                                             t.width, 0, SpriteMeshType.FullRect);
            sprites[path] = s;
            return s;
        }

        public static Shader Shader(string name)
        {
            if (!shaders.TryGetValue(name, out var sh))
            {
                sh = Resources.Load<Shader>("Shaders/" + name);
                if (sh != null && !sh.isSupported) sh = null;
                shaders[name] = sh;
            }
            return sh;
        }

        static SpriteRenderer Quad(string name, Transform parent, Vector2 pos, float size, Material mat, int order)
        {
            var sr = Draw.MakeSprite(name, parent, Draw.Square, pos, new Vector2(size, size), Color.white, order);
            sr.sharedMaterial = mat;
            return sr;
        }

        // ---------------------------------------------------------------- Suns

        /// <summary>A star with a boiling surface and flickering corona, tinted by its colour.
        /// Falls back to plain discs if the shader or texture is missing.</summary>
        public static void Sun(Transform parent, Vector2 pos, float diameter, Color color)
        {
            var sh = Shader("FBTSSun");
            var tex = Texture("Space/sun_surface", repeat: true);
            if (sh == null || tex == null)
            {
                Draw.Dot("Star", parent, pos, diameter, color, 5);
                Draw.Dot("Glow", parent, pos, diameter * 5f / 3f, new Color(color.r, color.g, color.b, 0.15f), 4);
                return;
            }
            // The texture slot is not _MainTex: SpriteRenderer replaces _MainTex with its own sprite.
            var mat = new Material(sh);
            mat.SetTexture("_PlasmaTex", tex);
            mat.SetColor("_Color", color);
            // The disc fills 60% of the quad; the rest is corona.
            Quad("Star", parent, pos, diameter / 0.6f, mat, 5);
        }

        // ---------------------------------------------------------------- Planets

        static readonly Dictionary<long, Texture2D> planetMaps = new Dictionary<long, Texture2D>();

        /// <summary>A slowly spinning globe whose map is the planet's real terrain (the same seas and mountains
        /// you land on), with painted detail mixed in, lit from the star with a day/night line and an atmosphere.</summary>
        public static void Planet(Transform parent, Planet p, Vector2 starPos, Color fallback)
        {
            var sh = Shader("FBTSPlanet");
            if (sh == null) { Draw.Dot(p.name, parent, p.Position, p.size, fallback, 10); return; }

            var mat = new Material(sh);
            mat.SetTexture("_MapTex", PlanetMap(p));
            Vector2 toStar = (starPos - p.Position).normalized;
            mat.SetVector("_LightDir", new Vector4(toStar.x, toStar.y, 0.45f, 0f));
            mat.SetColor("_Atmo", Atmosphere(p.type));
            mat.SetFloat("_Spin", 0.004f + (p.seed % 7) * 0.0012f);
            // The globe fills 85% of the quad; the rest is atmosphere glow.
            Quad(p.name, parent, p.Position, p.size / 0.85f, mat, 10);
        }

        static Color Atmosphere(PlanetType t)
        {
            switch (t)
            {
                case PlanetType.Ocean: return new Color(0.45f, 0.7f, 1f, 0.9f);
                case PlanetType.Ice: return new Color(0.75f, 0.9f, 1f, 0.6f);
                case PlanetType.Molten: return new Color(1f, 0.45f, 0.15f, 0.8f);
                case PlanetType.GasGiant: return new Color(1f, 0.85f, 0.6f, 0.6f);
                default: return new Color(0.9f, 0.7f, 0.5f, 0.35f);
            }
        }

        /// <summary>Equirectangular map (2:1) of the planet, built once from its terrain and cached.</summary>
        public static Texture2D PlanetMap(Planet p)
        {
            long key = ((long)p.seed << 3) ^ (long)p.type;
            if (planetMaps.TryGetValue(key, out var map)) return map;

            const int W = 256, H = 128;
            map = new Texture2D(W, H, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            var px = new Color[W * H];

            // Painted detail (greyscale) from the rocky planet texture, if present.
            Texture2D detail = Texture("Space/planet_detail");
            Color[] dpx = null;
            int dw = 0, dh = 0;
            if (detail != null && detail.isReadable) { dpx = detail.GetPixels(); dw = detail.width; dh = detail.height; }

            if (p.type == PlanetType.GasGiant)
            {
                var rng = new System.Random(p.seed);
                Color a = Color.HSVToRGB((float)rng.NextDouble() * 0.12f + 0.04f, 0.45f, 0.9f);
                Color b = Color.HSVToRGB((float)rng.NextDouble() * 0.12f + 0.02f, 0.3f, 0.55f);
                float phase = (float)rng.NextDouble() * 10f;
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        float u = x / (float)W, v = y / (float)H;
                        float turb = Mathf.PerlinNoise(u * 6f + phase, v * 18f) * 0.25f + Mathf.PerlinNoise(u * 20f, v * 40f + phase) * 0.08f;
                        float band = Mathf.Sin((v + turb) * Mathf.PI * 14f + phase) * 0.5f + 0.5f;
                        px[y * W + x] = Color.Lerp(b, a, band);
                    }
            }
            else
            {
                var terrain = new PlanetTerrain(p);
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        float tx = x / (float)W * terrain.circumference;
                        float ty = y / (float)H * terrain.worldHeight;
                        Color c = terrain.SurfaceColor(tx, ty);
                        if (dpx != null)
                        {
                            float g = dpx[(y * dh / H) * dw + (x * dw / W)].grayscale;
                            c *= Mathf.Lerp(1f, 0.55f + g, 0.6f);
                        }
                        // Polar caps on temperate worlds, so the globe reads as a planet
                        float lat = Mathf.Abs(y / (float)H - 0.5f) * 2f;
                        if (p.type != PlanetType.Molten && lat > 0.86f) c = Color.Lerp(c, new Color(0.93f, 0.96f, 1f), Mathf.InverseLerp(0.86f, 0.95f, lat));
                        c.a = 1f;
                        px[y * W + x] = c;
                    }
            }
            map.SetPixels(px);
            map.Apply(true, true);
            planetMaps[key] = map;
            return map;
        }

        // ---------------------------------------------------------------- Ships

        /// <summary>Layout of a painted hull, in the art's own space: x right, y toward the nose,
        /// coordinates as fractions of the image width (so -0.5..0.5).</summary>
        class HullLayout
        {
            public Vector2[] turrets = new Vector2[0];
            public Vector2[] lights = new Vector2[0];
            public Vector2[] engines = new Vector2[0];
            public Color lightColor = new Color(1f, 0.3f, 0.25f);
            public Color engineColor = new Color(1f, 0.65f, 0.3f);
            public float turretSize = 0.17f;
        }

        static readonly Dictionary<string, HullLayout> layouts = new Dictionary<string, HullLayout>
        {
            ["player"] = new HullLayout
            {
                turrets = new[] { new Vector2(-0.15f, 0f), new Vector2(0.15f, 0f) },
                lights = new[] { new Vector2(-0.33f, -0.27f), new Vector2(0.33f, -0.27f) },
                engines = new[] { new Vector2(-0.075f, -0.44f), new Vector2(0.075f, -0.44f) },
                lightColor = new Color(0.4f, 1f, 0.9f), engineColor = new Color(0.5f, 0.85f, 1f)
            },
            ["orrun"] = new HullLayout
            {
                turrets = new[] { new Vector2(0f, 0.12f) },
                lights = new[] { new Vector2(-0.4f, 0.28f), new Vector2(0.4f, 0.28f), new Vector2(-0.4f, -0.28f), new Vector2(0.4f, -0.28f) },
                engines = new[] { new Vector2(0f, -0.47f) },
                lightColor = new Color(0.3f, 1f, 0.9f), turretSize = 0.14f
            },
            ["varkesh"] = new HullLayout
            {
                turrets = new[] { new Vector2(-0.1f, 0.05f), new Vector2(0.1f, 0.05f) },
                lights = new[] { new Vector2(-0.44f, -0.36f), new Vector2(0.44f, -0.36f) },
                engines = new[] { new Vector2(0f, -0.44f) },
                lightColor = new Color(1f, 0.35f, 0.2f), engineColor = new Color(1f, 0.5f, 0.15f), turretSize = 0.13f
            },
            ["skarrow"] = new HullLayout
            {
                turrets = new[] { new Vector2(0f, 0.02f) },
                lights = new[] { new Vector2(-0.28f, -0.2f), new Vector2(0.3f, -0.12f) },
                engines = new[] { new Vector2(-0.08f, -0.45f), new Vector2(0.08f, -0.45f) },
                lightColor = new Color(1f, 0.85f, 0.3f), turretSize = 0.16f
            },
            ["drone"] = new HullLayout
            {
                lights = new[] { new Vector2(0f, 0.42f), new Vector2(0f, -0.42f), new Vector2(0f, 0f) },
                lightColor = new Color(0.9f, 0.95f, 1f)
            },
        };

        /// <summary>The art name for a race's ships.</summary>
        public static string ShipKind(string race)
        {
            switch (race)
            {
                case "Orrun Combine": return "orrun";
                case "Varkesh": return "varkesh";
                case "Skarrow": return "skarrow";
                case "Mother": return "drone";
                default: return "player";
            }
        }

        /// <summary>A ship whose nose points along +x (like the code-drawn ships), made from a painted hull with
        /// turrets, running lights and engine glow animated by a ShipRig. Falls back to the drawn shape.</summary>
        public static Transform Ship(string kind, string name, Transform parent, Color fallback, float length)
        {
            var hullSprite = Sprite("Ships/ship_" + kind);
            if (hullSprite == null) return Draw.Ship(name, parent, fallback, length * 0.6f);

            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            var hull = new GameObject("Hull").transform;
            hull.SetParent(root, false);
            hull.localRotation = Quaternion.Euler(0, 0, -90f);     // the art faces up; the game's forward is +x
            hull.localScale = Vector3.one * length;
            var sr = hull.gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = hullSprite;
            sr.sortingOrder = 20;

            var rig = root.gameObject.AddComponent<ShipRig>();
            rig.hull = hull;
            var lay = layouts.TryGetValue(kind, out var l) ? l : new HullLayout();

            var turretSprite = Sprite("Ships/turret");
            if (turretSprite != null)
                foreach (var t in lay.turrets)
                {
                    var tr = new GameObject("Turret").transform;
                    tr.SetParent(hull, false);
                    tr.localPosition = t;
                    tr.localScale = Vector3.one * lay.turretSize;
                    var tsr = tr.gameObject.AddComponent<SpriteRenderer>();
                    tsr.sprite = turretSprite;
                    tsr.sortingOrder = 22;
                    rig.turrets.Add(tr);
                }
            foreach (var e in lay.engines)
                rig.engines.Add(Draw.Dot("Engine", hull, e, 0.12f, lay.engineColor, 19));
            int i = 0;
            foreach (var lp in lay.lights)
            {
                var d = Draw.Dot("Light", hull, lp, 0.035f, lay.lightColor, 23);
                rig.lights.Add(d);
                rig.lightPhase.Add(i++ * 0.37f);
            }
            rig.slowPulse = kind == "drone";
            return root;
        }

        // ---------------------------------------------------------------- Terrain vehicle

        /// <summary>The terrain vehicle: painted body with scrolling treads (RoverRig). Falls back to the drawn shape.</summary>
        public static Transform Rover(Transform parent, float length)
        {
            var body = Sprite("Vehicles/rover_body");
            if (body == null) return Draw.Ship("Rover", parent, new Color(1f, 0.9f, 0.3f), length * 0.6f);

            var root = new GameObject("Rover").transform;
            root.SetParent(parent, false);
            var hull = new GameObject("Body").transform;
            hull.SetParent(root, false);
            hull.localRotation = Quaternion.Euler(0, 0, -90f);
            hull.localScale = Vector3.one * length;
            var sr = hull.gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = body;
            sr.sortingOrder = 21;

            var rig = root.gameObject.AddComponent<RoverRig>();
            rig.body = hull;
            rig.length = length;
            var tread = Sprite("Vehicles/tread_strip");
            if (tread != null)
            {
                // Each track: a tiled strip that slides along, shown only inside a mask the size of the track.
                const float trackW = 0.07f, trackH = 0.74f, trackY = -0.08f;
                const float stretch = 2.2f;    // lengthen the tread blocks so they read at game zoom
                float tileH = trackW * stretch * tread.rect.height / tread.rect.width;
                foreach (float x in new[] { -0.168f, 0.166f })
                {
                    var mask = new GameObject("TrackMask").transform;
                    mask.SetParent(hull, false);
                    mask.localPosition = new Vector2(x, trackY);
                    mask.localScale = new Vector3(trackW, trackH, 1f);
                    var sm = mask.gameObject.AddComponent<SpriteMask>();
                    sm.sprite = Draw.Square;

                    var strip = new GameObject("Tread").transform;
                    strip.SetParent(hull, false);
                    strip.localPosition = new Vector2(x, trackY);
                    var tsr = strip.gameObject.AddComponent<SpriteRenderer>();
                    tsr.sprite = tread;
                    tsr.drawMode = SpriteDrawMode.Tiled;
                    tsr.size = new Vector2(1f, (trackH + tileH * 2f) / (trackW * stretch));   // in the strip's own units
                    strip.localScale = new Vector3(trackW, trackW * stretch, 1f);
                    tsr.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                    tsr.sortingOrder = 22;
                    rig.treads.Add(strip);
                }
                rig.tileHeight = tileH;
                rig.blockLength = tileH / 11f;        // tread_strip.png has ~11 blocks per tile
                rig.trackY = trackY;
                rig.bodyScale = length;
            }
            return root;
        }
    }

    /// <summary>Animates a painted ship: turrets swing toward a target, running lights blink, engines glow
    /// with speed. Combat sets aim; everything else runs on its own.</summary>
    public class ShipRig : MonoBehaviour
    {
        public Transform hull;
        public readonly List<Transform> turrets = new List<Transform>();
        public readonly List<SpriteRenderer> engines = new List<SpriteRenderer>();
        public readonly List<SpriteRenderer> lights = new List<SpriteRenderer>();
        public readonly List<float> lightPhase = new List<float>();
        public bool slowPulse;
        [System.NonSerialized] public Vector2? aim;          // world position to point the turrets at (null = face forward)

        Vector3 lastPos;
        float speed, t;

        void Start() { lastPos = transform.position; }

        void Update()
        {
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            t += dt;
            float v = (transform.position - lastPos).magnitude / dt;
            lastPos = transform.position;
            speed = Mathf.Lerp(speed, v, 1f - Mathf.Exp(-6f * dt));

            // Engines: brighter and longer the faster we go, with a flicker
            float thrust = Mathf.Clamp01(speed / 4f);
            for (int k = 0; k < engines.Count; k++)
            {
                var e = engines[k];
                float f = 0.85f + 0.15f * Mathf.PerlinNoise(t * 9f, k * 3.7f);
                e.transform.localScale = new Vector3(0.08f + 0.06f * thrust, (0.08f + 0.22f * thrust) * f, 1f);
                Fx.SetAlpha(e, (0.35f + 0.6f * thrust) * f);
            }

            // Running lights: short blinks, staggered; the silent ships pulse slowly instead
            for (int i = 0; i < lights.Count; i++)
            {
                float a = slowPulse
                    ? 0.25f + 0.75f * (0.5f + 0.5f * Mathf.Sin(t * 1.4f + lightPhase[i] * 4f))
                    : (Mathf.Repeat(t * 0.8f + lightPhase[i], 1f) < 0.12f ? 1f : 0.15f);
                Fx.SetAlpha(lights[i], a);
            }

            // Turrets: swing toward the aim point at a limited rate
            foreach (var tr in turrets)
            {
                float want = 0f;
                if (aim.HasValue)
                {
                    Vector3 local = hull.InverseTransformPoint(aim.Value) - tr.localPosition;
                    want = Mathf.Atan2(local.y, local.x) * Mathf.Rad2Deg - 90f;
                }
                float cur = tr.localEulerAngles.z;
                tr.localRotation = Quaternion.Euler(0, 0, Mathf.MoveTowardsAngle(cur, want, 240f * dt));
            }
        }
    }

    /// <summary>Animates the terrain vehicle while it drives: scrolling treads, a little suspension
    /// bounce, and dust kicked up behind it.</summary>
    public class RoverRig : MonoBehaviour
    {
        public readonly List<Transform> treads = new List<Transform>();
        public Transform body;
        public float tileHeight = 0.2f, blockLength = 0.02f, trackY, bodyScale = 1f, length = 1f;
        /// <summary>Ground colour at a point (in the parent's space), used to tint the dust.</summary>
        public System.Func<Vector2, Color> groundColor;
        Vector3 lastPos;
        float travelled, t, dustTimer, moving;
        readonly List<(SpriteRenderer sr, float age)> dust = new List<(SpriteRenderer, float)>();

        void Start() { lastPos = transform.position; }

        void Update()
        {
            float dt = Time.deltaTime;
            Vector3 delta = transform.position - lastPos;
            lastPos = transform.position;
            if (delta.sqrMagnitude > 4f) delta = Vector3.zero;     // wrapped round the planet or teleported
            float along = Vector3.Dot(delta, transform.right);
            moving = Mathf.MoveTowards(moving, Mathf.Abs(along) > 0.0001f ? 1f : 0f, dt * 6f);

            // Treads: move with the ground, but never more than a third of a block per frame,
            // otherwise they strobe and look frozen (the wagon-wheel effect).
            float scroll = along / Mathf.Max(0.001f, bodyScale) * 0.9f;
            float cap = blockLength * 0.33f;
            scroll = Mathf.Clamp(scroll, -cap, cap);
            travelled = Mathf.Repeat(travelled + scroll, tileHeight);
            foreach (var tr in treads)
            {
                var p = tr.localPosition;
                tr.localPosition = new Vector3(p.x, trackY - travelled, p.z);
            }

            // Suspension: a small rattle and sway while driving
            t += dt;
            if (body != null)
            {
                body.localPosition = new Vector3(0f, Mathf.Sin(t * 37f) * 0.012f * length * moving, 0f);
                body.localRotation = Quaternion.Euler(0, 0, -90f + Mathf.Sin(t * 23f) * 0.8f * moving);
            }

            // Dust from the rear of both tracks
            if (moving > 0.5f && (dustTimer -= dt) <= 0f)
            {
                dustTimer = 0.07f;
                foreach (float side in new[] { -0.17f, 0.17f })
                    SpawnDust(transform.TransformPoint(new Vector3(-0.5f * length, side * length, 0f)));
            }
            for (int i = dust.Count - 1; i >= 0; i--)
            {
                var (sr, age) = dust[i];
                age += dt;
                if (sr == null) { dust.RemoveAt(i); continue; }
                if (age >= 0.7f) { Destroy(sr.gameObject); dust.RemoveAt(i); continue; }
                float k = age / 0.7f;
                sr.transform.localScale = Vector3.one * length * Mathf.Lerp(0.16f, 0.5f, Mathf.Sqrt(k));
                var c = sr.color; c.a = 0.75f * (1f - k) * (1f - k); sr.color = c;
                dust[i] = (sr, age);
            }
        }

        void SpawnDust(Vector3 at)
        {
            var go = new GameObject("Dust");
            go.transform.SetParent(transform.parent, true);
            go.transform.position = at + (Vector3)(Random.insideUnitCircle * 0.05f * length);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Draw.Circle;
            sr.color = DustColor(transform.parent != null ? (Vector2)transform.parent.InverseTransformPoint(at) : (Vector2)at);
            sr.sortingOrder = 20;
            dust.Add((sr, 0f));
        }

        /// <summary>The ground's own colour, lifted on dark ground and deepened on pale ground (snow, salt
        /// flats) so the dust always stands out against what it was kicked up from.</summary>
        Color DustColor(Vector2 p)
        {
            Color g = groundColor != null ? groundColor(p) : new Color(0.6f, 0.52f, 0.4f);
            float lum = 0.3f * g.r + 0.59f * g.g + 0.11f * g.b;
            Color c = lum < 0.6f ? Color.Lerp(g, Color.white, 0.45f) : Color.Lerp(g, g * 0.55f, 0.7f);
            c.a = 0.75f;
            return c;
        }

        void OnDestroy()
        {
            foreach (var (sr, _) in dust) if (sr != null) Destroy(sr.gameObject);
        }
    }
}
