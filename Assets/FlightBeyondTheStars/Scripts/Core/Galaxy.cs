using System.Collections.Generic;
using UnityEngine;

namespace FBTS
{
    public enum PlanetType { Rocky, Molten, Ice, Ocean, GasGiant }

    public class Planet
    {
        public string name;
        public PlanetType type;
        public float orbitRadius;
        public float angle;      // radians
        public float size;
        public float gravity;    // in g
        public string hazard;
        public int seed;
        public bool scanned;
        public bool surveySold;
        public List<string> minerals = new List<string>();
        public HashSet<long> minedDeposits = new HashSet<long>();
        public HashSet<long> visitedSections = new HashSet<long>();

        /// <summary>Surface sections around the equator; bigger planets take longer to circle.</summary>
        public int SectionsAround => Mathf.Clamp(Mathf.RoundToInt(6f + size * 8f), 8, 18);
        public const int SectionsTall = 5;

        public bool Landable => type != PlanetType.GasGiant;
        public Vector2 Position => new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * orbitRadius;
    }

    public class StarSystem
    {
        public int id;
        public string name;
        public Vector2 pos;      // light-years
        public Color color;
        public bool visited;
        public string starportRace; // null = no starport
        public List<Planet> planets = new List<Planet>();
        public List<int> links = new List<int>();
    }

    /// <summary>Seeded prototype galaxy: a home system, a ring of generated systems, and jump routes.</summary>
    public class Galaxy
    {
        public int seed;
        public List<StarSystem> systems = new List<StarSystem>();

        static readonly string[] Names =
        {
            "Haven", "Brightmarket", "Corvan", "Ysolde", "Tarrow", "Meridia", "Okkam", "Velis",
            "Sundra", "Quell", "Hesper", "Arden", "Kythe", "Lumen", "Pyre", "Naddo", "Orrel", "Sable"
        };

        static readonly Color[] StarColors =
        {
            new Color(1f, 0.95f, 0.75f), new Color(1f, 0.75f, 0.45f), new Color(1f, 0.55f, 0.45f),
            new Color(0.75f, 0.85f, 1f), new Color(1f, 1f, 1f)
        };

        public StarSystem Home => systems[0];

        public static Galaxy Generate(int seed, int count, float radiusLy)
        {
            var rng = new System.Random(seed);
            var g = new Galaxy { seed = seed };
            count = Mathf.Clamp(count, 4, Names.Length);

            // Place stars with a minimum spacing; Haven sits at the centre.
            int guard = 0;
            while (g.systems.Count < count && guard++ < 5000)
            {
                Vector2 p = g.systems.Count == 0
                    ? Vector2.zero
                    : new Vector2(Range(rng, -radiusLy, radiusLy), Range(rng, -radiusLy, radiusLy));
                if (p.magnitude > radiusLy) continue;
                bool tooClose = false;
                foreach (var s in g.systems) if (Vector2.Distance(s.pos, p) < 5.5f) { tooClose = true; break; }
                if (tooClose) continue;

                var sys = new StarSystem
                {
                    id = g.systems.Count,
                    name = Names[g.systems.Count],
                    pos = p,
                    color = StarColors[rng.Next(StarColors.Length)]
                };
                g.systems.Add(sys);
            }

            // Starports: humans at Haven, the Orrun Combine at Brightmarket (index 1).
            g.systems[0].starportRace = "Humans";
            g.systems[0].visited = true;
            if (g.systems.Count > 1) g.systems[1].starportRace = "Orrun Combine";

            BuildRoutes(g);
            foreach (var s in g.systems) BuildPlanets(s, rng);
            return g;
        }

        static void BuildRoutes(Galaxy g)
        {
            // Minimum spanning tree (every system reachable) plus each star's nearest extra neighbour
            // (redundant paths, so a severed route rarely strands the player).
            var inTree = new HashSet<int> { 0 };
            while (inTree.Count < g.systems.Count)
            {
                float best = float.MaxValue; int a = -1, b = -1;
                foreach (int i in inTree)
                    for (int j = 0; j < g.systems.Count; j++)
                    {
                        if (inTree.Contains(j)) continue;
                        float d = Vector2.Distance(g.systems[i].pos, g.systems[j].pos);
                        if (d < best) { best = d; a = i; b = j; }
                    }
                Link(g, a, b);
                inTree.Add(b);
            }
            foreach (var s in g.systems)
            {
                int nearest = -1; float best = float.MaxValue;
                foreach (var o in g.systems)
                {
                    if (o == s || s.links.Contains(o.id)) continue;
                    float d = Vector2.Distance(s.pos, o.pos);
                    if (d < best) { best = d; nearest = o.id; }
                }
                if (nearest >= 0 && best < 14f) Link(g, s.id, nearest);
            }
        }

        static void Link(Galaxy g, int a, int b)
        {
            if (!g.systems[a].links.Contains(b)) g.systems[a].links.Add(b);
            if (!g.systems[b].links.Contains(a)) g.systems[b].links.Add(a);
        }

        static void BuildPlanets(StarSystem s, System.Random rng)
        {
            int n = 2 + rng.Next(4); // 2-5
            float orbit = 4.5f;
            for (int i = 0; i < n; i++)
            {
                orbit += 2.5f + (float)rng.NextDouble() * 2.5f;
                var type = (PlanetType)rng.Next(5);
                if (s.id == 0 && i == 0) type = PlanetType.Rocky; // a safe first landing near home
                var p = new Planet
                {
                    name = $"{s.name} {Roman(i + 1)}",
                    type = type,
                    orbitRadius = orbit,
                    angle = (float)(rng.NextDouble() * Mathf.PI * 2),
                    size = type == PlanetType.GasGiant ? 1.6f : 0.6f + (float)rng.NextDouble() * 0.6f,
                    gravity = type == PlanetType.GasGiant ? 2.5f : 0.3f + (float)rng.NextDouble() * 1.5f,
                    seed = rng.Next()
                };
                p.hazard = HazardFor(type);
                p.minerals.AddRange(MineralsFor(type));
                s.planets.Add(p);
            }
        }

        public static string[] MineralsFor(PlanetType t)
        {
            switch (t)
            {
                case PlanetType.Rocky: return new[] { "Iron ore", "Copper", "Titanium" };
                case PlanetType.Molten: return new[] { "Iron ore", "Cobalt", "Platinum" };
                case PlanetType.Ice: return new[] { "Drive crystal ore", "Copper", "Rhodium" };
                case PlanetType.Ocean: return new[] { "Medicinal flora", "Copper", "Common specimen" };
                default: return new string[0];
            }
        }

        static string HazardFor(PlanetType t)
        {
            switch (t)
            {
                case PlanetType.Molten: return "Extreme heat";
                case PlanetType.Ice: return "Extreme cold";
                case PlanetType.Ocean: return "Storms";
                case PlanetType.GasGiant: return "Crushing atmosphere";
                default: return "None";
            }
        }

        static string Roman(int i)
        {
            string[] r = { "", "I", "II", "III", "IV", "V", "VI" };
            return i < r.Length ? r[i] : i.ToString();
        }

        static float Range(System.Random rng, float a, float b) => a + (float)rng.NextDouble() * (b - a);

        public bool HasRoute(StarSystem a, StarSystem b) => a.links.Contains(b.id);
    }
}
