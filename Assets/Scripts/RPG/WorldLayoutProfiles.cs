using UnityEngine;

/// <summary>
/// Per-world adventure layout: zone placement, routes, spawn patterns, blockers.
/// Keeps maps 1–4 roles but changes the silhouette of each world.
/// </summary>
public static class WorldLayoutProfiles
{
    public struct PathSeg
    {
        public Vector3 pos;
        public Vector3 scale;
        public float yawY;

        public PathSeg(float x, float z, float sx, float sz, float yaw = 0f)
        {
            pos = new Vector3(x, 0.03f, z);
            scale = new Vector3(sx, 0.06f, sz);
            yawY = yaw;
        }
    }

    public struct Blocker
    {
        public Vector3 pos;
        public Vector3 scale;
        public float yawY;

        public Blocker(float x, float z, float sx, float sy, float sz, float yaw = 0f)
        {
            pos = new Vector3(x, sy * 0.5f, z);
            scale = new Vector3(sx, sy, sz);
            yawY = yaw;
        }
    }

    public enum SpawnPattern
    {
        Diamond,
        Line,
        Arc,
        Cluster,
        WideRing,
        Flank
    }

    public class Layout
    {
        public Vector3 hub = Vector3.zero;
        public float hubScale = 8f;
        public Vector3[] zoneCenters = new Vector3[5];
        public Vector3[] spawnOffsets = new Vector3[5]; // local to zone
        public float[] zoneSizes = { 0f, 18f, 18f, 18f, 20f };
        public PathSeg[] paths = System.Array.Empty<PathSeg>();
        public Blocker[] blockers = System.Array.Empty<Blocker>();
        public Vector3 altarPos = new Vector3(10f, 0f, 10f);
        public Vector3 villageReturnPos = new Vector3(-8f, 0f, -8f);
        public SpawnPattern[] spawnPatterns = // index by map 1–3
        {
            SpawnPattern.Diamond, SpawnPattern.Diamond, SpawnPattern.Diamond, SpawnPattern.Diamond
        };
        public int[] mobCounts = { 0, 4, 4, 4, 1 }; // map 4 ignored (boss)
        public float leashMul = 1f;
    }

    public static Layout ForWorld(int world)
    {
        world = Mathf.Clamp(world, 1, ProgressionFormulas.WorldCount);
        switch (world)
        {
            case 1: return Plains();
            case 2: return Forest();
            case 3: return Ruins();
            case 4: return Volcano();
            case 5: return Apocalypse();
            case 6: return Frost();
            case 7: return Desert();
            case 8: return Swamp();
            case 9: return Sky();
            default: return Abyss();
        }
    }

    static Layout Base()
    {
        var L = new Layout();
        for (int i = 1; i <= 4; i++)
            L.spawnOffsets[i] = new Vector3(0f, 0.5f, -4f);
        return L;
    }

    static void SetZones(Layout L, Vector3 m1, Vector3 m2, Vector3 m3, Vector3 m4)
    {
        L.zoneCenters[1] = m1;
        L.zoneCenters[2] = m2;
        L.zoneCenters[3] = m3;
        L.zoneCenters[4] = m4;
    }

    /// <summary>Classic crossroads — readable starter world.</summary>
    static Layout Plains()
    {
        var L = Base();
        SetZones(L,
            new Vector3(-24f, 0f, 0f),
            new Vector3(24f, 0f, 0f),
            new Vector3(0f, 0f, 24f),
            new Vector3(0f, 0f, -22f));
        L.paths = new[]
        {
            new PathSeg(-12f, 0f, 14f, 4f),
            new PathSeg(12f, 0f, 14f, 4f),
            new PathSeg(0f, 12f, 4f, 14f),
            new PathSeg(0f, -11f, 4f, 12f)
        };
        L.spawnPatterns = new[] { SpawnPattern.Diamond, SpawnPattern.Cluster, SpawnPattern.Line, SpawnPattern.Diamond };
        L.mobCounts = new[] { 0, 3, 4, 5, 1 };
        L.altarPos = new Vector3(10f, 0f, 10f);
        L.villageReturnPos = new Vector3(-8f, 0f, -8f);
        return L;
    }

    /// <summary>Zones pulled out; L-bend routes through the woods.</summary>
    static Layout Forest()
    {
        var L = Base();
        SetZones(L,
            new Vector3(-26f, 0f, 6f),
            new Vector3(28f, 0f, -4f),
            new Vector3(6f, 0f, 28f),
            new Vector3(-4f, 0f, -26f));
        L.hub = new Vector3(0f, 0f, 0f);
        L.hubScale = 6.5f;
        L.zoneSizes = new[] { 0f, 17f, 19f, 18f, 19f };
        L.paths = new[]
        {
            new PathSeg(-10f, 2f, 12f, 3.2f, 15f),
            new PathSeg(-18f, 5f, 10f, 3f, 25f),
            new PathSeg(10f, -1f, 12f, 3.2f, -12f),
            new PathSeg(20f, -3f, 10f, 3f, -8f),
            new PathSeg(2f, 10f, 3.2f, 12f, 8f),
            new PathSeg(5f, 20f, 3f, 10f, 12f),
            new PathSeg(-1f, -10f, 3.2f, 12f, -10f),
            new PathSeg(-3f, -18f, 3f, 10f, -6f),
            // cross connectors between bends
            new PathSeg(-6f, 8f, 8f, 3f, 40f),
            new PathSeg(8f, -8f, 8f, 3f, -35f)
        };
        L.blockers = new[]
        {
            new Blocker(-4f, 4f, 6f, 2.2f, 2.2f, 25f),
            new Blocker(5f, -5f, 5f, 2f, 2.5f, -20f),
            new Blocker(2f, 6f, 2.5f, 2.4f, 7f, 10f),
            new Blocker(-6f, -3f, 7f, 2f, 2.2f, -15f)
        };
        L.spawnPatterns = new[] { SpawnPattern.Cluster, SpawnPattern.WideRing, SpawnPattern.Flank, SpawnPattern.Diamond };
        L.mobCounts = new[] { 0, 4, 5, 5, 1 };
        L.spawnOffsets[1] = new Vector3(0f, 0.5f, -3f);
        L.spawnOffsets[2] = new Vector3(-2f, 0.5f, -4f);
        L.altarPos = new Vector3(12f, 0f, 8f);
        L.villageReturnPos = new Vector3(-10f, 0f, -6f);
        L.leashMul = 1.15f;
        return L;
    }

    /// <summary>Broken axes — settlement near hub, mine offset, diagonal approaches.</summary>
    static Layout Ruins()
    {
        var L = Base();
        SetZones(L,
            new Vector3(-18f, 0f, -6f),
            new Vector3(26f, 0f, 8f),
            new Vector3(-10f, 0f, 26f),
            new Vector3(8f, 0f, -24f));
        L.hubScale = 7f;
        L.zoneSizes = new[] { 0f, 16f, 18f, 19f, 20f };
        L.paths = new[]
        {
            new PathSeg(-8f, -2f, 12f, 3.5f, -20f),
            new PathSeg(10f, 3f, 14f, 3.5f, 18f),
            new PathSeg(-4f, 12f, 3.5f, 14f, -25f),
            new PathSeg(3f, -10f, 3.5f, 12f, 20f),
            new PathSeg(-12f, 10f, 10f, 3.2f, 55f),
            new PathSeg(14f, -8f, 10f, 3.2f, -50f)
        };
        L.blockers = new[]
        {
            new Blocker(0f, 4f, 8f, 2.6f, 1.8f, 0f),
            new Blocker(4f, 0f, 1.8f, 2.8f, 9f, 0f),
            new Blocker(-8f, 8f, 5f, 2.2f, 2f, 35f),
            new Blocker(10f, -6f, 2f, 2.5f, 6f, -20f)
        };
        L.spawnPatterns = new[] { SpawnPattern.Line, SpawnPattern.Flank, SpawnPattern.Arc, SpawnPattern.Diamond };
        L.mobCounts = new[] { 0, 3, 4, 5, 1 };
        L.altarPos = new Vector3(8f, 0f, 14f);
        L.villageReturnPos = new Vector3(-6f, 0f, -10f);
        return L;
    }

    /// <summary>Ring around hub; crater boss pocket further south.</summary>
    static Layout Volcano()
    {
        var L = Base();
        SetZones(L,
            new Vector3(-22f, 0f, 10f),
            new Vector3(22f, 0f, 10f),
            new Vector3(0f, 0f, 26f),
            new Vector3(0f, 0f, -28f));
        L.hubScale = 10f;
        L.zoneSizes = new[] { 0f, 17f, 17f, 18f, 21f };
        L.paths = new[]
        {
            // ring segments
            new PathSeg(-8f, 8f, 10f, 3.5f, 45f),
            new PathSeg(8f, 8f, 10f, 3.5f, -45f),
            new PathSeg(8f, -6f, 10f, 3.5f, 45f),
            new PathSeg(-8f, -6f, 10f, 3.5f, -45f),
            new PathSeg(0f, 14f, 4f, 10f),
            new PathSeg(0f, -14f, 4.5f, 12f),
            new PathSeg(-14f, 8f, 10f, 3.5f, 20f),
            new PathSeg(14f, 8f, 10f, 3.5f, -20f)
        };
        L.blockers = new[]
        {
            new Blocker(-5f, 0f, 3f, 2.4f, 8f, 0f),
            new Blocker(5f, 0f, 3f, 2.4f, 8f, 0f),
            new Blocker(0f, -4f, 10f, 2f, 3f, 0f),
            new Blocker(-12f, -2f, 4f, 2.2f, 4f, 30f),
            new Blocker(12f, -2f, 4f, 2.2f, 4f, -30f)
        };
        L.spawnPatterns = new[] { SpawnPattern.Arc, SpawnPattern.Arc, SpawnPattern.WideRing, SpawnPattern.Diamond };
        L.mobCounts = new[] { 0, 4, 4, 5, 1 };
        L.spawnOffsets[4] = new Vector3(0f, 0.5f, -5f);
        L.altarPos = new Vector3(14f, 0f, 0f);
        L.villageReturnPos = new Vector3(-12f, 0f, -4f);
        return L;
    }

    /// <summary>Skewed diamond — chaotic offsets.</summary>
    static Layout Apocalypse()
    {
        var L = Base();
        SetZones(L,
            new Vector3(-28f, 0f, -8f),
            new Vector3(24f, 0f, 12f),
            new Vector3(12f, 0f, 26f),
            new Vector3(-8f, 0f, -26f));
        L.hub = new Vector3(2f, 0f, 2f);
        L.hubScale = 7f;
        L.paths = new[]
        {
            new PathSeg(-10f, -2f, 14f, 3.4f, -25f),
            new PathSeg(10f, 6f, 14f, 3.4f, 30f),
            new PathSeg(6f, 12f, 3.4f, 12f, 20f),
            new PathSeg(-2f, -10f, 3.4f, 14f, -15f),
            new PathSeg(-14f, 6f, 12f, 3.2f, 50f),
            new PathSeg(14f, -4f, 12f, 3.2f, -40f)
        };
        L.blockers = new[]
        {
            new Blocker(0f, 0f, 5f, 3f, 5f, 20f),
            new Blocker(-8f, 10f, 6f, 2.5f, 2.5f, 0f),
            new Blocker(10f, -8f, 2.5f, 2.5f, 8f, 15f),
            new Blocker(-4f, -8f, 8f, 2.2f, 2.5f, -25f)
        };
        L.spawnPatterns = new[] { SpawnPattern.Flank, SpawnPattern.Cluster, SpawnPattern.Line, SpawnPattern.Diamond };
        L.mobCounts = new[] { 0, 4, 5, 5, 1 };
        L.altarPos = new Vector3(-10f, 0f, 12f);
        L.villageReturnPos = new Vector3(8f, 0f, -10f);
        return L;
    }

    /// <summary>Frozen lake blocks east straight path; map2 swung NE.</summary>
    static Layout Frost()
    {
        var L = Base();
        SetZones(L,
            new Vector3(-26f, 0f, 4f),
            new Vector3(22f, 0f, 18f),
            new Vector3(-8f, 0f, 28f),
            new Vector3(4f, 0f, -24f));
        L.hubScale = 8f;
        L.paths = new[]
        {
            new PathSeg(-12f, 2f, 14f, 3.5f, 10f),
            new PathSeg(6f, 8f, 3.5f, 12f, 0f),
            new PathSeg(12f, 14f, 12f, 3.5f, 35f),
            new PathSeg(-4f, 14f, 3.5f, 12f, -15f),
            new PathSeg(2f, -10f, 3.5f, 12f, 8f),
            new PathSeg(8f, 4f, 10f, 3.2f, 50f)
        };
        // "Lake" ridge forcing northward detour to map2
        L.blockers = new[]
        {
            new Blocker(10f, 2f, 14f, 1.8f, 6f, 0f),
            new Blocker(14f, 6f, 6f, 2f, 8f, 0f),
            new Blocker(-2f, 6f, 4f, 2.2f, 4f, 0f),
            new Blocker(0f, -6f, 8f, 2f, 3f, 15f)
        };
        L.spawnPatterns = new[] { SpawnPattern.Line, SpawnPattern.WideRing, SpawnPattern.Arc, SpawnPattern.Diamond };
        L.mobCounts = new[] { 0, 3, 4, 5, 1 };
        L.altarPos = new Vector3(-12f, 0f, 10f);
        L.villageReturnPos = new Vector3(-6f, 0f, -8f);
        return L;
    }

    /// <summary>Wide desert spacing with diagonal dune corridors.</summary>
    static Layout Desert()
    {
        var L = Base();
        SetZones(L,
            new Vector3(-28f, 0f, 8f),
            new Vector3(28f, 0f, -8f),
            new Vector3(10f, 0f, 28f),
            new Vector3(-10f, 0f, -28f));
        L.hubScale = 5.5f;
        L.zoneSizes = new[] { 0f, 16f, 16f, 17f, 20f };
        L.paths = new[]
        {
            new PathSeg(-10f, 4f, 16f, 3.2f, 20f),
            new PathSeg(10f, -4f, 16f, 3.2f, -20f),
            new PathSeg(4f, 12f, 3.2f, 14f, 15f),
            new PathSeg(-4f, -12f, 3.2f, 14f, -15f),
            new PathSeg(0f, 0f, 18f, 3.2f, 45f),
            new PathSeg(0f, 0f, 18f, 3.2f, -45f)
        };
        L.blockers = new[]
        {
            new Blocker(-6f, -6f, 8f, 2.2f, 4f, 30f),
            new Blocker(6f, 6f, 8f, 2.2f, 4f, 30f),
            new Blocker(8f, -10f, 4f, 2.4f, 8f, -20f),
            new Blocker(-10f, 8f, 4f, 2.4f, 8f, -20f),
            new Blocker(0f, 8f, 6f, 2f, 3f, 0f)
        };
        L.spawnPatterns = new[] { SpawnPattern.WideRing, SpawnPattern.Line, SpawnPattern.Flank, SpawnPattern.Diamond };
        L.mobCounts = new[] { 0, 3, 4, 4, 1 };
        L.altarPos = new Vector3(6f, 0f, 6f);
        L.villageReturnPos = new Vector3(-8f, 0f, -4f);
        L.leashMul = 1.25f;
        return L;
    }

    /// <summary>Staggered pads connected by short bridge-like paths.</summary>
    static Layout Swamp()
    {
        var L = Base();
        SetZones(L,
            new Vector3(-22f, 0f, -10f),
            new Vector3(20f, 0f, 14f),
            new Vector3(-14f, 0f, 24f),
            new Vector3(12f, 0f, -24f));
        L.hub = new Vector3(-2f, 0f, 2f);
        L.hubScale = 6f;
        L.paths = new[]
        {
            new PathSeg(-10f, -2f, 10f, 3f, -30f),
            new PathSeg(6f, 6f, 10f, 3f, 35f),
            new PathSeg(-6f, 12f, 3f, 10f, -20f),
            new PathSeg(4f, -8f, 3f, 10f, 25f),
            new PathSeg(-8f, 6f, 8f, 3f, 55f),
            new PathSeg(8f, -2f, 8f, 3f, -40f),
            new PathSeg(0f, 0f, 8f, 3f, 10f)
        };
        L.blockers = new[]
        {
            new Blocker(2f, -2f, 10f, 1.6f, 5f, 10f),
            new Blocker(-8f, 4f, 5f, 1.8f, 8f, 0f),
            new Blocker(6f, 10f, 7f, 1.7f, 4f, -15f),
            new Blocker(-4f, -10f, 4f, 1.8f, 6f, 20f)
        };
        L.spawnPatterns = new[] { SpawnPattern.Cluster, SpawnPattern.Arc, SpawnPattern.Flank, SpawnPattern.Diamond };
        L.mobCounts = new[] { 0, 4, 5, 5, 1 };
        L.altarPos = new Vector3(10f, 0f, -6f);
        L.villageReturnPos = new Vector3(-10f, 0f, 6f);
        return L;
    }

    /// <summary>Spread sky pads — longer hops between zones.</summary>
    static Layout Sky()
    {
        var L = Base();
        SetZones(L,
            new Vector3(-28f, 0f, -4f),
            new Vector3(28f, 0f, 6f),
            new Vector3(0f, 0f, 30f),
            new Vector3(0f, 0f, -30f));
        L.hubScale = 9f;
        L.zoneSizes = new[] { 0f, 15f, 15f, 16f, 18f };
        L.paths = new[]
        {
            new PathSeg(-12f, -1f, 14f, 2.8f, -8f),
            new PathSeg(12f, 2f, 14f, 2.8f, 10f),
            new PathSeg(0f, 14f, 2.8f, 14f),
            new PathSeg(0f, -14f, 2.8f, 14f),
            // floating-looking side hops
            new PathSeg(-8f, 8f, 8f, 2.6f, 40f),
            new PathSeg(8f, -8f, 8f, 2.6f, 40f)
        };
        L.blockers = new[]
        {
            new Blocker(-6f, 4f, 3f, 3.5f, 3f, 0f),
            new Blocker(6f, -4f, 3f, 3.5f, 3f, 0f),
            new Blocker(4f, 8f, 3f, 3.2f, 3f, 0f),
            new Blocker(-4f, -8f, 3f, 3.2f, 3f, 0f)
        };
        L.spawnPatterns = new[] { SpawnPattern.Line, SpawnPattern.WideRing, SpawnPattern.Arc, SpawnPattern.Diamond };
        L.mobCounts = new[] { 0, 3, 4, 4, 1 };
        L.altarPos = new Vector3(10f, 0f, -10f);
        L.villageReturnPos = new Vector3(-10f, 0f, 8f);
        L.leashMul = 1.1f;
        return L;
    }

    /// <summary>Asymmetric abyss — narrow choke into boss sanctum.</summary>
    static Layout Abyss()
    {
        var L = Base();
        SetZones(L,
            new Vector3(-24f, 0f, 12f),
            new Vector3(26f, 0f, -6f),
            new Vector3(8f, 0f, 26f),
            new Vector3(-6f, 0f, -28f));
        L.hub = new Vector3(0f, 0f, -2f);
        L.hubScale = 6f;
        L.zoneSizes = new[] { 0f, 17f, 18f, 17f, 21f };
        L.paths = new[]
        {
            new PathSeg(-10f, 4f, 12f, 3.2f, 25f),
            new PathSeg(10f, -3f, 14f, 3.2f, -15f),
            new PathSeg(3f, 10f, 3.2f, 12f, 10f),
            // choke corridor to boss
            new PathSeg(-2f, -12f, 3.8f, 14f, -8f),
            new PathSeg(-4f, -20f, 3.5f, 8f, -5f),
            new PathSeg(8f, 6f, 8f, 3f, 40f)
        };
        L.blockers = new[]
        {
            new Blocker(-6f, -10f, 8f, 3f, 2.5f, 0f),
            new Blocker(4f, -10f, 8f, 3f, 2.5f, 0f),
            new Blocker(-1f, -16f, 2.5f, 3.2f, 6f, 0f),
            new Blocker(3f, -16f, 2.5f, 3.2f, 6f, 0f),
            new Blocker(8f, 0f, 5f, 2.8f, 5f, 20f),
            new Blocker(-10f, 2f, 4f, 2.6f, 6f, -15f)
        };
        L.spawnPatterns = new[] { SpawnPattern.Flank, SpawnPattern.Cluster, SpawnPattern.Line, SpawnPattern.Diamond };
        L.mobCounts = new[] { 0, 4, 5, 5, 1 };
        L.spawnOffsets[4] = new Vector3(0f, 0.5f, -3f);
        L.altarPos = new Vector3(12f, 0f, 10f);
        L.villageReturnPos = new Vector3(-8f, 0f, 0f);
        L.leashMul = 1.2f;
        return L;
    }

    public static Vector3[] MobLocalSpots(SpawnPattern pattern, int count)
    {
        count = Mathf.Clamp(count, 1, 8);
        var spots = new Vector3[count];
        switch (pattern)
        {
            case SpawnPattern.Line:
                for (int i = 0; i < count; i++)
                {
                    float t = count == 1 ? 0f : (i / (float)(count - 1) - 0.5f);
                    spots[i] = new Vector3(t * 10f, 0f, 1.5f);
                }
                break;
            case SpawnPattern.Arc:
                for (int i = 0; i < count; i++)
                {
                    float a = Mathf.Lerp(-0.9f, 0.9f, count == 1 ? 0.5f : i / (float)(count - 1));
                    spots[i] = new Vector3(Mathf.Sin(a) * 6f, 0f, 2f + Mathf.Cos(a) * 3f);
                }
                break;
            case SpawnPattern.Cluster:
                for (int i = 0; i < count; i++)
                {
                    float a = i / (float)count * Mathf.PI * 2f;
                    spots[i] = new Vector3(Mathf.Cos(a) * 2.2f, 0f, Mathf.Sin(a) * 2.2f + 1f);
                }
                break;
            case SpawnPattern.WideRing:
                for (int i = 0; i < count; i++)
                {
                    float a = i / (float)count * Mathf.PI * 2f + 0.2f;
                    spots[i] = new Vector3(Mathf.Cos(a) * 6.5f, 0f, Mathf.Sin(a) * 6.5f);
                }
                break;
            case SpawnPattern.Flank:
                for (int i = 0; i < count; i++)
                {
                    bool left = i % 2 == 0;
                    int row = i / 2;
                    spots[i] = new Vector3(left ? -5f : 5f, 0f, 2f - row * 2.5f);
                }
                break;
            default: // Diamond
                Vector3[] diamond =
                {
                    new Vector3(-4f, 0f, 2f),
                    new Vector3(4f, 0f, 2f),
                    new Vector3(-3f, 0f, -3f),
                    new Vector3(3f, 0f, -3f),
                    new Vector3(0f, 0f, 4f),
                    new Vector3(0f, 0f, -4f)
                };
                for (int i = 0; i < count; i++)
                    spots[i] = diamond[i % diamond.Length];
                break;
        }
        return spots;
    }
}
