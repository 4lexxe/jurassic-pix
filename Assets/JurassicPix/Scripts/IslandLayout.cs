using UnityEngine;

namespace JurassicPix
{
    public static class IslandLayout
    {
        public const int TerrainLayer = 9;
        public const float Scale = 4.89898f;
        public const float Radius = 424 * Scale, South = 0, North = 2 * Radius;
        private const float TravelLimit = 486 * Scale;
        public static readonly Vector2 Center = ToWorld(new Vector2(0, 424));
        // Lake centres in layout units with their half sizes in metres.
        private static readonly Vector4[] Lakes =
        {
            new(145, 387, 186, 132), new(-60, 230, 105, 78), new(-20, 540, 122, 86), new(-285, 340, 92, 70), new(150, 130, 112, 80),
            // Charcas chicas cerca de las manadas: bahía del sur, llano centro-oeste y meseta norte.
            new(30, 130, 52, 40), new(-160, 470, 58, 44), new(-40, 662, 56, 42)
        };
        public static int LakeCount => Lakes.Length;
        public static Vector2 LakeCenter(int lake) => ToWorld(new Vector2(Lakes[lake].x, Lakes[lake].y));
        public static Vector2 LakeSize(int lake) => new(Lakes[lake].z, Lakes[lake].w);
        public static float LakeLevel(int lake) => 16 + 17 * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(420, 704, Lakes[lake].y));
        public static Rect Bounds => new(-466 * Scale, -42 * Scale, 932 * Scale, 932 * Scale);
        private static readonly Vector2[] Sites =
        {
            new(-12, 141), new(-242, 344), new(-71, 659), new(239, 359), new(150, 585), new(-69, 435)
        };

        public static Vector2 ToWorld(Vector2 layout) => layout * Scale;

        public static Vector3 ToWorld(Vector3 layout) => new(layout.x * Scale, layout.y, layout.z * Scale);

        public static Vector2 RegionCenter(int index) => ToWorld(Sites[index]);

        public static int RegionAt(Vector2 position)
        {
            position /= Scale;
            int result = 0;
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < Sites.Length; i++)
            {
                float distance = (Sites[i] - position).sqrMagnitude;
                if (distance >= nearest)
                    continue;
                nearest = distance;
                result = i;
            }
            return result;
        }

        public static float ShoreDistance(Vector2 point)
        {
            Vector2 p = point - Center;
            float angle = Mathf.Atan2(p.y, p.x);
            float channel = Mathf.Sin(angle);
            float irregular = (.044f * Mathf.Sin(angle * 3 + .48f) + .036f * Mathf.Sin(angle * 7 - .9f)
                - .047f * Mathf.Cos(angle * 4 + .85f)) * channel * channel;
            return Radius * (1 + irregular) - p.magnitude;
        }

        public static float LagoonDistance(Vector2 point) => LagoonDistance(point, out _);

        public static float LagoonDistance(Vector2 point, out int lake)
        {
            float nearest = float.NegativeInfinity;
            lake = 0;
            for (int index = 0; index < Lakes.Length; index++)
            {
                Vector2 p = point - LakeCenter(index);
                Vector2 size = LakeSize(index);
                float angle = Mathf.Atan2(p.y, p.x);
                float edge = 1 + .094f * Mathf.Sin(3 * angle + .48f) + .04f * Mathf.Cos(5 * angle);
                float distance = (edge - new Vector2(p.x / size.x, p.y / size.y).magnitude) * size.y;
                if (distance <= nearest)
                    continue;
                nearest = distance;
                lake = index;
            }
            return nearest;
        }

        public static float SurfaceLevel(Vector2 point) => LagoonDistance(point, out int lake) > -16 ? LakeLevel(lake) : 0;

        public static Vector2 NearestLake(Vector2 point)
        {
            LagoonDistance(point, out int lake);
            return LakeCenter(lake);
        }

        private static Vector4[] lakePlaces;
        private static float[] lakeLevels;

        public static void Publish()
        {
            if (lakePlaces == null)
            {
                lakePlaces = new Vector4[8];
                lakeLevels = new float[8];
                for (int index = 0; index < Lakes.Length; index++)
                {
                    Vector2 center = LakeCenter(index);
                    lakePlaces[index] = new Vector4(center.x, center.y, Lakes[index].z, Lakes[index].w);
                    lakeLevels[index] = LakeLevel(index);
                }
            }
            Shader.SetGlobalVectorArray("_IslandLakes", lakePlaces);
            Shader.SetGlobalFloatArray("_IslandLakeLevels", lakeLevels);
        }

        private static readonly Vector2[] Glades = { new(30, 300), new(40, 610), new(-150, 250), new(140, 240) };

        public static bool Clearing(Vector2 point)
        {
            foreach (Vector2 glade in Glades)
                if ((point - ToWorld(glade)).sqrMagnitude < 85 * 85)
                    return true;
            return (point - RegionCenter(RegionAt(point))).sqrMagnitude < 125 * 125;
        }

        public static Vector3 LimitVelocity(Vector3 point, Vector3 velocity)
        {
            Vector2 offset = new(point.x - Center.x, point.z - Center.y);
            if (offset.sqrMagnitude < TravelLimit * TravelLimit)
                return velocity;
            Vector3 outward = new(offset.x, 0, offset.y);
            outward.Normalize();
            return velocity - outward * Mathf.Max(0, Vector3.Dot(velocity, outward));
        }
    }
}
