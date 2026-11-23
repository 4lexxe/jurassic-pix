using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace JurassicPix.Editor
{
    public static class ExpeditionScene
    {
        public const string ScenePath = "Assets/JurassicPix/Scenes/Expedicion.unity";
        private const string Root = "Assets/JurassicPix";
        private const string Generated = Root + "/Generated/";
        private readonly struct Animal
        {
            public readonly string Model, Name, Note;
            public readonly Vector3 Position;
            public readonly float Length, Heading, Range;
            public readonly Dinosaur.Profile Traits;

            public Animal(string model, string name, string note, Vector3 position, float length, float heading, float range,
                Dinosaur.Profile traits)
            {
                Model = model;
                Name = name;
                Note = note;
                Position = position;
                Length = length;
                Heading = heading;
                Range = range;
                Traits = traits;
            }
        }

        private static readonly (string Suffix, Color Tint)[] Coats =
        {
            ("", Color.white), (" rojizo", new Color(1.12f, .80f, .66f)), (" verdoso", new Color(.74f, 1.02f, .78f)),
            (" oscuro", new Color(.60f, .62f, .70f)), (" arena", new Color(1.14f, 1.04f, .80f))
        };
        private static int coat;

        private static readonly Animal[] Animals =
        {
            new("Triceratops", "Triceratops", "Tres cuernos y una gola ósea protegen su cabeza.", new Vector3(-16, 0, 117), 9, 12, 24,
                Profile("Corneada", "9 m de largo", 3.1f, 7.3f, 114, 11.5f, 2.45f, 5.1f, .47f, 19,
                    200, 46, .18f, .24f, .56f, Dinosaur.Temperament.Herd, stamina: 22)),
            new("Velociraptor", "Velociraptor", "Sus patas tenían una garra curvada en el segundo dedo.", new Vector3(150, 0, 572), 2.65f, -40, 16,
                Profile("Salto y zarpazo", "2,65 m de largo", 2.85f, 7.5f, 210, 5.4f, .91f, 2.5f, .46f, 20,
                    82, 23, .06f, .52f, .34f, Dinosaur.Temperament.Pack, stamina: 30)),
            new("Argentinosaurus", "Argentinosaurus", "Un saurópodo gigante conocido por fósiles de la Patagonia.", new Vector3(-266, 0, 310), 32, 24, 36,
                Profile("Barrido de cola", "32 m de largo", 3.55f, 5.8f, 51, 26, 7.7f, 15.4f, .50f, 24,
                    380, 64, .20f, .38f, .91f, Dinosaur.Temperament.Herd, tail: true, stamina: 12)),
            new("Quetzalcoatlus", "Quetzalcoatlus", "Pterosaurio con alas membranosas y un pico alargado.", new Vector3(21, 31, 249), 12, 158, 39,
                Profile("Picotazo", "12 m de envergadura", 2.45f, 4.9f, 105, 13, 2.1f, 5.0f, .47f, 36,
                    112, 32, .08f, .57f, .60f, Dinosaur.Temperament.Flyer, flightSpeed: 21.5f, stamina: 9)),
            new("Tyrannosaurus", "Tyrannosaurus rex", "Sus dientes robustos soportaban una mordida potente.", new Vector3(58, 0, 180), 12.3f, 246, 28,
                Profile("Mordida", "12,3 m de largo", 3.9f, 8.7f, 92, 15.5f, 3.7f, 6.3f, .46f, 27,
                    290, 82, .17f, .50f, .76f, Dinosaur.Temperament.Solitary, stamina: 26))
        };

        private static Dinosaur.Profile Profile(string attack, string size, float walk, float run, float turn,
            float cameraDistance, float cameraHeight, float reach, float impact, float patrol,
            int health, int damage, float armor, float guard, float recovery, Dinosaur.Temperament nature,
            bool tail = false, float flightSpeed = 0, float stamina = 20)
        {
            return new Dinosaur.Profile
            {
                AttackName = attack, SizeLabel = size, Walk = walk, Run = run, Turn = turn,
                CameraDistance = cameraDistance, CameraHeight = cameraHeight, AttackReach = reach,
                ImpactPhase = impact, PatrolRadius = patrol, TailStrike = tail, CanFly = flightSpeed > 0, FlightSpeed = flightSpeed,
                MaxHealth = health, Damage = damage, Armor = armor, GuardMultiplier = guard, Recovery = recovery, Nature = nature,
                Stamina = stamina
            };
        }

        [MenuItem("Jurassic Pix/Regenerar escena de expedición")]
        public static void Rebuild()
        {
            if (!Application.isBatchMode &&
                (!EditorUtility.DisplayDialog("Regenerar expedición", "Se reemplazará la escena Expedicion y sus assets generados.", "Regenerar", "Cancelar")
                || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()))
                return;

            EnsureFolder(Root, "Generated");
            EnsureFolder(Root, "Scenes");
            ConfigureImports();
            QualitySettings.antiAliasing = 0;
            PlayerSettings.productName = "Jurassic Pix";
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.defaultIsNativeResolution = true;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowDistance = 90;
            QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
            QualitySettings.shadowCascades = 1;
            // Sin sombras en cascada Unity no repite toda la escena en una pasada de profundidad previa.
            foreach (GraphicsTier tier in new[] { GraphicsTier.Tier1, GraphicsTier.Tier2, GraphicsTier.Tier3 })
            {
                UnityEditor.Rendering.TierSettings shading = UnityEditor.Rendering.EditorGraphicsSettings.GetTierSettings(BuildTargetGroup.Standalone, tier);
                shading.cascadedShadowMaps = false;
                UnityEditor.Rendering.EditorGraphicsSettings.SetTierSettings(BuildTargetGroup.Standalone, tier, shading);
            }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Expedition.Habitat[] habitats = MakeHabitats();
            var skybox = new Material(Shader.Find("Jurassic Pix/Habitat Sky"));
            skybox.SetTexture("_DayPanorama", ForestWorld.LoadSky("Day"));
            skybox.SetTexture("_NightPanorama", ForestWorld.LoadSky("Night"));
            RenderSettings.skybox = Persist(skybox, "Cielo.mat");
            RenderSettings.fog = false;

            GameObject sunObject = new("Sol y luna", typeof(Light), typeof(ForestLighting));
            Light sun = sunObject.GetComponent<Light>();
            sun.type = LightType.Directional;
            sun.lightmapBakeType = LightmapBakeType.Realtime;
            sun.shadows = LightShadows.Soft;
            sun.shadowNormalBias = .32f;
            RenderSettings.sun = sun;
            ForestLighting lighting = sunObject.GetComponent<ForestLighting>();
            lighting.Preview(habitats[0].Fog, habitats[0].Light, habitats[0].FogRange, 10);

            Transform world = new GameObject("Isla de la expedición").transform;
            Texture2D map = ForestWorld.Create(world);
            TriceratopsNavigationBake.Create(world);
            Mesh footprint = MakeContactShadow();
            var contact = Persist(new Material(Shader.Find("Jurassic Pix/Contact Shadow")), "Sombras.mat");

            Transform fauna = new GameObject("Fauna original").transform;
            var audio = new ExpeditionAudioSetup();
            GroundTremor tremor = audio.AddTremor();
            var sightings = new List<Sighting>();
            foreach (Animal animal in Animals)
                sightings.Add(PlaceCreature(animal, fauna, footprint, contact, audio, tremor, true));
            List<Sighting> wildlife = AddWildlife(sightings, footprint, contact, audio, tremor);

            GameObject output = new("Pantalla", typeof(Camera));
            Camera outputCamera = output.GetComponent<Camera>();
            outputCamera.clearFlags = CameraClearFlags.SolidColor;
            outputCamera.backgroundColor = new Color(0.028f, 0.041f, 0.032f);
            outputCamera.cullingMask = 0;
            outputCamera.depth = -5;
            outputCamera.allowMSAA = false;
            outputCamera.allowHDR = false;

            GameObject player = new("Jugador");
            player.layer = 2;
            player.transform.position = GroundPoint(IslandLayout.ToWorld(new Vector2(-12, 138))) + Vector3.up * .15f;

            GameObject eyes = new("Vista PSX", typeof(Camera), typeof(AudioListener), typeof(PsxView));
            eyes.tag = "MainCamera";
            eyes.transform.SetParent(player.transform, false);
            eyes.transform.localPosition = new Vector3(0, 1.67f, 0);
            eyes.transform.localRotation = Quaternion.Euler(-2.0f, 0, 0);
            Camera camera = eyes.GetComponent<Camera>();
            camera.renderingPath = RenderingPath.Forward;
            camera.useOcclusionCulling = false;
            camera.nearClipPlane = 0.13f;
            camera.farClipPlane = 1250 * IslandLayout.Scale;
            camera.fieldOfView = 61;
            camera.allowMSAA = false;
            camera.allowHDR = false;
            var view = new SerializedObject(eyes.GetComponent<PsxView>());
            view.FindProperty("colorShader").objectReferenceValue = Shader.Find("Hidden/Jurassic Pix/PSX Color");
            view.ApplyModifiedPropertiesWithoutUndo();
            ValleyWeather weather = audio.AddWeather(eyes.transform);
            FlightWind flightWind = audio.AddFlightWind(eyes.transform);
            audio.AddForest(lighting, eyes.transform, weather);

            Expedition expedition = player.AddComponent<Expedition>();
            var serialized = new SerializedObject(expedition);
            serialized.FindProperty("eyes").objectReferenceValue = camera;
            serialized.FindProperty("lighting").objectReferenceValue = lighting;
            serialized.FindProperty("weather").objectReferenceValue = weather;
            serialized.FindProperty("tremor").objectReferenceValue = tremor;
            serialized.FindProperty("flightWind").objectReferenceValue = flightWind;
            serialized.FindProperty("interfaceSound").objectReferenceValue = audio.AddInterface(eyes.transform);
            serialized.FindProperty("map").objectReferenceValue = map;
            serialized.FindProperty("network").objectReferenceValue =
                new GameObject("Red de expedición", typeof(ExpeditionNetwork)).GetComponent<ExpeditionNetwork>();
            SerializedProperty habitatArray = serialized.FindProperty("habitats");
            habitatArray.arraySize = habitats.Length;
            for (int i = 0; i < habitats.Length; i++)
            {
                Expedition.Habitat data = habitats[i];
                SerializedProperty value = habitatArray.GetArrayElementAtIndex(i);
                value.FindPropertyRelative("Name").stringValue = data.Name;
                value.FindPropertyRelative("Route").stringValue = data.Route;
                value.FindPropertyRelative("Fog").colorValue = data.Fog;
                value.FindPropertyRelative("Light").colorValue = data.Light;
                value.FindPropertyRelative("FogRange").vector2Value = data.FogRange;
            }
            SerializedProperty sightingsArray = serialized.FindProperty("sightings");
            sightingsArray.arraySize = sightings.Count;
            for (int i = 0; i < sightings.Count; i++)
                sightingsArray.GetArrayElementAtIndex(i).objectReferenceValue = sightings[i];
            SerializedProperty wildlifeArray = serialized.FindProperty("wildlife");
            wildlifeArray.arraySize = wildlife.Count;
            for (int i = 0; i < wildlife.Count; i++)
                wildlifeArray.GetArrayElementAtIndex(i).objectReferenceValue = wildlife[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"Expedición creada con {habitats.Length} hábitats, {sightings.Count} especies y {sightings.Count + wildlife.Count} animales.");
        }

        private static List<Sighting> AddWildlife(List<Sighting> roster, Mesh shadow, Material contact,
            ExpeditionAudioSetup audio, GroundTremor tremor)
        {
            var result = new List<Sighting>();
            var growth = new TriceratopsGrowthAssets();
            Transform root = new GameObject("Manadas y fauna silvestre").transform;

            Sighting Spawn(int species, Transform group, Vector3 at, float facing)
            {
                Animal template = Animals[species];
                var animal = new Animal(template.Model, template.Name, template.Note, at, template.Length,
                    facing, template.Range, template.Traits);
                Sighting sighting = PlaceCreature(animal, group, shadow, contact, audio, tremor);
                sighting.JournalSource = roster[species];
                result.Add(sighting);
                return sighting;
            }

            void Group(string name, int species, int count, Vector2 center, Vector2 radius, Vector2 spacing,
                float period, float phase, bool includePlayer, bool fly = false, int tint = 0)
            {
                coat = tint;
                var herd = new GameObject(name, typeof(WildlifeHerd)).GetComponent<WildlifeHerd>();
                herd.transform.SetParent(root, false);
                var formation = new Vector2[count];
                int columns = 3;
                int rows = Mathf.CeilToInt((float)count / columns);
                for (int i = 0; i < count; i++)
                {
                    int row = i / columns;
                    int rowSize = Mathf.Min(columns, count - row * columns);
                    float stagger = row % 2 == 1 ? spacing.x * .25f : 0;
                    formation[i] = new Vector2((i % columns - (rowSize - 1) * .5f) * spacing.x + stagger,
                        (row - (rows - 1) * .5f) * spacing.y);
                }
                herd.Configure(center, radius, formation, period, phase, fly);
                TriceratopsHerd triceratops = species == 0 ? herd.gameObject.AddComponent<TriceratopsHerd>() : null;
                if (includePlayer)
                {
                    Link(roster[species].GetComponent<Dinosaur>(), herd, 0);
                    if (triceratops != null)
                        growth.Install(roster[species].GetComponent<Dinosaur>(), triceratops, 331, false, false);
                }
                for (int slot = includePlayer ? 1 : 0; slot < count; slot++)
                {
                    Vector2 position = center + formation[slot];
                    float altitude = fly ? 31 + (slot % 4) * 7 : 0;
                    Dinosaur inhabitant = Spawn(species, herd.transform, new Vector3(position.x, altitude, position.y),
                        10 + (slot % 3) * 12).GetComponent<Dinosaur>();
                    Link(inhabitant, herd, slot);
                    if (triceratops != null)
                        growth.Install(inhabitant, triceratops, 275 + slot * 51, slot % 2 == 1, false);
                }
                if (triceratops != null)
                    AddYoung(triceratops, herd, center, count, includePlayer);
                coat = 0;
            }

            void AddYoung(TriceratopsHerd community, WildlifeHerd herd, Vector2 center, int startSlot, bool south)
            {
                Vector3 dry = GroundPoint(center);
                var nests = new[]
                {
                    GroundPoint(center + new Vector2(south ? -10 : 13, south ? 8 : -8)),
                    GroundPoint(center + new Vector2(south ? 11 : 22, south ? 18 : 14))
                };
                var pastures = new[]
                {
                    GroundPoint(center + new Vector2(-12, 12)), GroundPoint(center + new Vector2(20, -16)),
                    GroundPoint(center + new Vector2(south ? -34 : 28, 23))
                };
                IslandLayout.LagoonDistance(center, out int lake);
                Vector2 toward = center - IslandLayout.LakeCenter(lake);
                float bearing = Mathf.Atan2(toward.y / IslandLayout.LakeSize(lake).y, toward.x / IslandLayout.LakeSize(lake).x) * Mathf.Rad2Deg;
                var water = new[] { LagoonBank(lake, bearing - 13), LagoonBank(lake, bearing + 13) };
                community.Configure(dry, nests, pastures, water);
                Vector2[] positions = { new(-4, -7), new(3, -5), new(6, 6), new(-3, 3), new(4, 4) };
                float[] ages = { 12, 32, 102, 0, 0 };
                for (int index = 0; index < positions.Length; index++)
                {
                    Vector2 point = center + positions[index];
                    Dinosaur animal = Spawn(0, herd.transform, new Vector3(point.x, 0, point.y), 29).GetComponent<Dinosaur>();
                    Link(animal, herd, startSlot + index);
                    growth.Install(animal, community, ages[index], index % 2 == 0, index >= 3);
                }
            }

            Spawn(4, root, IslandLayout.ToWorld(new Vector3(262, 0, 462)), 235);
            Spawn(4, root, IslandLayout.ToWorld(new Vector3(-265, 0, 569)), 71);
            coat = 3;
            Spawn(4, root, IslandLayout.ToWorld(new Vector3(120, 0, 535)), 140);
            coat = 1;
            Spawn(4, root, IslandLayout.ToWorld(new Vector3(-185, 0, 655)), 200);
            coat = 4;
            Spawn(4, root, IslandLayout.ToWorld(new Vector3(-95, 0, 335)), 20);
            coat = 0;
            Group("Triceratops · Bahía del sur", 0, 14, IslandLayout.RegionCenter(0), new Vector2(38, 41),
                new Vector2(11, 14), 70, 7, true);
            Group("Argentinosaurus · Bosque occidental", 2, 8, IslandLayout.RegionCenter(1), new Vector2(34, 46),
                new Vector2(24, 34), 92, 11, true);
            Group("Argentinosaurus · Cabos del norte", 2, 8, IslandLayout.RegionCenter(2) + IslandLayout.ToWorld(new Vector2(-12, -15)),
                new Vector2(41, 26), new Vector2(24, 34), 83, 19, false);
            Group("Triceratops · Laguna oriental", 0, 12, IslandLayout.RegionCenter(3) + IslandLayout.ToWorld(new Vector2(16, -17)),
                new Vector2(29, 34), new Vector2(12, 15), 66, 22, false);
            Group("Velociraptor · Altos del noreste", 1, 7, IslandLayout.RegionCenter(4) + IslandLayout.ToWorld(new Vector2(-9, -10)),
                new Vector2(38, 37), new Vector2(5.6f, 8.3f), 65, 8, true);
            Group("Argentinosaurus · Llanura central", 2, 8, IslandLayout.RegionCenter(5), new Vector2(47, 36),
                new Vector2(24, 34), 99, 12, false);
            Group("Quetzalcoatlus · Sierras del sur", 3, 5, IslandLayout.ToWorld(new Vector2(0, 118)),
                IslandLayout.ToWorld(new Vector2(88, 80)), new Vector2(22, 31), 127 * IslandLayout.Scale, 31, true, true);
            Group("Triceratops · Pradera central", 0, 6, IslandLayout.ToWorld(new Vector2(30, 300)), new Vector2(30, 32),
                new Vector2(11, 14), 74, 3, false, tint: 1);
            Group("Triceratops · Valle del norte", 0, 6, IslandLayout.ToWorld(new Vector2(40, 610)), new Vector2(30, 32),
                new Vector2(11, 14), 68, 15, false, tint: 2);
            Group("Argentinosaurus · Costa sudoeste", 2, 6, IslandLayout.ToWorld(new Vector2(-150, 250)), new Vector2(36, 34),
                new Vector2(24, 34), 88, 27, false, tint: 3);
            Group("Velociraptor · Bosque del sur", 1, 6, IslandLayout.ToWorld(new Vector2(140, 240)), new Vector2(36, 34),
                new Vector2(5.6f, 8.3f), 61, 17, false, tint: 3);
            return result;
        }

        private static void Link(Dinosaur animal, WildlifeHerd herd, int slot)
        {
            animal.Herd = herd;
            animal.HerdSlot = slot;
        }

        private static Vector3 GroundPoint(Vector2 point) => new(point.x, ForestWorld.Height(point.x, point.y), point.y);

        private static Vector3 LagoonBank(int lake, float degrees)
        {
            float angle = degrees * Mathf.Deg2Rad;
            Vector2 ray = Vector2.Scale(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), IslandLayout.LakeSize(lake));
            for (float magnitude = .86f; magnitude < 2.2f; magnitude += .011f)
            {
                Vector2 point = IslandLayout.LakeCenter(lake) + ray * magnitude;
                if (IslandLayout.LagoonDistance(point) > -6.8f)
                    continue;
                Vector3 bank = GroundPoint(point);
                if (bank.y >= IslandLayout.LakeLevel(lake) + .6f)
                    return bank;
            }
            throw new InvalidOperationException("Falta una orilla seca para los Triceratops.");
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name))
                AssetDatabase.CreateFolder(parent, name);
        }

        private static void ConfigureImports()
        {
            foreach (string guid in AssetDatabase.FindAssets("", new[] { Root + "/Models" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                switch (AssetImporter.GetAtPath(path))
                {
                    case ModelImporter model:
                        ConfigureModel(model, path.StartsWith(Root + "/Models/Creatures/", StringComparison.Ordinal),
                            path.StartsWith(ForestWorld.Folder + "/", StringComparison.Ordinal));
                        break;
                    case TextureImporter texture when path.StartsWith(ForestWorld.Folder + "/", StringComparison.Ordinal):
                        ConfigureForestTexture(texture, path);
                        break;
                    case TextureImporter texture when texture.filterMode != FilterMode.Point ||
                        texture.maxTextureSize != 256 || !texture.mipmapEnabled || texture.anisoLevel != 0:
                        texture.filterMode = FilterMode.Point;
                        texture.maxTextureSize = 256;
                        texture.mipmapEnabled = true;
                        texture.anisoLevel = 0;
                        texture.SaveAndReimport();
                        break;
                }
            }
        }

        private static void ConfigureForestTexture(TextureImporter texture, string path)
        {
            bool panorama = path.EndsWith(".hdr", StringComparison.OrdinalIgnoreCase);
            bool cutout = System.IO.Path.GetFileName(path).StartsWith("Leaf", StringComparison.Ordinal);
            var filter = panorama ? FilterMode.Bilinear : FilterMode.Point;
            var wrapV = panorama ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            var alpha = cutout ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            string filename = System.IO.Path.GetFileNameWithoutExtension(path);
            int size = panorama || filename is "MountainRange" or "HikingPeak" or "LeafWoodland" or "LeafGrove" ? 1024 : 512;
            if (texture.textureShape == TextureImporterShape.Texture2D && texture.maxTextureSize == size &&
                texture.filterMode == filter && texture.wrapModeU == TextureWrapMode.Repeat && texture.wrapModeV == wrapV &&
                texture.mipmapEnabled == !panorama && texture.alphaIsTransparency == cutout &&
                texture.sRGBTexture == !panorama && texture.alphaSource == alpha &&
                texture.npotScale == TextureImporterNPOTScale.None && texture.mipMapsPreserveCoverage == cutout)
                return;

            texture.textureShape = TextureImporterShape.Texture2D;
            texture.maxTextureSize = size;
            texture.filterMode = filter;
            texture.wrapModeU = TextureWrapMode.Repeat;
            texture.wrapModeV = wrapV;
            texture.mipmapEnabled = !panorama;
            texture.alphaIsTransparency = cutout;
            texture.sRGBTexture = !panorama;
            texture.alphaSource = alpha;
            texture.npotScale = TextureImporterNPOTScale.None;
            texture.mipMapsPreserveCoverage = cutout;
            texture.alphaTestReferenceValue = .42f;
            texture.SaveAndReimport();
        }

        private static void ConfigureModel(ModelImporter model, bool creature, bool forest)
        {
            bool readableTerrain = forest && model.assetPath.EndsWith("/Terrain.fbx", StringComparison.Ordinal);
            bool growthMesh = creature && model.assetPath.EndsWith("/Triceratops.fbx", StringComparison.Ordinal);
            if (model.importAnimation != creature || model.importCameras || model.importLights || !model.useFileScale ||
                model.materialImportMode != ModelImporterMaterialImportMode.None ||
                (forest && (!model.preserveHierarchy || (readableTerrain && !model.isReadable))) ||
                (creature && (model.animationType != ModelImporterAnimationType.Generic || growthMesh && !model.isReadable)))
            {
                model.importAnimation = creature;
                model.importCameras = false;
                model.importLights = false;
                model.useFileScale = true;
                model.materialImportMode = ModelImporterMaterialImportMode.None;
                if (forest)
                {
                    model.preserveHierarchy = true;
                    if (readableTerrain)
                        model.isReadable = true;
                }
                if (creature)
                {
                    model.animationType = ModelImporterAnimationType.Generic;
                    model.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    model.animationCompression = ModelImporterAnimationCompression.Optimal;
                    if (growthMesh)
                        model.isReadable = true;
                }
                model.SaveAndReimport();
            }

            if (!creature)
                return;

            ModelImporterClipAnimation[] clips = model.defaultClipAnimations;
            bool changed = clips.Length != model.clipAnimations.Length;
            for (int index = 0; index < clips.Length; index++)
            {
                ModelImporterClipAnimation clip = clips[index];
                string take = clip.takeName;
                clip.name = take.Substring(take.LastIndexOf('|') + 1);
                clip.loopTime = clip.name is "Idle" or "Walk" or "Run" or "Fly" or "Crouch" or "Sneak" or "Feed" or "Drink" or "Court" or "Sleep";
                clip.loopPose = clip.loopTime;
                clip.lockRootRotation = true;
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = true;
                if (index < model.clipAnimations.Length)
                {
                    ModelImporterClipAnimation current = model.clipAnimations[index];
                    changed |= current.name != clip.name || current.firstFrame != clip.firstFrame ||
                        current.lastFrame != clip.lastFrame || current.loopTime != clip.loopTime || !current.lockRootPositionXZ;
                }
            }
            if (changed)
            {
                model.clipAnimations = clips;
                model.SaveAndReimport();
            }
        }

        private static Expedition.Habitat[] MakeHabitats() => new Expedition.Habitat[]
        {
            new()
            {
                Name = "Bahía del sur", Route = "Playa, bosque y manadas de Triceratops",
                Fog = new Color(.18f, .265f, .25f),
                Light = new Color(1.07f, 1.02f, .87f), FogRange = new Vector2(170, 1010)
            },
            new()
            {
                Name = "Bosque occidental", Route = "Oeste · arboledas de gigantes",
                Fog = new Color(.23f, .315f, .335f),
                Light = new Color(1.10f, 1.05f, .91f), FogRange = new Vector2(165, 1080)
            },
            new()
            {
                Name = "Cabos del norte", Route = "Norte · cordillera y praderas",
                Fog = new Color(.245f, .29f, .275f),
                Light = new Color(1.0f, .99f, .89f), FogRange = new Vector2(250, 1490)
            },
            new()
            {
                Name = "Laguna oriental", Route = "Este · aguas bajas y arcos de piedra",
                Fog = new Color(.25f, .33f, .38f),
                Light = new Color(1.08f, 1.04f, .95f), FogRange = new Vector2(180, 1230)
            },
            new()
            {
                Name = "Altos del noreste", Route = "Noreste · abetos y laderas de roca",
                Fog = new Color(.27f, .34f, .36f),
                Light = new Color(1.12f, 1.08f, .98f), FogRange = new Vector2(240, 1480)
            },
            new()
            {
                Name = "Llanura central", Route = "Interior · manadas de saurópodos",
                Fog = new Color(.30f, .35f, .42f),
                Light = new Color(1.13f, 1.09f, 1.03f), FogRange = new Vector2(205, 1325)
            }
        };

        private static Mesh SaveMesh(List<Vector3> points, List<Color> colors, List<int> faces, string name)
        {
            Mesh mesh = new() { vertices = points.ToArray(), colors = colors.ToArray(), triangles = faces.ToArray() };
            mesh.RecalculateNormals();
            return Persist(mesh, name);
        }

        private static Mesh MakeContactShadow()
        {
            var points = new List<Vector3>();
            var colors = new List<Color>();
            var faces = new List<int>();
            const int sides = 20;
            for (int ring = 0; ring < 4; ring++)
            {
                float radius = Mathf.Max(0.01f, ring / 3f);
                int first = points.Count;
                float opacity = 0.43f * (1 - Mathf.SmoothStep(0, 1, radius));
                for (int i = 0; i < sides; i++)
                {
                    float angle = Mathf.PI * 2 * i / sides;
                    points.Add(new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius));
                    colors.Add(new Color(0.028f, 0.022f, 0.016f, opacity));
                    if (ring == 0)
                        continue;
                    int a = first + i;
                    int b = first + (i + 1) % sides;
                    faces.AddRange(new[] { a - sides, b - sides, a, b - sides, b, a });
                }
            }
            return SaveMesh(points, colors, faces, "Sombras.asset");
        }

        private static Sighting PlaceCreature(Animal animal, Transform parent, Mesh footprint, Material contact,
            ExpeditionAudioSetup audio, GroundTremor tremor, bool playable = false)
        {
            GameObject anchor = new(animal.Name, typeof(CharacterController), typeof(Sighting), typeof(Dinosaur));
            anchor.layer = Dinosaur.CreatureLayer;
            anchor.transform.SetParent(parent);
            Vector3 position = playable ? IslandLayout.ToWorld(animal.Position) : animal.Position;
            anchor.transform.position = new Vector3(position.x, position.y + ForestWorld.Height(position.x, position.z), position.z);
            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/Models/Creatures/{animal.Model}.png");
            Material material = Surface(animal.Model + Coats[coat].Suffix, Coats[coat].Tint, albedo, 0.53f, 0.10f, false);
            material.SetFloat("_StippleWeight", .058f);
            EditorUtility.SetDirty(material);
            string modelPath = $"{Root}/Models/Creatures/{animal.Model}.fbx";
            GameObject model = ImportModel("Creatures/" + animal.Model, anchor.transform, material);
            Transform[] joints = model.GetComponentsInChildren<Transform>();
            Transform hip = Array.Find(joints, item => item.name == "Hips");
            Transform head = Array.Find(joints, item => item.name == "Head");
            Vector3 forward = head.position - hip.position;
            float correction = -Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            model.transform.rotation = Quaternion.Euler(0, correction, 0) * model.transform.rotation;
            SkinnedMeshRenderer skin = model.GetComponentInChildren<SkinnedMeshRenderer>();
            skin.shadowCastingMode = ShadowCastingMode.On;
            skin.receiveShadows = true;
            Vector3 extent = skin.sharedMesh.bounds.extents;
            Vector3 axis = skin.localToWorldMatrix.transpose.MultiplyVector(
                animal.Traits.CanFly ? Vector3.right : Vector3.forward);
            float length = 2 * (extent.x * Mathf.Abs(axis.x) + extent.y * Mathf.Abs(axis.y) + extent.z * Mathf.Abs(axis.z));
            if (length < 0.01f)
                throw new InvalidOperationException($"El rig de {animal.Name} tiene una escala inválida.");
            model.transform.localScale *= animal.Length / length;
            Bounds bind = SkinBounds(skin);
            model.transform.position += Vector3.up * (anchor.transform.position.y - bind.min.y);

            Bounds envelope = skin.localBounds;
            envelope.Expand(envelope.size * 0.65f);
            skin.localBounds = envelope;
            Animator animator = model.GetComponentInChildren<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var clips = new Dictionary<string, AnimationClip>(StringComparer.Ordinal);
            foreach (UnityEngine.Object part in AssetDatabase.LoadAllAssetsAtPath(modelPath))
            {
                if (part is AnimationClip clip && !clip.name.StartsWith("__", StringComparison.Ordinal))
                    clips.Add(clip.name, clip);
            }
            GameObject focus = new("Objetivo de avistamiento");
            focus.transform.SetParent(anchor.transform, false);
            focus.transform.localPosition = new Vector3(0, animal.Traits.CameraHeight, 0.35f);
            anchor.transform.rotation = Quaternion.Euler(0, animal.Heading, 0);
            Sighting sighting = anchor.GetComponent<Sighting>();
            sighting.Species = animal.Name;
            sighting.FieldNote = animal.Note;
            sighting.Focus = focus.transform;
            sighting.Range = animal.Range;

            CharacterController capsule = anchor.GetComponent<CharacterController>();
            capsule.radius = Mathf.Max(0.28f, animal.Traits.CameraHeight * 0.36f);
            capsule.height = Mathf.Max(0.8f, animal.Traits.CameraHeight * 1.42f);
            capsule.center = Vector3.up * (capsule.height * 0.5f);
            capsule.skinWidth = capsule.radius * 0.1f;
            capsule.stepOffset = Mathf.Clamp(capsule.height * 0.24f, 0.22f, 0.95f);
            capsule.minMoveDistance = 0;
            GameObject shadow = Shape("Sombra de contacto", anchor.transform, footprint, contact);
            shadow.transform.localScale = new Vector3(capsule.radius * 1.85f, 1,
                animal.Length * (animal.Traits.CanFly ? 0.28f : 0.32f));

            Dinosaur creature = anchor.GetComponent<Dinosaur>();
            creature.Traits = animal.Traits;
            creature.Traits.Length = animal.Length;
            creature.Playable = playable;
            creature.Shadow = shadow.transform;
            creature.Motion = new DinosaurMotion
            {
                Animator = animator, Idle = clips["Idle"], Walk = clips["Walk"], Run = clips["Run"], Attack = clips["Attack"],
                Crouch = clips["Crouch"], Sneak = clips["Sneak"], Death = clips["Death"],
                Topple = clips["Topple"], Feed = clips["Feed"], Drink = clips["Drink"], Court = clips["Court"], Sleep = clips["Sleep"],
                Fly = animal.Traits.CanFly ? clips["Fly"] : null,
                Dive = animal.Traits.CanFly ? clips["Dive"] : null,
                Jaw = Array.Find(joints, item => item.name == "Jaw"), BodyModel = model.transform, Head = head
            };
            if (animal.Traits.CanFly)
            {
                creature.Motion.GroundedModel = model.transform;
                var restMesh = new Mesh();
                skin.BakeMesh(restMesh, useScale: true);
                Vector3[] feet = restMesh.vertices;
                creature.Motion.LeftFoot = GroundMarker(joints, "Hind.03.L", feet, skin.transform, anchor.transform.right);
                creature.Motion.RightFoot = GroundMarker(joints, "Hind.03.R", feet, skin.transform, anchor.transform.right);
                UnityEngine.Object.DestroyImmediate(restMesh);
            }
            Transform tail = animal.Traits.TailStrike ? Array.Find(joints, item => item.name == "Tail.04") : null;
            audio.AddCreature(creature, animal.Model, head, tail, animal.Length, tremor);
            Debug.Log($"{animal.Name}: {skin.bones.Length} huesos, {clips.Count} clips, escala {animal.Length:0.##} m.");
            return sighting;
        }

        private static Transform GroundMarker(Transform[] joints, string foot, Vector3[] vertices, Transform skin, Vector3 right)
        {
            Transform bone = Array.Find(joints, item => item.name == foot);
            Vector3 contact = bone.position;
            foreach (Vector3 vertex in vertices)
            {
                Vector3 world = skin.TransformPoint(vertex);
                Vector3 difference = world - bone.position;
                if (world.y >= contact.y || Mathf.Abs(Vector3.Dot(difference, right)) > .31f)
                    continue;
                difference.y = 0;
                if (difference.sqrMagnitude < 1.30f * 1.30f)
                    contact = world;
            }
            var marker = new GameObject("Apoyo del pie").transform;
            marker.SetParent(bone, false);
            marker.position = contact;
            return marker;
        }

        private static Bounds SkinBounds(SkinnedMeshRenderer skin)
        {
            Mesh baked = new();
            skin.BakeMesh(baked, useScale: true);
            Vector3[] vertices = baked.vertices;
            Matrix4x4 matrix = skin.localToWorldMatrix;
            var bounds = new Bounds(matrix.MultiplyPoint3x4(vertices[0]), Vector3.zero);
            for (int i = 1; i < vertices.Length; i++)
                bounds.Encapsulate(matrix.MultiplyPoint3x4(vertices[i]));
            UnityEngine.Object.DestroyImmediate(baked);
            return bounds;
        }

        private static GameObject ImportModel(string path, Transform parent, Material material)
        {
            string modelPath = $"{Root}/Models/{path}.fbx";
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (asset == null)
                throw new InvalidOperationException($"Falta el modelo {modelPath}");

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>())
                ConfigureRenderer(renderer, material);
            return instance;
        }

        private static GameObject Shape(string name, Transform parent, Mesh mesh, Material material)
        {
            GameObject obj = new(name, typeof(MeshFilter), typeof(MeshRenderer));
            obj.transform.SetParent(parent, false);
            obj.GetComponent<MeshFilter>().sharedMesh = mesh;
            ConfigureRenderer(obj.GetComponent<MeshRenderer>(), material);
            return obj;
        }

        private static void ConfigureRenderer(Renderer renderer, Material material)
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private static Material Surface(string name, Color tint, Texture2D texture, float affine,
            float emission = 0, bool useVertexColor = true)
        {
            Material mat = new(Shader.Find("Jurassic Pix/PSX Surface"));
            mat.color = tint;
            if (texture != null)
                mat.mainTexture = texture;
            mat.SetFloat("_Affine", affine);
            mat.SetFloat("_Unlit", emission);
            mat.SetFloat("_VertexColor", useVertexColor ? 1 : 0);
            return Persist(mat, name + ".mat");
        }

        private static T Persist<T>(T asset, string name) where T : UnityEngine.Object
        {
            string path = Generated + name;
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            asset.name = System.IO.Path.GetFileNameWithoutExtension(name);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(asset, path);
                return asset;
            }
            EditorUtility.CopySerialized(asset, existing);
            UnityEngine.Object.DestroyImmediate(asset);
            return existing;
        }
    }
}
