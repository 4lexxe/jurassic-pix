using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace JurassicPix.Editor
{
    internal sealed class TriceratopsGrowthAssets
    {
        private const string Folder = "Assets/JurassicPix/Generated/";
        private Mesh forms, shell;
        private Material eggshell;

        public void Install(Dinosaur animal, TriceratopsHerd herd, float years, bool female, bool dormant)
        {
            SkinnedMeshRenderer skin = animal.GetComponentInChildren<SkinnedMeshRenderer>(true);
            forms ??= BuildForms(skin, animal.transform, animal.Motion.Head);
            skin.sharedMesh = forms;
            var egg = animal.Playable ? null : CreateEgg(animal.transform);
            animal.gameObject.AddComponent<TriceratopsLife>().Configure(herd, skin, egg, years, female, dormant);
            if (dormant)
                animal.Motion.BodyModel.gameObject.SetActive(false);
        }

        private static Mesh BuildForms(SkinnedMeshRenderer skin, Transform animal, Transform head)
        {
            Mesh mesh = Object.Instantiate(skin.sharedMesh);
            mesh.name = "Triceratops · siluetas de crecimiento";
            mesh.ClearBlendShapes();
            Vector3[] originals = mesh.vertices;
            Vector3[] normals = mesh.normals;
            Matrix4x4 toAnimal = animal.worldToLocalMatrix * skin.transform.localToWorldMatrix;
            Matrix4x4 toMesh = toAnimal.inverse;
            Vector3 headPoint = animal.InverseTransformPoint(head.position);
            AddForm("Crianza", .27f, .13f, .24f, .42f);
            AddForm("Juventud", .12f, .056f, .10f, .21f);
            return Persist(mesh, "EtapasTriceratops.asset");

            void AddForm(string name, float wideHead, float highHead, float shortTip, float shortHorns)
            {
                var deltas = new Vector3[originals.Length];
                var coordinates = new Vector3[originals.Length];
                for (int i = 0; i < originals.Length; i++)
                {
                    Vector3 point = toAnimal.MultiplyPoint3x4(originals[i]);
                    float skull = Smooth(headPoint.z - 1.12f, headPoint.z + .35f, point.z);
                    float horn = skull * Smooth(headPoint.y + .12f, headPoint.y + .74f, point.y) *
                        Smooth(headPoint.z + .35f, headPoint.z + 1.25f, point.z);
                    float torso = 1 - skull;
                    point.x *= 1 + wideHead * skull - highHead * .44f * torso;
                    point.y += skull * (point.y - headPoint.y) * highHead;
                    point.y -= horn * shortHorns;
                    point.z -= skull * Mathf.Max(0, point.z - headPoint.z) * (shortTip + horn * .09f);
                    point.z -= torso * Mathf.Min(0, point.z) * shortTip * .41f;
                    coordinates[i] = toMesh.MultiplyPoint3x4(point);
                    deltas[i] = coordinates[i] - originals[i];
                }
                var shaped = Object.Instantiate(mesh);
                shaped.vertices = coordinates;
                shaped.RecalculateNormals();
                Vector3[] normalDeltas = shaped.normals;
                for (int i = 0; i < normalDeltas.Length; i++)
                    normalDeltas[i] -= normals[i];
                mesh.AddBlendShapeFrame(name, 100, deltas, normalDeltas, null);
                Object.DestroyImmediate(shaped);
            }
        }

        private GameObject CreateEgg(Transform parent)
        {
            shell ??= BuildEggMesh();
            if (eggshell == null)
            {
                var material = new Material(Shader.Find("Jurassic Pix/PSX Surface"));
                material.color = new Color(.81f, .70f, .47f);
                material.SetFloat("_VertexColor", 0);
                material.SetFloat("_Grain", .11f);
                material.SetFloat("_StippleWeight", .19f);
                eggshell = Persist(material, "CascaronTriceratops.mat");
            }
            var egg = new GameObject("Huevo de Triceratops", typeof(MeshFilter), typeof(MeshRenderer));
            egg.transform.SetParent(parent, false);
            egg.GetComponent<MeshFilter>().sharedMesh = shell;
            egg.GetComponent<MeshRenderer>().sharedMaterial = eggshell;
            egg.SetActive(false);
            return egg;
        }

        private static Mesh BuildEggMesh()
        {
            const int segments = 10, rings = 7;
            var vertices = new List<Vector3>(segments * rings);
            var triangles = new List<int>();
            for (int row = 0; row <= rings; row++)
            {
                float polar = Mathf.PI * row / rings;
                float radius = Mathf.Sin(polar) * (.31f + .068f * Mathf.Cos(polar));
                for (int side = 0; side < segments; side++)
                {
                    float angle = side * (2 * Mathf.PI / segments);
                    vertices.Add(new Vector3(Mathf.Cos(angle) * radius, .45f - .45f * Mathf.Cos(polar), Mathf.Sin(angle) * radius));
                    if (row == rings)
                        continue;
                    int a = row * segments + side;
                    int b = row * segments + (side + 1) % segments;
                    triangles.AddRange(new[] { a, a + segments, b, a + segments, b + segments, b });
                }
            }
            var mesh = new Mesh { name = "Cascarón de bajo poligonaje", vertices = vertices.ToArray(), triangles = triangles.ToArray() };
            mesh.RecalculateNormals();
            return Persist(mesh, "HuevoTriceratops.asset");
        }

        private static float Smooth(float from, float to, float x) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(from, to, x));

        private static T Persist<T>(T asset, string name) where T : Object
        {
            T saved = AssetDatabase.LoadAssetAtPath<T>(Folder + name);
            if (saved == null)
            {
                AssetDatabase.CreateAsset(asset, Folder + name);
                return asset;
            }
            EditorUtility.CopySerialized(asset, saved);
            Object.DestroyImmediate(asset);
            EditorUtility.SetDirty(saved);
            return saved;
        }
    }
}
