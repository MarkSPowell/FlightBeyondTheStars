using System.Collections.Generic;
using UnityEngine;

namespace FBTS
{
    public enum TerrainClass { Sea, Coast, Lowland, Highland, Mountain }
    public enum SiteKind { Mineral, Life, Ruin }

    /// <summary>A deposit, life form or ruin at a fixed spot on a planet.</summary>
    public class SiteSpec
    {
        public long key;
        public SiteKind kind;
        public Vector2 pos;      // world tile coordinates (wrapped column)
        public string commodity; // empty for ruins
        public int amount;
    }

    /// <summary>
    /// One planet's terrain: a seamless elevation field that wraps east-west, sea level, terrain classes,
    /// colours for the surface and the orbital topographic map, and where deposits, life and ruins sit.
    /// The orbital map and the surface both read from this, so what you pick from orbit is what you drive on.
    /// </summary>
    public class PlanetTerrain
    {
        public const int SW = 48, SH = 30; // tiles per surface section

        public readonly Planet planet;
        public readonly int around;
        public readonly float circumference, worldHeight;
        public readonly float seaLevel;
        readonly float ox, oy;

        const float CoastBand = 0.06f, HighlandLevel = 0.58f, MountainLevel = 0.74f;

        public PlanetTerrain(Planet p)
        {
            planet = p;
            around = p.SectionsAround;
            circumference = around * SW;
            worldHeight = Planet.SectionsTall * SH;
            ox = (p.seed % 1000) * 0.37f;
            oy = (p.seed / 1000 % 1000) * 0.53f;
            switch (p.type)
            {
                case PlanetType.Ocean: seaLevel = 0.52f; break;
                case PlanetType.Molten: seaLevel = 0.36f; break; // lava seas
                case PlanetType.Ice: seaLevel = 0.33f; break;    // frozen seas, drivable
                default: seaLevel = 0.30f; break;
            }
        }

        public int WrapCol(int cx) => ((cx % around) + around) % around;
        public float WrapX(float x) => ((x % circumference) + circumference) % circumference;
        public static long SectionKey(int col, int row) => ((long)col << 16) | (uint)row;

        public float Latitude(float y) => (y / worldHeight - 0.5f) * 144f;
        public float Longitude(float x) => WrapX(x) / circumference * 360f - 180f;

        /// <summary>Elevation 0..1, sampled on a cylinder so the planet wraps east-west.</summary>
        public float Elevation(float x, float y)
        {
            float u = x / circumference * Mathf.PI * 2f;
            float r = 0.12f * circumference / (Mathf.PI * 2f);
            float a = Mathf.PerlinNoise(ox + Mathf.Cos(u) * r, oy + y * 0.12f);
            float b = Mathf.PerlinNoise(ox + 50f + Mathf.Sin(u) * r, oy + 50f + y * 0.12f);
            float fine = (a + b) * 0.5f;
            float c = Mathf.PerlinNoise(ox + 200f + Mathf.Cos(u) * r * 0.15f, oy + y * 0.02f);
            float d = Mathf.PerlinNoise(ox + 300f + Mathf.Sin(u) * r * 0.15f, oy + 300f + y * 0.02f);
            float broad = (c + d) * 0.5f;
            float n = fine * 0.55f + broad * 0.45f;
            return Mathf.Clamp01((n - 0.5f) * 2.4f + 0.5f);
        }

        public TerrainClass Classify(float e)
        {
            if (e < seaLevel) return TerrainClass.Sea;
            if (e < seaLevel + CoastBand) return TerrainClass.Coast;
            if (e >= MountainLevel) return TerrainClass.Mountain;
            if (e >= HighlandLevel) return TerrainClass.Highland;
            return TerrainClass.Lowland;
        }

        public TerrainClass ClassAt(float x, float y) => Classify(Elevation(x, y));

        /// <summary>Seas block the rover except on ice worlds, where they are frozen solid.</summary>
        public bool Passable(float x, float y)
        {
            if (y < 0f || y > worldHeight) return false;
            return planet.type == PlanetType.Ice || ClassAt(x, y) != TerrainClass.Sea;
        }

        public string SeaName => planet.type == PlanetType.Molten ? "lava sea" : planet.type == PlanetType.Ice ? "frozen sea" : "sea";

        public string Describe(TerrainClass c)
        {
            switch (c)
            {
                case TerrainClass.Sea: return SeaName;
                case TerrainClass.Coast: return planet.type == PlanetType.Molten ? "lava shore" : "coast";
                case TerrainClass.Lowland: return "lowland";
                case TerrainClass.Highland: return "highland";
                default: return "mountains";
            }
        }

        // ---------------------------------------------------------------- Colours

        /// <summary>Close-up surface colour.</summary>
        public Color SurfaceColor(float x, float y)
        {
            float e = Elevation(x, y);
            var cls = Classify(e);
            Color c;
            switch (planet.type)
            {
                case PlanetType.Molten:
                    if (cls == TerrainClass.Sea) return Color.Lerp(new Color(0.85f, 0.25f, 0.05f), new Color(1f, 0.65f, 0.2f), Mathf.InverseLerp(0f, seaLevel, e));
                    c = Color.Lerp(new Color(0.16f, 0.09f, 0.08f), new Color(0.42f, 0.36f, 0.34f), Mathf.InverseLerp(seaLevel, 1f, e));
                    return c;
                case PlanetType.Ice:
                    if (cls == TerrainClass.Sea) c = Color.Lerp(new Color(0.55f, 0.75f, 0.88f), new Color(0.7f, 0.85f, 0.95f), Mathf.InverseLerp(0f, seaLevel, e));
                    else c = Color.Lerp(new Color(0.78f, 0.84f, 0.9f), new Color(0.97f, 0.98f, 1f), Mathf.InverseLerp(seaLevel, 1f, e));
                    break;
                default:
                    bool ocean = planet.type == PlanetType.Ocean;
                    switch (cls)
                    {
                        case TerrainClass.Sea:
                            c = Color.Lerp(new Color(0.04f, 0.14f, 0.38f), new Color(0.12f, 0.36f, 0.62f), Mathf.InverseLerp(0f, seaLevel, e));
                            break;
                        case TerrainClass.Coast:
                            c = ocean ? new Color(0.78f, 0.72f, 0.5f) : new Color(0.6f, 0.53f, 0.4f);
                            break;
                        case TerrainClass.Lowland:
                            c = ocean ? Color.Lerp(new Color(0.22f, 0.48f, 0.22f), new Color(0.35f, 0.55f, 0.28f), e)
                                      : Color.Lerp(new Color(0.45f, 0.38f, 0.3f), new Color(0.55f, 0.47f, 0.37f), e);
                            break;
                        case TerrainClass.Highland:
                            c = ocean ? new Color(0.45f, 0.48f, 0.3f) : new Color(0.5f, 0.42f, 0.33f);
                            break;
                        default:
                            c = Color.Lerp(new Color(0.45f, 0.42f, 0.4f), new Color(0.85f, 0.85f, 0.88f), Mathf.InverseLerp(MountainLevel, 1f, e));
                            break;
                    }
                    break;
            }
            float polar = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(50f, 70f, Mathf.Abs(Latitude(y))));
            return Color.Lerp(c, new Color(0.93f, 0.96f, 1f), polar * 0.8f);
        }

        /// <summary>What the ground is made of, for the painted-ground shader: R = sea, G = rock (rising through
        /// the highlands to full on mountains), B = polar snow, A = hill shading (0.5 = flat).</summary>
        public Color GroundMask(float x, float y)
        {
            float e = Elevation(x, y);
            float sea = e < seaLevel ? 1f : 0f;
            float rock = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(HighlandLevel - 0.04f, MountainLevel + 0.04f, e));
            float polar = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(50f, 70f, Mathf.Abs(Latitude(y))));
            if (planet.type == PlanetType.Ice) polar = 0f;   // the whole world is snow already
            // Hill shading, lit from the upper left: worked out here per km and blended smoothly on screen
            // (a slope taken in the shader from blended heights would show up as squares).
            float ex = Elevation(x + 1f, y) - Elevation(x - 1f, y);
            float ey = Elevation(x, y + 1f) - Elevation(x, y - 1f);
            float shade = sea > 0f ? 0.5f : Mathf.Clamp01(0.5f + (-ex + ey) * 1.6f);
            return new Color(sea, rock, polar * 0.9f, shade);
        }

        /// <summary>Orbital map colour: hypsometric tints with contour lines every elevation band.</summary>
        public Color TopoColor(float x, float y, float stepX, float stepY)
        {
            float e = Elevation(x, y);
            Color c = SurfaceColor(x, y);
            if (e >= seaLevel)
            {
                // Exaggerate relief so highlands and mountains read at a glance.
                float t = Mathf.InverseLerp(seaLevel, 1f, e);
                c = Color.Lerp(c, Color.Lerp(new Color(0.3f, 0.5f, 0.25f), new Color(0.75f, 0.6f, 0.4f), t), 0.25f);
                c *= Mathf.Lerp(0.85f, 1.15f, t);
            }
            const float bands = 14f;
            int band = Mathf.FloorToInt(e * bands);
            bool edge = Mathf.FloorToInt(Elevation(x + stepX, y) * bands) != band || Mathf.FloorToInt(Elevation(x, y + stepY) * bands) != band;
            bool shore = (e < seaLevel) != (Elevation(x + stepX, y) < seaLevel) || (e < seaLevel) != (Elevation(x, y + stepY) < seaLevel);
            if (shore) return new Color(0.05f, 0.05f, 0.08f);
            if (edge) c *= 0.7f;
            c.a = 1f;
            return c;
        }

        // ---------------------------------------------------------------- Orbital readings

        float LifeFactor => planet.type == PlanetType.Ocean ? 1f : planet.type == PlanetType.Rocky ? 0.4f : planet.type == PlanetType.Ice ? 0.2f : 0f;

        float AreaAverage(float x, float y, System.Func<TerrainClass, float> weight)
        {
            float sum = 0f; int n = 0;
            for (int i = -3; i <= 3; i++)
                for (int j = -2; j <= 2; j++)
                {
                    sum += weight(ClassAt(x + i * 8f, Mathf.Clamp(y + j * 6f, 0f, worldHeight)));
                    n++;
                }
            return sum / n;
        }

        /// <summary>0..1 chance of finding minerals around a point (about one section across).</summary>
        public float MineralDensity(float x, float y)
        {
            bool any = false;
            foreach (var m in planet.minerals) { var c = GameData.Commodity(m); if (c == null || c.category == "Minerals") any = true; }
            return any ? AreaAverage(x, y, MineralWeight) : 0f;
        }

        /// <summary>0..1 chance of finding plants and animals around a point.</summary>
        public float LifeSigns(float x, float y) => AreaAverage(x, y, LifeWeight) * LifeFactor;

        public static string Level(float v, bool detailed)
        {
            if (!detailed) return v < 0.15f ? "low" : v < 0.45f ? "medium" : "high";
            return v < 0.06f ? "very low" : v < 0.18f ? "low" : v < 0.38f ? "moderate" : v < 0.6f ? "high" : "very high";
        }

        /// <summary>Centre of a ruin's search area: offset from the true spot by up to 70% of the radius.</summary>
        public static Vector2 SearchAreaCenter(SiteSpec ruin, float radius)
        {
            var rng = new System.Random((int)(ruin.key ^ (ruin.key >> 32)) * 31 + 7);
            float ang = (float)(rng.NextDouble() * Mathf.PI * 2);
            // Uniform over an annulus (25%-90% of the radius): never near the middle, so the
            // centre gives nothing away, and never right on the edge, so the circle still contains it.
            float inner = 0.25f, outer = 0.9f;
            float u = (float)rng.NextDouble();
            float dist = radius * Mathf.Sqrt(Mathf.Lerp(inner * inner, outer * outer, u));
            return ruin.pos + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * dist;
        }

        /// <summary>Spots for city lights on an inhabited world: coastal and lowland land.</summary>
        public List<Vector2> CitySites(int count)
        {
            var list = new List<Vector2>();
            var rng = new System.Random(planet.seed * 17 + 3);
            for (int tries = 0; tries < count * 60 && list.Count < count; tries++)
            {
                var p = new Vector2((float)rng.NextDouble() * circumference, worldHeight * (0.2f + 0.6f * (float)rng.NextDouble()));
                var c = ClassAt(p.x, p.y);
                if (c == TerrainClass.Coast || c == TerrainClass.Lowland) list.Add(p);
            }
            return list;
        }

        // ---------------------------------------------------------------- Sites

        static float MineralWeight(TerrainClass c) =>
            c == TerrainClass.Mountain ? 1f : c == TerrainClass.Highland ? 0.55f : c == TerrainClass.Lowland ? 0.15f : c == TerrainClass.Coast ? 0.1f : 0f;
        static float LifeWeight(TerrainClass c) =>
            c == TerrainClass.Coast ? 1f : c == TerrainClass.Lowland ? 0.35f : c == TerrainClass.Highland ? 0.1f : c == TerrainClass.Mountain ? 0.02f : 0f;
        static float RuinWeight(TerrainClass c) =>
            c == TerrainClass.Coast ? 1f : c == TerrainClass.Lowland ? 0.3f : c == TerrainClass.Sea ? 0f : 0.05f;

        string[] LifeFor(out float[] weights, out int maxCount)
        {
            switch (planet.type)
            {
                case PlanetType.Ocean: weights = new[] { 0.5f, 0.4f, 0.1f }; maxCount = 5; return new[] { "Medicinal flora", "Common specimen", "Rare specimen" };
                case PlanetType.Rocky: weights = new[] { 0.85f, 0.15f }; maxCount = 2; return new[] { "Common specimen", "Rare specimen" };
                case PlanetType.Ice: weights = new[] { 0.9f, 0.1f }; maxCount = 1; return new[] { "Common specimen", "Rare specimen" };
                default: weights = new float[0]; maxCount = 0; return new string[0];
            }
        }

        static string Pick(System.Random rng, string[] items, float[] weights)
        {
            double roll = rng.NextDouble(), acc = 0;
            for (int i = 0; i < items.Length; i++) { acc += weights[i]; if (roll < acc) return items[i]; }
            return items[items.Length - 1];
        }

        /// <summary>Everything placed in one section (by wrapped column), deterministic from the planet seed.</summary>
        public List<SiteSpec> SectionSites(int col, int row)
        {
            var list = new List<SiteSpec>();
            var rng = new System.Random(planet.seed ^ (col * 73856093) ^ (row * 19349663));
            long baseKey = SectionKey(col, row) << 8;

            // Minerals: planet's mineral list, weighted toward the commoner ones.
            var minerals = new List<string>();
            foreach (var m in planet.minerals)
            {
                var c = GameData.Commodity(m);
                if (c == null || c.category == "Minerals") minerals.Add(m);
            }
            int mineralCount = minerals.Count == 0 ? 0 : 4 + rng.Next(4);
            float[] mw = minerals.Count == 3 ? new[] { 0.5f, 0.35f, 0.15f } : null;
            for (int i = 0; i < mineralCount; i++)
            {
                if (!Place(rng, col, row, MineralWeight, out var pos)) continue;
                string com = mw != null ? Pick(rng, minerals.ToArray(), mw) : minerals[rng.Next(minerals.Count)];
                list.Add(new SiteSpec { key = baseKey | (uint)i, kind = SiteKind.Mineral, pos = pos, commodity = com, amount = 1 + rng.Next(4) });
            }

            // Life forms and plants, mostly along coasts.
            var life = LifeFor(out var lw, out int maxLife);
            int lifeCount = maxLife == 0 ? 0 : rng.Next(maxLife + 1);
            for (int i = 0; i < lifeCount; i++)
            {
                if (!Place(rng, col, row, LifeWeight, out var pos)) continue;
                list.Add(new SiteSpec { key = baseKey | (uint)(20 + i), kind = SiteKind.Life, pos = pos, commodity = Pick(rng, life, lw), amount = 1 + rng.Next(3) });
            }

            // Ruins: rare, near coasts.
            if (rng.NextDouble() < 0.18 && Place(rng, col, row, RuinWeight, out var rp))
                list.Add(new SiteSpec { key = baseKey | 100u, kind = SiteKind.Ruin, pos = rp, commodity = "", amount = 0 });

            return list;
        }

        bool Place(System.Random rng, int col, int row, System.Func<TerrainClass, float> weight, out Vector2 pos)
        {
            for (int tries = 0; tries < 30; tries++)
            {
                var p = new Vector2(col * SW + 1 + (float)rng.NextDouble() * (SW - 2), row * SH + 1 + (float)rng.NextDouble() * (SH - 2));
                float w = weight(ClassAt(p.x, p.y));
                if (rng.NextDouble() < w) { pos = p; return true; }
            }
            pos = Vector2.zero;
            return false;
        }
    }
}
