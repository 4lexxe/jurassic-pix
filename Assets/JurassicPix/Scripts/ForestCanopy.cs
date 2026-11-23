using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace JurassicPix
{
    public sealed class ForestCanopy : MonoBehaviour
    {
        [System.Serializable]
        public struct Part
        {
            public Mesh Mesh;
            public Material Material;
            public Matrix4x4 Pose;
        }

        [System.Serializable]
        public struct Kind
        {
            public Part[] Parts, Far;
            public float Reach;
        }

        [SerializeField] private Kind[] kinds;
        [SerializeField] private Vector3[] places, shapes;
        [SerializeField] private ushort[] models;
        [SerializeField, Tooltip("Altura de cada árbol con tronco sólido; 0 para arbustos.")] private float[] trunks;
        [SerializeField, Min(40)] private float cellSize = 200, range = 1400, detail = 95, shadows = 120;

        private sealed class Batch
        {
            public Part[] Near, Distant;
            public float Reach;
            public readonly List<Matrix4x4> Pending = new();
            public Matrix4x4[][][] NearRuns, DistantRuns;
        }

        private sealed class Cell
        {
            public Vector3 Center;
            public Bounds Bounds;
            public float Tallest;
            public List<int> Trees = new();
            public readonly Dictionary<int, Batch> Batches = new();
        }

        private readonly List<Cell> cells = new();
        private readonly Plane[] frustum = new Plane[6];
        private Camera viewer;

        public void Configure(Kind[] species, Vector3[] positions, Vector3[] sizes, ushort[] indices, float[] trunkHeights)
        {
            kinds = species;
            places = positions;
            shapes = sizes;
            models = indices;
            trunks = trunkHeights;
        }

        private void Awake()
        {
            var grid = new Dictionary<Vector2Int, Cell>();
            for (int i = 0; i < places.Length; i++)
            {
                Vector3 place = places[i];
                var key = new Vector2Int(Mathf.FloorToInt(place.x / cellSize), Mathf.FloorToInt(place.z / cellSize));
                if (!grid.TryGetValue(key, out Cell cell))
                {
                    cell = new Cell
                    {
                        Center = new Vector3((key.x + .5f) * cellSize, place.y, (key.y + .5f) * cellSize),
                        Bounds = new Bounds(place, Vector3.zero)
                    };
                    grid.Add(key, cell);
                    cells.Add(cell);
                }
                if (!cell.Batches.TryGetValue(models[i], out Batch batch))
                {
                    Kind kind = kinds[models[i]];
                    batch = new Batch
                    {
                        Near = kind.Parts, Distant = kind.Far != null && kind.Far.Length > 0 ? kind.Far : kind.Parts,
                        Reach = kind.Reach > 0 ? kind.Reach : range
                    };
                    cell.Batches.Add(models[i], batch);
                }
                Vector3 shape = shapes[i];
                batch.Pending.Add(Matrix4x4.TRS(place, Quaternion.Euler(0, shape.z, 0), new Vector3(shape.y, 1, shape.y) * shape.x));
                cell.Bounds.Encapsulate(place);
                float height = trunks != null && i < trunks.Length ? trunks[i] : 0;
                cell.Tallest = Mathf.Max(cell.Tallest, height > 0 ? height : 6);
                if (height > 0)
                    cell.Trees.Add(i);
            }
            foreach (Cell cell in cells)
            {
                foreach (Batch batch in cell.Batches.Values)
                {
                    batch.NearRuns = Runs(batch.Near, batch.Pending);
                    batch.DistantRuns = batch.Distant == batch.Near ? batch.NearRuns : Runs(batch.Distant, batch.Pending);
                    batch.Pending.Clear();
                }
                cell.Bounds.Encapsulate(cell.Bounds.max + Vector3.up * (cell.Tallest + 4));
                cell.Bounds.Expand(new Vector3(cell.Tallest, 2, cell.Tallest));
            }
            StartCoroutine(GrowTrunks());
        }

        // Los troncos sólidos se crean al cargar, agrupados por celda, en vez de guardar un objeto por árbol en la escena.
        private IEnumerator GrowTrunks()
        {
            Camera view = Camera.main;
            Vector3 eye = view != null ? view.transform.position : Vector3.zero;
            var order = new List<Cell>(cells);
            order.Sort((a, b) => (a.Center - eye).sqrMagnitude.CompareTo((b.Center - eye).sqrMagnitude));
            int made = 0;
            foreach (Cell cell in order)
            {
                if (cell.Trees.Count == 0)
                    continue;
                var holder = new GameObject("Troncos");
                holder.transform.SetParent(transform, false);
                holder.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                foreach (int tree in cell.Trees)
                {
                    var trunk = holder.AddComponent<CapsuleCollider>();
                    trunk.radius = trunks[tree] * .043f;
                    trunk.height = Mathf.Max(2 * trunk.radius, Mathf.Min(trunks[tree] * .46f, 5));
                    trunk.center = places[tree] + Vector3.up * (trunk.height * .5f);
                }
                made += cell.Trees.Count;
                cell.Trees = null;
                if (made < 1500)
                    continue;
                made = 0;
                yield return null;
            }
        }

        private static Matrix4x4[][][] Runs(Part[] parts, List<Matrix4x4> instances)
        {
            var result = new Matrix4x4[parts.Length][][];
            for (int part = 0; part < parts.Length; part++)
            {
                int count = (instances.Count + 1022) / 1023;
                result[part] = new Matrix4x4[count][];
                for (int run = 0; run < count; run++)
                {
                    int length = Mathf.Min(1023, instances.Count - run * 1023);
                    var matrices = new Matrix4x4[length];
                    for (int i = 0; i < length; i++)
                        matrices[i] = instances[run * 1023 + i] * parts[part].Pose;
                    result[part][run] = matrices;
                }
            }
            return result;
        }

        private void LateUpdate()
        {
            if (!SystemInfo.supportsInstancing)
                return;
            if (viewer == null)
                viewer = Camera.main;
            if (viewer == null)
                return;
            Vector3 eye = viewer.transform.position;
            GeometryUtility.CalculateFrustumPlanes(viewer, frustum);
            // Más allá de donde cierra la niebla un árbol tiene el color del fondo: no hace falta dibujarlo.
            float haze = Shader.GetGlobalVector("_HabitatFogRange").y;
            float limit = haze > 0 ? Mathf.Min(range, haze + 25) : range;
            float reach = cellSize * .75f;
            foreach (Cell cell in cells)
            {
                Vector3 offset = cell.Center - eye;
                offset.y = 0;
                float distance = offset.magnitude - reach;
                if (distance > limit)
                    continue;
                bool visible = GeometryUtility.TestPlanesAABB(frustum, cell.Bounds);
                // Fuera de cuadro solo importan los árboles cercanos, por la sombra que proyectan hacia adentro.
                if (!visible && distance > shadows)
                    continue;
                ShadowCastingMode cast = !visible ? ShadowCastingMode.ShadowsOnly :
                    distance < shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                // Con la niebla ya espesa alcanza con una parte de los árboles; el orden de plantado es aleatorio.
                float veil = distance / limit;
                float share = veil > .72f ? .4f : veil > .5f ? .65f : 1;
                foreach (Batch batch in cell.Batches.Values)
                {
                    if (distance > batch.Reach)
                        continue;
                    bool close = distance < detail;
                    Part[] parts = close ? batch.Near : batch.Distant;
                    Matrix4x4[][][] runs = close ? batch.NearRuns : batch.DistantRuns;
                    for (int part = 0; part < parts.Length; part++)
                    {
                        foreach (Matrix4x4[] run in runs[part])
                            Graphics.DrawMeshInstanced(parts[part].Mesh, 0, parts[part].Material, run,
                                Mathf.CeilToInt(run.Length * share), null, cast, true, 0);
                    }
                }
            }
        }
    }
}
