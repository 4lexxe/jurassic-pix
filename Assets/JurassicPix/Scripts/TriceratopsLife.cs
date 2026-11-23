using UnityEngine;

namespace JurassicPix
{
    public enum TriceratopsStage { Calf, Juvenile, Adult }

    [RequireComponent(typeof(Dinosaur))]
    public sealed class TriceratopsLife : MonoBehaviour
    {
        public const float JuvenileAge = 72, AdultAge = 205, LeaderStature = 1.12f;
        [SerializeField] private TriceratopsHerd community;
        [SerializeField] private SkinnedMeshRenderer skin;
        [SerializeField] private GameObject egg;
        [SerializeField] private float initialAge;
        [SerializeField] private bool female, vacant;

        private Dinosaur animal;
        private TriceratopsLife protector;
        private TriceratopsRoute route;
        private float age, eggSeconds, appliedAge = -1, hunger, thirst, fatigue;
        private float recovery, decline, stature = 1, appliedStature = 1;
        private int knownSites;
        private Vector3 facing;
        private bool posted, catching;

        public TriceratopsHerd Community => community;
        internal Dinosaur Animal => animal;
        internal float Age => age;
        internal float Hunger => hunger;
        internal float Thirst => thirst;
        internal float Fatigue => fatigue;
        internal bool Female => female;
        internal bool Present => Activity is not (LifeActivity.Dormant or LifeActivity.Egg);
        internal float TimeForSnapshot => Activity == LifeActivity.Egg ? eggSeconds : age;
        internal TriceratopsLife Protector => protector;
        internal LifeActivity Activity { get; private set; }
        internal TriceratopsStage Stage => age < JuvenileAge ? TriceratopsStage.Calf :
            age < AdultAge ? TriceratopsStage.Juvenile : TriceratopsStage.Adult;
        internal float Scale { get; private set; } = 1;
        internal float Maturity => age < JuvenileAge ? Dinosaur.Juvenile * age / JuvenileAge
            : Mathf.Lerp(Dinosaur.Juvenile, 1, Mathf.InverseLerp(JuvenileAge, AdultAge, age));
        internal bool CanFight => Stage == TriceratopsStage.Adult && !animal.IsControlled && animal.HealthRatio >= .35f;
        internal float Speed => Stage switch
        {
            TriceratopsStage.Calf => Mathf.Lerp(.63f, .82f, age / JuvenileAge),
            TriceratopsStage.Juvenile => Mathf.Lerp(.82f, 1, (age - JuvenileAge) / (AdultAge - JuvenileAge)),
            _ => 1
        };

        public void Configure(TriceratopsHerd herd, SkinnedMeshRenderer model, GameObject shell, float startAge, bool isFemale, bool poolSlot)
        {
            community = herd;
            skin = model;
            egg = shell;
            initialAge = startAge;
            female = isFemale;
            vacant = poolSlot;
        }

        internal void Initialize(Dinosaur owner)
        {
            animal = owner;
            route = new TriceratopsRoute();
            age = initialAge;
            hunger = .27f + animal.HerdSlot % 4 * .044f;
            thirst = .33f + animal.HerdSlot % 5 * .044f;
            SetActivity(vacant ? LifeActivity.Dormant : LifeActivity.Rest);
            UpdateGrowth();
        }

        internal void Advance(float delta)
        {
            if (Activity == LifeActivity.Egg)
            {
                eggSeconds -= delta;
                if (eggSeconds <= 0)
                    Hatch();
                return;
            }
            if (!Present || animal.IsDead)
                return;

            if (animal.IsControlled)
                SetActivity(LifeActivity.None);
            if (!animal.Playable)
                age += delta;
            UpdateGrowth();
            float travelTime = delta / IslandLayout.Scale;
            hunger = Mathf.Clamp01(hunger + travelTime * (Stage == TriceratopsStage.Calf ? .0038f : .0027f));
            thirst = Mathf.Clamp01(thirst + travelTime * (animal.Motion.GaitWeight > 1 ? .0068f : .0032f));
            fatigue = Mathf.Clamp01(fatigue + delta * (animal.Motion.GaitWeight > 1 ? (Stage == TriceratopsStage.Calf ? .019f : .012f) :
                Activity is LifeActivity.Rest or LifeActivity.Sleep ? -.038f : .0018f));
            if (Activity is LifeActivity.Rest or LifeActivity.Sleep && hunger < .54f && thirst < .50f && !community.HasThreat)
            {
                recovery += delta;
                if (recovery >= 2.7f)
                {
                    recovery = 0;
                    animal.Heal(Mathf.Max(1, animal.MaxHealth / 55));
                }
            }
            float span = community.Lifespan * (.85f + animal.HerdSlot * 37 % 31 * .01f);
            bool weary = age >= span || hunger >= 1 && thirst >= 1;
            if (!weary || animal.Playable)
                return;
            decline += delta;
            if (decline < 4)
                return;
            decline = 0;
            if (age >= span)
                animal.ReceiveHit(null, Mathf.Max(1, animal.MaxHealth / 30), Vector3.zero);
            else if (animal.HealthRatio > .3f)
                animal.ReceiveHit(null, Mathf.Max(1, animal.MaxHealth / 40), Vector3.zero);
        }

        internal void Mature(float maturity)
        {
            age = maturity >= 1 ? Mathf.Max(age, AdultAge) : maturity < Dinosaur.Juvenile ? JuvenileAge * maturity / Dinosaur.Juvenile
                : Mathf.Lerp(JuvenileAge, AdultAge, Mathf.InverseLerp(Dinosaur.Juvenile, 1, maturity));
            UpdateGrowth();
        }

        internal void Sustain(float delta, bool drinking, bool eating)
        {
            if (drinking)
                thirst = Mathf.Max(0, thirst - delta * .15f);
            if (eating)
                hunger = Mathf.Max(0, hunger - delta * .14f);
        }

        internal Vector3 Steer(out bool run, out bool guard, out bool attack)
        {
            run = guard = attack = false;
            facing = Vector3.zero;
            var here = transform.position;
            if (animal.WaterDepth > Mathf.Max(.9f, animal.Height * .45f))
            {
                Vector2 flat = new(here.x, here.z);
                Vector2 shore = IslandLayout.LagoonDistance(flat) > -16 ? flat - IslandLayout.NearestLake(flat) : IslandLayout.Center - flat;
                run = true;
                SetActivity(LifeActivity.Follow);
                return new Vector3(shore.x, 0, shore.y).normalized;
            }
            if (Stage != TriceratopsStage.Adult &&
                (protector == null || !protector.Present || protector.animal.IsDead ||
                    Distance(here, protector.transform.position) > 55 && !community.HasThreat))
                protector = community.FindProtector(this);

            Vector3 goal;
            LifeActivity activity;
            if (community.HasThreat && community.Tactic != HerdTactic.Calm)
                goal = RespondToThreat(here, out activity, out run, out guard, out attack);
            else if (Stage != TriceratopsStage.Adult && animal.HealthRatio < .46f && protector != null)
            {
                goal = BesideProtector();
                activity = Distance(here, goal) > 2.8f ? LifeActivity.Shelter : LifeActivity.Rest;
            }
            else if (Stage != TriceratopsStage.Adult && protector != null &&
                Distance(here, protector.transform.position) > (Stage == TriceratopsStage.Calf ? 9 : 19))
            {
                goal = BesideProtector();
                float separation = Distance(here, protector.transform.position);
                run = separation > 15;
                activity = separation > (Stage == TriceratopsStage.Calf ? 17 : 29) ? LifeActivity.Panic : LifeActivity.Follow;
            }
            else if (Stage != TriceratopsStage.Adult && protector == null && Distance(here, community.Center) > 19)
            {
                goal = community.Rendezvous(knownSites);
                run = true;
                activity = LifeActivity.Panic;
            }
            else
                goal = QuietGoal(here, out activity, out run);

            SetActivity(activity);
            if (activity is LifeActivity.Rest or LifeActivity.Sleep or LifeActivity.Graze or LifeActivity.Drink)
                return Vector3.zero;
            if (facing != Vector3.zero)
                return facing * .1f;

            Vector3 direction = route.Steer(here, goal, run || guard, animal.HerdSlot);
            return Vector3.ClampMagnitude(direction * (guard ? .62f : .28f), run ? 1 : .83f);
        }

        private Vector3 RespondToThreat(Vector3 here, out LifeActivity activity, out bool run, out bool guard, out bool attack)
        {
            guard = attack = false;
            HerdTactic tactic = community.Tactic;
            Vector3 danger = community.ThreatPosition;
            Vector3 away = TriceratopsRoute.Horizontal(here - danger).normalized;
            float distance = Distance(here, danger);
            if (tactic != HerdTactic.Alert)
                posted = false;
            if (Stage != TriceratopsStage.Adult)
            {
                run = true;
                if (tactic == HerdTactic.Stampede)
                {
                    activity = Distance(here, community.Center) > 19 ? LifeActivity.Panic : LifeActivity.Shelter;
                    return community.StampedePost(this);
                }
                float guardianDistance = protector == null ? float.PositiveInfinity : Distance(here, protector.transform.position);
                activity = guardianDistance > 19 ? LifeActivity.Panic : LifeActivity.Shelter;
                if (guardianDistance > 19)
                    return protector != null && guardianDistance <= 65 ? BesideProtector() : community.Rendezvous(knownSites);
                if (guardianDistance > 6 || animal.HealthRatio < .55f)
                    return BesideProtector() + away * 3.5f;
                return community.Center + away * (distance < 13 ? 12 : 6.5f);
            }

            if (tactic == HerdTactic.Stampede && !community.IsCharger(this))
            {
                run = true;
                activity = LifeActivity.Retreat;
                return community.StampedePost(this);
            }

            Dinosaur foe = community.NearestPredator(here, 15);
            if (foe == null)
                foe = community.Threat;
            Vector3 toFoe = TriceratopsRoute.Horizontal(foe.transform.position - here);
            float gap = toFoe.magnitude;
            Vector3 bearing = gap > .01f ? toFoe / gap : transform.forward;
            float reach = animal.Traits.AttackReach * Scale + foe.Width * .4f;
            bool incoming = foe.AttackActive && gap < foe.Traits.AttackReach + animal.Width + 1 &&
                Vector3.Dot(foe.transform.forward, -bearing) > .45f;
            attack = CanFight && !incoming && gap <= reach && animal.ReadyToAttack &&
                Vector3.Dot(transform.forward, bearing) > .57f;
            if (incoming && !attack && !animal.AttackActive)
            {
                run = false;
                guard = true;
                activity = LifeActivity.Defend;
                return foe.transform.position;
            }
            if (!CanFight)
            {
                Vector3 refuge = community.Refuge(this);
                run = Distance(here, refuge) > 6;
                activity = LifeActivity.Retreat;
                return refuge;
            }
            if (community.IsCharger(this) || gap < reach + 4)
            {
                activity = LifeActivity.Defend;
                run = gap > reach * 1.3f;
                if (gap > reach * .8f)
                    return foe.transform.position;
                facing = bearing;
                return here;
            }

            Vector3 post = community.LinePost(this);
            float offset = Distance(here, post);
            posted = offset < (posted ? 5.5f : 2.5f);
            activity = LifeActivity.Alert;
            run = false;
            if (posted)
                facing = TriceratopsRoute.Horizontal(danger - here).normalized;
            return post;
        }

        private Vector3 QuietGoal(Vector3 here, out LifeActivity activity, out bool run)
        {
            run = false;
            if (Stage == TriceratopsStage.Adult && animal.HealthRatio >= .55f)
            {
                TriceratopsLife separated = community.FindSeparatedYoung(this);
                if (separated != null)
                {
                    activity = LifeActivity.Defend;
                    return separated.transform.position;
                }
            }
            if (community.Courting(this, out TriceratopsLife partner))
            {
                Vector3 gap = TriceratopsRoute.Horizontal(partner.transform.position - here);
                if (gap.magnitude > 7.5f)
                {
                    activity = LifeActivity.Follow;
                    return partner.transform.position;
                }
                activity = LifeActivity.Court;
                facing = gap.normalized;
                return here;
            }
            if (community.IsNesting(this))
            {
                activity = LifeActivity.Nest;
                return community.Nest;
            }
            if (community.Sleeping)
            {
                if (community.Sentinel == this)
                {
                    activity = LifeActivity.Alert;
                    facing = new Vector3(Mathf.Sin(Time.time * .22f), 0, Mathf.Cos(Time.time * .22f));
                    return here;
                }
                float angle = animal.HerdSlot * 2.4f;
                Vector3 bed = community.Camp + new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * (5 + animal.HerdSlot % 5 * 3.2f);
                catching = Stage == TriceratopsStage.Adult && Distance(here, bed) > (catching ? 3 : 7);
                if (catching)
                {
                    activity = LifeActivity.Follow;
                    return bed;
                }
                fatigue = Mathf.Max(0, fatigue - animal.Tick * .032f);
                activity = LifeActivity.Sleep;
                return here;
            }

            if (community.TryResource(this, out Vector3 resource, out int site, out bool water))
            {
                if (Distance(here, resource) < (water ? 7.5f : 7.2f))
                {
                    knownSites |= 1 << site;
                    if (water)
                    {
                        thirst = Mathf.Max(0, thirst - animal.Tick * .15f);
                        activity = LifeActivity.Drink;
                    }
                    else
                    {
                        hunger = Mathf.Max(0, hunger - community.Feed(site, animal.Tick) * .14f);
                        activity = LifeActivity.Graze;
                    }
                    return resource;
                }
                run = water && thirst > .5f;
                activity = LifeActivity.Follow;
                return resource;
            }
            float phase = Time.time + animal.HerdSlot * 1.71f;
            bool traveling = community.Traveling;
            if (fatigue > (traveling ? .85f : .57f) || !traveling && Mathf.Repeat(phase, 29) < 6)
            {
                fatigue = Mathf.Max(0, fatigue - animal.Tick * .032f);
                activity = LifeActivity.Rest;
                return here;
            }
            if (traveling && community.Leader == this)
            {
                Vector3 course = community.LeaderGoal(out bool waiting);
                activity = waiting ? LifeActivity.Alert : LifeActivity.Explore;
                return course;
            }
            if (traveling && (Stage == TriceratopsStage.Adult || protector == null))
            {
                Vector3 post = community.FollowPost(this);
                catching = Distance(here, post) > (catching ? 6 : 16);
                run = catching || community.Hurried && Distance(here, post) > 5;
                activity = LifeActivity.Follow;
                return post;
            }
            if (Stage != TriceratopsStage.Adult && protector != null)
            {
                TriceratopsLife playmate = community.FindPlaymate(this);
                if (playmate != null && thirst < .65f && hunger < .7f && Mathf.Repeat(phase, 21) < 8)
                {
                    activity = LifeActivity.Play;
                    return playmate.transform.position + new Vector3(Mathf.Cos(phase * 1.38f), 0, Mathf.Sin(phase * 1.38f)) * 3.7f;
                }
                activity = Stage == TriceratopsStage.Calf ? LifeActivity.Follow : LifeActivity.Explore;
                return BesideProtector() + new Vector3(Mathf.Sin(phase * .35f), 0, Mathf.Cos(phase * .35f)) *
                    (Stage == TriceratopsStage.Calf ? 2.3f : 7.8f);
            }
            activity = LifeActivity.Explore;
            return community.Roam(animal.HerdSlot, phase);
        }

        private Vector3 BesideProtector()
        {
            Transform adult = protector.transform;
            float side = animal.HerdSlot % 2 == 0 ? 1 : -1;
            return adult.position - adult.forward * 4.6f + adult.right * (side * (2.7f + animal.HerdSlot % 3 * .45f));
        }

        internal void LayAt(Vector3 location, float incubation, TriceratopsLife mother)
        {
            protector = mother;
            eggSeconds = incubation;
            transform.SetPositionAndRotation(location, Quaternion.Euler(0, (animal.HerdSlot * 67) % 360, 0));
            SetActivity(LifeActivity.Egg);
        }

        private void Hatch()
        {
            age = 0;
            hunger = .22f;
            thirst = .26f;
            fatigue = .14f;
            recovery = 0;
            knownSites = 0;
            route.Reset();
            SetActivity(LifeActivity.Follow);
            appliedAge = -1;
            UpdateGrowth();
            animal.StartWildLife(transform.position);
            community.RecordBirth();
            if (protector == null || !protector.Present || protector.animal.IsDead)
                protector = community.FindProtector(this);
        }

        internal void Retire()
        {
            SetActivity(LifeActivity.Dormant);
            protector = null;
        }

        internal void Replica(float clock, LifeActivity activity)
        {
            SetActivity(activity);
            if (activity == LifeActivity.Egg)
                eggSeconds = clock;
            else if (activity != LifeActivity.Dormant)
            {
                age = clock;
                UpdateGrowth();
            }
        }

        private void UpdateGrowth()
        {
            stature = Mathf.MoveTowards(stature, community.Leader == this ? LeaderStature : 1, animal.Tick * .02f);
            if (stature == appliedStature && (Mathf.Abs(age - appliedAge) < .36f || (age >= AdultAge && appliedAge >= AdultAge)))
                return;
            appliedAge = age;
            appliedStature = stature;
            float calf = Mathf.Clamp01(age / JuvenileAge);
            float juvenile = Mathf.InverseLerp(JuvenileAge, AdultAge, age);
            float grown = age < JuvenileAge ? Mathf.Lerp(.36f, .64f, calf) : Mathf.Lerp(.64f, 1, juvenile);
            Scale = grown * stature;
            skin.SetBlendShapeWeight(0, (1 - calf) * 100);
            skin.SetBlendShapeWeight(1, (age < JuvenileAge ? calf : 1 - juvenile) * 100);
            animal.ChangeSize(Scale, Mathf.Lerp(.25f, 1, Mathf.InverseLerp(.36f, 1, grown)) * stature);
        }

        private void SetActivity(LifeActivity activity)
        {
            if (Activity == activity)
                return;
            bool hadBody = Present;
            Activity = activity;
            if (hadBody != Present)
                animal.SetLifeVisibility(Present);
            if (egg != null)
                egg.SetActive(activity == LifeActivity.Egg);
        }

        internal static float Distance(Vector3 a, Vector3 b) => TriceratopsRoute.Horizontal(a - b).magnitude;
    }
}
