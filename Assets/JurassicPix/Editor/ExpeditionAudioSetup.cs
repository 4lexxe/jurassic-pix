using System;
using UnityEditor;
using UnityEngine;

namespace JurassicPix.Editor
{
    internal sealed class ExpeditionAudioSetup
    {
        internal const string Folder = "Assets/JurassicPix/Audio/";

        private readonly AudioClip[] lightSteps = Steps("Pisada_Seca");
        private readonly AudioClip[] heavySteps = Steps("Pisada_Gigante", 2);
        private readonly AudioClip[] wetSteps = Steps("Pisada_Agua", 3);
        private readonly AudioClip[] bites = Steps("Bocado");
        private readonly AudioClip swing = Load("Barrido");
        private readonly AudioClip impact = Load("Impacto");

        public void AddCreature(Dinosaur creature, string model, Transform head, Transform tail, float length, GroundTremor ground)
        {
            var (fourLegs, heavy, pitch, volume, mass) = model switch
            {
                "Triceratops" => (true, true, 1.03f, .74f, .48f),
                "Argentinosaurus" => (true, true, .93f, .92f, 1f),
                "Tyrannosaurus" => (false, true, .96f, .92f, .65f),
                "Velociraptor" => (false, false, 1.16f, .70f, .14f),
                "Quetzalcoatlus" => (false, false, 1.00f, .80f, .20f),
                _ => throw new InvalidOperationException($"Falta el perfil de audio de {model}.")
            };
            // La caída es inversa a la distancia: lo cercano domina la mezcla y lo lejano queda apenas de fondo.
            float close = Mathf.Clamp(length * 1.1f, 6, 26);
            float reach = Mathf.Clamp(length * 7f, 60, 200);
            AudioSource voice = Spatial("Voz de la criatura", head, volume, close, reach, 88);
            voice.gameObject.AddComponent<AudioLowPassFilter>().cutoffFrequency = 22000;
            AudioSource body = Spatial("Pisadas y aleteo", creature.transform, heavy ? .46f : .66f, close * .85f, reach * .6f, 109);
            body.transform.localPosition = Vector3.up * .35f;
            if (creature.Traits.CanFly)
                body.dopplerLevel = .18f;
            AudioSource strike = Spatial("Barrido e impacto", tail != null ? tail : head, .78f, close, reach * .75f, 91);
            creature.gameObject.AddComponent<DinosaurAudio>().Configure(voice, body, strike, Load("Voz_" + model),
                heavy ? heavySteps : lightSteps, swing, impact, creature.Traits.CanFly ? Load("Aleteo") : null,
                fourLegs, creature.Traits.TailStrike ? .25f : .42f, pitch, mass,
                Mathf.Clamp(length * .11f, .32f, 2.6f), Mathf.Clamp(length * .19f, .46f, 5.4f), ground, wetSteps, bites);
        }

        public GroundTremor AddTremor()
        {
            var terrain = new GameObject("Pisoteo del valle", typeof(GroundTremor)).GetComponent<GroundTremor>();
            AudioSource stampede = Spatial("Estampida", terrain.transform, 0, 22, 130, 89);
            stampede.clip = Load("Estampida");
            stampede.loop = true;
            terrain.SetSource(stampede);
            return terrain;
        }

        public FlightWind AddFlightWind(Transform listener)
        {
            FlightWind flight = listener.gameObject.AddComponent<FlightWind>();
            flight.SetSource(Bed(listener, "Viento de vuelo", "Ambiente_Vuelo"));
            return flight;
        }

        public ValleyWeather AddWeather(Transform listener)
        {
            var weather = new GameObject("Clima del valle", typeof(ValleyWeather)).GetComponent<ValleyWeather>();
            AudioSource rain = Bed(weather.transform, "Lluvia en hojas", "Ambiente_Lluvia");
            AudioSource downpour = Bed(weather.transform, "Aguacero", "Ambiente_Aguacero");
            // Los truenos no usan distancia 3D: ValleyWeather decide el volumen y el lado según dónde cae el rayo.
            var thunder = new AudioSource[5];
            for (int i = 0; i < thunder.Length; i++)
                thunder[i] = Source($"Trueno {i + 1}", weather.transform, 1, 20);
            AudioClip[] claps = { Load("Trueno_Tormenta"), Load("Trueno_Largo"), Load("Trueno_Valle"), Load("Trueno_Lejano") };
            var storm = new GameObject("Gotas de lluvia", typeof(ParticleSystem)).GetComponent<ParticleSystem>();
            storm.transform.SetParent(weather.transform, false);
            storm.transform.rotation = Quaternion.Euler(90, 0, 0);
            var main = storm.main;
            main.playOnAwake = false;
            main.loop = true;
            main.startSpeed = new ParticleSystem.MinMaxCurve(24, 31);
            main.startLifetime = new ParticleSystem.MinMaxCurve(.7f, 1.05f);
            main.startSize = new ParticleSystem.MinMaxCurve(.03f, .065f);
            main.startColor = new Color(.72f, .82f, .88f, .38f);
            main.maxParticles = 5200;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var shape = storm.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(46, 46, .75f);
            var velocity = storm.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            var streaks = storm.GetComponent<ParticleSystemRenderer>();
            streaks.renderMode = ParticleSystemRenderMode.Stretch;
            streaks.velocityScale = .05f;
            streaks.lengthScale = 3f;
            streaks.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            const string path = "Assets/JurassicPix/Generated/Lluvia.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Hidden/Jurassic Pix/Rain"));
                AssetDatabase.CreateAsset(material, path);
            }
            streaks.sharedMaterial = material;
            weather.Configure(listener, storm, rain, downpour, thunder, claps);
            return weather;
        }

        public AudioSource AddInterface(Transform listener)
        {
            AudioSource foley = Source("Sonidos de la interfaz", listener, .6f, 69);
            foley.clip = Load("Diario");
            return foley;
        }

        public void AddForest(ForestLighting clock, Transform listener, ValleyWeather weather)
        {
            var soundscape = new GameObject("Paisaje sonoro", typeof(ForestAmbience));
            Transform root = soundscape.transform;
            AudioSource day = Bed(root, "Aves de día", "Ambiente_Pajaros");
            AudioSource night = Bed(root, "Insectos de noche", "Ambiente_Grillos");
            AudioSource wind = Bed(root, "Viento del valle y la cordillera", "Ambiente_Brisa");
            AudioSource surf = Bed(root, "Oleaje de la isla", "Ambiente_Costa");
            AudioSource water = Bed(root, "Agua de lagos y orillas", "Ambiente_Agua");
            Vector2[] points = { new(-60, 163), new(45, 165), new(-273, 367), new(267, 372), new(-137, 459),
                new(-128, 675), new(182, 548) };
            var perches = new AudioSource[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                Vector2 point = IslandLayout.ToWorld(points[i]);
                AudioSource source = Spatial($"Percha {i + 1}", root, .42f, 12, 95, 127);
                source.transform.position = new Vector3(point.x, ForestWorld.Height(point.x, point.y) + 8 + i, point.y);
                perches[i] = source;
            }
            soundscape.GetComponent<ForestAmbience>().Configure(clock, listener, weather, day, night, wind, surf, water, perches,
                Load("Ave_Bosque"), Load("Ave_Nocturna"));
        }

        private static AudioSource Bed(Transform parent, string name, string file)
        {
            AudioSource source = Source(name, parent, 0, 42);
            source.clip = Load(file);
            source.loop = true;
            return source;
        }

        private static AudioSource Spatial(string name, Transform parent, float volume, float minDistance, float maxDistance, int priority)
        {
            AudioSource source = Source(name, parent, volume, priority);
            source.rolloffMode = AudioRolloffMode.Custom;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            float near = Mathf.Clamp(minDistance / maxDistance, .01f, .8f);
            var falloff = new (float, float)[14];
            falloff[0] = (0, 1);
            for (int i = 0; i < 13; i++)
            {
                float x = Mathf.Lerp(near, 1, i / 12f);
                falloff[i + 1] = (x, Mathf.Clamp01((near / x - near) / (1 - near)));
            }
            source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, Curve(falloff));
            // De cerca el sonido se abre entre los dos parlantes en vez de saltar entero a un lado; lejos se ubica con precisión.
            source.SetCustomCurve(AudioSourceCurveType.Spread,
                Curve((0, .42f), (near, .28f), (Mathf.Lerp(near, 1, .3f), .07f), (1, 0)));
            source.SetCustomCurve(AudioSourceCurveType.SpatialBlend,
                Curve((0, .6f), (near, .88f), (Mathf.Lerp(near, 1, .2f), 1), (1, 1)));
            return source;
        }

        private static AnimationCurve Curve(params (float Time, float Value)[] points)
        {
            var keys = new Keyframe[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                float before = i > 0 ? (points[i].Value - points[i - 1].Value) / (points[i].Time - points[i - 1].Time) : 0;
                float after = i < points.Length - 1 ? (points[i + 1].Value - points[i].Value) / (points[i + 1].Time - points[i].Time) : 0;
                keys[i] = new Keyframe(points[i].Time, points[i].Value, i > 0 ? before : after, i < points.Length - 1 ? after : before);
            }
            return new AnimationCurve(keys);
        }

        private static AudioSource Source(string name, Transform parent, float volume, int priority)
        {
            AudioSource source = new GameObject(name, typeof(AudioSource)).GetComponent<AudioSource>();
            source.transform.SetParent(parent, false);
            source.playOnAwake = false;
            source.dopplerLevel = 0;
            source.volume = volume;
            source.priority = priority;
            return source;
        }

        private static AudioClip[] Steps(string prefix, int count = 4)
        {
            var clips = new AudioClip[count];
            for (int i = 0; i < count; i++)
                clips[i] = Load($"{prefix}_{i + 1}");
            return clips;
        }

        private static AudioClip Load(string name)
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Folder + name + ".wav");
            if (clip == null)
                throw new InvalidOperationException($"Falta el sonido {name}.");
            return clip;
        }
    }

    internal sealed class ExpeditionAudioImport : AssetPostprocessor
    {
        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(ExpeditionAudioSetup.Folder, StringComparison.Ordinal))
                return;

            var importer = (AudioImporter)assetImporter;
            bool ambience = assetPath.StartsWith(ExpeditionAudioSetup.Folder + "Ambiente_", StringComparison.Ordinal);
            importer.defaultSampleSettings = new AudioImporterSampleSettings
            {
                loadType = ambience ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad,
                compressionFormat = ambience ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM,
                sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate,
                quality = .66f,
                preloadAudioData = !ambience
            };
        }
    }
}
