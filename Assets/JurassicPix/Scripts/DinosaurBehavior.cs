using UnityEngine;

namespace JurassicPix
{
    internal enum BehaviorState : byte { Idle, Travel, Drink, Eat, Rest, Stalk, Chase, Fight, Flee, Guard }

    internal sealed class DinosaurBehavior
    {
        private readonly Dinosaur animal;
        private Dinosaur target, attacker, meal;
        private Vector3 herdCenter;
        private Vector3 lastSeen, progressFrom, detour;
        private float nextSense, alertedUntil, huntStarted, restUntil, mealUntil, digestUntil, nextHeal, calmSince;
        private float searchUntil, nextProgress, detourUntil, nextFooting, thirst, errandUntil, nextSwoop;
        private Vector3 errandPlace;
        private object quarryHerd, shunnedHerd;
        private int errand;
        private bool shoreErrand;
        private BehaviorState proposal;
        private float stateSince;
        private Vector3 escape;
        private int frames, wanting, stuck;
        private bool sidestep;
        private Quaternion bypass = Quaternion.identity;
        private float bypassSide = 1;

        internal bool WantsStrike { get; private set; }
        internal bool WantsCrouch { get; private set; }
        internal bool WantsFeed { get; private set; }
        internal bool WantsLanded { get; private set; }
        internal float WantsAltitude { get; private set; }
        internal LifeActivity Activity { get; private set; }
        internal BehaviorState State { get; private set; }
        internal float Thirst => thirst;
        internal float Hunger => Sated ? .1f : Mathf.Clamp01(.45f + (Time.time - restUntil) / 240);
        internal float ForwardAllowance { get; private set; } = 1;

        private WildlifeHerd Pack => animal.Traits.Nature == Dinosaur.Temperament.Pack ? animal.Herd : null;
        private bool Sated => Time.time < (Pack != null ? Pack.SatedUntil : restUntil);
        private Dinosaur Carcass => Pack != null ? Pack.Meal :
            meal != null && meal.IsPresent && meal.IsDead && Time.time < mealUntil ? meal : null;

        public DinosaurBehavior(Dinosaur animal) => this.animal = animal;

        public void Provoke(Dinosaur source)
        {
            attacker = source;
            alertedUntil = Time.time + 12;
            nextSense = 0;
            if (animal.Traits.Nature == Dinosaur.Temperament.Herd && animal.Herd != null && source.Herd != animal.Herd &&
                NearHerd(source))
                animal.Herd.Dread(source, 12);
        }

        internal void Killed(Dinosaur prey)
        {
            if (animal.Traits.Nature != Dinosaur.Temperament.Herd)
                TakeMeal(prey);
        }

        public void Reset()
        {
            target = attacker = meal = null;
            State = BehaviorState.Idle;
            nextSense = alertedUntil = huntStarted = restUntil = mealUntil = digestUntil = searchUntil = detourUntil = 0;
        }

        public Vector3 Steer(Dinosaur[] animals, out bool run, out bool guard)
        {
            WantsStrike = WantsCrouch = WantsFeed = WantsLanded = guard = false;
            WantsAltitude = 0;
            Activity = LifeActivity.None;
            shoreErrand = false;
            proposal = BehaviorState.Idle;
            ForwardAllowance = 1;
            Vector3 here = animal.transform.position;
            int packmates = 0;
            Vector3 space = Separation(animals, here, ref packmates);
            if (animal.Life != null)
            {
                Vector3 intent = animal.Life.Steer(out run, out guard, out bool strike);
                shoreErrand = animal.Life.Community.Errand == HerdErrand.Drink;
                WantsStrike = strike;
                return FinalSteering(here, intent, space, run, guard);
            }
            if (Time.time >= nextSense)
            {
                Sense(animals, here, packmates);
                nextSense = Time.time + .38f + animal.HerdSlot * .013f;
            }

            Vector3 direction;
            switch (animal.Traits.Nature)
            {
                case Dinosaur.Temperament.Pack:
                case Dinosaur.Temperament.Solitary:
                    direction = Hunt(here, packmates, out run, out guard);
                    break;
                case Dinosaur.Temperament.Herd:
                    direction = HoldGround(here, packmates, out run);
                    break;
                default:
                    direction = Soar(animals, here, out run);
                    break;
            }
            Settle(ref direction, ref run);
            Recover();
            return FinalSteering(here, direction, space, run, guard);
        }

        private void Settle(ref Vector3 direction, ref bool run)
        {
            BehaviorState next = WantsStrike || animal.AttackActive ? BehaviorState.Fight
                : proposal != BehaviorState.Idle ? proposal
                : WantsFeed ? BehaviorState.Eat
                : Activity switch
                {
                    LifeActivity.Drink => BehaviorState.Drink,
                    LifeActivity.Browse or LifeActivity.Graze => BehaviorState.Eat,
                    LifeActivity.Rest or LifeActivity.Sleep => BehaviorState.Rest,
                    _ => direction.sqrMagnitude > .04f && ForwardAllowance > 0 ? BehaviorState.Travel : BehaviorState.Idle
                };
            float dwell = State switch
            {
                BehaviorState.Flee => 2.5f,
                BehaviorState.Stalk or BehaviorState.Guard => 1.2f,
                BehaviorState.Drink or BehaviorState.Eat or BehaviorState.Rest => 1.5f,
                _ => .4f
            };
            if (next != State && (next is BehaviorState.Flee or BehaviorState.Fight || Time.time - stateSince >= dwell))
            {
                State = next;
                stateSince = Time.time;
            }
            if (State != BehaviorState.Flee)
                return;
            if (next == BehaviorState.Flee)
                escape = direction;
            else
            {
                direction = escape;
                run = true;
                WantsCrouch = WantsFeed = false;
                ForwardAllowance = Mathf.Max(ForwardAllowance, .6f);
            }
        }

        private Vector3 FinalSteering(Vector3 here, Vector3 direction, Vector3 space, bool run, bool guard)
        {
            if (!animal.IsFlying && animal.WaterDepth > Mathf.Max(.7f, animal.Height * .3f))
                return IslandSites.Inland(here);
            Vector3 awayFromWater = DriftFromWater(here, direction);
            Vector3 steering = direction + space * (guard ? .40f : 1.24f) + awayFromWater;
            return Vector3.ClampMagnitude(Unstick(here, AvoidSlopes(here, AvoidTrunks(here, steering)), guard), run ? 1 : .87f);
        }

        private void Sense(Dinosaur[] animals, Vector3 here, int packmates)
        {
            Dinosaur.Temperament nature = animal.Traits.Nature;
            if (attacker != null && attacker.IsPresent && !attacker.IsDead && Time.time < alertedUntil &&
                (attacker.transform.position - here).sqrMagnitude < 52 * 52)
            {
                if (Pack != null && Fearsome(attacker, packmates))
                    Pack.Dread(attacker, 6);
                else
                    Engage(attacker);
                return;
            }
            if (nature == Dinosaur.Temperament.Flyer || animals == null)
            {
                target = null;
                return;
            }

            bool hungry = nature != Dinosaur.Temperament.Herd && !Sated && Carcass == null;
            Dinosaur choice = null, carrion = null;
            float best = float.PositiveInfinity, nearest = 40;
            foreach (Dinosaur other in animals)
            {
                if (other == animal || other.Traits.Nature == nature)
                    continue;
                Vector3 difference = other.Spot - here;
                if (Mathf.Abs(difference.x) > 48 || Mathf.Abs(difference.z) > 48 || !other.IsPresent || other.IsFlying)
                    continue;
                float distance = Horizontal(difference).magnitude;
                if (distance > 48 || Mathf.Abs(difference.y) > 5)
                    continue;
                if (other.IsDead)
                {
                    if (!hungry || distance >= nearest || Hidden(here, other))
                        continue;
                    carrion = other;
                    nearest = distance;
                    continue;
                }
                if (Pack != null && Fearsome(other, packmates))
                {
                    if (distance < 30 && !Hidden(here, other))
                        Pack.Dread(other, 5);
                    continue;
                }
                float score = Appeal(other, distance, packmates, hungry);
                if (score >= best || Hidden(here, other))
                    continue;
                choice = other;
                best = score;
            }

            if (carrion != null)
                TakeMeal(carrion);
            else if (Pack == null || Pack.Leader == null || Pack.Leader == animal)
                Engage(choice);
            else
                target = Pack.Foe;
        }

        private float Appeal(Dinosaur other, float distance, int packmates, bool hungry)
        {
            Dinosaur.Temperament kind = other.Traits.Nature;
            switch (animal.Traits.Nature)
            {
                case Dinosaur.Temperament.Herd:
                    // Un cazador mucho más chico que el animal no asusta a la manada.
                    if (other.Length < animal.Length * .25f)
                        return float.PositiveInfinity;
                    return kind == Dinosaur.Temperament.Pack && distance < (Night ? 12 : 17) ||
                        kind == Dinosaur.Temperament.Solitary && distance < (Night ? 24 : 34) ? distance : float.PositiveInfinity;
                case Dinosaur.Temperament.Solitary:
                    if (distance > (hungry ? 46 : 13) || other.Length > 17 && other.HealthRatio > .4f)
                        return float.PositiveInfinity;
                    break;
                default:
                    if (!hungry || distance > 34 || other.Length > 16 || packmates < (other.Length > 5 ? 2 : 1))
                        return float.PositiveInfinity;
                    break;
            }
            Dinosaur current = Pack != null ? Pack.Foe : target;
            float stray = other.Life == null ? 0 : Mathf.Clamp(
                (TriceratopsLife.Distance(other.transform.position, other.Life.Community.Center) - 14) * .5f, 0, 12);
            return distance - (1 - other.HealthRatio) * 16 - (other.Length < 6 ? 10 : 0) - (other == current ? 12 : 0) - stray;
        }

        private void Engage(Dinosaur prey)
        {
            if (prey != target)
                huntStarted = Time.time;
            target = prey;
            if (prey != null && Pack != null && (Pack.Foe == null || Pack.Foe == prey))
                Pack.Pursue(prey, 9);
        }

        private void TakeMeal(Dinosaur carcass)
        {
            target = null;
            searchUntil = 0;
            if (Pack != null)
            {
                Pack.Feast(carcass, 40);
                return;
            }
            meal = carcass;
            mealUntil = Time.time + 40;
            restUntil = mealUntil + 70;
        }

        private void GiveUp()
        {
            target = null;
            searchUntil = 0;
            shunnedHerd = quarryHerd;
            errandUntil = 0;
            if (Pack != null)
                Pack.Abandon(24);
            else
                restUntil = Mathf.Max(restUntil, Time.time + 20);
        }

        private bool Hidden(Vector3 here, Dinosaur other) =>
            Physics.Linecast(here + Vector3.up * (animal.Traits.CameraHeight * .65f),
                other.transform.position + Vector3.up * (other.Traits.CameraHeight * .65f), Dinosaur.Scenery,
                QueryTriggerInteraction.Ignore);

        private static bool Fearsome(Dinosaur other, int packmates) =>
            other.Traits.Nature == Dinosaur.Temperament.Solitary && other.Length > 7 && (other.HealthRatio >= .4f || packmates < 3);

        private Vector3 Hunt(Vector3 here, int packmates, out bool run, out bool guard)
        {
            guard = false;
            run = true;
            WildlifeHerd pack = Pack;
            Vector3 bearing;
            float distance;
            if (pack != null && Locate(pack.Menace, here, 52, out bearing, out distance))
            {
                calmSince = Time.time;
                run = distance < 36;
                proposal = run ? BehaviorState.Flee : BehaviorState.Guard;
                WantsCrouch = !run;
                return run ? -bearing : Face(bearing);
            }

            Dinosaur quarry = pack != null ? pack.Foe : target;
            if (quarry != null && !quarry.IsDead && Locate(quarry, here, pack != null ? 80 : 60, out bearing, out distance))
            {
                calmSince = Time.time;
                proposal = BehaviorState.Chase;
                float reach = animal.Traits.AttackReach + quarry.Width * .42f;
                bool fleeing = quarry.Motion.GaitWeight > 1.05f;
                bool social = animal.Traits.Nature == Dinosaur.Temperament.Pack;
                if (animal.HealthRatio < (social ? .34f : .28f) || social && packmates == 0 && quarry.Length > 5)
                {
                    if (!social)
                        restUntil = Mathf.Max(restUntil, Time.time + 30);
                    searchUntil = 0;
                    proposal = BehaviorState.Flee;
                    return -bearing;
                }
                bool defending = Time.time < alertedUntil;
                if (pack != null ? pack.HuntTime > (fleeing ? 30 : 50) && quarry.HealthRatio > .55f
                    : !defending && Time.time - huntStarted > 34 && distance > reach * 2.2f)
                {
                    GiveUp();
                    return Wander(here, out run);
                }
                if (pack == null)
                {
                    lastSeen = quarry.transform.position;
                    searchUntil = Time.time + 8;
                }
                if (fleeing && distance > reach * 1.5f)
                {
                    Vector3 aim = Horizontal(quarry.transform.position +
                        quarry.Velocity * Mathf.Min(distance / animal.Traits.Run, 2.2f) - here);
                    if (aim.sqrMagnitude > 1)
                        bearing = aim.normalized;
                }

                bool aimed = Vector3.Dot(animal.transform.forward, bearing) > .57f;
                if (!social)
                {
                    run = distance <= 26 || fleeing || defending || Time.time - huntStarted > 11;
                    WantsCrouch = !run;
                    if (!run)
                        proposal = BehaviorState.Stalk;
                    WantsStrike = distance <= reach && aimed && animal.ReadyToAttack && !quarry.AttackActive;
                    if (quarry.AttackActive && distance < quarry.Traits.AttackReach + animal.Width && !WantsStrike &&
                        Vector3.Dot(quarry.transform.forward, -bearing) > .45f)
                    {
                        guard = true;
                        run = false;
                        return bearing * .50f;
                    }
                    return bearing * (distance < reach * .68f ? .42f : 1);
                }

                float ring = reach + 5.5f;
                if (fleeing || quarry.Length < 4.5f || packmates == 0 || distance > ring + 6)
                {
                    WantsStrike = distance <= reach && aimed && animal.ReadyToAttack;
                    if (distance <= reach * 1.25f)
                        return bearing * (distance < reach * .68f ? .42f : 1);
                    return (bearing + Vector3.Cross(Vector3.up, bearing) * ((animal.HerdSlot % 3 - 1) * .55f)).normalized;
                }
                bool open = Vector3.Dot(quarry.transform.forward, bearing) > -.3f;
                bool turn = (Mathf.FloorToInt(Time.time / 1.5f) + animal.HerdSlot) % 3 == 0;
                if (animal.ReadyToAttack && (turn && open || distance <= reach) && (open || !quarry.AttackActive))
                {
                    WantsStrike = distance <= reach && aimed;
                    return bearing;
                }
                return (Vector3.Cross(Vector3.up, bearing) + bearing * Mathf.Clamp((distance - ring) / 4, -1, 1)).normalized;
            }

            if (Locate(Carcass, here, 90, out bearing, out distance))
            {
                float seat = animal.Traits.AttackReach * .55f + Carcass.Width * .4f + animal.HerdSlot % 3 * .9f;
                run = distance > seat + 12;
                if (distance > seat)
                    return bearing * (run ? 1 : .8f);
                if (Time.time >= nextHeal)
                {
                    nextHeal = Time.time + 1.1f;
                    animal.Heal(Mathf.Max(1, animal.MaxHealth / 30));
                }
                digestUntil = Time.time + 14;
                WantsFeed = true;
                return Face(bearing);
            }
            if (pack == null && Time.time < searchUntil && !Sated)
            {
                Vector3 trace = Horizontal(lastSeen - here);
                if (trace.sqrMagnitude > 7 * 7)
                {
                    run = false;
                    proposal = BehaviorState.Stalk;
                    return trace.normalized * .85f;
                }
                searchUntil = 0;
            }
            if (pack == null && Time.time < digestUntil)
            {
                run = false;
                return Vector3.zero;
            }
            return Wander(here, out run);
        }

        private Vector3 HoldGround(Vector3 here, int packmates, out bool run)
        {
            WildlifeHerd herd = animal.Herd;
            Dinosaur threat = target != null ? target : herd != null ? herd.Menace : null;
            if (threat == null || threat.IsDead || !Locate(threat, here, 60, out Vector3 bearing, out float distance))
                return Wander(here, out run);

            calmSince = Time.time;
            proposal = BehaviorState.Guard;
            bool tail = animal.Traits.TailStrike;
            float reach = animal.Traits.AttackReach + threat.Width * .42f;
            Vector3 weapon = tail ? -animal.transform.forward : animal.transform.forward;
            WantsStrike = distance <= reach && Vector3.Dot(weapon, bearing) > (tail ? .3f : .57f) && animal.ReadyToAttack;
            if (herd != null)
            {
                // Ver al depredador alcanza para alarmar a toda la manada, que huye detrás de su guía.
                if (target != null && NearHerd(target))
                    herd.Dread(target, 6);
                if (!herd.Guards(animal))
                {
                    WantsStrike = false;
                    proposal = BehaviorState.Flee;
                    Vector3 course = Wander(here, out run);
                    run = true;
                    return course;
                }
            }
            if (animal.HealthRatio < .45f)
            {
                run = true;
                proposal = BehaviorState.Flee;
                if (packmates == 0)
                    return -bearing;
                Vector3 refuge = Horizontal(herdCenter - here) + Horizontal(herdCenter - threat.transform.position).normalized * 16;
                if (refuge.sqrMagnitude > 8 * 8)
                    return refuge.normalized;
            }
            run = false;
            bool alarmed = herd != null && herd.Menace == threat;
            // Los guardias de una manada que huye no se van con ella: aguantan en el lugar de cara al peligro.
            if (distance < reach * 1.8f || alarmed && (distance < 36 || herd.Fleeing))
                return Face(tail ? -bearing : bearing);
            return Wander(here, out run);
        }

        // El peligro alarma a la manada solo mientras sigue cerca del grueso del grupo, no de los guardias que quedaron atrás.
        private bool NearHerd(Dinosaur danger) =>
            TriceratopsLife.Distance(danger.transform.position, animal.Herd.Middle) < 75;

        private void Recover()
        {
            if (animal.HurtRecently)
                calmSince = Time.time;
            if (Time.time - calmSince < 12 || Time.time < nextHeal)
                return;
            nextHeal = Time.time + 3;
            animal.Heal(Mathf.Max(1, animal.MaxHealth / 60));
        }

        private static bool Locate(Dinosaur other, Vector3 here, float range, out Vector3 bearing, out float distance)
        {
            bearing = Vector3.zero;
            distance = 0;
            if (other == null || !other.IsPresent || other.IsFlying)
                return false;
            Vector3 difference = other.transform.position - here;
            distance = Horizontal(difference).magnitude;
            if (distance <= .01f || distance >= range || Mathf.Abs(difference.y) >= 5 + distance * .1f)
                return false;
            bearing = Horizontal(difference) / distance;
            return true;
        }

        private Vector3 AvoidSlopes(Vector3 here, Vector3 steering)
        {
            if (animal.IsFlying || steering.sqrMagnitude < .09f)
                return steering;
            if (Time.time < nextFooting)
                return bypass * steering;

            nextFooting = Time.time + .28f + animal.HerdSlot * .011f;
            Vector3 heading = Horizontal(steering).normalized;
            float reach = animal.Width * .5f + 3.5f;
            bypass = Quaternion.identity;
            if (Walkable(here, heading, reach) && Walkable(here, heading, reach * 2.4f))
                return steering;
            for (int step = 1; step <= 5; step++)
            {
                for (int side = 0; side < 2; side++)
                {
                    float angle = step * 32 * (side == 0 ? bypassSide : -bypassSide);
                    Quaternion turn = Quaternion.Euler(0, angle, 0);
                    if (!Walkable(here, turn * heading, reach))
                        continue;
                    bypassSide = Mathf.Sign(angle);
                    bypass = turn;
                    return turn * steering;
                }
            }
            return steering;
        }

        private static bool Walkable(Vector3 here, Vector3 heading, float reach)
        {
            Vector3 ahead = here + heading * reach;
            if (!Physics.Raycast(ahead + Vector3.up * 140, Vector3.down, out RaycastHit ground, 300, 1 << Dinosaur.TerrainLayer,
                QueryTriggerInteraction.Ignore))
                return true;
            float rise = ground.point.y - here.y;
            return rise < reach * .6f && rise > -reach * 1.2f && ground.normal.y > .66f;
        }

        private Vector3 Unstick(Vector3 here, Vector3 steering, bool guard)
        {
            if (Time.time < detourUntil)
                return detour;
            frames++;
            if (steering.sqrMagnitude > .5f * .5f && ForwardAllowance > .8f && !guard && !animal.AttackActive && !animal.IsFlying)
                wanting++;
            if (Time.time < nextProgress)
                return steering;

            bool held = wanting > frames * .8f && Horizontal(here - progressFrom).sqrMagnitude < .6f * .6f;
            stuck = held ? stuck + 1 : 0;
            progressFrom = here;
            nextProgress = Time.time + 1.4f;
            frames = wanting = 0;
            if (stuck < 2)
                return steering;

            stuck = 0;
            sidestep = !sidestep;
            Vector3 forward = Horizontal(steering).normalized;
            detour = (Vector3.Cross(Vector3.up, forward) * (sidestep ? 1 : -1) - forward * .35f).normalized;
            detourUntil = Time.time + 1.2f + 90 / animal.Traits.Turn;
            return detour;
        }

        private static bool Night => Shader.GetGlobalFloat("_ForestNight") > .6f;

        private Vector3 Face(Vector3 bearing)
        {
            ForwardAllowance = 0;
            return bearing;
        }

        private Vector3 Wander(Vector3 here, out bool run)
        {
            if (animal.Herd != null)
            {
                Vector3 course = animal.Herd.Steer(animal, out run, out LifeActivity activity, out bool hold);
                Activity = activity;
                shoreErrand = animal.Herd.Errand == HerdErrand.Drink;
                if (animal.Traits.CanFly)
                {
                    WantsLanded = activity is LifeActivity.Drink or LifeActivity.Rest or LifeActivity.Sleep;
                    WantsAltitude = 20 + animal.HerdSlot % 4 * 6;
                    return WantsLanded ? Vector3.zero : course;
                }
                WantsCrouch = activity == LifeActivity.Drink && animal.Length < 6;
                return hold ? Face(course) : course;
            }
            return Errands(here, out run);
        }

        private Vector3 Errands(Vector3 here, out bool run)
        {
            run = false;
            shoreErrand = errand == 1;
            bool drinking = errand == 1 && Horizontal(errandPlace - here).magnitude < 5 + animal.Length * .2f;
            thirst = Mathf.Clamp01(thirst + animal.Tick * (drinking ? -.05f : .0045f));
            if (Time.time > errandUntil || errand == 0)
            {
                if (errand == 0)
                    thirst = Mathf.Repeat(animal.Home.x * .013f, .5f);
                quarryHerd = null;
                if (thirst > .55f)
                {
                    errand = 1;
                    errandPlace = IslandSites.Water(here);
                }
                else if (!Sated && IslandSites.Prey(animal, here, shunnedHerd, out errandPlace, out quarryHerd))
                    errand = 2;
                else
                {
                    errand = 3;
                    float turn = Time.time * .21f + animal.Home.x;
                    errandPlace = animal.Home + new Vector3(Mathf.Sin(turn), 0, Mathf.Cos(turn)) * (animal.Traits.PatrolRadius * 2.5f);
                }
                errandUntil = Time.time + 60 + TriceratopsLife.Distance(here, errandPlace) / 2;
            }
            if (errand == 2 && quarryHerd != null)
                errandPlace = quarryHerd is TriceratopsHerd community ? community.Center : ((WildlifeHerd)quarryHerd).Middle;
            Vector3 gap = Horizontal(errandPlace - here);
            float distance = gap.magnitude;
            if (drinking)
            {
                Activity = LifeActivity.Drink;
                WantsCrouch = true;
                if (thirst < .05f)
                    errandUntil = 0;
                return Face(-IslandSites.Inland(errandPlace));
            }
            if (errand == 3 && distance < 8)
            {
                if (Night)
                {
                    Activity = LifeActivity.Sleep;
                    return Vector3.zero;
                }
                if (Mathf.Repeat(Time.time + animal.Home.z, 30) > 9)
                    errandUntil = Mathf.Min(errandUntil, Time.time + 9);
                return Vector3.zero;
            }
            WantsCrouch = errand == 2 && distance < 70;
            if (WantsCrouch)
                proposal = BehaviorState.Stalk;
            if (errand == 2 && distance < 14)
            {
                shunnedHerd = quarryHerd;
                errandUntil = 0;
            }
            return gap / Mathf.Max(distance, .1f) * .8f;
        }

        private Vector3 Soar(Dinosaur[] animals, Vector3 here, out bool run)
        {
            Vector3 direction = Wander(here, out run);
            Vector3 bearing;
            float distance;
            if (!animal.IsFlying && Locate(target, here, 46, out bearing, out _))
            {
                WantsLanded = false;
                run = true;
                proposal = BehaviorState.Flee;
                return -bearing;
            }
            WildlifeHerd flock = animal.Herd;
            Dinosaur carcass = Carcass;
            if (carcass != null && Locate2D(carcass, here, out bearing, out distance) && distance < 160)
            {
                run = distance > 40;
                WantsAltitude = 5;
                WantsLanded = distance < 26;
                WantsFeed = !animal.IsFlying && distance < 7;
                if (!animal.IsFlying)
                    return distance > 5 ? bearing * .8f : Face(bearing);
                return bearing * Mathf.Clamp(distance / 30, .12f, 1);
            }
            if (flock == null || flock.Errand != HerdErrand.Hunt || !flock.Arrived || Sated || animals == null)
                return direction;

            Dinosaur prey = null;
            float nearest = 75;
            foreach (Dinosaur other in animals)
            {
                if (other == animal || !other.IsPresent || other.IsDead || other.IsFlying || other.Length > 6.5f ||
                    other.Traits.Nature == Dinosaur.Temperament.Flyer)
                    continue;
                float range = TriceratopsLife.Distance(other.transform.position, here) - (1 - other.HealthRatio) * 20;
                if (range >= nearest)
                    continue;
                nearest = range;
                prey = other;
            }
            if (prey == null || Time.time < nextSwoop || !Locate2D(prey, here, out bearing, out distance))
                return direction;
            run = true;
            WantsLanded = false;
            WantsAltitude = distance < 34 ? 3 : 11;
            float reach = animal.Traits.AttackReach + prey.Width * .5f;
            if (distance <= reach && Vector3.Dot(animal.transform.forward, bearing) > .5f && animal.ReadyToAttack &&
                here.y - prey.transform.position.y < 6.5f)
            {
                WantsStrike = true;
                nextSwoop = Time.time + 6;
            }
            return bearing;
        }

        private static bool Locate2D(Dinosaur other, Vector3 here, out Vector3 bearing, out float distance)
        {
            Vector3 gap = Horizontal(other.transform.position - here);
            distance = gap.magnitude;
            bearing = distance > .01f ? gap / distance : Vector3.forward;
            return distance > .01f;
        }

        private Vector3 Separation(Dinosaur[] animals, Vector3 here, ref int packmates)
        {
            Vector3 push = Vector3.zero, herdSum = here;
            int herdSize = 1;
            herdCenter = here;
            if (animals == null)
                return push;

            foreach (Dinosaur other in animals)
            {
                if (other == animal)
                    continue;
                Vector3 place = other.Spot;
                bool sameHerd = animal.Herd != null && animal.Herd == other.Herd;
                // La mayoría de la isla queda lejos: se descarta antes de mirar nada más.
                if (!sameHerd && (Mathf.Abs(here.x - place.x) > 37 || Mathf.Abs(here.z - place.z) > 37))
                    continue;
                if (!other.IsPresent || other.IsDead || Mathf.Abs(here.y - place.y) > (animal.IsFlying ? 10 : 4))
                    continue;
                if (sameHerd)
                {
                    herdSum += place;
                    herdSize++;
                }
                Vector3 offset = Horizontal(here - place);
                float square = offset.sqrMagnitude;
                if (sameHerd && square < 28 * 28)
                    packmates++;
                if (square >= 37 * 37)
                    continue;
                float distance = Mathf.Sqrt(square);
                Vector3 normal = distance > .05f ? offset / distance : (animal.HerdSlot % 2 == 0 ? Vector3.right : Vector3.left);
                float side = (animal.Width + other.Width) * (sameHerd ? .83f : .61f) + (sameHerd ? 3.6f : 1);
                float length = animal.Length * .38f * Mathf.Abs(Vector3.Dot(normal, animal.Facing))
                    + other.Length * .38f * Mathf.Abs(Vector3.Dot(normal, other.Facing));
                float room = Mathf.Max(side, length + 1.6f);
                if (distance < room)
                    push += normal * ((sameHerd ? 3.55f : 1.95f) * (1 - distance / room));
                if (sameHerd && Vector3.Dot(-normal, animal.Facing) > .38f)
                {
                    float brake = Mathf.InverseLerp(side * .82f, room * 1.06f + 1, distance);
                    ForwardAllowance = Mathf.Min(ForwardAllowance, brake);
                }
            }
            herdCenter = herdSum / herdSize;
            return Vector3.ClampMagnitude(push, 1.7f);
        }

        private Vector3 DriftFromWater(Vector3 here, Vector3 movement)
        {
            Vector2 current = new(here.x, here.z);
            Vector2 ahead = current + new Vector2(movement.x, movement.z) * 13;
            Vector2 push = Vector2.zero;
            float coast = IslandLayout.ShoreDistance(ahead);
            float beach = shoreErrand ? -8 : 14, bank = shoreErrand ? -9 : 11;
            if (coast < beach)
                push += (IslandLayout.Center - current).normalized * Mathf.Clamp01((beach - coast) / 21);
            float lagoon = IslandLayout.LagoonDistance(ahead);
            if (!animal.IsFlying && lagoon > -bank)
                push += (current - IslandLayout.NearestLake(current)).normalized * Mathf.Clamp01((lagoon + bank) / 14);
            return new Vector3(push.x, 0, push.y) * 1.8f;
        }

        private Vector3 AvoidTrunks(Vector3 here, Vector3 direction)
        {
            if (animal.IsFlying || direction.sqrMagnitude < .11f ||
                !Physics.SphereCast(here + Vector3.up * .81f, Mathf.Min(animal.Width * .34f, 1.65f), direction.normalized,
                    out RaycastHit hit, 3.2f + animal.Width * .45f, 1 << 0, QueryTriggerInteraction.Ignore))
                return direction;

            Vector3 normal = Horizontal(hit.normal).normalized;
            Vector3 tangent = Vector3.Cross(Vector3.up, normal);
            return normal * .8f + tangent * (Vector3.Dot(tangent, direction) < 0 ? -.85f : .85f);
        }

        private static Vector3 Horizontal(Vector3 value) => new(value.x, 0, value.z);
    }
}
