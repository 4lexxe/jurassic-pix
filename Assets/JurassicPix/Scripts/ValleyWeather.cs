using UnityEngine;

namespace JurassicPix
{
    [DefaultExecutionOrder(-70)]
    public sealed class ValleyWeather : MonoBehaviour
    {
        private const float Cycle = 174;
        private const int Bolts = 7;
        [SerializeField] private Transform listener;
        [SerializeField] private ParticleSystem drops;
        [SerializeField] private AudioSource rainSound, downpour;
        [SerializeField] private AudioSource[] thunder;
        [SerializeField, Tooltip("Truenos ordenados del más cercano al más lejano.")] private AudioClip[] claps;
        [SerializeField, Range(0, 1)] private float rainChance = .3f;

        private float elapsed, lastPlayed = -1;
        private bool raining;
        private int voice;
        private Vector4 bolt;
        private float thunderAt = -1, rumble;
        internal float Elapsed => elapsed;
        internal float Rain { get; private set; }
        internal float Wetness { get; private set; }
        internal float Wind { get; private set; }
        internal float Flash { get; private set; }
        // Mientras retumba un trueno el resto del ambiente baja para que se imponga.
        internal float Rumble => rumble;
        internal string Label => Rain > .25f ? "TORMENTA" : Wind > .43f ? "RÁFAGAS" : "DESPEJADO";

        public void Configure(Transform eyes, ParticleSystem particles, AudioSource rain, AudioSource heavy,
            AudioSource[] strikes, AudioClip[] thunderClaps)
        {
            listener = eyes;
            drops = particles;
            rainSound = rain;
            downpour = heavy;
            thunder = strikes;
            claps = thunderClaps;
        }

        private void Awake()
        {
            elapsed = Random.Range(0, 64) * Cycle;
        }

        private void Start()
        {
            rainSound.volume = downpour.volume = 0;
            rainSound.Play();
            downpour.Play();
        }

        internal void Synchronize(float serverElapsed)
        {
            if (Mathf.Abs(serverElapsed - elapsed) > .25f)
                elapsed = serverElapsed;
        }

        internal void NextPhase()
        {
            int cycle = Mathf.FloorToInt(elapsed / Cycle), storm = cycle;
            bool passed = Stormy(cycle) && elapsed - cycle * Cycle >= 57;
            while (!passed && !Stormy(storm) && storm < cycle + 64)
                storm++;
            elapsed = !passed && Stormy(storm) ? storm * Cycle + 69 : (cycle + 1) * Cycle;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            int cycle = Mathf.FloorToInt(elapsed / Cycle);
            float cycleStart = cycle * Cycle;
            float phase = elapsed - cycleStart;
            bool storm = Stormy(cycle);
            Rain = storm ? Smooth(61, 74, phase) * (1 - Smooth(127, 151, phase)) : 0;
            Wetness = SurfaceWetness(storm, phase);
            float gale = Smooth(43, 68, phase) * (1 - Smooth(129, 164, phase));
            Wind = .14f + gale * (.53f + .22f * (.5f + .5f * Mathf.Sin(elapsed * 1.47f)));
            Flash = 0;
            bolt.w = 0;
            for (int i = 0; storm && i < Bolts; i++)
            {
                // Cada relámpago cae a una hora, un rumbo y una distancia propios de su tormenta.
                float strike = 72 + i * 9.2f + Chance(cycle, i * 3) * 5;
                float since = phase - strike;
                if (since < 0 || since > .62f)
                    continue;
                float bearing = Chance(cycle, i * 3 + 1) * Mathf.PI * 2;
                float far = Chance(cycle, i * 3 + 2);
                float flicker = since < .09f || since > .17f && since < .30f ? 1 : .4f;
                Flash = Mathf.Lerp(1, .5f, far) * (1 - Smooth(.05f, .62f, since)) * flicker;
                bolt = new Vector4(Mathf.Cos(bearing), Mathf.Sin(bearing), Chance(cycle, i * 3 + 7) * 40, Flash * Mathf.Lerp(1, .6f, far));
                float at = cycleStart + strike;
                if (lastPlayed < at)
                {
                    lastPlayed = at;
                    Thunder(new Vector3(bolt.x, 0, bolt.y), far);
                }
            }
            if (thunderAt >= 0 && Time.time >= thunderAt)
            {
                thunderAt = -1;
                rumble = 1;
            }
            rumble = Mathf.MoveTowards(rumble, 0, Time.deltaTime / 4);
            Shader.SetGlobalVector("_ValleyWeather", new Vector4(Wind, Rain, Flash, elapsed));
            Shader.SetGlobalFloat("_SurfaceWetness", Wetness);
            Shader.SetGlobalVector("_StormBolt", bolt);
        }

        private void Thunder(Vector3 toward, float far)
        {
            if (thunder.Length == 0 || claps.Length == 0)
                return;
            // El trueno suena sin atenuación 3D: el volumen y el lado dependen de dónde cayó el rayo respecto de la cámara.
            float delay = Mathf.Lerp(140, 1300, far * far) / 343f;
            float side = Vector3.Dot(listener.right, toward) * .72f;
            int clap = Mathf.Min(claps.Length - 1, Mathf.FloorToInt(far * claps.Length));
            Clap(claps[clap], Mathf.Lerp(1, .88f, far), side, delay);
            // Debajo de cada estallido suena además el retumbo largo, para que el trueno llene y no se corte seco.
            if (clap != 1 && claps.Length > 1)
                Clap(claps[1], Mathf.Lerp(.8f, .6f, far), side * .5f, delay + .12f);
            thunderAt = Time.time + delay;
        }

        private void Clap(AudioClip clip, float volume, float side, float delay)
        {
            AudioSource source = thunder[voice++ % thunder.Length];
            source.Stop();
            source.clip = clip;
            source.volume = volume;
            source.pitch = Random.Range(.94f, 1.05f);
            source.panStereo = side;
            source.PlayDelayed(delay);
        }

        private void LateUpdate()
        {
            float duck = 1 - rumble * .5f;
            rainSound.volume = Rain * .62f * duck;
            downpour.volume = Smooth(.4f, 1, Rain) * .66f * duck;
            Vector3 ahead = listener.forward;
            ahead.y = 0;
            drops.transform.position = listener.position + ahead.normalized * 9 + new Vector3(-2, 11, 0);
            var emission = drops.emission;
            emission.rateOverTime = Rain * (Application.isMobilePlatform ? 1300 : 3400);
            var velocity = drops.velocityOverLifetime;
            velocity.x = Wind * 6.5f;
            velocity.z = Wind * 2.3f;
            bool active = Rain > .015f;
            if (active == raining)
                return;
            raining = active;
            if (active)
                drops.Play();
            else
                drops.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private bool Stormy(int cycle) => Chance(cycle, 0) < rainChance;

        internal static float SurfaceWetness(bool storm, float phase) =>
            storm ? Smooth(61, 85, phase) * (1 - Smooth(142, Cycle, phase)) : 0;

        private static float Chance(int cycle, int salt)
        {
            unchecked
            {
                uint hash = (uint)cycle + 0x9E3779B9 + (uint)salt * 0x85EBCA6B;
                hash = (hash ^ hash >> 16) * 0x7FEB352Du;
                hash = (hash ^ hash >> 15) * 0x846CA68B;
                return ((hash ^ hash >> 16) >> 8) / 16777216f;
            }
        }

        private static float Smooth(float start, float end, float value) =>
            Mathf.SmoothStep(0, 1, Mathf.InverseLerp(start, end, value));
    }
}
