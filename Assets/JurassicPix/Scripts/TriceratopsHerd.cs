using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace JurassicPix
{
    internal enum HerdTactic : byte { Calm, Alert, Stampede }
    internal enum HerdErrand : byte { Roam, Graze, Drink, Hunt, Rest }

    [DefaultExecutionOrder(-34)]
    public sealed class TriceratopsHerd : MonoBehaviour
    {
        [SerializeField] private Vector3 start, nest;
        [SerializeField] private Vector3[] meadows, banks, nestingSites;
        [SerializeField] private bool reproduction = true;
        [SerializeField, Min(1)] private float incubation = 36, breedingInterval = 95;
        [SerializeField, Min(220)] private float lifespan = 7200;

        private readonly List<TriceratopsLife> members = new();
        private readonly List<Dinosaur> predators = new();
        private float[] forage;
        private Dinosaur threat;
        private TriceratopsLife mother, suitor;
        private float courtUntil;
        private float nextSurvey, threatUntil, nextBreeding, nestDeadline, meanHunger, meanThirst;
        private readonly List<Vector3> range = new();
        private TriceratopsLife leader;
        private HerdErrand errand;
        private Vector3 destination, heading = Vector3.forward;
        private float errandDeadline;
        private int errandSite, lastRange = -1, trips;
        private bool planned, arrived, stragglers, commanded;
        private float idleSince;
        private readonly List<TriceratopsLife> chargers = new(), ranked = new();
        private Vector3 flight, core;
        private float engagedUntil;
        private bool coreHasYoung;

        internal HerdTactic Tactic { get; private set; }
        internal Dinosaur Threat => threat;
        internal Vector3 Center { get; private set; }
        internal TriceratopsLife Leader => leader;
        internal bool Traveling => planned && !arrived;
        internal bool Sleeping { get; private set; }
        internal TriceratopsLife Sentinel { get; private set; }
        internal Vector3 Camp => destination;
        internal HerdErrand Errand => errand;
        internal bool Hurried => commanded && leader != null && leader.Animal.Motion.GaitWeight > 1.15f;
        internal int Living { get; private set; }
        private static bool Night => Shader.GetGlobalFloat("_ForestNight") > .6f;
        internal Vector3 Nest => nest;
        internal float Lifespan => lifespan;
        internal bool HasThreat => threat != null && threat.IsPresent && !threat.IsDead && Time.time < threatUntil;
        internal Vector3 ThreatPosition => threat.transform.position;
        internal int Births { get; private set; }

        public void Configure(Vector3 home, Vector3[] nesting, Vector3[] grass, Vector3[] water)
        {
            start = home;
            nestingSites = nesting;
            nest = nesting[0];
            meadows = grass;
            banks = water;
        }

        internal void Bind(Dinosaur[] population)
        {
            foreach (Dinosaur animal in population)
            {
                if (animal.Life != null && animal.Life.Community == this)
                    members.Add(animal.Life);
                if (animal.Traits.Nature is Dinosaur.Temperament.Pack or Dinosaur.Temperament.Solitary)
                    predators.Add(animal);
            }
            Center = start;
            Living = members.Count;
            if (!IslandSites.Communities.Contains(this))
                IslandSites.Communities.Add(this);
            if (nestingSites == null || nestingSites.Length == 0)
                nestingSites = new[] { nest };
            nest = nestingSites[0];
            forage = new float[meadows.Length];
            for (int i = 0; i < forage.Length; i++)
                forage[i] = 1;
            nextBreeding = Time.time + 24 + Mathf.Repeat(start.x, 16);
        }

        private void Update()
        {
            if (members.Count == 0)
                return;
            if (members[0].Animal.IsReplica)
            {
                if (Time.time >= nextSurvey)
                {
                    nextSurvey = Time.time + 1;
                    leader = ElectLeader();
                }
                return;
            }
            if (Tactic == HerdTactic.Stampede)
                TrackCore();
            if (Time.time < nextSurvey)
                return;

            float tick = .44f;
            nextSurvey = Time.time + tick;
            for (int i = 0; i < forage.Length; i++)
                forage[i] = Mathf.Min(1, forage[i] + tick * .016f);

            int seen = 0, needy = 0;
            Vector3 average = Vector3.zero;
            meanHunger = meanThirst = 0;
            float nearest = Night ? 28 : 37;
            Dinosaur danger = null;
            foreach (TriceratopsLife member in members)
            {
                if (!Alive(member))
                    continue;
                seen++;
                Vector3 place = member.transform.position;
                average += place;
                if (!member.Animal.IsControlled)
                {
                    needy++;
                    meanHunger += member.Hunger;
                    meanThirst += member.Thirst;
                }
                foreach (Dinosaur predator in predators)
                {
                    if (!predator.IsPresent || predator.IsDead || predator.IsFlying || predator.Length < 1.6f)
                        continue;
                    Vector3 point = predator.transform.position;
                    float distance = TriceratopsLife.Distance(point, place) * (predator.Crouching ? 1.5f : 1);
                    if (distance >= nearest || Mathf.Abs(point.y - place.y) > 7 ||
                        Physics.Linecast(place + Vector3.up * (1.4f * member.Scale), point + Vector3.up * 1.5f,
                            Dinosaur.Scenery, QueryTriggerInteraction.Ignore))
                        continue;
                    nearest = distance;
                    danger = predator;
                }
            }
            Center = seen == 0 ? start : average / seen;
            Living = seen;
            if (needy > 0)
            {
                meanHunger /= needy;
                meanThirst /= needy;
            }
            if (danger != null)
            {
                threat = danger;
                threatUntil = Time.time + 4.6f;
            }
            UpdateTactic();
            UpdateErrand();
            UpdateNest();
        }

        internal void Warn(Dinosaur attacker)
        {
            threat = attacker;
            threatUntil = Time.time + 7;
            nextSurvey = 0;
            if (attacker.Life == null || attacker.Life.Community != this)
                engagedUntil = Time.time + 8;
        }

        private void UpdateTactic()
        {
            if (!HasThreat)
            {
                Tactic = HerdTactic.Calm;
                chargers.Clear();
                flight = Vector3.zero;
                engagedUntil = 0;
                return;
            }

            Vector3 danger = threat.transform.position;
            float range = float.PositiveInfinity;
            int fighters = 0;
            foreach (TriceratopsLife member in members)
            {
                if (!Alive(member))
                    continue;
                if (!chargers.Contains(member))
                    range = Mathf.Min(range, TriceratopsLife.Distance(member.transform.position, danger));
                if (member.CanFight)
                    fighters++;
            }
            if (float.IsPositiveInfinity(range))
                range = TriceratopsLife.Distance(Center, danger);

            // La manada ya no planta cara entera: escapa detrás de su líder apenas el peligro se acerca o lastima a alguien,
            // y solo uno o dos adultos se quedan a cubrir la retirada. Cuando puso distancia, se frena a mirar.
            bool wasFleeing = Tactic == HerdTactic.Stampede;
            bool scared = range < 27 || range < 46 && (wasFleeing || Time.time < engagedUntil);
            Tactic = scared ? HerdTactic.Stampede : HerdTactic.Alert;
            if (scared)
            {
                ChooseFlight(danger);
                if (!wasFleeing)
                    TrackCore();
            }
            if (scared || chargers.Count > 0)
                ChooseChargers(danger, fighters > 3 ? 2 : fighters > 1 ? 1 : 0);
        }

        private void ChooseChargers(Vector3 danger, int count)
        {
            ranked.Clear();
            for (int pick = 0; pick < count; pick++)
            {
                TriceratopsLife best = null;
                float nearest = 42;
                foreach (TriceratopsLife member in members)
                {
                    if (!Alive(member) || !member.CanFight || member == leader || ranked.Contains(member))
                        continue;
                    float distance = TriceratopsLife.Distance(member.transform.position, danger) -
                        (chargers.Contains(member) ? 6 : 0);
                    if (distance >= nearest)
                        continue;
                    nearest = distance;
                    best = member;
                }
                if (best == null)
                    break;
                ranked.Add(best);
            }
            chargers.Clear();
            chargers.AddRange(ranked);
        }

        private void ChooseFlight(Vector3 danger)
        {
            Vector3 away = TriceratopsRoute.Horizontal(Center - danger);
            away = away.sqrMagnitude > .01f ? away.normalized : flight != Vector3.zero ? flight : Vector3.forward;
            if (flight != Vector3.zero && Vector3.Dot(flight, away) > .3f && OpenGround(flight))
                return;
            flight = away;
            for (int step = 0; step < 9; step++)
            {
                Vector3 option = Quaternion.Euler(0, (step + 1) / 2 * 30 * (step % 2 == 1 ? 1 : -1), 0) * away;
                if (!OpenGround(option))
                    continue;
                flight = option;
                return;
            }
        }

        private bool OpenGround(Vector3 direction)
        {
            for (int step = 1; step <= 2; step++)
            {
                Vector3 ahead = Center + direction * (17 * step);
                Vector2 point = new(ahead.x, ahead.z);
                if (IslandLayout.ShoreDistance(point) < 12 || IslandLayout.LagoonDistance(point) > -6)
                    return false;
            }
            return true;
        }

        private void TrackCore()
        {
            Vector3 young = Vector3.zero, adults = Vector3.zero;
            int youngCount = 0, adultCount = 0;
            foreach (TriceratopsLife member in members)
            {
                if (!Alive(member) || member.Animal.IsControlled)
                    continue;
                if (member.Stage == TriceratopsStage.Adult)
                {
                    adults += member.transform.position;
                    adultCount++;
                }
                else
                {
                    young += member.transform.position;
                    youngCount++;
                }
            }
            core = youngCount > 0 ? young / youngCount : adultCount > 0 ? adults / adultCount : Center;
            coreHasYoung = youngCount > 0;
        }

        internal bool IsCharger(TriceratopsLife member) => chargers.Contains(member);

        internal Dinosaur NearestPredator(Vector3 from, float range)
        {
            Dinosaur result = null;
            foreach (Dinosaur predator in predators)
            {
                if (!predator.IsPresent || predator.IsDead || predator.IsFlying)
                    continue;
                Vector3 point = predator.transform.position;
                float distance = TriceratopsLife.Distance(point, from);
                if (distance >= range || Mathf.Abs(point.y - from.y) > 7)
                    continue;
                range = distance;
                result = predator;
            }
            return result;
        }

        internal Vector3 StampedePost(TriceratopsLife member)
        {
            int slot = member.Animal.HerdSlot;
            Vector3 side = Vector3.Cross(Vector3.up, flight);
            if (leader != null && Alive(leader) && !chargers.Contains(leader))
            {
                // El líder marca la huida y el resto corre detrás de él: las crías pegadas a su rastro y los adultos en dos
                // filas. Nadie vuelve hacia el peligro para ocupar su lugar.
                Vector3 head = leader.transform.position;
                if (member == leader)
                    return head + flight * 30;
                Vector3 post = member.Stage != TriceratopsStage.Adult
                    ? head - flight * 5 + side * ((slot % 3 - 1) * 2.8f)
                    : head - flight * (9 + slot % 3 * 4 + slot / 6 * 6.5f) + side * ((slot % 2 == 0 ? 1 : -1) * (5.5f + slot / 2 % 3 * 3.6f));
                Vector3 gap = post - member.transform.position;
                // Quien ya va por delante de su lugar sigue corriendo y solo se acomoda de costado.
                if (Vector3.Dot(gap, flight) < 6)
                    post = member.transform.position + flight * 18 + side * Mathf.Clamp(Vector3.Dot(gap, side), -8, 8);
                return post;
            }
            if (member.Stage != TriceratopsStage.Adult)
                return core + flight * 18 + side * ((slot % 3 - 1) * 2.8f);
            float lane = (slot % 2 == 0 ? 1 : -1) * (5.5f + slot / 2 % 3 * 3.6f);
            float lead = (coreHasYoung ? 3 - slot % 3 * 3 : 12 - slot % 3 * 4) - slot / 6 * 6.5f;
            return core + flight * lead + side * lane;
        }

        internal Vector3 LinePost(TriceratopsLife member)
        {
            int slot = member.Animal.HerdSlot;
            float angle = (slot % 2 == 0 ? 1 : -1) * (14 + slot / 2 % 3 * 26);
            return Center + Quaternion.Euler(0, angle, 0) * Front() * (9.5f + slot / 6 * 5.5f);
        }

        internal Vector3 Refuge(TriceratopsLife member)
        {
            Vector3 front = Front();
            return Center - front * 7 + Vector3.Cross(Vector3.up, front) * ((member.Animal.HerdSlot % 3 - 1) * 3.2f);
        }

        private Vector3 Front()
        {
            Vector3 front = TriceratopsRoute.Horizontal(threat.transform.position - Center);
            return front.sqrMagnitude > .01f ? front.normalized : Vector3.forward;
        }

        private void UpdateErrand()
        {
            TriceratopsLife commander = null;
            foreach (TriceratopsLife member in members)
            {
                if (Alive(member) && member.Animal.Leading)
                    commander = member;
            }
            if (commander != null)
                leader = commander;
            else if (!CanLead(leader))
                leader = ElectLeader();
            if (Tactic != HerdTactic.Calm)
            {
                Sleeping = false;
                errandDeadline += .44f;
                return;
            }
            if (commander != null)
            {
                Sleeping = false;
                Command(commander);
                return;
            }
            if (commanded)
                commanded = planned = false;
            if (Night && meanThirst < .7f && meanHunger < .7f)
            {
                if (!Sleeping)
                {
                    planned = arrived = true;
                    errand = HerdErrand.Roam;
                    destination = Center;
                }
                Sleeping = true;
                errandDeadline = Time.time + 6;
                int guards = 0;
                foreach (TriceratopsLife member in members)
                {
                    if (CanLead(member))
                        guards++;
                }
                Sentinel = null;
                int watch = guards > 0 ? Mathf.FloorToInt(Time.time / 45) % guards : -1;
                foreach (TriceratopsLife member in members)
                {
                    if (!CanLead(member) || watch-- != 0)
                        continue;
                    Sentinel = member;
                    break;
                }
                return;
            }
            Sleeping = false;

            Vector3 guide = leader != null ? leader.transform.position : Center;
            if (!planned)
                Plan();
            else if (!arrived)
            {
                if (TriceratopsLife.Distance(guide, destination) < (errand == HerdErrand.Drink ? 13 : 7))
                {
                    arrived = true;
                    errandDeadline = Time.time + (errand == HerdErrand.Roam ? 16 + trips % 3 * 7 : 55);
                }
                else if (Time.time > errandDeadline)
                    Plan();
            }
            else if (Time.time > errandDeadline || errand == HerdErrand.Drink && meanThirst < .14f ||
                errand == HerdErrand.Graze && (meanHunger < .14f || forage[errandSite] < .1f) ||
                errand == HerdErrand.Roam && meanThirst > .8f)
                Plan();

            int adults = 0, close = 0;
            foreach (TriceratopsLife member in members)
            {
                if (member == leader || !CanLead(member))
                    continue;
                adults++;
                if (TriceratopsLife.Distance(member.transform.position, guide) < 38)
                    close++;
            }
            stragglers = close * 2 < adults;
            Vector3 course = TriceratopsRoute.Horizontal(destination - guide);
            if (!arrived && course.sqrMagnitude > 1)
                heading = course.normalized;
        }

        private void Command(TriceratopsLife commander)
        {
            commanded = planned = true;
            Vector3 place = commander.transform.position;
            if (commander.Animal.Velocity.sqrMagnitude > .6f)
            {
                idleSince = Time.time;
                heading = TriceratopsRoute.Horizontal(commander.transform.forward).normalized;
            }
            errandDeadline = Time.time + 20;
            if (Time.time - idleSince <= 2.5f)
            {
                arrived = false;
                errand = HerdErrand.Roam;
                destination = place;
                return;
            }
            if (arrived)
                return;
            arrived = true;
            int bank = Nearest(banks, place), meadow = Nearest(meadows, place);
            errand = HerdErrand.Roam;
            destination = place;
            if (TriceratopsLife.Distance(banks[bank], place) < 45)
            {
                errand = HerdErrand.Drink;
                errandSite = bank;
                destination = banks[bank];
            }
            else if (TriceratopsLife.Distance(meadows[meadow], place) < 45)
            {
                errand = HerdErrand.Graze;
                errandSite = meadow;
                destination = meadows[meadow];
            }
        }

        private void Plan()
        {
            planned = true;
            arrived = false;
            trips++;
            if (meanThirst > .5f)
            {
                errand = HerdErrand.Drink;
                errandSite = Nearest(banks, Center);
                destination = banks[errandSite];
            }
            else if (meanHunger > .42f)
            {
                errand = HerdErrand.Graze;
                errandSite = FindMeadow(Center);
                destination = meadows[errandSite];
            }
            else
            {
                errand = HerdErrand.Roam;
                destination = NextExtreme();
            }
            errandDeadline = Time.time + 60 + TriceratopsLife.Distance(Center, destination) / 1.5f;
        }

        private Vector3 NextExtreme()
        {
            if (range.Count == 0)
            {
                for (int i = 0; i < 6; i++)
                {
                    Vector3 direction = Quaternion.Euler(0, i * 60 + 20, 0) * Vector3.forward;
                    for (float reach = 95; reach >= 45; reach -= 25)
                    {
                        Vector3 point = start + direction * reach;
                        Vector2 flat = new(point.x, point.z);
                        if (IslandLayout.ShoreDistance(flat) < 20 || IslandLayout.LagoonDistance(flat) > -12 ||
                            !NavMesh.SamplePosition(point, out NavMeshHit ground, 9, NavMesh.AllAreas))
                            continue;
                        range.Add(ground.position);
                        break;
                    }
                }
                if (range.Count == 0)
                    range.Add(start);
            }

            int choice = 0;
            float best = float.NegativeInfinity;
            for (int i = 0; i < range.Count; i++)
            {
                float score = TriceratopsLife.Distance(range[i], Center) + (i * 37 + trips * 17) % 29 - (i == lastRange ? 1000 : 0) -
                    (NearestPredator(range[i], 70) != null ? 400 : 0);
                if (score <= best)
                    continue;
                best = score;
                choice = i;
            }
            lastRange = choice;
            return range[choice];
        }

        private TriceratopsLife ElectLeader()
        {
            TriceratopsLife eldest = null;
            foreach (TriceratopsLife member in members)
            {
                if (CanLead(member) && (eldest == null || member.Age > eldest.Age))
                    eldest = member;
            }
            return eldest;
        }

        internal Vector3 LeaderGoal(out bool waiting)
        {
            waiting = stragglers;
            return waiting ? leader.transform.position : destination;
        }

        internal Vector3 FollowPost(TriceratopsLife member)
        {
            int slot = member.Animal.HerdSlot;
            Vector3 guide = leader != null ? leader.transform.position : destination;
            return guide - heading * (6.5f + slot / 3 * 6.5f) + Vector3.Cross(Vector3.up, heading) * ((slot % 3 - 1) * 6.5f);
        }

        private void UpdateNest()
        {
            if (!reproduction)
                return;
            if (mother != null)
            {
                if (HasThreat || Time.time >= nestDeadline || !CanBreed(mother))
                {
                    mother = null;
                    nextBreeding = Time.time + 17;
                }
                else if (Time.time >= courtUntil && TriceratopsLife.Distance(mother.transform.position, nest) < 7)
                {
                    TryLay(mother);
                    mother = null;
                    nextBreeding = Time.time + breedingInterval;
                }
                return;
            }
            if (Time.time < nextBreeding || HasThreat || meanHunger > .68f || meanThirst > .69f || Traveling)
                return;
            nextBreeding = Time.time + 12;
            nest = ChooseNest();
            if (TriceratopsLife.Distance(nest, Center) > 70)
                return;

            TriceratopsLife candidate = null, mate = null;
            float distance = float.PositiveInfinity;
            bool partner = false;
            bool space = false;
            foreach (TriceratopsLife member in members)
            {
                space |= member.Activity == LifeActivity.Dormant;
                if (!CanBreed(member))
                    continue;
                partner |= !member.Female;
                if (!member.Female && !member.Animal.IsControlled)
                    mate = member;
                float range = TriceratopsLife.Distance(member.transform.position, nest);
                if (member.Female && !member.Animal.Playable && range < distance)
                {
                    candidate = member;
                    distance = range;
                }
            }
            if (space && partner && candidate != null)
            {
                mother = candidate;
                suitor = mate;
                courtUntil = mate != null ? Time.time + 16 : 0;
                nestDeadline = Time.time + 91;
            }
        }

        internal bool TryLay(TriceratopsLife parent)
        {
            if (!reproduction || HasThreat || !CanBreed(parent) || !parent.Female || parent.Community != this)
                return false;

            bool partner = members.Exists(member => !member.Female && CanBreed(member));
            bool resources = forage[FindMeadow(nest)] > .32f && meanThirst < .79f;
            if (!partner || !resources)
                return false;

            foreach (TriceratopsLife slot in members)
            {
                if (slot.Activity != LifeActivity.Dormant)
                    continue;
                Vector3 position = nest + new Vector3(slot.Animal.HerdSlot % 2 == 0 ? 1.9f : -1.9f, 0, .7f);
                if (Physics.Raycast(position + Vector3.up * 4, Vector3.down, out RaycastHit ground, 9,
                    1 << Dinosaur.TerrainLayer, QueryTriggerInteraction.Ignore))
                    position = ground.point + Vector3.up * .02f;
                slot.LayAt(position, incubation, parent);
                return true;
            }
            return false;
        }

        internal void RecordBirth() => Births++;

        private Vector3 ChooseNest()
        {
            float bestScore = float.NegativeInfinity;
            Vector3 site = nestingSites[0];
            foreach (Vector3 candidate in nestingSites)
            {
                int pasture = FindMeadow(candidate);
                float score = forage[pasture] - TriceratopsLife.Distance(candidate, Center) / 115 -
                    TriceratopsLife.Distance(candidate, meadows[pasture]) / 180;
                if (score <= bestScore)
                    continue;
                bestScore = score;
                site = candidate;
            }
            return site;
        }

        internal void CountYoung(out int calves, out int juveniles, out int eggs)
        {
            calves = juveniles = eggs = 0;
            foreach (TriceratopsLife life in members)
            {
                if (life.Activity == LifeActivity.Egg)
                    eggs++;
                else if (Alive(life))
                {
                    if (life.Stage == TriceratopsStage.Calf)
                        calves++;
                    else if (life.Stage == TriceratopsStage.Juvenile)
                        juveniles++;
                }
            }
        }

        internal bool IsNesting(TriceratopsLife member) => mother == member && Time.time >= courtUntil;

        internal bool Courting(TriceratopsLife member, out TriceratopsLife partner)
        {
            partner = member == mother ? suitor : mother;
            return Time.time < courtUntil && mother != null && suitor != null && (member == mother || member == suitor) &&
                Alive(partner);
        }

        internal TriceratopsLife FindProtector(TriceratopsLife child)
        {
            float nearest = 105;
            TriceratopsLife result = null;
            foreach (TriceratopsLife other in members)
            {
                if (other == child || !Alive(other) || other.Stage != TriceratopsStage.Adult)
                    continue;
                float distance = TriceratopsLife.Distance(other.transform.position, child.transform.position)
                    + (other.Animal.IsControlled ? 11 : 0) + (other.Female ? 0 : 2);
                if (distance >= nearest)
                    continue;
                nearest = distance;
                result = other;
            }
            return result;
        }

        internal TriceratopsLife FindPlaymate(TriceratopsLife child)
        {
            foreach (TriceratopsLife other in members)
            {
                if (other != child && Alive(other) && other.Stage != TriceratopsStage.Adult && other.Animal.HealthRatio > .7f &&
                    TriceratopsLife.Distance(other.transform.position, child.transform.position) < 10)
                    return other;
            }
            return null;
        }

        internal TriceratopsLife FindSeparatedYoung(TriceratopsLife adult)
        {
            TriceratopsLife result = null;
            float highest = 0;
            foreach (TriceratopsLife child in members)
            {
                if (!Alive(child) || child.Protector != adult || child.Stage == TriceratopsStage.Adult)
                    continue;
                float distance = TriceratopsLife.Distance(adult.transform.position, child.transform.position);
                float alertDistance = child.Stage == TriceratopsStage.Calf ? 18 : 28;
                if (distance < alertDistance || distance > 52)
                    continue;
                float priority = (1 - child.Animal.HealthRatio) * 55 + 55 - distance;
                if (priority <= highest)
                    continue;
                highest = priority;
                result = child;
            }
            return result;
        }

        internal bool TryResource(TriceratopsLife member, out Vector3 point, out int site, out bool water)
        {
            int slot = member.Animal.HerdSlot;
            water = errand == HerdErrand.Drink;
            point = Vector3.zero;
            site = 0;
            if (!planned || errand == HerdErrand.Roam ||
                !arrived && TriceratopsLife.Distance(member.transform.position, destination) > 12)
                return false;
            if (water)
            {
                Vector3 inland = IslandSites.Inland(banks[errandSite]);
                point = banks[errandSite] + Vector3.Cross(Vector3.up, inland) * ((slot % 7 - 3) * 3.4f) + inland * (slot / 7 * 5f);
                site = meadows.Length + errandSite;
                return member.Thirst > .12f;
            }
            site = errandSite;
            point = meadows[site] + new Vector3(Mathf.Sin(slot * 1.7f), 0, Mathf.Cos(slot * 1.7f)) * (4 + slot % 3 * 1.7f);
            return member.Hunger > .12f && forage[site] > .10f;
        }

        internal float Feed(int site, float delta)
        {
            float amount = Mathf.Min(delta * .016f, forage[site]);
            forage[site] -= amount;
            return delta <= 0 ? 0 : amount / (delta * .016f);
        }

        internal Vector3 Rendezvous(int knowledge)
        {
            float distance = 36;
            Vector3 result = Center;
            for (int i = 0; i < meadows.Length + banks.Length; i++)
            {
                if ((knowledge & (1 << i)) == 0)
                    continue;
                Vector3 place = i < meadows.Length ? meadows[i] : banks[i - meadows.Length];
                float range = TriceratopsLife.Distance(place, Center);
                if (range < distance)
                {
                    result = place;
                    distance = range;
                }
            }
            return result;
        }

        internal Vector3 Roam(int slot, float phase)
        {
            Vector3 origin = planned && arrived ? destination : Center;
            Vector3 direction = new(Mathf.Sin(phase * .072f), 0, Mathf.Cos(phase * .072f));
            Vector3 point = origin + direction * (10 + slot % 4 * 2.7f);
            return Physics.Raycast(point + Vector3.up * 35, Vector3.down, out RaycastHit ground, 66, 1 << Dinosaur.TerrainLayer)
                ? ground.point : point;
        }

        private int FindMeadow(Vector3 here)
        {
            int choice = 0;
            float best = float.NegativeInfinity;
            for (int i = 0; i < meadows.Length; i++)
            {
                float score = forage[i] - TriceratopsLife.Distance(here, meadows[i]) / 120;
                if (score <= best)
                    continue;
                best = score;
                choice = i;
            }
            return choice;
        }

        private static int Nearest(Vector3[] points, Vector3 from)
        {
            int selection = 0;
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < points.Length; i++)
            {
                float distance = (points[i] - from).sqrMagnitude;
                if (distance >= nearest)
                    continue;
                nearest = distance;
                selection = i;
            }
            return selection;
        }

        private static bool CanLead(TriceratopsLife member) => member != null && member.CanFight && Alive(member);
        private static bool Alive(TriceratopsLife member) => member.Present && !member.Animal.IsDead;
        private static bool CanBreed(TriceratopsLife member) => Alive(member) && member.Stage == TriceratopsStage.Adult &&
            member.Animal.HealthRatio > .65f && member.Hunger < .78f && member.Thirst < .78f && member.Fatigue < .75f;
    }
}
