using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace JurassicPix
{
    [DefaultExecutionOrder(30)]
    public sealed class ForestAmbience : MonoBehaviour
    {
        [SerializeField] private ForestLighting lighting;
        [SerializeField] private ValleyWeather weather;
        [SerializeField] private Transform listener;
        [SerializeField] private AudioSource birds, insects, wind, surf, water;
        [SerializeField] private AudioSource[] perches;
        [SerializeField] private AudioClip birdsong, owl;
        [SerializeField, Range(0, 1)] private float dayVolume = .30f, nightVolume = .27f;
        [SerializeField, Range(0, 1)] private float valleyWind = .15f, ridgeWind = .30f;

        private float[] nextCall;
        private float chorus, chorusGoal, chorusUntil, chorusSide, chorusDrift;

        public void Configure(ForestLighting clock, Transform ears, ValleyWeather conditions, AudioSource day, AudioSource night, AudioSource breeze, AudioSource shore, AudioSource lake,
            AudioSource[] focalPoints, AudioClip dayCall, AudioClip nightCall)
        {
            lighting = clock;
            weather = conditions;
            listener = ears;
            birds = day;
            insects = night;
            wind = breeze;
            surf = shore;
            water = lake;
            perches = focalPoints;
            birdsong = dayCall;
            owl = nightCall;
        }

        private void Start()
        {
            nextCall = new float[perches.Length];
            chorusUntil = Time.time + Random.Range(4f, 10f);
            for (int i = 0; i < nextCall.Length; i++)
                nextCall[i] = Time.time + 2.4f + i * 1.75f;
            UpdateMix();
            birds.Play();
            insects.Play();
            wind.Play();
            surf.Play();
            water.Play();
        }

        private void LateUpdate()
        {
            if (!birds.isPlaying && !insects.isPlaying && !wind.isPlaying)
                return;

            UpdateMix();
            float dark = lighting.NightBlend;
            for (int i = 0; i < perches.Length; i++)
            {
                if (Time.time < nextCall[i])
                    continue;

                AudioSource perch = perches[i];
                float radius = perch.maxDistance * .85f;
                if ((perch.transform.position - listener.position).sqrMagnitude > radius * radius)
                {
                    nextCall[i] = Time.time + 1;
                    continue;
                }
                bool nocturnal = Random.value < dark;
                perch.pitch = Random.Range(.93f, 1.08f);
                perch.PlayOneShot(nocturnal ? owl : birdsong, 1 - weather.Rain * .83f);
                nextCall[i] = Time.time + (nocturnal ? Random.Range(17, 30f) : Random.Range(7, 16f));
            }
        }

        private void UpdateMix()
        {
            float dark = lighting.NightBlend;
            float calm = (1 - weather.Rain * .74f) * (1 - weather.Rumble * .5f);
            // El coro de pájaros no suena todo el tiempo: aparece de a ratos, cada vez desde otro lado, y se va apagando.
            if (Time.time >= chorusUntil)
            {
                chorusGoal = chorusGoal > 0 ? 0 : 1;
                chorusUntil = Time.time + (chorusGoal > 0 ? Random.Range(9f, 20f) : Random.Range(14f, 36f));
                if (chorusGoal > 0)
                {
                    chorusSide = Random.Range(.35f, .85f) * (Random.value < .5f ? -1 : 1);
                    chorusDrift = Random.Range(-.035f, .035f);
                    birds.pitch = Random.Range(.96f, 1.05f);
                }
            }
            chorus = Mathf.MoveTowards(chorus, chorusGoal, Time.deltaTime / 3);
            if (chorusGoal > 0)
                chorusSide = Mathf.Clamp(chorusSide + chorusDrift * Time.deltaTime, -.9f, .9f);
            birds.panStereo = chorusSide;
            birds.volume = dayVolume * 1.25f * Mathf.Sqrt(1 - dark) * calm * chorus;
            insects.volume = nightVolume * Mathf.Sqrt(dark) * calm;
            float exposure = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(310, 660, listener.position.z / IslandLayout.Scale));
            wind.volume = (Mathf.Lerp(valleyWind, ridgeWind, exposure) + weather.Wind * .25f) * (1 - weather.Rumble * .4f);
            Vector3 ears = listener.position;
            var point = new Vector2(ears.x, ears.z);
            float ocean = 1 - Mathf.InverseLerp(12, 100, Mathf.Abs(IslandLayout.ShoreDistance(point)));
            float lake = 1 - Mathf.InverseLerp(4, 34, Mathf.Abs(IslandLayout.LagoonDistance(point)));
            float low = 1 - Mathf.InverseLerp(18, 85, ears.y);
            surf.volume = ocean * .66f * low;
            water.volume = Mathf.Max(lake * .6f, ocean * .25f) * low;
            // El agua se escucha del lado donde está: el lago más cercano o el mar abierto.
            Vector3 right = listener.right;
            Vector2 toLake = IslandLayout.NearestLake(point) - point, toSea = point - IslandLayout.Center;
            float lakeSide = Vector2.Dot(new Vector2(right.x, right.z), toLake.normalized);
            float seaSide = Vector2.Dot(new Vector2(right.x, right.z), toSea.normalized);
            float blend = 1 - Mathf.Exp(-Time.deltaTime * 4);
            water.panStereo = Mathf.Lerp(water.panStereo, (lake * .5f >= ocean * .22f ? lakeSide : seaSide) * .6f, blend);
            surf.panStereo = Mathf.Lerp(surf.panStereo, seaSide * .55f, blend);
        }
    }
}
