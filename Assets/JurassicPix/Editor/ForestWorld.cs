using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace JurassicPix.Editor
{
    internal sealed class ForestWorld
    {
        public const string Folder = "Assets/JurassicPix/Models/Forest";
        public const string TerrainMeshPrefix = "Plataforma insular ";
        private const string Generated = "Assets/JurassicPix/Generated/Forest";
        private static readonly string[] Ponderosas = { "Ponderosa", "PonderosaTall", "PonderosaWide" };
        private enum Flora { Woodland, Ponderosa, Alpine }

        private static readonly (string Model, float Low, float High)[][] Trees =
        {
            new[]
            {
                ("AlpineFir", 11f, 21f), ("GroveFir", 14f, 26f), ("AlpinePine", 15f, 27f), ("GroveSpruce", 12f, 20f),
                ("AlpineSlim", 12f, 22f), ("GroveTree1", 15f, 27f), ("GroveBirch", 9f, 15f), ("AlpineFir", 9f, 16f),
                ("GroveTree5", 13f, 22f), ("GroveTree6", 17f, 30f), ("GroveYoungFir", 8f, 13f), ("AlpinePine", 13f, 22f)
            },
            new[]
            {
                ("Ponderosa", 13f, 23f), ("PonderosaTall", 15f, 26f), ("GroveBirch", 9f, 15f), ("PonderosaWide", 12f, 21f),
                ("GroveTree2", 14f, 24f), ("PonderosaYoung", 6f, 11f), ("GroveTree5", 12f, 22f), ("PonderosaSapling", 4f, 8f),
                ("AlpinePine", 14f, 22f), ("Ponderosa", 12f, 20f)
            },
            new[]
            {
                ("AlpineFir", 10f, 19f), ("GroveTree3", 11f, 19f), ("AlpinePine", 15f, 27f), ("GroveYoungFir", 8f, 13f),
                ("AlpineFir", 8f, 15f), ("GroveTree7", 15f, 26f), ("AlpineSlim", 12f, 22f), ("GroveTree4", 14f, 25f),
                ("GroveTree8", 15f, 26f), ("GroveFir", 13f, 22f)
            }
        };
        private static readonly (string Model, float Low, float High)[][] Plants =
        {
            new[]
            {
                ("AlpineBush", .9f, 2f), ("GroveBush", 1.2f, 2.4f), ("GroveThicket", 1.2f, 2.6f), ("GroveWeed", 1.2f, 2.2f),
                ("AlpineBush", .8f, 1.5f), ("GroveShrub", 2.5f, 5f), ("GroveThicket", 1f, 2f), ("AlpineFir", 1.6f, 3.2f)
            },
            new[]
            {
                ("GroveThicket", 1.2f, 2.6f), ("PonderosaSapling", 1.6f, 3.2f), ("AlpineBush", .8f, 1.6f), ("GroveBush", 1.2f, 2.2f),
                ("GroveShrub", 2.5f, 4.5f), ("GroveThicket", 1f, 2f), ("AlpineBush", .7f, 1.3f), ("GroveWeed", 1f, 1.8f)
            },
            new[]
            {
                ("AlpineBush", 1.1f, 2.4f), ("GroveWeed", 1.2f, 2.4f), ("GroveShrub", 2.5f, 5f), ("AlpineFir", 1.6f, 3.4f),
                ("AlpineBush", .9f, 1.8f), ("GroveBush", 1.1f, 2f), ("GroveWeed", 1f, 1.8f), ("GroveThicket", 1f, 2.2f)
            }
        };
        private static readonly (string Model, float Low, float High, Vector3 Share)[] Tufts =
        {
            ("WoodGrass", .5f, 1.1f, new Vector3(4, 1, 2)), ("WoodReeds", .5f, 1f, new Vector3(3, 1, 2)),
            ("PineGrass", .4f, .75f, new Vector3(2, 5, 1)), ("PineReeds", .5f, .95f, new Vector3(1, 3, 2)),
            ("GroveGrass", .6f, 1.1f, new Vector3(4, 3, 4))
        };
        private static readonly string[] Stones = { "MossBoulder", "GroveStone", "MossBoulder", "GroveStoneFlat" };
        private static readonly string[] AlpineStones = { "AlpineBoulder", "GroveStoneTall", "AlpineBoulder", "GroveStone" };

        private readonly Transform root;
        private readonly System.Random random = new(70723);
        private readonly Dictionary<string, Model> models = new();
        private readonly Dictionary<string, Material> materials = new();
        private readonly List<Vector2> trees = new();
        private readonly Dictionary<Vector2Int, List<Vector2>> treeGrid = new();
        private readonly List<string> canopyKinds = new();
        private readonly List<float> canopyReach = new();
        private readonly List<Vector3> canopyPlaces = new(), canopyShapes = new();
        private readonly List<ushort> canopyModels = new();
        private readonly List<float> canopyTrunks = new();
        // Troncos de los árboles instanciados: ya no son objetos de la escena, pero la navegación los sigue esquivando.
        internal static readonly List<(Vector3 Base, float Radius, float Height)> Trunks = new();
        private readonly List<(Vector2 Center, float Radius)> giantTrunks = new();

        private sealed class Model
        {
            public GameObject Prefab;
            public Bounds Bounds;
        }

        private ForestWorld(Transform root) => this.root = root;

        public static Texture2D Create(Transform root)
        {
            if (!AssetDatabase.IsValidFolder(Generated))
                AssetDatabase.CreateFolder("Assets/JurassicPix/Generated", "Forest");

            Trunks.Clear();
            var forest = new ForestWorld(root);
            forest.AddTerrain();
            forest.AddRelief();
            Physics.SyncTransforms();
            forest.AddVegetation();
            return forest.BakeIslandSurfaces();
        }

        public static float Height(float x, float z)
        {
            if (Physics.Raycast(new Vector3(x, 900, z), Vector3.down, out RaycastHit hit, 1000, 1 << Dinosaur.TerrainLayer))
                return hit.point.y;
            throw new InvalidOperationException($"El terreno importado no cubre ({x:0.0}, {z:0.0}).");
        }

        public static Texture2D LoadSky(string name) => Load<Texture2D>($"{Folder}/Skies/{name}.hdr");

        private void AddTerrain()
        {
            Rect world = IslandLayout.Bounds;
            Model source = GetModel("Terrain");
            Transform reference = Group("Muestra de Pine Forest at Sunset");
            reference.localScale = new Vector3(world.width / source.Bounds.size.x, 1, world.height / source.Bounds.size.z);
            reference.position = new Vector3(world.center.x, 0, world.center.y) -
                Vector3.Scale(reference.localScale, source.Bounds.center);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source.Prefab, reference);
            MeshFilter sourceGeometry = instance.GetComponentInChildren<MeshFilter>();
            var sample = sourceGeometry.gameObject.AddComponent<MeshCollider>();
            sample.sharedMesh = sourceGeometry.sharedMesh;
            Physics.SyncTransforms();
            Mesh[] meshes = BuildIslandTerrain(world, sample);
            UnityEngine.Object.DestroyImmediate(reference.gameObject);

            Transform terrain = Group("Isla · relieve de Pine Forest at Sunset");
            Material ground = MaterialFor("Terrain");
            foreach (Mesh mesh in meshes)
            {
                var tile = new GameObject(mesh.name, typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
                tile.transform.SetParent(terrain, false);
                tile.layer = Dinosaur.TerrainLayer;
                tile.GetComponent<MeshFilter>().sharedMesh = mesh;
                tile.GetComponent<MeshCollider>().sharedMesh = mesh;
                var renderer = tile.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = ground;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
            Physics.SyncTransforms();
        }

        private static Mesh[] BuildIslandTerrain(Rect world, MeshCollider source)
        {
            const int tilesPerSide = 8;
            const int tileCells = 96;
            const int cells = tilesPerSide * tileCells;
            var samples = new Vector3[cells + 1, cells + 1];
            var uv = new Vector2[cells + 1, cells + 1];
            for (int z = 0; z <= cells; z++)
            for (int x = 0; x <= cells; x++)
            {
                uv[x, z] = new Vector2(x / (float)cells, z / (float)cells);
                Vector2 point = world.position + Vector2.Scale(uv[x, z], world.size);
                float original = source.Raycast(new Ray(new Vector3(point.x, 110, point.y), Vector3.down), out RaycastHit hit, 220)
                    ? Mathf.Clamp(hit.point.y, -12, 19) : 0;
                samples[x, z] = new Vector3(point.x, Heightfield(point, original), point.y);
            }

            string path = Generated + "/IslandTerrain.asset";
            var main = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            var saved = new Dictionary<string, Mesh>();
            if (main != null)
            {
                foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (asset is Mesh mesh)
                        saved.Add(mesh.name, mesh);
            }
            var meshes = new Mesh[tilesPerSide * tilesPerSide];
            for (int row = 0; row < tilesPerSide; row++)
            for (int column = 0; column < tilesPerSide; column++)
            {
                int index = row * tilesPerSide + column;
                string name = TerrainMeshPrefix + $"{column + 1}–{row + 1}";
                Mesh tile = TerrainTile(name, samples, uv, column * tileCells, row * tileCells, tileCells);
                saved.TryGetValue(name, out Mesh prior);
                prior = index == 0 ? main : prior;
                if (prior != null)
                {
                    EditorUtility.CopySerialized(tile, prior);
                    EditorUtility.SetDirty(prior);
                    UnityEngine.Object.DestroyImmediate(tile);
                    meshes[index] = prior;
                    continue;
                }
                if (index == 0)
                    AssetDatabase.CreateAsset(tile, path);
                else
                    AssetDatabase.AddObjectToAsset(tile, path);
                meshes[index] = tile;
            }
            return meshes;
        }

        private static Mesh TerrainTile(string name, Vector3[,] samples, Vector2[,] sourceUV, int originX, int originZ, int cells)
        {
            int side = cells + 1;
            int last = samples.GetLength(0) - 1;
            var vertices = new Vector3[side * side];
            var normals = new Vector3[vertices.Length];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[(side - 1) * (side - 1) * 6];
            int face = 0;
            for (int row = 0; row < side; row++)
            for (int column = 0; column < side; column++)
            {
                int vertex = row * side + column;
                int x = originX + column;
                int z = originZ + row;
                vertices[vertex] = samples[x, z];
                uv[vertex] = sourceUV[x, z];
                // At chunk boundaries both sides sample the same neighbours, keeping the lighting continuous.
                Vector3 east = samples[Mathf.Min(last, x + 1), z] - samples[Mathf.Max(0, x - 1), z];
                Vector3 north = samples[x, Mathf.Min(last, z + 1)] - samples[x, Mathf.Max(0, z - 1)];
                normals[vertex] = Vector3.Cross(north, east).normalized;
                if (row == side - 1 || column == side - 1)
                    continue;
                triangles[face++] = vertex;
                triangles[face++] = vertex + side;
                triangles[face++] = vertex + 1;
                triangles[face++] = vertex + 1;
                triangles[face++] = vertex + side;
                triangles[face++] = vertex + side + 1;
            }

            var mesh = new Mesh { name = name, vertices = vertices, normals = normals, uv = uv, triangles = triangles };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static float Heightfield(Vector2 point, float original)
        {
            float coast = IslandLayout.ShoreDistance(point);
            if (coast <= 0)
                return Mathf.Max(-42, coast * .37f);
            float shoulder = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(12, 89, coast));
            Vector2 layout = point / IslandLayout.Scale;
            float rolling = Mathf.PerlinNoise(layout.x * .0072f + 17, layout.y * .0069f + 24) * 14
                + Mathf.PerlinNoise(point.x * .011f + 42, point.y * .011f + 54) * 3;
            float rise = 18 * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(420, 704, layout.y));
            float terrain = Mathf.Min(4.7f, coast * .24f) + shoulder * (10 + rise + rolling + original * .87f);
            float lagoon = IslandLayout.LagoonDistance(point, out int lake);
            if (lagoon > -90)
            {
                float bed = IslandLayout.LakeLevel(lake) - Mathf.Clamp(lagoon * .2f, -2.5f, 10);
                terrain = Mathf.Lerp(terrain, bed, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-90, -6, lagoon)));
            }
            return terrain;
        }

        private void AddRelief()
        {
            Transform hills = Group("Cordillera y laderas");
            Relief(hills, "Cordillera del norte", "MountainRange", new Vector3(-5, 8, 733), new Vector3(290, 235, 112));
            Relief(hills, "Cordillera occidental", "MountainRange", new Vector3(-171, 12, 513), new Vector3(135, 160, 176));
            Relief(hills, "Pico oriental", "HikingPeak", new Vector3(262, 9, 580), new Vector3(145, 135, 175));
            Transform slope = Relief(hills, "Laderas del bosque", "LandscapeTerrain", new Vector3(-284, 8, 246),
                new Vector3(116, 33, 105));
            Instantiate("LandscapePlants", slope);
            Relief(hills, "Cerros del sudeste", "HikingPeak", new Vector3(246, 6, 198), new Vector3(96, 120, 88), 180);
            Relief(hills, "Sierra del noroeste", "MountainRange", new Vector3(-234, 7, 708), new Vector3(104, 140, 84), 180);
            Relief(hills, "Farallón occidental", "HikingPeak", new Vector3(-342, 6, 438), new Vector3(78, 125, 104));
            Relief(hills, "Cerro del sudoeste", "MountainRange", new Vector3(-180, 6, 126), new Vector3(92, 95, 80));
            Relief(hills, "Acantilados del este", "MountainRange", new Vector3(354, 6, 426), new Vector3(66, 105, 96), 180);
            Relief(hills, "Macizo central", "HikingPeak", new Vector3(55, 8, 462), new Vector3(84, 165, 80), 90);
            Relief(hills, "Sierra del sur", "MountainRange", new Vector3(100, 6, 62), new Vector3(70, 110, 56), 180);
            Relief(hills, "Picos del noreste", "HikingPeak", new Vector3(204, 7, 720), new Vector3(80, 135, 64));
            Relief(hills, "Cumbre del este", "HikingPeak", new Vector3(330, 6, 255), new Vector3(60, 125, 60), 180);
            Relief(hills, "Cordón del oeste", "MountainRange", new Vector3(-330, 7, 560), new Vector3(52, 140, 76), 90);
        }

        private Transform Relief(Transform parent, string name, string model, Vector3 origin, Vector3 size, float yaw = 0)
        {
            origin = IslandLayout.ToWorld(origin);
            size = IslandLayout.ToWorld(size);
            size.y *= 1.9f;
            Bounds bounds = GetModel(model).Bounds;
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            group.localScale = new Vector3(size.x / bounds.size.x, size.y / bounds.size.y, size.z / bounds.size.z);
            group.rotation = Quaternion.Euler(0, yaw, 0);
            group.position = origin - group.rotation * Vector3.Scale(group.localScale, new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
            GameObject instance = Instantiate(model, group);
            foreach (MeshFilter mesh in instance.GetComponentsInChildren<MeshFilter>())
            {
                mesh.gameObject.layer = Dinosaur.TerrainLayer;
                mesh.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh.sharedMesh;
            }
            return group;
        }

        private void AddVegetation()
        {
            Transform giants = Group("Arboleda de gigantes");
            Transform undergrowth = Group("Arbustos y sotobosque");
            Transform landmarks = Group("Rocas y troncos caídos");

            Mesh trunk = GiantTrunk();
            Vector2[] greatTrees =
            {
                new(-58, 129), new(43, 118), new(-95, 225), new(96, 207), new(-311, 329),
                new(-165, 350), new(-334, 482), new(-238, 577), new(-153, 638), new(-104, 735),
                new(125, 669), new(236, 686), new(323, 429), new(245, 482), new(238, 240),
                new(26, 353), new(35, 458), new(-16, 523), new(-123, 470), new(139, 545),
                new(-52, 187), new(116, 275), new(-149, 283), new(-270, 398), new(-211, 439),
                new(-282, 555), new(-144, 569), new(-41, 735), new(130, 736), new(294, 507),
                new(273, 465), new(115, 476), new(65, 528), new(184, 287),
                new(-126, 282), new(18, 300), new(126, 270), new(90, 492), new(102, 66),
                new(204, 720), new(324, 252), new(-300, 262), new(-200, 640), new(60, 760)
            };
            foreach (Vector2 location in greatTrees)
            {
                Vector2 point = IslandLayout.ToWorld(location);
                if (!Plantable(point, 19))
                    continue;
                string crown = FloraAt(point) == Flora.Alpine ? "AlpineFir" : "GroveFir";
                Giant(giants, trunk, crown, point.x, point.y, Range(89, 121), Range(5.7f, 8.0f));
            }
            for (int index = 0; index < 100000; index++)
            {
                float z = Range(IslandLayout.South, IslandLayout.North);
                float x = Range(-IslandLayout.Radius, IslandLayout.Radius);
                var point = new Vector2(x, z);
                if (!Plantable(point, 13) || IslandLayout.Clearing(point) ||
                    Crowded(point) ||
                    giantTrunks.Exists(giant => (giant.Center - point).sqrMagnitude < giant.Radius * giant.Radius))
                    continue;

                var species = Trees[(int)FloraAt(point)];
                var tree = species[index % species.Length];
                Tree(tree.Model, x, z, Range(tree.Low, tree.High));
            }

            for (int index = 0; index < 30000; index++)
            {
                float z = Range(IslandLayout.South, IslandLayout.North);
                float x = Range(-IslandLayout.Radius, IslandLayout.Radius);
                var point = new Vector2(x, z);
                if (!Plantable(point, 4))
                    continue;
                var cover = Plants[(int)FloraAt(point)];
                var plant = cover[index % cover.Length];
                Plant(plant.Model, x, z, Range(plant.Low, plant.High), 380);
            }
            for (int index = 0; index < 1500; index++)
            {
                float z = Range(IslandLayout.South, IslandLayout.North);
                float x = Range(-IslandLayout.Radius, IslandLayout.Radius);
                var point = new Vector2(x, z);
                if (!Plantable(point, 4) || IslandLayout.Clearing(point))
                    continue;
                bool log = index % 5 == 0;
                string[] stones = FloraAt(point) == Flora.Alpine ? AlpineStones : Stones;
                string prefab = log ? "PineLog" : stones[index % stones.Length];
                float height = log ? Range(.45f, 1f) : Range(.5f, 3.8f);
                Place(landmarks, prefab, x, z, height, log ? 0 : height * .52f);
            }
            AddMeadow();
            AddCanopy();
        }

        private void AddMeadow()
        {
            var meadow = new GameObject("Yuyos y pastizales", typeof(MeadowGrass)).GetComponent<MeadowGrass>();
            meadow.transform.SetParent(root, false);
            var surfaces = new List<Material>();
            var kinds = new List<MeadowGrass.Tuft>();
            foreach ((string name, float low, float high, Vector3 share) in Tufts)
            {
                Model model = GetModel(name);
                MeshFilter part = model.Prefab.GetComponentInChildren<MeshFilter>();
                Material surface = MaterialFor(part.name.Split('.')[0]);
                if (!surfaces.Contains(surface))
                    surfaces.Add(surface);
                Matrix4x4 pose = part.transform.localToWorldMatrix;
                var foot = new Vector3(model.Bounds.center.x, model.Bounds.min.y, model.Bounds.center.z);
                Vector3[] vertices = part.sharedMesh.vertices;
                for (int i = 0; i < vertices.Length; i++)
                    vertices[i] = (pose.MultiplyPoint3x4(vertices[i]) - foot) / Mathf.Max(.01f, model.Bounds.size.y);
                kinds.Add(new MeadowGrass.Tuft
                {
                    Vertices = vertices, UV = part.sharedMesh.uv, Triangles = part.sharedMesh.triangles,
                    Material = surfaces.IndexOf(surface), Height = new Vector2(low, high), Share = share
                });
            }
            meadow.Configure(kinds.ToArray(), surfaces.ToArray());
        }

        private Flora FloraAt(Vector2 point)
        {
            if (Height(point.x, point.y) > 62)
                return Flora.Alpine;
            Flora local = IslandLayout.RegionAt(point) switch
            {
                1 => Flora.Ponderosa,
                2 or 4 => Flora.Alpine,
                _ => Flora.Woodland
            };
            return local != Flora.Woodland && Range(0, 1) < .14f ? Flora.Woodland : local;
        }

        private static float Footing(float x, float z, float reach)
        {
            float center = Height(x, z);
            float ground = center;
            for (int i = 0; i < 4 && reach > .15f; i++)
            {
                float angle = i * Mathf.PI * .5f;
                ground = Mathf.Min(ground, Height(x + Mathf.Cos(angle) * reach, z + Mathf.Sin(angle) * reach));
            }
            return Mathf.Max(ground, center - 1.6f);
        }

        private static bool Plantable(Vector2 point, float margin) =>
            IslandLayout.ShoreDistance(point) > margin && IslandLayout.LagoonDistance(point) < -margin;

        private bool Crowded(Vector2 point)
        {
            var cell = new Vector2Int(Mathf.FloorToInt(point.x / 7), Mathf.FloorToInt(point.y / 7));
            for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
            {
                if (!treeGrid.TryGetValue(cell + new Vector2Int(x, y), out List<Vector2> near))
                    continue;
                foreach (Vector2 other in near)
                    if ((other - point).sqrMagnitude < 46)
                        return true;
            }
            return false;
        }

        private void Remember(Vector2 point)
        {
            var cell = new Vector2Int(Mathf.FloorToInt(point.x / 7), Mathf.FloorToInt(point.y / 7));
            if (!treeGrid.TryGetValue(cell, out List<Vector2> near))
                treeGrid.Add(cell, near = new List<Vector2>());
            near.Add(point);
            trees.Add(point);
        }

        private void Plant(string model, float x, float z, float height, float reach)
        {
            Model entry = GetModel(model);
            int kind = canopyKinds.IndexOf(model);
            if (kind < 0)
            {
                kind = canopyKinds.Count;
                canopyKinds.Add(model);
                canopyReach.Add(reach);
            }
            float scale = height / Mathf.Max(.01f, entry.Bounds.size.y);
            float ground = Footing(x, z, Mathf.Min(entry.Bounds.extents.x * scale * .3f, 1.4f));
            canopyPlaces.Add(new Vector3(x, ground - .04f - Mathf.Max(0, entry.Bounds.min.y) * scale, z));
            canopyShapes.Add(new Vector3(scale, 1, Range(0, 360)));
            canopyModels.Add((ushort)kind);
            canopyTrunks.Add(0);
        }

        private void Tree(string model, float x, float z, float height)
        {
            Plant(model, x, z, height, 0);
            // ForestCanopy arma el colisionador con estas mismas proporciones al cargar la escena.
            canopyTrunks[^1] = height;
            float radius = height * .043f;
            Trunks.Add((canopyPlaces[^1], radius, Mathf.Max(2 * radius, Mathf.Min(height * .46f, 5))));
            Remember(new Vector2(x, z));
        }

        private ForestCanopy.Part[] Parts(string model, bool windy)
        {
            var parts = new List<ForestCanopy.Part>();
            foreach (MeshRenderer renderer in GetModel(model).Prefab.GetComponentsInChildren<MeshRenderer>())
            {
                string part = renderer.name.Split('.')[0];
                parts.Add(new ForestCanopy.Part
                {
                    Mesh = renderer.GetComponent<MeshFilter>().sharedMesh, Material = windy ? WindMaterial(part) : MaterialFor(part),
                    Pose = renderer.transform.localToWorldMatrix
                });
            }
            return parts.ToArray();
        }

        private void AddCanopy()
        {
            var kinds = new ForestCanopy.Kind[canopyKinds.Count];
            for (int i = 0; i < kinds.Length; i++)
            {
                string model = canopyKinds[i];
                bool tree = canopyReach[i] <= 0;
                kinds[i] = new ForestCanopy.Kind
                {
                    Parts = Parts(model, tree), Reach = canopyReach[i],
                    Far = Array.IndexOf(Ponderosas, model) >= 0 ? Parts(model + "Far", true) : new ForestCanopy.Part[0]
                };
            }
            var canopy = new GameObject("Bosque instanciado", typeof(ForestCanopy)).GetComponent<ForestCanopy>();
            canopy.transform.SetParent(root, false);
            canopy.Configure(kinds, canopyPlaces.ToArray(), canopyShapes.ToArray(), canopyModels.ToArray(), canopyTrunks.ToArray());
        }

        private void Giant(Transform parent, Mesh trunkMesh, string model, float x, float z, float height, float radius)
        {
            var tree = new GameObject($"{model} gigante").transform;
            tree.SetParent(parent, false);
            Transform canopy = Place(tree, model, x, z, height,
                far: Array.IndexOf(Ponderosas, model) >= 0 ? model + "Far" : null, widthScale: .86f, windy: true);

            var baseTrunk = new GameObject("Tronco y raíces", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
            baseTrunk.transform.SetParent(tree, false);
            baseTrunk.transform.SetPositionAndRotation(canopy.position - Vector3.up * 1.1f, canopy.rotation);
            baseTrunk.transform.localScale = new Vector3(radius, height * .77f, radius);
            baseTrunk.GetComponent<MeshFilter>().sharedMesh = trunkMesh;
            baseTrunk.GetComponent<MeshCollider>().sharedMesh = trunkMesh;
            baseTrunk.GetComponent<MeshRenderer>().sharedMaterial = WindMaterial("WoodBark");
            giantTrunks.Add((new Vector2(x, z), radius * 1.45f + 2));
            Remember(new Vector2(x, z));
        }

        private static Mesh GiantTrunk()
        {
            const string path = Generated + "/GiantTrunk.asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null)
                return mesh;

            const int sides = 10;
            var heights = new[] { -.03f, .03f, .13f, .43f, .74f, 1f };
            var radii = new[] { 1.38f, 1.31f, .91f, .69f, .42f, .09f };
            var vertices = new Vector3[heights.Length * (sides + 1)];
            var uv = new Vector2[vertices.Length];
            var triangles = new List<int>();
            for (int ring = 0; ring < heights.Length; ring++)
            {
                for (int side = 0; side <= sides; side++)
                {
                    int vertex = ring * (sides + 1) + side;
                    float angle = side * (2 * Mathf.PI / sides);
                    float root = 1 + (side % 2 == 0 ? .12f : -.09f) * (1 - Mathf.Clamp01(heights[ring] * 4));
                    float spread = radii[ring] * root;
                    vertices[vertex] = new Vector3(Mathf.Cos(angle) * spread + heights[ring] * .20f,
                        heights[ring], Mathf.Sin(angle) * spread);
                    uv[vertex] = new Vector2(side * .4f, heights[ring] * 9);
                    if (ring == heights.Length - 1 || side == sides)
                        continue;

                    int nextRing = vertex + sides + 1;
                    triangles.Add(vertex);
                    triangles.Add(nextRing);
                    triangles.Add(vertex + 1);
                    triangles.Add(vertex + 1);
                    triangles.Add(nextRing);
                    triangles.Add(nextRing + 1);
                }
            }
            mesh = new Mesh
            {
                name = "Tronco gigante",
                vertices = vertices,
                uv = uv,
                triangles = triangles.ToArray()
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private Transform Place(Transform parent, string name, float x, float z, float height, float radius = 0, string far = null,
            float widthScale = 1, bool windy = false)
        {
            var entry = GetModel(name);
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            float scale = height / Mathf.Max(.01f, entry.Bounds.size.y);
            float ground = Footing(x, z, Mathf.Min(entry.Bounds.extents.x * scale * widthScale * .3f, 1.4f));
            float lift = Mathf.Max(0, entry.Bounds.min.y) * scale;
            group.SetPositionAndRotation(new Vector3(x, ground - 0.04f - lift, z), Quaternion.Euler(0, Range(0, 360), 0));
            group.localScale = new Vector3(widthScale, 1, widthScale) * scale;
            GameObject close = Instantiate(name, group, windy);
            if (far != null)
            {
                GameObject distant = Instantiate(far, group, windy);
                var lod = group.gameObject.AddComponent<LODGroup>();
                lod.SetLODs(new[]
                {
                    new LOD(.18f, close.GetComponentsInChildren<Renderer>()),
                    new LOD(.015f, distant.GetComponentsInChildren<Renderer>())
                });
                lod.RecalculateBounds();
            }
            if (radius <= 0)
                return group;

            GameObject obstacle = new("Colisión", typeof(CapsuleCollider));
            obstacle.transform.SetParent(parent, false);
            obstacle.transform.position = group.position;
            var collider = obstacle.GetComponent<CapsuleCollider>();
            collider.radius = radius;
            collider.height = Mathf.Max(2 * radius, windy ? Mathf.Min(height * .46f, 5) : height);
            collider.center = Vector3.up * (collider.height * .5f);
            return group;
        }

        private GameObject Instantiate(string name, Transform parent, bool windy = false)
        {
            var obj = (GameObject)PrefabUtility.InstantiatePrefab(GetModel(name).Prefab, parent);
            foreach (MeshRenderer renderer in obj.GetComponentsInChildren<MeshRenderer>())
            {
                string part = renderer.name.Split('.')[0];
                renderer.sharedMaterial = windy ? WindMaterial(part) : MaterialFor(part);
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
            return obj;
        }

        private Model GetModel(string name)
        {
            if (models.TryGetValue(name, out Model model))
                return model;

            var prefab = Load<GameObject>($"{Folder}/{name}.fbx");
            var renderers = prefab.GetComponentsInChildren<MeshRenderer>();
            if (renderers.Length == 0)
                throw new InvalidOperationException($"El modelo {name} no contiene mallas.");
            Bounds bounds = renderers[0].bounds;
            foreach (MeshRenderer renderer in renderers)
                bounds.Encapsulate(renderer.bounds);
            model = new Model { Prefab = prefab, Bounds = bounds };
            models[name] = model;
            return model;
        }

        private Material MaterialFor(string part)
        {
            if (materials.TryGetValue(part, out Material material))
                return material;

            string path = $"{Generated}/{part}.mat";
            material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Jurassic Pix/PSX Surface"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.mainTexture = Load<Texture2D>($"{Folder}/Textures/{part}.png");
            material.SetFloat("_Affine", part == "Terrain" ? .18f : .43f);
            material.SetFloat("_VertexColor", 0);
            material.enableInstancing = true;
            if (part is "MountainRange" or "HikingPeak")
            {
                material.SetFloat("_FogScale", part == "MountainRange" ? .25f : .48f);
                material.SetFloat("_Affine", .16f);
                material.SetTexture("_CliffTex", Load<Texture2D>($"{Folder}/Textures/MossRock.png"));
                material.EnableKeyword("MOUNTAIN_TERRAIN");
                material.SetFloat("_Mountain", 1);
            }
            if (part.StartsWith("Leaf", StringComparison.Ordinal))
            {
                material.SetFloat("_Cutoff", .42f);
                material.SetFloat("_Cull", (float)CullMode.Off);
                material.SetOverrideTag("RenderType", "TransparentCutout");
                material.renderQueue = (int)RenderQueue.AlphaTest;
            }
            if (part == "Terrain")
            {
                material.SetTexture("_ForestFloor", Load<Texture2D>($"{Folder}/Textures/WoodFloor.png"));
                material.SetTexture("_MeadowTex", Load<Texture2D>($"{Folder}/Textures/Meadow.png"));
                material.SetTexture("_GravelTex", Load<Texture2D>($"{Folder}/Textures/TrailGravel.png"));
                material.EnableKeyword("FOREST_GROUND");
                material.SetFloat("_Ground", 1);
                material.SetFloat("_IslandScale", IslandLayout.Scale);
            }
            EditorUtility.SetDirty(material);
            materials[part] = material;
            return material;
        }

        private Material WindMaterial(string part)
        {
            string key = part + "_Viento";
            if (materials.TryGetValue(key, out Material cached))
                return cached;
            Material original = MaterialFor(part);
            string path = $"{Generated}/{key}.mat";
            Material wind = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (wind == null)
            {
                wind = new Material(original);
                AssetDatabase.CreateAsset(wind, path);
            }
            else
                wind.CopyPropertiesFromMaterial(original);
            wind.SetFloat("_TreeWind", 1);
            wind.enableInstancing = true;
            EditorUtility.SetDirty(wind);
            materials[key] = wind;
            return wind;
        }

        private Texture2D BakeIslandSurfaces()
        {
            const int width = 1024;
            Rect world = IslandLayout.Bounds;
            var pixels = new Color32[width * width];
            var heights = new Color32[pixels.Length];
            for (int y = 0; y < width; y++)
            {
                float z = Mathf.Lerp(world.yMin, world.yMax, (y + .5f) / width);
                for (int x = 0; x < width; x++)
                {
                    float east = Mathf.Lerp(world.xMin, world.xMax, (x + .5f) / width);
                    var ray = new Vector3(east, 900, z);
                    var meadow = new Vector2(east, z);
                    Color color = new(.08f, .24f, .30f);
                    float height = -42;
                    if (Physics.Raycast(ray, Vector3.down, out RaycastHit ground, 1000, 1 << Dinosaur.TerrainLayer))
                    {
                        height = ground.point.y;
                        float shore = height - IslandLayout.SurfaceLevel(meadow);
                        if (shore <= 0)
                            color = Color.Lerp(new Color(.08f, .24f, .30f), new Color(.22f, .53f, .50f),
                                Mathf.InverseLerp(-15, 0, shore));
                        else
                        {
                            bool cleared = IslandLayout.Clearing(meadow);
                            color = cleared ? new Color(.34f, .43f, .25f) : new Color(.20f, .35f, .23f);
                            float rocky = Mathf.Clamp01(.49f * (1 - ground.normal.y) + Mathf.InverseLerp(55, 155, height));
                            color = Color.Lerp(color, new Color(.45f, .40f, .33f), rocky);
                            if (Mathf.Repeat(height + 1, 14) < 1.6f)
                                color *= .79f;
                            if (shore < 4.6f)
                                color = Color.Lerp(new Color(.70f, .58f, .39f), color, shore / 5.6f);
                        }
                    }
                    int index = y * width + x;
                    pixels[index] = color;
                    heights[index] = new Color(Mathf.Clamp01((height + 44) / 94), 0, 0, 1);
                }
            }
            foreach (Vector2 tree in trees)
            {
                int x = Mathf.Clamp(Mathf.FloorToInt(Mathf.InverseLerp(world.xMin, world.xMax, tree.x) * width), 0, width - 1);
                int y = Mathf.Clamp(Mathf.FloorToInt(Mathf.InverseLerp(world.yMin, world.yMax, tree.y) * width), 0, width - 1);
                pixels[y * width + x] = new Color32(49, 79, 49, 255);
            }
            Texture2D bed = PersistSurface("ProfundidadIsla", width, heights, true);
            AddWater("Océano", new Vector3(0, 0, IslandLayout.Center.y), Vector2.one * (4200 * IslandLayout.Scale), bed, false);
            for (int lake = 0; lake < IslandLayout.LakeCount; lake++)
            {
                Vector2 middle = IslandLayout.LakeCenter(lake);
                AddWater($"Laguna {lake + 1}", new Vector3(middle.x, IslandLayout.LakeLevel(lake), middle.y),
                    IslandLayout.LakeSize(lake) * 2.5f, bed, true);
            }
            return PersistSurface("MapaDeLaIsla", width, pixels, false);
        }

        private static Texture2D PersistSurface(string name, int side, Color32[] pixels, bool linear)
        {
            string path = $"{Generated}/{name}.asset";
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                texture = new Texture2D(side, side, TextureFormat.RGBA32, false, linear);
                AssetDatabase.CreateAsset(texture, path);
            }
            else if ((texture.width != side || texture.height != side) && !texture.Reinitialize(side, side))
                throw new InvalidOperationException($"No se pudo redimensionar {name}.");
            texture.filterMode = linear ? FilterMode.Bilinear : FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixels32(pixels);
            texture.Apply(false);
            EditorUtility.SetDirty(texture);
            return texture;
        }

        private void AddWater(string name, Vector3 position, Vector2 size, Texture2D bed, bool lagoon)
        {
            var water = GameObject.CreatePrimitive(PrimitiveType.Quad);
            water.name = name;
            water.transform.SetParent(root, false);
            water.transform.SetPositionAndRotation(position, Quaternion.Euler(90, 0, 0));
            water.transform.localScale = new Vector3(size.x, size.y, 1);
            water.layer = 4;
            UnityEngine.Object.DestroyImmediate(water.GetComponent<Collider>());
            string path = $"{Generated}/{(lagoon ? "AguaLaguna" : "AguaOceano")}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Jurassic Pix/Island Water"));
                AssetDatabase.CreateAsset(material, path);
            }
            Rect bounds = IslandLayout.Bounds;
            material.SetTexture("_Bed", bed);
            material.SetVector("_IslandRect", new Vector4(bounds.xMin, bounds.yMin, bounds.width, bounds.height));
            material.SetFloat("_IslandScale", IslandLayout.Scale);
            material.SetFloat("_Lagoon", lagoon ? 1 : 0);
            EditorUtility.SetDirty(material);
            var renderer = water.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        private Transform Group(string name)
        {
            var group = new GameObject(name).transform;
            group.SetParent(root, false);
            return group;
        }

        private float Range(float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());

        private static T Load<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                throw new InvalidOperationException("Falta el recurso " + path);
            return asset;
        }
    }
}
