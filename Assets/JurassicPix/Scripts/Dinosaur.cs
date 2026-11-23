using UnityEngine;

namespace JurassicPix
{
    internal enum DinosaurDeed : byte { None, Drink, Graze, Feed }

    [DefaultExecutionOrder(-20)]
    [RequireComponent(typeof(CharacterController))]
    public sealed class Dinosaur : MonoBehaviour, IReplicatedCreature
    {
        public enum Temperament { Herd, Pack, Solitary, Flyer }

        [System.Serializable]
        public class Profile
        {
            public string SizeLabel;
            public string AttackName;
            public float Walk, Run, Turn, CameraDistance, CameraHeight;
            public float AttackReach, ImpactPhase, PatrolRadius, FlightSpeed;
            public int MaxHealth, Damage;
            public float Armor, GuardMultiplier, Recovery, Length;
            public Temperament Nature;
            public bool TailStrike, CanFly;
            public float Stamina = 20;
        }

        public const int CreatureLayer = 8;
        public const int TerrainLayer = IslandLayout.TerrainLayer;
        private const int Ground = 1 << TerrainLayer;
        public const int Scenery = (1 << 0) | Ground;
        internal const float Juvenile = .3f;
        private const float GrowthSeconds = 480, BirthScale = .36f;

        // La cámara publica dónde está y hasta dónde se ve para que la fauna que nadie mira gaste menos.
        internal static Vector3 Focus;
        internal static float Sight = 1000;
        internal static bool Throttle = true;
        private static int census;

        public Profile Traits;
        public DinosaurMotion Motion;
        public Transform Shadow;
        public WildlifeHerd Herd;
        public int HerdSlot;
        public bool Playable;

        public bool IsControlled { get; private set; }
        public bool IsFlying { get; private set; }
        public bool AttackActive => attackTime >= 0;
        public bool AttackConnected { get; private set; }
        public bool Guarding { get; private set; }
        public bool Crouching { get; private set; }
        public bool Feeding { get; private set; }
        public bool Toppled => Time.time - toppleAt < Motion.ToppleDuration;
        public bool Latched => perch != null;
        public bool Leading { get; private set; }
        internal BehaviorState State => behavior.State;
        internal float Thirst => OwnNeeds ? ownThirst : Life != null ? Life.Thirst : Herd != null ? Herd.Thirst : behavior.Thirst;
        internal float Hunger => OwnNeeds ? ownHunger : Life != null ? Life.Hunger : Herd != null ? Herd.Hunger : behavior.Hunger;
        internal DinosaurDeed Deed => Feeding ? DinosaurDeed.Feed : Activity switch
        {
            LifeActivity.Drink => DinosaurDeed.Drink, LifeActivity.Graze => DinosaurDeed.Graze, _ => DinosaurDeed.None
        };
        internal float Stamina => stamina;
        internal bool Winded => winded;
        internal float Maturity => Life != null ? Life.Maturity : maturity;
        internal bool Grown => Maturity >= 1;
        internal bool Starving => OwnNeeds && (ownHunger >= 1 || ownThirst >= 1);
        internal bool Drowning => !IsDead && Time.time < waterDamageAt;
        internal Vector3 Spot { get; private set; }
        internal Vector3 Facing { get; private set; }
        internal float Tick { get; private set; }
        public bool IsDead => vitals.Dead;
        public TriceratopsLife Life { get; private set; }
        public bool IsPresent => Life == null || Life.Present;
        internal bool IsReplica => replica;
        internal int Health => vitals.Health;
        internal int MaxHealth => vitals.Maximum;
        internal float HealthRatio => vitals.Ratio;
        internal bool HurtRecently => Time.time - lastHurt < .52f;
        internal float Width => width;
        internal float Size => Life != null ? Life.Scale : scale;
        internal float Length => Traits.Length * Size;
        internal float Height => Traits.CameraHeight * Size;
        internal Vector3 Home => home;
        internal Vector3 Velocity => transform.forward * speed;
        internal bool ReadyToAttack => !IsDead && !AttackActive && attackDelay <= 0;
        public string Species => sighting.Species;
        internal float AttackProgress => AttackActive ? attackTime / Motion.AttackDuration(diving) : -1;

        private Sighting sighting;
        private Dinosaur[] neighbors;
        private CharacterController motor;
        private Vector3 home, requestedDirection, knockback, heading;
        private float stamina = 1, stagger, toppleAt = -100, nextTopple, nextMouthful;
        private bool winded, toppleFlag;
        private Dinosaur perch;
        private int riders;
        private Vector3 perchOffset;
        private float perchUntil, nextGnaw;
        private LifeActivity replicaActivity;
        private Quaternion homeFacing;
        private bool requestedRun, requestedGuard, requestedCrouch, requestedDeed, landing, playedImpact, diving;
        private float ownThirst, ownHunger;
        private LifeActivity deed;
        private float requestedLift, speed, vertical, travelAltitude, attackDelay, attackTime = -1;
        private float deathAt, lastHurt = -10, protectedUntil, waterDamageAt;
        private float groundY;
        private bool replica;
        private DinosaurVitals vitals;
        private DinosaurBehavior behavior;
        private Vector3 adultCenter, adultFocus, adultShadow;
        private float adultRadius, adultHeight, adultStep, adultSkin;
        private float maturity = 1, scale = 1, width, backlog, nextPang;
        private int ordinal;
        private bool watched = true;

        private bool OwnNeeds => replica ? Playable : IsControlled;
        private LifeActivity Activity => IsControlled ? deed : Life != null ? Life.Activity :
            replica ? replicaActivity : behavior.Activity;
        private float Pace => Life != null ? Life.Speed : Mathf.Lerp(.66f, 1, maturity);
        private float WalkSpeed => Traits.Walk * Pace;
        private float RunSpeed => Traits.Run * (Life != null ? Life.Speed * Mathf.Lerp(1, .73f, Life.Fatigue) : Pace);
        private float Gait => speed <= WalkSpeed ? speed / WalkSpeed
            : 1 + Mathf.InverseLerp(WalkSpeed, RunSpeed, speed);

        private void Awake()
        {
            sighting = GetComponent<Sighting>();
            adultFocus = sighting.Focus.localPosition;
            if (Shadow != null)
                adultShadow = Shadow.localScale;
            motor = GetComponent<CharacterController>();
            adultCenter = motor.center;
            adultRadius = motor.radius;
            adultHeight = motor.height;
            adultStep = motor.stepOffset;
            adultSkin = motor.skinWidth;
            width = motor.radius * 2;
            ordinal = census++;
            Track();
            vitals = new DinosaurVitals(Traits.MaxHealth);
            behavior = new DinosaurBehavior(this);
            home = transform.position;
            homeFacing = transform.rotation;
            groundY = GroundUnder(home);
            IsFlying = Traits.CanFly && home.y - groundY > 3;
            travelAltitude = Mathf.Max(5.5f, home.y - groundY);
            Motion.Initialize();
            Motion.Evaluate(0, 0, IsFlying, -1, false);
            Life = GetComponent<TriceratopsLife>();
            if (Life != null)
                Life.Initialize(this);
        }

        public void SetNeighbors(Dinosaur[] animals) => neighbors = animals;

        public void SetControl(bool controlled)
        {
            if (motor == null)
                return;
            if (controlled && !IsControlled)
            {
                ownThirst = Thirst;
                ownHunger = Hunger;
            }
            bool released = IsControlled && !controlled;
            IsControlled = controlled;
            Leading = false;
            Guarding = false;
            deed = LifeActivity.None;
            SetInput(Vector3.zero, false, 0);
            // Sin jugador, la criatura vuelve a ser un adulto silvestre de su grupo.
            if (released && Playable && !replica)
                Mature(1);
        }

        internal void SetLeading(bool lead) => Leading = lead && IsControlled && !IsDead && Grown;

        private void Track()
        {
            Spot = transform.position;
            Facing = transform.forward;
        }

        private void Mature(float value)
        {
            value = Mathf.Clamp01(value);
            if (Life != null)
            {
                if (!replica)
                    Life.Mature(value);
                return;
            }
            if (Mathf.Abs(value - maturity) < .002f && value < 1 == maturity < 1)
                return;
            maturity = value;
            scale = Mathf.Lerp(BirthScale, 1, maturity);
            Motion.SetYouth(1 - maturity);
            ChangeSize(scale, Mathf.Lerp(.25f, 1, maturity));
        }

        // El jugador empieza de cero: una cría junto a su grupo, con poca vida y todo por crecer.
        internal void BeBorn()
        {
            if (!Playable || replica)
                return;
            if (perch != null)
                LetGo();
            Mature(0);
            Vector3 crib = Life != null ? Life.Community.Center : Herd != null && Herd.Count > 0 && !Traits.CanFly ? Herd.Middle : home;
            Reappear(IslandSites.Dry(crib + new Vector3(3, 0, -3)));
            ownThirst = .2f;
            ownHunger = .25f;
            nextPang = 0;
            protectedUntil = Time.time + 6;
        }

        private void Survive(float delta)
        {
            if (!Playable)
                return;
            if (ownHunger < .8f && ownThirst < .8f && Maturity < 1)
                Mature(Maturity + delta / GrowthSeconds);
            if ((ownHunger < 1 && ownThirst < 1) || Time.time < nextPang)
                return;
            nextPang = Time.time + 4;
            if (HealthRatio > .25f)
                ReceiveHit(null, Mathf.Max(1, MaxHealth / 25), Vector3.zero);
        }

        private bool Watch()
        {
            float gap = (Spot - Focus).sqrMagnitude, reach = Sight + Length + 25;
            bool inSight = gap < reach * reach;
            Motion.Show(inSight);
            return gap < 130 * 130 || inSight && Motion.Visible;
        }

        public void SetInput(Vector3 direction, bool run, float lift, bool guard = false, bool crouch = false, bool act = false)
        {
            requestedCrouch = crouch;
            requestedDeed = act;
            requestedDirection = direction;
            requestedRun = run;
            requestedLift = lift;
            requestedGuard = guard;
            if (IsControlled && !IsDead && !AttackActive)
                Guarding = guard;
        }

        void IPlayerControl.SetInput(DinosaurInput input) =>
            SetInput(input.Direction, input.Run, input.Lift, input.Guard, input.Crouch, input.Act);
        void IPlayerControl.Attack() => TryAttack();
        void IPlayerControl.SetLeading(bool lead) => SetLeading(lead);
        void IReplicatedCreature.BeBorn() => BeBorn();
        void IReplicatedCreature.SetReplica(bool enabled) => SetReplica(enabled);
        DinosaurPose IReplicatedCreature.CapturePose() => CapturePose();
        void IReplicatedCreature.ApplyReplica(DinosaurPose pose, float delta) => ApplyReplica(pose, delta);

        internal DinosaurPose CapturePose()
        {
            return new DinosaurPose(transform.position, transform.eulerAngles.y, Gait, attackTime,
                IsFlying, diving, AttackConnected, Health, Guarding,
                Life != null ? Life.TimeForSnapshot : Playable ? maturity * 1000 : 0,
                Activity, Crouching, Toppled, stagger > .15f, Feeding, stamina);
        }

        internal void SetReplica(bool enabled)
        {
            if (motor == null)
                return;
            if (enabled && !replica)
            {
                ownThirst = Thirst;
                ownHunger = Hunger;
            }
            replica = enabled;
            motor.enabled = !enabled && !IsDead && IsPresent;
            SetControl(false);
            speed = vertical = 0;
            knockback = Vector3.zero;
            attackTime = -1;
            attackDelay = 0;
            landing = diving = false;
            AttackConnected = false;
            if (!enabled)
            {
                if (Playable)
                    Mature(1);
                if (IsDead)
                    deathAt = Time.time;
                groundY = GroundUnder(transform.position);
                travelAltitude = Mathf.Max(5.5f, transform.position.y - groundY);
            }
        }

        internal void ApplyReplica(DinosaurPose pose, float delta)
        {
            bool wasDead = IsDead;
            bool wasPresent = IsPresent;
            if (pose.Health < Health)
                lastHurt = Time.time;
            transform.SetPositionAndRotation(pose.Position, Quaternion.Euler(0, pose.Yaw, 0));
            Track();
            watched = Watch();
            if (Life != null)
                Life.Replica(pose.LifeClock, pose.Activity);
            else if (Playable)
            {
                float grown = pose.LifeClock * .001f;
                if (grown < maturity - .05f)
                {
                    ownThirst = .2f;
                    ownHunger = .25f;
                }
                if (Mathf.Abs(grown - maturity) >= .002f || grown >= 1 != maturity >= 1)
                {
                    maturity = Mathf.Clamp01(grown);
                    scale = Mathf.Lerp(BirthScale, 1, maturity);
                    Motion.SetYouth(1 - maturity);
                    ChangeSize(scale, Mathf.Lerp(.25f, 1, maturity));
                }
            }
            vitals.SetHealth(pose.Health);
            if (!IsPresent)
                return;
            if (IsDead)
            {
                Guarding = IsFlying = AttackConnected = false;
                attackTime = -1;
                Motion.Fall(delta, Height);
                return;
            }
            if (wasDead || !wasPresent)
                Motion.Stand();
            IsFlying = pose.Flying;
            diving = pose.Diving;
            attackTime = pose.AttackTime;
            AttackConnected = pose.Impact;
            Guarding = pose.Guard;
            Crouching = pose.Crouch;
            replicaActivity = pose.Activity;
            Feeding = pose.Feed;
            if (Playable)
                Sustain(delta, pose.Activity == LifeActivity.Drink, pose.Feed || pose.Activity == LifeActivity.Graze);
            stamina = pose.Stamina;
            winded = stamina <= .01f || winded && stamina <= .35f;
            stagger = Mathf.MoveTowards(stagger, pose.Stagger ? 1 : 0, delta * (pose.Stagger ? 8 : 1.15f));
            if (pose.Toppled != toppleFlag)
                toppleAt = pose.Toppled ? Time.time : -100;
            toppleFlag = pose.Toppled;
            EvaluatePose(delta, pose.Gait);
        }

        public void ToggleFlight()
        {
            if (!Traits.CanFly || IsDead)
                return;

            if (IsFlying)
                landing = !landing;
            else
            {
                IsFlying = true;
                landing = false;
                travelAltitude = 6;
                vertical = 3;
            }
        }

        public bool TryAttack()
        {
            if (!ReadyToAttack || Guarding || Toppled || perch != null || !IsPresent ||
                Life != null && Life.Stage == TriceratopsStage.Calf && !IsControlled)
                return false;

            attackTime = 0;
            diving = IsFlying;
            playedImpact = false;
            AttackConnected = false;
            return true;
        }

        private void Update()
        {
            if (replica)
                return;

            backlog += Time.deltaTime;
            watched = Watch();
            if (Throttle && !watched && !IsControlled && attackTime < 0 && perch == null && (Time.frameCount + ordinal) % 3 != 0)
                return;
            float dt = Mathf.Min(backlog, .25f);
            backlog = 0;
            Tick = dt;
            Track();
            if (Life != null)
                Life.Advance(dt);
            if (!IsPresent)
                return;
            if (IsDead)
            {
                UpdateDeath(dt);
                return;
            }
            attackDelay = Mathf.Max(0, attackDelay - dt);
            deed = LifeActivity.None;
            if (perch != null)
            {
                Cling(dt);
                return;
            }
            groundY = GroundUnder(transform.position);
            stagger = Mathf.MoveTowards(stagger, 0, dt * 1.15f);
            float endurance = Traits.Stamina > 0 ? Traits.Stamina : 20;
            if (Toppled)
            {
                Guarding = Crouching = Feeding = false;
                stamina = Mathf.Clamp01(stamina + dt * .5f / endurance);
                speed = Mathf.MoveTowards(speed, 0, dt * 20);
                MoveBody(dt);
                EvaluatePose(dt, 0);
                return;
            }

            bool run = requestedRun;
            bool guard = requestedGuard;
            Vector3 direction = IsControlled ? requestedDirection : behavior.Steer(neighbors, out run, out guard);
            if (!IsControlled)
            {
                heading = Vector3.Lerp(heading, direction, 1 - Mathf.Exp(-dt * (behavior.WantsStrike || AttackActive ? 14 : 5.5f)));
                direction = heading;
            }
            if (!IsControlled && Traits.CanFly)
            {
                if (behavior.WantsLanded == (IsFlying && !landing))
                    ToggleFlight();
                if (IsFlying && !landing && behavior.WantsAltitude > 0)
                    travelAltitude = Mathf.MoveTowards(travelAltitude, behavior.WantsAltitude, dt * 9);
            }
            Guarding = guard && !AttackActive;
            Crouching = !IsFlying && !AttackActive && !Guarding && (IsControlled ? requestedCrouch : behavior.WantsCrouch ||
                Life != null && Life.Stage != TriceratopsStage.Adult && Life.Activity == LifeActivity.Shelter);
            run &= !Crouching;
            bool sprinting = run && !winded && !IsFlying && !AttackActive && direction.sqrMagnitude > .25f;
            stamina = Mathf.Clamp01(stamina + dt * (sprinting ? -.68f / endurance : (speed < WalkSpeed * .3f ? 1.15f : .72f) / endurance));
            if (stamina <= 0 && !winded)
            {
                winded = true;
                stagger = 1;
            }
            else if (winded && stamina > .35f)
                winded = false;
            run &= !winded || IsFlying;
            DinosaurDeed act = IsControlled && requestedDeed && !Guarding && direction.sqrMagnitude < .01f
                ? Opportunity() : DinosaurDeed.None;
            deed = act switch
            {
                DinosaurDeed.Drink => LifeActivity.Drink, DinosaurDeed.Graze => LifeActivity.Graze, _ => LifeActivity.None
            };
            Feeding = !IsFlying && !AttackActive &&
                (IsControlled ? act == DinosaurDeed.Feed || Crouching && CanFeed() : behavior.WantsFeed);
            if (IsControlled)
            {
                bool eating = Feeding || act == DinosaurDeed.Graze;
                if (act == DinosaurDeed.Drink && Thirst > .02f)
                    stamina = Mathf.Clamp01(stamina + dt * 1.8f / endurance);
                if (eating && Time.time >= nextMouthful && (Feeding || Hunger > .02f))
                {
                    nextMouthful = Time.time + 1.1f;
                    Heal(Mathf.Max(1, MaxHealth / 30));
                }
                Sustain(dt, act == DinosaurDeed.Drink, eating);
                Survive(dt);
            }
            if (direction.sqrMagnitude > 0.001f && !AttackActive)
            {
                Quaternion facing = Quaternion.LookRotation(direction, Vector3.up);
                float resolve = IsControlled ? 1 : Mathf.Clamp01(direction.magnitude / .3f);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, facing,
                    Traits.Turn * dt * Mathf.Lerp(1.2f, 1, Size) * resolve);
            }
            if (!IsControlled && behavior.WantsStrike)
                TryAttack();

            float input = Mathf.Clamp01(direction.magnitude) * (Guarding ? .27f : Crouching ? .6f : 1);
            float target = (run ? RunSpeed * Mathf.Lerp(.84f, 1, Mathf.InverseLerp(0, .25f, stamina)) : WalkSpeed) * input;
            target *= 1 - .45f * stagger;
            if (IsFlying)
                target = Traits.FlightSpeed * Pace * (run ? 1 : 0.57f) * input;
            else if (WaterDepth > .35f)
                target *= .64f;

            if (AttackActive)
            {
                float progress = attackTime / Motion.AttackDuration(diving);
                float lunge = diving ? Traits.FlightSpeed * 0.67f : RunSpeed * 0.48f;
                target = Traits.TailStrike ? 0 : lunge
                    * Mathf.Sin(Mathf.PI * Mathf.Clamp01((progress - 0.13f) / 0.64f));
            }
            else if (!IsControlled)
                target *= behavior.ForwardAllowance;

            speed = Mathf.MoveTowards(speed, target, dt * (RunSpeed * 2.6f + 2));
            MoveBody(dt);
            EvaluatePose(dt, Gait);
            if (AttackActive && !IsDead)
                AdvanceAttack(dt);
        }

        private bool CanFeed()
        {
            if (neighbors == null || Traits.Nature == Temperament.Herd)
                return false;
            foreach (Dinosaur other in neighbors)
            {
                if (!other.IsDead || !other.IsPresent || other == this)
                    continue;
                Vector3 gap = other.transform.position - transform.position;
                float reach = Traits.AttackReach + other.Width * .5f;
                if (new Vector2(gap.x, gap.z).sqrMagnitude < reach * reach && Mathf.Abs(gap.y) < 4)
                    return true;
            }
            return false;
        }

        internal DinosaurDeed Opportunity()
        {
            if (IsDead || !IsPresent || IsFlying || AttackActive || Toppled || perch != null)
                return DinosaurDeed.None;
            if (CanFeed())
                return DinosaurDeed.Feed;
            Vector3 here = transform.position, reach = transform.forward * (Length * .28f);
            if (Wet(here) || Wet(here + reach) || Wet(here + reach * 2))
                return DinosaurDeed.Drink;
            return Traits.Nature == Temperament.Herd ? DinosaurDeed.Graze : DinosaurDeed.None;
        }

        private static bool Wet(Vector3 point) =>
            IslandLayout.SurfaceLevel(new Vector2(point.x, point.z)) - GroundUnder(point) > .05f;

        private void Sustain(float delta, bool drinking, bool eating)
        {
            if (Life != null && !replica)
                Life.Sustain(delta, drinking, eating);
            ownThirst = Mathf.Clamp01(ownThirst + delta * (drinking ? -.15f : .0019f));
            ownHunger = Mathf.Clamp01(ownHunger + delta * (eating ? -.14f : .0015f));
        }

        private void EvaluatePose(float delta, float gait)
        {
            Motion.Feeding = Feeding;
            Motion.ToppleTime = Toppled ? Time.time - toppleAt : -1;
            Motion.Stagger = stagger;
            Motion.Breath = 1 - stamina;
            Motion.Watched = watched;
            Motion.Evaluate(delta, gait, IsFlying, attackTime, diving, Guarding, Activity, Crouching);
        }

        internal void ChangeSize(float size, float healthScale)
        {
            Motion.SetGrowth(size);
            motor.height = adultHeight * size;
            motor.radius = adultRadius * size;
            motor.center = adultCenter * size;
            motor.stepOffset = adultStep * size;
            motor.skinWidth = adultSkin * size;
            width = motor.radius * 2;
            sighting.Focus.localPosition = adultFocus * size;
            if (Shadow != null)
                Shadow.localScale = adultShadow * size;
            vitals.Resize(Mathf.Max(1, Mathf.RoundToInt(Traits.MaxHealth * healthScale)));
        }

        internal void Heal(int health)
        {
            if (IsPresent && !IsDead)
                vitals.SetHealth(Health + health);
        }

        internal void SetLifeVisibility(bool visible)
        {
            Motion.BodyModel.gameObject.SetActive(visible);
            if (Shadow != null)
                Shadow.gameObject.SetActive(visible);
            motor.enabled = visible && !replica && !IsDead;
        }

        internal float WaterDepth => IslandLayout.SurfaceLevel(new Vector2(transform.position.x, transform.position.z))
            - transform.position.y;

        private void MoveBody(float delta)
        {
            if (IsFlying)
            {
                travelAltitude = Mathf.Clamp(travelAltitude + requestedLift * delta * 10, 3, 65);
                float floor = landing ? 0.04f : travelAltitude;
                float surface = IslandLayout.SurfaceLevel(new Vector2(transform.position.x, transform.position.z));
                float goal = (Mathf.Max(groundY, surface) + floor - transform.position.y) * 2.7f;
                if (diving && AttackActive && !landing)
                    goal -= 2.5f * Mathf.Sin(Mathf.PI * attackTime / Motion.AttackDuration(true));
                vertical = landing ? Mathf.Clamp(goal, -10f, 0) : Mathf.Clamp(goal, -8, 8f);
                if (landing && transform.position.y - Mathf.Max(groundY, surface) < .17f)
                {
                    IsFlying = false;
                    landing = false;
                }
            }
            else
                vertical = motor.isGrounded ? -2.3f : Mathf.Max(-22, vertical - delta * 26);

            Vector3 velocity = transform.forward * speed + Vector3.up * vertical + knockback;
            motor.Move(IslandLayout.LimitVelocity(transform.position, velocity) * delta);
            Track();
            knockback = Vector3.MoveTowards(knockback, Vector3.zero, delta * 15);
            if (!IsFlying && Life == null && WaterDepth > Mathf.Clamp(Height * .62f + .55f, .75f, 4.6f) &&
                Time.time >= waterDamageAt)
            {
                waterDamageAt = Time.time + 1.25f;
                ReceiveHit(null, Mathf.CeilToInt(MaxHealth * (IsControlled ? .12f : .34f)), Vector3.zero);
            }
        }

        private void AdvanceAttack(float delta)
        {
            attackTime += delta;
            float duration = Motion.AttackDuration(diving);
            if (!playedImpact && attackTime >= duration * Traits.ImpactPhase)
            {
                playedImpact = true;
                DeliverImpact();
            }
            if (attackTime < duration)
                return;

            attackTime = -1;
            attackDelay = Traits.Recovery;
            diving = false;
        }

        private void DeliverImpact()
        {
            if (neighbors == null)
                return;

            Vector3 origin = transform.position + Vector3.up * (Height * 0.54f);
            Vector3 axis = Traits.TailStrike ? -transform.forward : transform.forward;
            Dinosaur closest = null;
            float nearest = float.PositiveInfinity;
            foreach (Dinosaur other in neighbors)
            {
                if (other == this || !other.IsPresent || other.IsDead || (!IsControlled && other.Herd != null && other.Herd == Herd))
                    continue;

                Vector3 difference = other.motor.bounds.center - origin;
                float horizontal = new Vector2(difference.x, difference.z).magnitude;
                if (horizontal > Traits.AttackReach * Size + other.motor.radius ||
                    Mathf.Abs(difference.y) > motor.height * 0.65f + other.motor.height * 0.5f)
                    continue;

                Vector3 planar = Vector3.ProjectOnPlane(difference, Vector3.up).normalized;
                if (Vector3.Dot(planar, axis) < (Traits.TailStrike ? 0.04f : 0.50f) ||
                    Physics.Linecast(origin, other.motor.bounds.center, Scenery, QueryTriggerInteraction.Ignore))
                    continue;

                if (Traits.TailStrike)
                    Strike(other);
                else if (horizontal < nearest)
                {
                    closest = other;
                    nearest = horizontal;
                }
            }
            if (closest != null)
                Strike(closest);
        }

        private void Strike(Dinosaur target)
        {
            Vector3 push = Vector3.ProjectOnPlane(target.transform.position - transform.position, Vector3.up).normalized
                * Mathf.Clamp(4.5f * motor.radius / target.motor.radius, 1.4f, 6);
            bool landed = target.ReceiveHit(this, Mathf.RoundToInt(Traits.Damage * Size), push * Size) > 0;
            AttackConnected |= landed;
            if (landed && IsControlled && Herd != null && Traits.Nature == Temperament.Pack && !target.IsDead && target.Herd != Herd)
                Herd.Pursue(target, 14);
            if (!landed || Traits.Nature != Temperament.Pack || IsFlying || target.IsDead || target.IsFlying ||
                target.Length < Length * 2 || target.riders >= 6)
                return;
            perch = target;
            int seat = target.riders++;
            float side = seat % 2 == 0 ? 1 : -1;
            perchOffset = new Vector3(side * (target.Traits.CameraHeight * .45f + .9f), target.Traits.CameraHeight * .75f,
                (seat / 2 - 1) * .13f * target.Traits.Length);
            perchUntil = Time.time + 3.2f + HerdSlot % 3 * .6f;
            nextGnaw = Time.time + .5f;
            motor.enabled = false;
            speed = 0;
            knockback = Vector3.zero;
        }

        private void Cling(float delta)
        {
            bool shaken = perch.Toppled || perch.AttackActive && perch.AttackProgress > .35f;
            if (shaken || perch.IsDead || !perch.IsPresent || perch.IsFlying || Time.time > perchUntil || IsControlled && requestedCrouch)
            {
                Vector3 away = Vector3.ProjectOnPlane(transform.position - perch.transform.position, Vector3.up).normalized;
                LetGo();
                attackDelay = Traits.Recovery + (shaken ? 1.2f : .5f);
                if (shaken)
                {
                    stagger = 1;
                    knockback = away * 5;
                }
                return;
            }
            float inward = -Mathf.Sign(perchOffset.x);
            transform.SetPositionAndRotation(perch.transform.TransformPoint(perchOffset * perch.Size),
                Quaternion.LookRotation(perch.transform.right * inward, Vector3.up) * Quaternion.Euler(-28, 0, 0));
            Track();
            attackTime = Mathf.Repeat(Mathf.Max(0, attackTime) + delta, Motion.AttackDuration(false) - .02f);
            if (Time.time >= nextGnaw)
            {
                nextGnaw = Time.time + .9f;
                AttackConnected = perch.ReceiveHit(this, Mathf.Max(1, Mathf.RoundToInt(Traits.Damage * .55f)), Vector3.zero) > 0;
            }
            EvaluatePose(delta, 0);
        }

        private void LetGo()
        {
            perch.riders = Mathf.Max(0, perch.riders - 1);
            perch = null;
            attackTime = -1;
            AttackConnected = false;
            vertical = speed = 0;
            transform.rotation = Quaternion.Euler(0, transform.eulerAngles.y, 0);
            motor.enabled = !replica && IsPresent;
        }

        internal int ReceiveHit(Dinosaur attacker, int damage, Vector3 shove)
        {
            if (replica || !IsPresent || IsDead || damage <= 0 || Time.time < protectedUntil && attacker != null)
                return 0;

            bool blocked = attacker != null && Guarding &&
                Vector3.Dot(transform.forward, Vector3.ProjectOnPlane(attacker.transform.position - transform.position,
                    Vector3.up).normalized) >= .37f;
            int actual = vitals.Hit(damage, attacker != null ? Traits.Armor * Size : 0,
                blocked ? Mathf.Lerp(1, Traits.GuardMultiplier, Size) : 1);
            lastHurt = Time.time;
            knockback += shove * (blocked ? .3f : 1);
            if (attacker != null)
            {
                behavior.Provoke(attacker);
                if (Life != null)
                    Life.Community.Warn(attacker);
            }
            if (attacker != null && !IsDead && !IsFlying && !blocked)
            {
                Vector3 source = Vector3.ProjectOnPlane(attacker.transform.position - transform.position, Vector3.up).normalized;
                bool exposed = winded || Vector3.Dot(transform.forward, source) < .3f;
                if (exposed && attacker.Length > Length * 1.15f && Time.time >= nextTopple && Motion.ToppleDuration > 0)
                {
                    toppleAt = Time.time;
                    nextTopple = Time.time + Motion.ToppleDuration + 14;
                    attackTime = -1;
                    speed *= .3f;
                }
                else
                    stagger = 1;
            }
            if (IsDead)
            {
                if (perch != null)
                    LetGo();
                toppleAt = -100;
                Leading = false;
                Feeding = false;
                deed = LifeActivity.None;
                Guarding = IsFlying = AttackConnected = Crouching = false;
                landing = diving = false;
                attackTime = -1;
                deathAt = Time.time;
                speed = vertical = 0;
                if (attacker != null)
                    attacker.behavior.Killed(this);
            }
            return actual;
        }

        private void UpdateDeath(float delta)
        {
            if (motor.enabled)
            {
                vertical = Mathf.Max(-22, vertical - 26 * delta);
                motor.Move(Vector3.up * (vertical * delta));
                if (motor.isGrounded)
                    motor.enabled = false;
            }
            Motion.Fall(delta, Height);
            if (Time.time - deathAt >= (Playable ? 12f : 45f))
            {
                if (Life != null && !Playable)
                    Life.Retire();
                else if (Playable && IsControlled)
                    BeBorn();
                else
                    Reappear();
            }
        }

        internal void StartWildLife(Vector3 bornAt)
        {
            home = bornAt;
            homeFacing = transform.rotation;
            Reappear();
        }

        private void Reappear() => Reappear(home);

        private void Reappear(Vector3 place)
        {
            motor.enabled = false;
            transform.SetPositionAndRotation(place, homeFacing);
            Track();
            groundY = GroundUnder(place);
            IsFlying = Traits.CanFly && place.y - groundY > 3;
            travelAltitude = Mathf.Max(5.5f, place.y - groundY);
            Motion.Stand();
            behavior.Reset();
            heading = Vector3.zero;
            SetInput(Vector3.zero, false, 0);
            knockback = Vector3.zero;
            vertical = speed = attackDelay = 0;
            waterDamageAt = 0;
            lastHurt = -10;
            protectedUntil = Time.time + 2.4f;
            vitals.SetHealth(MaxHealth);
            stamina = 1;
            stagger = 0;
            perch = null;
            riders = 0;
            toppleAt = -100;
            winded = false;
            motor.enabled = true;
        }

        private static float GroundUnder(Vector3 point)
        {
            return Physics.Raycast(point + Vector3.up * 20, Vector3.down, out RaycastHit hit, 160, Ground)
                ? hit.point.y : point.y;
        }

        private void LateUpdate()
        {
            if (!IsPresent || Shadow == null || !watched)
                return;

            if (!Physics.Raycast(transform.position + Vector3.up * 0.65f, Vector3.down, out RaycastHit hit, 30, Ground))
            {
                Shadow.gameObject.SetActive(false);
                return;
            }

            Shadow.gameObject.SetActive(true);
            Quaternion slope = Quaternion.FromToRotation(Vector3.up, hit.normal);
            Shadow.SetPositionAndRotation(hit.point + hit.normal * 0.065f, slope * transform.rotation);
        }

        private void OnDestroy() => Motion.Dispose();
    }
}
