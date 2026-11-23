using System.Collections.Generic;
using UnityEngine;

namespace JurassicPix
{
    [DefaultExecutionOrder(40)]
    [RequireComponent(typeof(Dinosaur))]
    public sealed class DinosaurAudio : MonoBehaviour
    {
        [SerializeField] private AudioSource voice, body, strike;
        [SerializeField] private AudioClip call, swing, impact, wing;
        [SerializeField] private AudioClip[] steps, wet, bites;
        [SerializeField] private bool quadruped;
        [SerializeField, Range(0, 1)] private float runningForeOffset = .42f;
        [SerializeField] private float pitch = 1;
        [SerializeField] private float mass = .15f, footWidth = .48f, footLength = .48f;
        [SerializeField] private GroundTremor ground;
        [SerializeField] private Transform listener;
        [SerializeField] private AudioLowPassFilter voiceOcclusion;

        // Mezcla compartida: quién se escucha caminar, cada cuánto llama un grupo y cuántos rugidos se pisan.
        private static readonly List<DinosaurAudio> chorus = new(), walkers = new();
        private static readonly Dictionary<object, float> groupCalls = new();
        private static float nextRanking, nextIdleCall, nextBattleCry;

        private Dinosaur creature;
        private float previousStride, previousFlight, previousAttack = -1;
        private float nextCall, nextBite;
        private bool wasFlying, wasImpact;
        private bool wasDead;
        private Vector3 previousPosition;
        private int nextStep;
        private float nextOcclusion, occlusionCutoff = 22000, vocalStart, vocalEnd;
        private LifeActivity lastActivity;
        private float distance = 1000, hearing = 200, stepGain;
        private bool focus;
        private AnimationCurve[] blends;

        public void Configure(AudioSource mouth, AudioSource feet, AudioSource contact, AudioClip vocal,
            AudioClip[] footsteps, AudioClip swish, AudioClip hit, AudioClip wings, bool fourLegs,
            float runningOffset, float voicePitch, float stepMass, float width, float length, GroundTremor tremor,
            AudioClip[] splashes, AudioClip[] mouthfuls)
        {
            voice = mouth;
            body = feet;
            strike = contact;
            call = vocal;
            steps = footsteps;
            swing = swish;
            impact = hit;
            wing = wings;
            quadruped = fourLegs;
            runningForeOffset = runningOffset;
            pitch = voicePitch;
            mass = stepMass;
            footWidth = width;
            footLength = length;
            ground = tremor;
            wet = splashes;
            bites = mouthfuls;
            voiceOcclusion = voice.GetComponent<AudioLowPassFilter>();
        }

        internal void SetListener(Transform ears) => listener = ears;

        // La criatura del jugador suena casi sin atenuación ni paneo duro: se escucha como propia.
        internal void SetFocus(bool own)
        {
            if (focus == own || blends == null)
                return;
            focus = own;
            AudioSource[] sources = { voice, body, strike };
            for (int i = 0; i < sources.Length; i++)
            {
                if (own)
                    sources[i].spatialBlend = .3f;
                else
                    sources[i].SetCustomCurve(AudioSourceCurveType.SpatialBlend, blends[i]);
            }
            voiceOcclusion.cutoffFrequency = occlusionCutoff = 22000;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetMix()
        {
            chorus.Clear();
            walkers.Clear();
            groupCalls.Clear();
            nextRanking = nextIdleCall = nextBattleCry = 0;
        }

        private void Awake()
        {
            blends = new[]
            {
                voice.GetCustomCurve(AudioSourceCurveType.SpatialBlend), body.GetCustomCurve(AudioSourceCurveType.SpatialBlend),
                strike.GetCustomCurve(AudioSourceCurveType.SpatialBlend)
            };
            hearing = Mathf.Max(voice.maxDistance, body.maxDistance) + 15;
        }

        private void OnEnable() => chorus.Add(this);
        private void OnDisable() => chorus.Remove(this);

        private void Start()
        {
            creature = GetComponent<Dinosaur>();
            previousPosition = transform.position;
            wasFlying = creature.IsFlying;
            nextStep = Random.Range(0, steps.Length);
            nextCall = Time.time + Random.Range(4f, 20f);
            nextOcclusion = Time.time + Random.value * .25f;
        }

        private static void Rank(Vector3 ears)
        {
            nextRanking = Time.time + .2f;
            walkers.Clear();
            foreach (DinosaurAudio member in chorus)
            {
                Dinosaur animal = member.creature;
                if (animal == null)
                    continue;
                member.distance = Vector3.Distance(animal.Spot, ears);
                member.stepGain = member.focus ? 1 : 0;
                if (!member.focus && member.distance < member.body.maxDistance && !animal.IsDead && animal.IsPresent &&
                    animal.Motion.GaitWeight > .12f)
                    walkers.Add(member);
            }
            // Solo los pasos más cercanos suenan enteros; el resto de la manada queda de fondo o en silencio.
            walkers.Sort((a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < walkers.Count; i++)
                walkers[i].stepGain = i < 3 ? 1 : i < 7 ? .5f : i < 12 ? .22f : 0;
        }

        private void LateUpdate()
        {
            if (listener != null && Time.time >= nextRanking)
                Rank(listener.position);
            if (creature.IsDead || !creature.IsPresent)
            {
                if (!wasDead)
                {
                    wasDead = true;
                    voice.Stop();
                    body.Stop();
                    strike.Stop();
                }
                return;
            }
            if (wasDead)
            {
                wasDead = false;
                previousPosition = transform.position;
                previousAttack = -1;
                wasImpact = false;
                wasFlying = creature.IsFlying;
                nextCall = Time.time + Random.Range(4, 12);
            }
            DinosaurMotion motion = creature.Motion;
            float stride = motion.StridePhase;
            float flight = motion.FlightPhase;
            float attack = creature.AttackProgress;
            bool flying = creature.IsFlying;
            if (!focus && distance > hearing)
            {
                // Fuera del alcance del oído no se reproduce nada: solo se sigue el estado para cuando se acerque.
                previousPosition = creature.Spot;
                previousStride = stride;
                previousFlight = flight;
                previousAttack = attack;
                wasFlying = flying;
                wasImpact = creature.AttackConnected;
                lastActivity = creature.Life != null ? creature.Life.Activity : LifeActivity.None;
                return;
            }
            LifeActivity activity = creature.Life != null ? creature.Life.Activity : LifeActivity.None;
            if (activity != lastActivity && (activity is LifeActivity.Alert or LifeActivity.Panic or LifeActivity.Shelter) &&
                !voice.isPlaying)
                Vocalize(false);
            lastActivity = activity;
            Vector3 travel = creature.Spot - previousPosition;
            if (flying)
            {
                if (!wasFlying || Crossed(previousFlight, flight, .34f))
                    PlayWing(motion.GaitWeight);
            }
            else if (wasFlying)
                PlayFootstep(1f, 0, false);
            else if (attack < 0 && motion.GaitWeight > .12f && travel.x * travel.x + travel.z * travel.z > .0000001f)
                PlayPlantContacts(stride, motion.GaitWeight);

            if (attack >= 0)
            {
                if (previousAttack < 0)
                    Vocalize(true);
                float swingPhase = creature.Traits.ImpactPhase * .7f;
                if (previousAttack < swingPhase && attack >= swingPhase)
                    strike.PlayOneShot(swing, .72f);
            }
            else if (Time.time >= nextCall && !voice.isPlaying)
                Vocalize(false);

            if (creature.AttackConnected && !wasImpact)
                strike.PlayOneShot(impact, 1);
            if (attack < 0 && Time.time >= nextBite)
                Chew();

            if (listener != null && !focus && voice.isPlaying)
                UpdateOcclusion();
            if (attack < 0 && Time.time < vocalEnd)
            {
                float progress = Mathf.InverseLerp(vocalStart, vocalEnd, Time.time);
                creature.Motion.OpenMouth((1 - progress) * Mathf.Sin(progress * Mathf.PI * 2) *
                    Mathf.Sin(progress * Mathf.PI * 2) * 12, transform.right);
            }
            previousPosition = creature.Spot;
            previousStride = stride;
            previousFlight = flight;
            previousAttack = attack;
            wasFlying = flying;
            wasImpact = creature.AttackConnected;
        }

        private void PlayPlantContacts(float stride, float gait)
        {
            if (Crossed(previousStride, stride, 0))
                PlayFootstep(gait, 1, false);
            if (Crossed(previousStride, stride, .5f))
                PlayFootstep(gait, -1, false);
            if (!quadruped)
                return;

            // These offsets match the foreleg contacts in the skeletal walk and run clips.
            float foreOffset = Mathf.Lerp(.25f, runningForeOffset, Mathf.Clamp01(gait - 1));
            if (Crossed(previousStride, stride, 1 - foreOffset))
                PlayFootstep(gait, 1, true);
            if (Crossed(previousStride, stride, .5f - foreOffset))
                PlayFootstep(gait, -1, true);
        }

        private void PlayFootstep(float gait, int side, bool front)
        {
            ground.Footfall(creature, gait, mass * creature.Size * creature.Size);
            if (stepGain <= 0)
                return;
            body.transform.localPosition = new Vector3(footWidth * side, .25f, front ? footLength : -footLength * .36f) * creature.Size;
            float run = Mathf.Clamp01(gait - 1);
            body.pitch = Random.Range(.95f, 1.05f) * Mathf.Lerp(1, .88f, run * mass) * Mathf.Lerp(1.25f, 1, creature.Size);
            float weight = (Mathf.Lerp(.37f, .72f, Mathf.Clamp01(gait)) + .27f * run) * (front ? .87f : 1) *
                Mathf.Lerp(.39f, 1, creature.Size) * stepGain;
            Vector3 place = creature.Spot;
            bool wading = wet.Length > 0 && IslandLayout.SurfaceLevel(new Vector2(place.x, place.z)) - place.y > .12f;
            body.PlayOneShot(steps[nextStep], wading ? weight * .45f : weight);
            if (wading)
                body.PlayOneShot(wet[nextStep % wet.Length], Mathf.Min(1, weight * 2.2f));
            nextStep = (nextStep + 1) % steps.Length;
        }

        private void PlayWing(float gait)
        {
            if (stepGain <= 0)
                return;
            body.pitch = Random.Range(.89f, .97f);
            body.transform.localPosition = Vector3.up * .65f;
            body.PlayOneShot(wing, Mathf.Lerp(.42f, .82f, Mathf.Clamp01(gait * .6f)) * stepGain);
        }

        // Bocados al comer o pastar y lengüetazos al beber, solo para lo que está cerca.
        private void Chew()
        {
            DinosaurDeed deed = creature.Deed;
            if (deed == DinosaurDeed.None || !focus && distance > 45)
            {
                nextBite = Time.time + .3f;
                return;
            }
            bool sipping = deed == DinosaurDeed.Drink;
            AudioClip[] clips = sipping ? wet : bites;
            nextBite = Time.time + (sipping ? Random.Range(.55f, .85f) : Random.Range(.8f, 1.3f));
            if (clips.Length == 0)
                return;
            body.transform.localPosition = new Vector3(0, .4f, footLength * 1.4f) * creature.Size;
            body.pitch = Random.Range(.9f, 1.1f) * Mathf.Lerp(1.3f, sipping ? 1 : .78f, creature.Size);
            body.PlayOneShot(clips[Random.Range(0, clips.Length)], (sipping ? .5f : .8f) * (focus ? 1 : .6f));
        }

        private bool MayCall(bool attacking)
        {
            if (focus)
                return true;
            float now = Time.time;
            if (distance > voice.maxDistance)
                return false;
            if (attacking)
            {
                if (now < nextBattleCry)
                    return false;
                nextBattleCry = now + .45f;
                return true;
            }
            object group = creature.Life != null ? creature.Life.Community : creature.Herd != null ? creature.Herd : this;
            groupCalls.TryGetValue(group, out float ready);
            if (now < nextIdleCall || now < ready)
                return false;
            nextIdleCall = now + Random.Range(2.5f, 5f);
            groupCalls[group] = now + Random.Range(8f, 15f);
            return true;
        }

        private void Vocalize(bool attacking)
        {
            if (!MayCall(attacking))
            {
                nextCall = Time.time + Random.Range(3f, 8f);
                return;
            }
            nextCall = Time.time + Random.Range(13f, 29f);
            if (attacking)
                voice.Stop();
            voice.pitch = pitch * Mathf.Lerp(1.42f, 1, creature.Size) *
                (attacking ? Random.Range(.96f, 1.04f) : Random.Range(.84f, .94f));
            voice.PlayOneShot(call, attacking ? 1 : .6f);
            vocalStart = Time.time;
            vocalEnd = vocalStart + call.length / voice.pitch;
        }

        private void UpdateOcclusion()
        {
            if (Time.time >= nextOcclusion)
            {
                nextOcclusion = Time.time + .26f;
                bool obstructed = Physics.Linecast(listener.position, voice.transform.position, Dinosaur.Scenery,
                    QueryTriggerInteraction.Ignore);
                occlusionCutoff = obstructed ? 1900 : 22000;
            }
            voiceOcclusion.cutoffFrequency = Mathf.Lerp(voiceOcclusion.cutoffFrequency, occlusionCutoff,
                1 - Mathf.Exp(-Time.deltaTime * 7));
        }

        private static bool Crossed(float before, float after, float mark)
        {
            float offset = Mathf.Repeat(mark - before, 1);
            return offset > 0 && offset <= Mathf.Repeat(after - before, 1);
        }
    }
}
