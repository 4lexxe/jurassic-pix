using UnityEngine;

namespace JurassicPix
{
    [DefaultExecutionOrder(-90)]
    [RequireComponent(typeof(Light))]
    public sealed class ForestLighting : MonoBehaviour
    {
        private const float NightSine = -.17f, DaySine = .25f;
        private const float Morning = 8.5f;
        private static readonly float Dawn = 6 + Mathf.Asin((NightSine + DaySine) / 2) * 12 / Mathf.PI, Dusk = 24 - Dawn;
        private static readonly Vector3 Moon = new Vector3(.47f, .63f, -.62f).normalized;
        [SerializeField, Min(60)] private float daySeconds = 1200, nightSeconds = 600;
        [SerializeField, Range(0, 24)] private float initialHour = 7.6f;

        private Light sun;
        private float hour;
        private float night;
        private Vector3 direction, disk;
        internal float Hour { get => hour; set => hour = value; }
        internal float NightBlend => night;
        internal float Sight { get; private set; } = 1000;
        private bool IsNight => hour < Dawn || hour >= Dusk;

        public string TimeLabel => IsNight ? "NOCHE" : hour < 11 ? "MAÑANA" : hour < 17 ? "DÍA" : "ATARDECER";

        private void Awake()
        {
            sun = GetComponent<Light>();
            hour = initialHour;
            night = Darkness(hour);
            disk = SunPosition(hour);
            direction = LightDirection(disk, night);
        }

        public void ToggleDayNight()
        {
            hour = Darkness(hour) > .5f ? Morning : 23;
        }

        public void Apply(Color fog, Color light, Vector2 range, float delta, bool advanceClock = true, float rain = 0, float flash = 0)
        {
            if (advanceClock)
                hour = Mathf.Repeat(hour + delta * (IsNight ? (24 - Dusk + Dawn) / nightSeconds : (Dusk - Dawn) / daySeconds), 24);
            night = Mathf.MoveTowards(night, Darkness(hour), delta * .68f);
            float follow = 1 - Mathf.Exp(-delta * 1.5f);
            disk = Vector3.Slerp(disk, SunPosition(hour), follow);
            direction = Vector3.Slerp(direction, LightDirection(disk, night), follow);
            // El sol bajo tiñe la luz y la niebla de un tono cálido al amanecer y al atardecer.
            float low = (1 - Mathf.Clamp01(disk.y * 2.4f)) * (1 - night) * (1 - rain);
            sun.transform.rotation = Quaternion.LookRotation(-direction, Vector3.up);
            sun.color = Color.Lerp(Color.Lerp(new Color(1, .91f, .75f), new Color(1, .62f, .36f), low), new Color(.47f, .66f, 1), night);
            sun.intensity = Mathf.Lerp(1.36f, .13f, night) * Mathf.Lerp(1, .47f, rain) + flash * 1.3f;
            sun.shadowStrength = Mathf.Lerp(.84f, .79f, night) * Mathf.Lerp(1, .56f, rain);
            light = Color.Lerp(light, light * new Color(1.12f, .84f, .62f), low * .85f);
            fog = Color.Lerp(fog, fog * new Color(1.3f, .92f, .7f), low * .6f);
            fog = Color.Lerp(fog, new Color(.20f, .26f, .31f), rain * .82f);
            light = Color.Lerp(light, new Color(.66f, .77f, .87f), rain * .7f);
            // La lluvia cierra la vista: la niebla empieza a pocos metros y tapa todo a unos 250 m.
            range = Vector2.Lerp(range, new Vector2(12, 250), rain * .94f);
            ApplyGlobals(fog, light, range, direction, disk, night);
        }

        public void Preview(Color fog, Color light, Vector2 range, float previewHour)
        {
            Awake();
            hour = previewHour;
            night = Darkness(hour);
            disk = SunPosition(hour);
            direction = LightDirection(disk, night);
            Apply(fog, light, range, 0);
        }

        private static float Darkness(float time)
        {
            return 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(NightSine, DaySine, Mathf.Sin((time - 6) * Mathf.PI / 12)));
        }

        // El sol sale por el este (+X) a las 6, pasa a 60° de altura por el norte (+Z) al mediodía y se pone por el oeste a las 18.
        private static Vector3 SunPosition(float time)
        {
            float turn = (time - 6) * Mathf.PI / 12;
            return new Vector3(Mathf.Cos(turn), Mathf.Sin(turn) * .866f, Mathf.Sin(turn) * .5f);
        }

        private static Vector3 LightDirection(Vector3 sunPosition, float darkness)
        {
            // Cerca del horizonte la luz se mantiene algo elevada para que las sombras no se estiren al infinito.
            Vector3 day = new Vector3(sunPosition.x, Mathf.Max(sunPosition.y, .2f), sunPosition.z).normalized;
            return Vector3.Slerp(day, Moon, darkness);
        }

        private void ApplyGlobals(Color fog, Color light, Vector2 range, Vector3 direction, Vector3 sunPosition, float darkness)
        {
            Color nightFog = new(.0014f, .0024f, .0058f);
            Color nightLight = new(.028f, .042f, .078f);
            Vector2 dark = new(Mathf.Min(16, range.x), Mathf.Min(170, range.y));
            range = Vector2.Lerp(range, dark, darkness);
            Sight = range.y;
            Shader.SetGlobalColor("_HabitatFog", Color.Lerp(fog, nightFog, darkness));
            Shader.SetGlobalColor("_HabitatLight", Color.Lerp(light, nightLight, darkness));
            Shader.SetGlobalVector("_HabitatFogRange", new Vector4(range.x, range.y, 0, 0));
            Shader.SetGlobalVector("_ForestSunDirection", new Vector4(direction.x, direction.y, direction.z, 1));
            Shader.SetGlobalVector("_ForestSunDisk", new Vector4(sunPosition.x, sunPosition.y, sunPosition.z, 1));
            Shader.SetGlobalVector("_ForestMoonDirection", Moon);
            Shader.SetGlobalFloat("_ForestNight", darkness);
            IslandLayout.Publish();
        }
    }
}
