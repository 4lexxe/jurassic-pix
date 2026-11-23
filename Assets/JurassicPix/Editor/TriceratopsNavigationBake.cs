using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace JurassicPix.Editor
{
    internal static class TriceratopsNavigationBake
    {
        private const string Path = "Assets/JurassicPix/Generated/NavegacionIsla.asset";

        public static void Create(Transform world)
        {
            Physics.SyncTransforms();
            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(world, Dinosaur.Scenery, NavMeshCollectGeometry.PhysicsColliders, 0,
                new List<NavMeshBuildMarkup>(), sources);
            foreach ((Vector3 at, float radius, float height) in ForestWorld.Trunks)
            {
                sources.Add(new NavMeshBuildSource
                {
                    shape = NavMeshBuildSourceShape.Box, size = new Vector3(radius * 2, height, radius * 2),
                    transform = Matrix4x4.Translate(at + Vector3.up * (height * .5f)), area = 0
                });
            }
            var temporary = new List<Mesh>();
            for (int i = sources.Count - 1; i >= 0; i--)
            {
                NavMeshBuildSource source = sources[i];
                if (source.shape != NavMeshBuildSourceShape.Mesh || source.sourceObject is not Mesh mesh ||
                    !mesh.name.StartsWith(ForestWorld.TerrainMeshPrefix, StringComparison.Ordinal))
                    continue;
                var dry = DryTerrain(mesh, source.transform);
                if (dry == null)
                {
                    sources.RemoveAt(i);
                    continue;
                }
                source.sourceObject = dry;
                sources[i] = source;
                temporary.Add(dry);
            }
            NavMeshBuildSettings settings = NavMesh.GetSettingsByIndex(0);
            settings.agentRadius = 1.06f;
            settings.agentHeight = 3.55f;
            settings.agentSlope = 36;
            settings.agentClimb = .48f;
            settings.overrideVoxelSize = true;
            settings.voxelSize = .8f;
            settings.overrideTileSize = true;
            settings.tileSize = 128;
            settings.maxJobWorkers = 4;
            Rect extent = IslandLayout.Bounds;
            var bounds = new Bounds(new Vector3(extent.center.x, 240, extent.center.y), new Vector3(extent.width, 560, extent.height));
            NavMeshData data = AssetDatabase.LoadAssetAtPath<NavMeshData>(Path);
            if (data == null)
            {
                data = new NavMeshData(settings.agentTypeID);
                AssetDatabase.CreateAsset(data, Path);
            }
            try
            {
                if (!NavMeshBuilder.UpdateNavMeshData(data, settings, sources, bounds))
                    throw new InvalidOperationException("No se pudo cocinar la navegación de la isla.");
                EditorUtility.SetDirty(data);
                new GameObject("Navegación de Triceratops", typeof(TriceratopsNavigation))
                    .GetComponent<TriceratopsNavigation>().Configure(data);
            }
            finally
            {
                foreach (Mesh mesh in temporary)
                    UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        private static Mesh DryTerrain(Mesh terrain, Matrix4x4 toWorld)
        {
            Vector3[] vertices = terrain.vertices;
            var dry = new bool[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 point = toWorld.MultiplyPoint3x4(vertices[i]);
                dry[i] = point.y > IslandLayout.SurfaceLevel(new Vector2(point.x, point.z)) + .18f;
            }
            int[] triangles = terrain.triangles;
            var retained = new List<int>(triangles.Length);
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                if (!dry[a] || !dry[b] || !dry[c])
                    continue;
                retained.Add(a);
                retained.Add(b);
                retained.Add(c);
            }
            return retained.Count == 0 ? null : new Mesh
                { name = "Tierra seca navegable", vertices = vertices, triangles = retained.ToArray() };
        }
    }
}
