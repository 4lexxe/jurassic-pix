using System.Collections.Generic;
using UnityEngine;

namespace JurassicPix
{
    public sealed class GroundTremor : MonoBehaviour
    {
        [SerializeField] private AudioSource stampede;
        private Dinosaur[] animals;
        private float[] lastRunStep;
        private readonly Dictionary<Dinosaur, int> slots = new();
        private Vector3 listener;
        private float energy;

        public void SetSource(AudioSource source) => stampede = source;

        internal void Bind(Dinosaur[] population)
        {
            animals = population;
            lastRunStep = new float[animals.Length];
            for (int i = 0; i < animals.Length; i++)
            {
                slots.Add(animals[i], i);
                lastRunStep[i] = -10;
            }
        }

        internal void Footfall(Dinosaur sender, float gait, float mass)
        {
            if (!slots.TryGetValue(sender, out int slot) || sender.Length < 5)
                return;

            if (gait > 1.22f)
                lastRunStep[slot] = Time.time;
            float distance = Vector3.Distance(listener, sender.transform.position);
            float reach = Mathf.Lerp(25, 89, mass);
            float attenuation = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(3, reach, distance));
            float running = Mathf.Clamp01(gait - 1);
            float impulse = Mathf.Lerp(.020f, .102f, running) * mass * attenuation * Mathf.Clamp01(gait + .16f);
            energy = Mathf.Min(.16f, energy + impulse);
        }

        internal Vector3 Sample(Vector3 camera, float delta)
        {
            listener = camera;
            energy *= Mathf.Exp(-delta * 7.5f);
            int running = 0;
            Vector3 center = Vector3.zero;
            float strength = 0;
            float now = Time.time;
            for (int i = 0; i < animals.Length; i++)
            {
                Vector3 point = animals[i].Spot;
                if (now - lastRunStep[i] > .82f || animals[i].IsFlying || animals[i].IsDead || !animals[i].IsPresent ||
                    (point - listener).sqrMagnitude > 6500)
                    continue;
                running++;
                center += point;
                strength += 1 - Mathf.Clamp01(Vector3.Distance(point, listener) / 81);
            }
            float collective = running < 3 ? 0 : Mathf.Clamp01(strength * .21f);
            stampede.volume = Mathf.MoveTowards(stampede.volume, collective * .56f, delta * .9f);
            if (running >= 3)
            {
                stampede.transform.position = center / running;
                if (!stampede.isPlaying)
                    stampede.Play();
            }
            else if (stampede.volume <= .001f && stampede.isPlaying)
                stampede.Stop();

            float lift = Mathf.Min(.17f, energy + collective * .045f);
            return new Vector3(Mathf.Sin(now * 28) * .38f, Mathf.Sin(now * 36), Mathf.Sin(now * 21) * .19f) * lift;
        }
    }
}
