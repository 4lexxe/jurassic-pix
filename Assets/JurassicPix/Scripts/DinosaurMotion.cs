using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace JurassicPix
{
    [Serializable]
    public sealed class DinosaurMotion
    {
        public Animator Animator;
        public AnimationClip Idle, Walk, Run, Attack, Fly, Dive, Crouch, Sneak, Death, Topple, Feed, Drink, Court, Sleep;
        public Transform Jaw, GroundedModel, LeftFoot, RightFoot;
        public Transform BodyModel, Head;
        internal Transform Observer { private get; set; }
        internal bool Feeding { private get; set; }
        internal float ToppleTime { private get; set; } = -1;
        internal float Stagger { private get; set; }
        internal float Breath { private get; set; }
        internal bool Watched { private get; set; } = true;
        internal bool Visible => skin == null || skin.isVisible;
        internal float ToppleDuration => Topple != null ? Topple.length : 0;

        private PlayableGraph graph;
        private AnimationMixerPlayable mixer;
        private AnimationClipPlayable[] sources;
        private AnimationClip[] clips;
        private float stride, idleTime, flightTime, movement, flight;
        private Quaternion jawPose;
        private Vector3 restModel;
        private float footAdjustment;
        private Vector3 bodyPosition, bodyScale;
        private Quaternion bodyRotation, headPose;
        private float fall, brace, ageScale = 1, feeding, resting;
        private float nextPoseTime, timeSincePose;
        private Transform chest;
        private Transform[] neck, tail;
        private float[] tailLag;
        private float heading, bend, glance, turnRate, crouching, yawnAt, yawnLength, nextYawn;
        private float tailDodge, tailDodgeGoal, nextTailProbe, eating, sipping, wooing, dozing;
        private static readonly RaycastHit[] Obstacles = new RaycastHit[8];
        private Renderer skin;
        private Vector3 headScale;
        private float youth;
        private bool shown = true, bigHead;

        internal float StridePhase => stride;
        internal float FlightPhase => Fly == null ? 0 : flightTime / Fly.length;
        internal float GaitWeight => movement;

        public float AttackDuration(bool airborne) => (airborne && Dive != null ? Dive : Attack).length;

        public void Initialize()
        {
            if (GroundedModel != null)
                restModel = GroundedModel.localPosition;
            bodyPosition = BodyModel.localPosition;
            bodyRotation = BodyModel.localRotation;
            bodyScale = BodyModel.localScale;
            skin = BodyModel.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (Head != null)
                headScale = Head.localScale;
            var joints = new Dictionary<string, Transform>();
            foreach (Transform joint in Animator.GetComponentsInChildren<Transform>(true))
                joints[joint.name] = joint;
            joints.TryGetValue("Chest", out chest);
            neck = Chain(joints, "Neck");
            tail = Chain(joints, "Tail");
            tailLag = new float[tail.Length];
            heading = BodyModel.parent.eulerAngles.y;
            glance = BodyModel.parent.position.x * .37f + BodyModel.parent.position.z * .11f;
            clips = new[] { Idle, Walk, Run, Attack, Fly, Dive, Crouch, Sneak, Death, Topple, Feed, Drink, Court, Sleep };
            nextYawn = Time.time + UnityEngine.Random.Range(3f, 16f);
            sources = new AnimationClipPlayable[clips.Length];
            graph = PlayableGraph.Create(Animator.name + " skeletal motion");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            mixer = AnimationMixerPlayable.Create(graph, clips.Length);
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] == null)
                    continue;

                sources[i] = AnimationClipPlayable.Create(graph, clips[i]);
                sources[i].SetApplyFootIK(false);
                graph.Connect(sources[i], 0, mixer, i);
            }
            AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "Skeleton", Animator);
            output.SetSourcePlayable(mixer);
            Animator.applyRootMotion = false;
            graph.Play();
        }

        public void Evaluate(float delta, float gait, bool airborne, float attackTime, bool dive, bool guard = false,
            LifeActivity life = LifeActivity.None, bool crouch = false)
        {
            float yaw = BodyModel.parent.eulerAngles.y;
            if (delta > 0)
                turnRate = Mathf.Lerp(turnRate, Mathf.DeltaAngle(heading, yaw) / delta, 1 - Mathf.Exp(-delta * 8));
            heading = yaw;
            if (!airborne)
                gait = Mathf.Max(gait, Mathf.Clamp01(Mathf.Abs(turnRate) / 36) * .8f);
            crouching = Mathf.MoveTowards(crouching, crouch && !airborne && Crouch != null ? 1 : 0, delta * 3.6f);
            movement = Mathf.MoveTowards(movement, gait, delta * 5.7f);
            flight = Mathf.MoveTowards(flight, airborne ? 1 : 0, delta * 4.3f);
            float runWeight = Mathf.Clamp01(movement - 1);
            float walkRun = Mathf.Clamp01(movement);
            float cycleRate = Mathf.Lerp(1 / Walk.length, 1 / Run.length, runWeight);
            stride = Mathf.Repeat(stride + delta * cycleRate * Mathf.Lerp(0.82f, 1.16f, runWeight) * walkRun *
                Mathf.Lerp(1.35f, 1, ageScale), 1);
            idleTime = Mathf.Repeat(idleTime + delta, Idle.length);
            if (Fly != null)
                flightTime = Mathf.Repeat(flightTime + delta, Fly.length);

            timeSincePose += delta;
            // Lejos alcanza con unas 20 poses por segundo; si nadie la ve, con 4.
            bool distant = Observer != null && (BodyModel.position - Observer.position).sqrMagnitude > 150 * 150;
            if (delta > 0 && (distant || !Watched) && Time.time < nextPoseTime)
                return;
            nextPoseTime = Time.time + (Watched ? .047f : .25f);
            float visualDelta = timeSincePose;
            timeSincePose = 0;

            float attackLength = AttackDuration(dive);
            float attackWeight = attackTime < 0 ? 0
                : Mathf.SmoothStep(0, 1, Mathf.Min(attackTime / 0.085f, (attackLength - attackTime) / 0.16f));
            float toppled = Topple == null || ToppleTime < 0 ? 0
                : Mathf.SmoothStep(0, 1, Mathf.Min(ToppleTime / .1f, (Topple.length - ToppleTime) / .25f));
            attackWeight *= 1 - toppled;
            eating = Mathf.MoveTowards(eating, (Feeding || life == LifeActivity.Graze) && Feed != null ? 1 : 0, visualDelta * 2.6f);
            sipping = Mathf.MoveTowards(sipping, life == LifeActivity.Drink && Drink != null ? 1 : 0, visualDelta * 2.2f);
            wooing = Mathf.MoveTowards(wooing, life == LifeActivity.Court && Court != null ? 1 : 0, visualDelta * 2.2f);
            dozing = Mathf.MoveTowards(dozing, life == LifeActivity.Sleep && Sleep != null ? 1 : 0, visualDelta * .7f);
            float plain = (1 - eating) * (1 - sipping) * (1 - wooing) * (1 - dozing);
            float groundWeight = (1 - flight) * (1 - attackWeight) * (1 - toppled);
            float still = (1 - walkRun) * groundWeight, walking = walkRun * (1 - runWeight) * groundWeight;
            Sample(0, idleTime, still * (1 - crouching) * plain);
            Sample(1, stride * Walk.length, walking * (1 - crouching));
            if (Crouch != null)
            {
                Sample(6, Mathf.Repeat(Time.time, Crouch.length), still * crouching * plain);
                Sample(7, stride * Sneak.length, walking * crouching);
                Sample(8, 0, 0);
            }
            if (Topple != null)
            {
                Sample(9, Mathf.Clamp(ToppleTime, 0, Topple.length - .02f), toppled * (1 - flight));
                Sample(10, Mathf.Repeat(Time.time, Feed.length), still * eating * (1 - sipping) * (1 - wooing));
            }
            if (Drink != null)
            {
                Sample(11, Mathf.Repeat(Time.time, Drink.length), still * sipping * (1 - wooing));
                Sample(12, Mathf.Repeat(Time.time, Court.length), still * wooing);
            }
            if (Sleep != null)
            {
                Sample(13, Mathf.Repeat(Time.time + glance, Sleep.length), still * dozing);
            }
            Sample(2, stride * Run.length, walkRun * runWeight * groundWeight);
            Sample(3, Mathf.Max(attackTime, 0), dive ? 0 : attackWeight);
            if (Fly != null)
            {
                Sample(4, flightTime, flight * (1 - attackWeight));
                Sample(5, Mathf.Max(attackTime, 0), dive ? attackWeight : 0);
            }
            graph.Evaluate();
            if (bigHead)
            {
                // Las crías tienen la cabeza grande para su cuerpo.
                Head.localScale = headScale * (1 + .34f * youth);
                bigHead = youth > 0;
            }
            if (Jaw != null)
            {
                if (Time.time >= nextYawn)
                {
                    yawnAt = Time.time;
                    yawnLength = UnityEngine.Random.Range(.9f, 2.6f);
                    nextYawn = Time.time + yawnLength + UnityEngine.Random.Range(5f, 19f);
                }
                float yawn = Mathf.Sin(Mathf.PI * Mathf.Clamp01((Time.time - yawnAt) / Mathf.Max(.1f, yawnLength)));
                Vector3 hinge = Jaw.parent.InverseTransformDirection(BodyModel.parent.right);
                float pant = Mathf.Clamp01((Breath - .45f) * 2.4f) * (.55f + .45f * Mathf.Sin(Time.time * 9.5f)) * 11;
                Jaw.localRotation = Quaternion.AngleAxis((yawn * yawn * 15 + pant) * (1 - attackWeight) * plain, hinge) *
                    Jaw.localRotation;
                jawPose = Jaw.localRotation;
            }
            Flex(visualDelta, walkRun, runWeight);
            if (GroundedModel != null)
                AlignFeet(visualDelta);
            if (Head != null)
            {
                headPose = Head.localRotation;
                brace = Mathf.MoveTowards(brace, guard ? 1 : 0, visualDelta * 7);
                float nose = life switch
                {
                    LifeActivity.Drink => Drink != null ? 0 : 33,
                    LifeActivity.Graze => Feed != null ? 0 : 24 + Mathf.Sin(idleTime * 10) * 3.8f,
                    LifeActivity.Alert or LifeActivity.Panic => -12,
                    LifeActivity.Play => -6 + Mathf.Sin(idleTime * 12) * 5,
                    LifeActivity.Browse => -15 + Mathf.Sin(idleTime * 6) * 4,
                    _ => 0
                };
                feeding = Mathf.MoveTowards(feeding, nose, visualDelta * 77);
                Vector3 right = Head.parent.InverseTransformDirection(BodyModel.parent.right);
                Head.localRotation = Quaternion.AngleAxis(brace * 12 + feeding, right) * headPose;
                foreach (Transform joint in neck)
                    joint.localRotation = Quaternion.AngleAxis(feeding * (neck.Length > 3 ? .45f : .12f),
                        joint.parent.InverseTransformDirection(BodyModel.parent.right)) * joint.localRotation;
            }
            if (GroundedModel == null)
            {
                resting = Mathf.MoveTowards(resting, life == LifeActivity.Rest ? 1 : 0, visualDelta * 1.6f);
                float skip = life == LifeActivity.Play ? Mathf.Abs(Mathf.Sin(stride * Mathf.PI * 2)) * .075f * ageScale : 0;
                BodyModel.localPosition = bodyPosition * ageScale + Vector3.up * (skip - resting * .07f * ageScale);
            }
        }

        private static Transform[] Chain(Dictionary<string, Transform> joints, string prefix)
        {
            var chain = new List<Transform>();
            for (int index = 1; joints.TryGetValue($"{prefix}.{index:00}", out Transform joint); index++)
                chain.Add(joint);
            return chain.ToArray();
        }

        private void Flex(float delta, float walkRun, float runWeight)
        {
            Transform root = BodyModel.parent;
            float turning = Mathf.Clamp(turnRate / 34, -1, 1);
            bend = Mathf.Lerp(bend, turning, 1 - Mathf.Exp(-delta * 5.5f));
            Vector3 up = root.up;
            float idle = (1 - walkRun) * (1 - flight) * (1 - dozing);
            float look = (Mathf.PerlinNoise(Time.time * .12f, glance) - .5f) * 34 * idle;
            float share = (bend * 36 + look) / (neck.Length + 1.5f);
            if (chest != null)
                chest.rotation = Quaternion.AngleAxis(bend * 9, up) * chest.rotation;
            foreach (Transform joint in neck)
                joint.rotation = Quaternion.AngleAxis(share, up) * joint.rotation;
            if (Head != null)
                Head.rotation = Quaternion.AngleAxis(share * 1.5f, up) * Head.rotation;
            if (tail.Length > 1 && Time.time >= nextTailProbe)
                ProbeTail(root);
            tailDodge = Mathf.Lerp(tailDodge, tailDodgeGoal, 1 - Mathf.Exp(-delta * 5));
            for (int index = 0; index < tail.Length; index++)
            {
                tailLag[index] = Mathf.Lerp(tailLag[index], turning, 1 - Mathf.Exp(-delta * (4.6f - index * .75f)));
                float swing = (tailDodge * 62 - tailLag[index] * 46) / tail.Length;
                tail[index].rotation = Quaternion.AngleAxis(swing, up) * tail[index].rotation;
            }
            float lean = bend * Mathf.Lerp(Mathf.Lerp(3, 8, runWeight), 24, flight);
            float reel = Stagger * (1 - flight);
            float sway = Mathf.Sin(Time.time * 11.5f + glance) * 7.5f * reel;
            if (Head != null)
                Head.rotation = Quaternion.AngleAxis(-sway * 1.4f, up) * Head.rotation;
            BodyModel.localRotation = Quaternion.AngleAxis(sway - lean, Vector3.forward) *
                Quaternion.AngleAxis(Mathf.Sin(Time.time * 7.3f + glance) * 3.5f * reel + 3 * reel, Vector3.right) * bodyRotation;
        }

        private void ProbeTail(Transform root)
        {
            nextTailProbe = Time.time + .12f;
            tailDodgeGoal = 0;
            float length = 0;
            for (int index = 1; index < tail.Length; index++)
                length += (tail[index].position - tail[index - 1].position).magnitude;
            length *= tail.Length / (tail.Length - 1f);
            Vector3 origin = tail[0].position, back = -root.forward;
            float reach = length * .78f, nearest = float.PositiveInfinity;
            int found = Physics.SphereCastNonAlloc(origin + back * (length * .22f), Mathf.Max(.12f, length * .07f), back, Obstacles,
                reach, (1 << 0) | (1 << Dinosaur.CreatureLayer), QueryTriggerInteraction.Ignore);
            for (int index = 0; index < found; index++)
            {
                RaycastHit hit = Obstacles[index];
                if (hit.distance >= nearest || hit.transform.IsChildOf(root))
                    continue;
                nearest = hit.distance;
                Vector3 spot = hit.distance > 0 ? hit.point : hit.collider.bounds.center;
                tailDodgeGoal = (Vector3.Dot(spot - origin, root.right) >= 0 ? 1 : -1) * Mathf.Lerp(1, .35f, hit.distance / reach);
            }
        }

        internal void SetYouth(float value)
        {
            youth = value;
            bigHead |= Head != null;
        }

        internal void Show(bool visible)
        {
            if (shown == visible || skin == null)
                return;
            shown = visible;
            skin.enabled = visible;
        }

        internal void SetGrowth(float scale)
        {
            ageScale = scale;
            BodyModel.localScale = bodyScale * ageScale;
            BodyModel.localPosition = bodyPosition * ageScale;
        }

        internal void Fall(float delta, float stature)
        {
            if (Death == null)
            {
                fall = Mathf.MoveTowards(fall, 1, delta * 1.48f);
                float curve = Mathf.SmoothStep(0, 1, fall);
                BodyModel.localPosition = bodyPosition * ageScale + Vector3.up * (curve * stature * .24f);
                BodyModel.localRotation = Quaternion.AngleAxis(-81 * curve, Vector3.forward) * bodyRotation;
                return;
            }
            bool settled = fall >= Death.length;
            fall = Mathf.Min(fall + delta, Death.length);
            if (settled && delta > 0)
                return;
            for (int input = 0; input < clips.Length; input++)
            {
                if (clips[input] != null)
                    mixer.SetInputWeight(input, input == 8 ? 1 : 0);
            }
            sources[8].SetTime(Mathf.Min(fall, Death.length - .02f));
            graph.Evaluate();
            BodyModel.SetLocalPositionAndRotation(bodyPosition * ageScale, bodyRotation);
        }

        internal void Stand()
        {
            fall = brace = footAdjustment = feeding = resting = bend = turnRate = crouching = tailDodge = tailDodgeGoal = 0;
            heading = BodyModel.parent.eulerAngles.y;
            nextPoseTime = timeSincePose = 0;
            BodyModel.SetLocalPositionAndRotation(bodyPosition * ageScale, bodyRotation);
        }

        internal void OpenMouth(float amount, Vector3 right)
        {
            if (Jaw != null)
                Jaw.localRotation = Quaternion.AngleAxis(amount, Jaw.parent.InverseTransformDirection(right)) * jawPose;
        }

        private void AlignFeet(float delta)
        {
            float target = 0;
            if (flight < .98f)
            {
                float left = FloorOffset(LeftFoot.position);
                float right = FloorOffset(RightFoot.position);
                target = Mathf.Clamp(Mathf.Max(left, right) + footAdjustment, -.95f, .46f) * (1 - flight);
            }
            footAdjustment = Mathf.Lerp(footAdjustment, target, delta <= 0 ? 1 : 1 - Mathf.Exp(-delta * 20));
            GroundedModel.localPosition = restModel * ageScale + Vector3.up * footAdjustment;
        }

        private static float FloorOffset(Vector3 foot)
        {
            if (Physics.Raycast(foot + Vector3.up * 2.3f, Vector3.down, out RaycastHit ground, 5.4f,
                    1 << Dinosaur.TerrainLayer, QueryTriggerInteraction.Ignore))
                return ground.point.y - foot.y + .018f;
            return 0;
        }

        private void Sample(int input, float time, float weight)
        {
            sources[input].SetTime(time);
            mixer.SetInputWeight(input, weight);
        }

        public void Dispose()
        {
            if (graph.IsValid())
                graph.Destroy();
        }
    }
}
