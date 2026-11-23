using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace JurassicPix
{
    public sealed class MeadowGrass : MonoBehaviour
    {
        [System.Serializable]
        public struct Tuft
        {
            public Vector3[] Vertices;
            public Vector2[] UV;
            public int[] Triangles;
            public int Material;
            public Vector2 Height;
            public Vector3 Share;
        }

        [SerializeField] private Tuft[] tufts;
        [SerializeField] private Material[] materials;
        [SerializeField, Min(8)] private float cellSize = 40;
        [SerializeField, Min(40)] private float range = 200;
        [SerializeField, Range(0, 900)] private int tuftsPerCell = 460;

        private readonly Dictionary<Vector2Int, MeshFilter> cells = new();
        private readonly Stack<MeshFilter> spare = new();
        private readonly List<Vector2Int> distant = new();
        private readonly List<Vector3> vertices = new(), normals = new();
        private readonly List<Vector2> coordinates = new();
        private List<int>[] triangles;
        private Transform viewer;
        private float nextSweep;

        public void Configure(Tuft[] kinds, Material[] surfaces)
        {
            tufts = kinds;
            materials = surfaces;
        }

        private void Awake()
        {
            triangles = new List<int>[materials.Length];
            for (int i = 0; i < triangles.Length; i++)
                triangles[i] = new List<int>();
        }

        private void Update()
        {
            if (viewer == null)
            {
                Camera view = Camera.main;
                if (view == null)
                    return;
                viewer = view.transform;
            }
            Vector3 eye = viewer.position;
            var middle = new Vector2Int(Mathf.FloorToInt(eye.x / cellSize), Mathf.FloorToInt(eye.z / cellSize));
            int reach = Mathf.CeilToInt(range / cellSize);
            if (Time.time >= nextSweep)
            {
                nextSweep = Time.time + .6f;
                distant.Clear();
                foreach (Vector2Int cell in cells.Keys)
                {
                    if (Mathf.Abs(cell.x - middle.x) > reach + 1 || Mathf.Abs(cell.y - middle.y) > reach + 1)
                        distant.Add(cell);
                }
                foreach (Vector2Int cell in distant)
                {
                    MeshFilter patch = cells[cell];
                    cells.Remove(cell);
                    if (patch == null)
                        continue;
                    patch.gameObject.SetActive(false);
                    spare.Push(patch);
                }
            }

            for (int built = 0; built < 2; built++)
            {
                bool found = false;
                Vector2Int next = default;
                float nearest = range * range;
                for (int x = -reach; x <= reach; x++)
                for (int z = -reach; z <= reach; z++)
                {
                    var cell = new Vector2Int(middle.x + x, middle.y + z);
                    float distance = new Vector2((cell.x + .5f) * cellSize - eye.x, (cell.y + .5f) * cellSize - eye.z).sqrMagnitude;
                    if (distance >= nearest || cells.ContainsKey(cell))
                        continue;
                    nearest = distance;
                    next = cell;
                    found = true;
                }
                if (!found)
                    return;
                cells.Add(next, Grow(next));
            }
        }

        private MeshFilter Grow(Vector2Int cell)
        {
            vertices.Clear();
            normals.Clear();
            coordinates.Clear();
            foreach (List<int> list in triangles)
                list.Clear();

            var random = new System.Random(cell.x * 73856093 ^ cell.y * 19349663);
            for (int i = 0; i < tuftsPerCell; i++)
            {
                float x = (cell.x + (float)random.NextDouble()) * cellSize;
                float z = (cell.y + (float)random.NextDouble()) * cellSize;
                float chance = (float)random.NextDouble(), pick = (float)random.NextDouble();
                float turn = (float)random.NextDouble() * Mathf.PI * 2, size = (float)random.NextDouble();
                var point = new Vector2(x, z);
                if (IslandLayout.ShoreDistance(point) < 4 || IslandLayout.LagoonDistance(point) > -3)
                    continue;
                Vector2 layout = point / IslandLayout.Scale;
                float glade = Mathf.Sin(layout.x * .031f + Mathf.Sin(layout.y * .023f) * 1.7f) *
                    Mathf.Sin(layout.y * .027f + Mathf.Sin(layout.x * .019f) * 1.3f);
                if (chance > Mathf.Lerp(.55f, 1, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.02f, .38f, glade))) ||
                    !Physics.Raycast(new Vector3(x, 900, z), Vector3.down, out RaycastHit ground, 1000, 1 << IslandLayout.TerrainLayer,
                        QueryTriggerInteraction.Ignore) ||
                    ground.normal.y < .8f || ground.point.y < IslandLayout.SurfaceLevel(point) + 4.5f)
                    continue;

                int region = IslandLayout.RegionAt(point);
                int flora = ground.point.y > 62 || region is 2 or 4 ? 2 : region == 1 ? 1 : 0;
                float total = 0;
                foreach (Tuft kind in tufts)
                    total += kind.Share[flora];
                int choice = 0;
                for (float share = pick * total; choice < tufts.Length - 1; choice++)
                {
                    share -= tufts[choice].Share[flora];
                    if (share <= 0)
                        break;
                }

                Tuft tuft = tufts[choice];
                float scale = Mathf.Lerp(tuft.Height.x, tuft.Height.y, size);
                float cos = Mathf.Cos(turn) * scale, sin = Mathf.Sin(turn) * scale;
                Vector3 foot = ground.point + Vector3.down * .05f;
                int first = vertices.Count;
                for (int v = 0; v < tuft.Vertices.Length; v++)
                {
                    Vector3 local = tuft.Vertices[v];
                    vertices.Add(foot + new Vector3(local.x * cos + local.z * sin, local.y * scale, local.z * cos - local.x * sin));
                    normals.Add(Vector3.up);
                    coordinates.Add(tuft.UV[v]);
                }
                List<int> faces = triangles[tuft.Material];
                foreach (int index in tuft.Triangles)
                    faces.Add(first + index);
            }
            if (vertices.Count == 0)
                return null;

            MeshFilter patch = spare.Count > 0 ? spare.Pop() : Plot();
            Mesh mesh = patch.sharedMesh;
            mesh.Clear();
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, coordinates);
            mesh.subMeshCount = triangles.Length;
            for (int i = 0; i < triangles.Length; i++)
                mesh.SetTriangles(triangles[i], i, false);
            mesh.RecalculateBounds();
            patch.gameObject.SetActive(true);
            return patch;
        }

        private MeshFilter Plot()
        {
            var patch = new GameObject("Pastizal", typeof(MeshFilter), typeof(MeshRenderer));
            patch.transform.SetParent(transform, false);
            var surface = patch.GetComponent<MeshRenderer>();
            surface.sharedMaterials = materials;
            surface.shadowCastingMode = ShadowCastingMode.Off;
            surface.lightProbeUsage = LightProbeUsage.Off;
            surface.reflectionProbeUsage = ReflectionProbeUsage.Off;
            var filter = patch.GetComponent<MeshFilter>();
            filter.sharedMesh = new Mesh { name = "Pastizal", indexFormat = IndexFormat.UInt32 };
            return filter;
        }

        private void OnDestroy()
        {
            foreach (MeshFilter patch in GetComponentsInChildren<MeshFilter>(true))
                Destroy(patch.sharedMesh);
        }
    }
}
