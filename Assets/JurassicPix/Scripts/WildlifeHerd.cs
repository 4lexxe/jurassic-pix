using System.Collections.Generic;
using UnityEngine;

namespace JurassicPix
{
    public sealed class WildlifeHerd : MonoBehaviour
    {
        [SerializeField] private Vector2 center, radius;
        [SerializeField] private Vector2[] formation;
        [SerializeField] private bool aerial;
        [SerializeField] private float period, phase;

        private readonly List<Dinosaur> members = new();
        private Dinosaur foe, menace, meal;
        private float foeUntil, menaceUntil, mealUntil, huntStarted;
        private Vector3 home, destination, heading = Vector3.forward, lastTree;
        private float deadline, thirst, hunger, nextSurvey, depth;
        private object quarry, shunned;
        private bool planned, managed, commanded;
        private float idleSince;
        private int trips;
        private readonly List<Dinosaur> guards = new();
        private bool fleeing;

        internal float SatedUntil { get; private set; }
        internal float HuntTime => Time.time - huntStarted;
        internal Dinosaur Foe => Active(foe) && Time.time < foeUntil ? foe : null;
        internal Dinosaur Menace => Active(menace) && Time.time < menaceUntil ? menace : null;
        internal Dinosaur Meal => meal != null && meal.IsPresent && meal.IsDead && Time.time < mealUntil ? meal : null;
        internal HerdErrand Errand { get; private set; }
        internal bool Arrived { get; private set; }
        internal Dinosaur Leader { get; private set; }
        internal Vector3 Middle { get; private set; }
        internal int Count { get; private set; }
        internal float Weakest { get; private set; } = 1;
        internal Dinosaur Sample => members.Count > 0 ? members[0] : null;
        internal Vector3 Goal => destination;
        internal float Thirst => thirst;
        internal float Hunger => hunger;
        internal bool Fleeing => fleeing && Menace != null;

        // En una manada que huye solo los guardias plantan cara; el resto corre detrás del guía.
        internal bool Guards(Dinosaur member) => !Fleeing || guards.Contains(member);

        public void Configure(Vector2 point, Vector2 roam, Vector2[] places, float cycle, float start,
            bool fly = false)
        {
            center = point;
            radius = roam;
            formation = places;
            period = cycle;
            phase = start;
            aerial = fly;
        }

        internal void Bind(Dinosaur[] population)
        {
            members.Clear();
            foreach (Dinosaur animal in population)
                if (animal.Herd == this)
                    members.Add(animal);
            managed = members.Count > 0 && members[0].Life == null;
            home = IslandSites.Ground(center);
            Middle = destination = home;
            Count = members.Count;
            thirst = Mathf.Repeat(phase * .137f, .5f);
            hunger = Mathf.Repeat(phase * .071f, .4f);
            foreach (Vector2 place in formation)
                depth = Mathf.Max(depth, Mathf.Abs(place.y));
            if (managed && !IslandSites.Herds.Contains(this))
                IslandSites.Herds.Add(this);
        }

        internal void Pursue(Dinosaur prey, float seconds)
        {
            if (Foe != prey)
                huntStarted = Time.time;
            foe = prey;
            foeUntil = Time.time + seconds;
        }

        internal void Dread(Dinosaur danger, float seconds)
        {
            menace = danger;
            menaceUntil = Time.time + seconds;
        }

        internal void Feast(Dinosaur carcass, float seconds)
        {
            meal = carcass;
            mealUntil = Time.time + seconds;
            foe = null;
            SatedUntil = mealUntil + 70;
            hunger = 0;
            shunned = null;
            planned = false;
        }

        internal void Abandon(float rest)
        {
            foe = null;
            SatedUntil = Mathf.Max(SatedUntil, Time.time + rest);
            shunned = quarry;
            planned = false;
        }

        private void Update()
        {
            if (!managed || members[0].IsReplica || Time.time < nextSurvey)
                return;
            const float tick = .5f;
            nextSurvey = Time.time + tick;
            Vector3 sum = Vector3.zero;
            Count = 0;
            Weakest = 1;
            Leader = null;
            Dinosaur commander = null;
            foreach (Dinosaur member in members)
            {
                if (member.IsDead || !member.IsPresent)
                    continue;
                Count++;
                sum += member.transform.position;
                Weakest = Mathf.Min(Weakest, member.HealthRatio);
                if (member.Leading)
                    commander = member;
                if (Leader == null && !member.IsControlled)
                    Leader = member;
            }
            if (commander != null)
                Leader = commander;
            if (Leader == null)
                return;
            Middle = sum / Count;
            Dinosaur.Temperament nature = Leader.Traits.Nature;
            thirst = Mathf.Clamp01(thirst + tick * (Arrived && Errand == HerdErrand.Drink ? -.05f : .0024f));
            hunger = Mathf.Clamp01(hunger + tick * (Arrived && Errand == HerdErrand.Graze ? -.03f : .0020f));
            if (commander != null)
            {
                Command(commander, nature == Dinosaur.Temperament.Herd);
                return;
            }
            if (commanded)
                commanded = planned = false;
            if (nature == Dinosaur.Temperament.Herd && !aerial)
            {
                if (Menace != null)
                {
                    Escape();
                    return;
                }
                if (fleeing)
                {
                    fleeing = planned = false;
                    guards.Clear();
                }
            }
            if (Foe != null || Menace != null || Meal != null)
            {
                deadline += tick;
                return;
            }
            Vector3 guide = Leader.transform.position;
            if (Errand == HerdErrand.Hunt && !Arrived && quarry != null)
                destination = quarry is TriceratopsHerd community ? community.Center : ((WildlifeHerd)quarry).Middle;
            if (!planned || Time.time > deadline)
            {
                if (planned && Errand == HerdErrand.Hunt)
                {
                    shunned = quarry;
                    if (aerial)
                        hunger = 0;
                }
                Plan(nature);
            }
            else if (!Arrived && TriceratopsLife.Distance(guide, destination) < (aerial ? 30 : 10 + Leader.Length * .6f))
            {
                Arrived = true;
                deadline = Time.time + Errand switch
                {
                    HerdErrand.Drink => 24, HerdErrand.Graze => 45, HerdErrand.Hunt => 30, HerdErrand.Rest => 22, _ => 14
                };
            }
            Vector3 course = TriceratopsRoute.Horizontal(destination - guide);
            if (!Arrived && course.sqrMagnitude > 4)
                heading = course.normalized;
        }

        // La manada de herbívoros escapa del peligro detrás de su guía; hasta dos adultos sanos se quedan a cubrirla.
        private void Escape()
        {
            Vector3 danger = Menace.transform.position;
            if (!fleeing)
            {
                fleeing = true;
                guards.Clear();
                int wanted = Count > 4 ? 2 : Count > 2 ? 1 : 0;
                while (guards.Count < wanted)
                {
                    Dinosaur nearest = null;
                    float range = float.PositiveInfinity;
                    foreach (Dinosaur member in members)
                    {
                        if (member == Leader || member.IsDead || !member.IsPresent || member.IsControlled || member.HealthRatio < .45f ||
                            guards.Contains(member))
                            continue;
                        float distance = TriceratopsLife.Distance(member.transform.position, danger);
                        if (distance >= range)
                            continue;
                        range = distance;
                        nearest = member;
                    }
                    if (nearest == null)
                        break;
                    guards.Add(nearest);
                }
            }
            Vector3 guide = Leader.transform.position;
            Vector3 away = TriceratopsRoute.Horizontal(guide - danger);
            away = away.sqrMagnitude > 1 ? away.normalized : heading;
            Vector3 goal = guide + away * 70;
            var point = new Vector2(goal.x, goal.z);
            // Contra la costa o un lago la huida dobla hacia tierra firme.
            if (IslandLayout.ShoreDistance(point) < 30 || IslandLayout.LagoonDistance(point) > -20)
            {
                away = (away + IslandSites.Inland(guide) * 1.5f).normalized;
                goal = guide + away * 70;
            }
            planned = true;
            Arrived = false;
            Errand = HerdErrand.Roam;
            quarry = null;
            destination = goal;
            heading = away;
            deadline = Time.time + 30;
        }

        private void Command(Dinosaur commander, bool grazer)
        {
            commanded = planned = true;
            quarry = null;
            Vector3 place = commander.transform.position;
            if (commander.Velocity.sqrMagnitude > .6f || commander.IsFlying)
            {
                idleSince = Time.time;
                heading = TriceratopsRoute.Horizontal(commander.transform.forward).normalized;
            }
            deadline = Time.time + 20;
            if (Time.time - idleSince <= 2.5f)
            {
                Arrived = false;
                Errand = HerdErrand.Roam;
                destination = place;
                return;
            }
            if (Arrived)
                return;
            Arrived = true;
            Vector3 water = IslandSites.Water(place);
            if (TriceratopsLife.Distance(water, place) < 40)
            {
                Errand = HerdErrand.Drink;
                destination = water;
            }
            else if (grazer && IslandSites.GiantTree(place, place, new Vector3(1e6f, 0, 1e6f), out Vector3 tree) &&
                TriceratopsLife.Distance(tree, place) < 55)
            {
                Errand = HerdErrand.Graze;
                destination = lastTree = tree;
            }
            else
            {
                Errand = HerdErrand.Rest;
                destination = place;
            }
        }

        private void Plan(Dinosaur.Temperament nature)
        {
            planned = true;
            Arrived = false;
            trips++;
            quarry = null;
            bool hunter = nature != Dinosaur.Temperament.Herd;
            if (thirst > .55f)
            {
                Errand = HerdErrand.Drink;
                destination = IslandSites.Water(Middle);
            }
            else if (hunger > .5f && !hunter && IslandSites.GiantTree(Middle, home, lastTree, out Vector3 tree))
            {
                Errand = HerdErrand.Graze;
                destination = lastTree = tree;
            }
            else if (hunger > .4f && hunter && Time.time >= SatedUntil &&
                IslandSites.Prey(Leader, Middle, shunned, out Vector3 prey, out quarry))
            {
                Errand = HerdErrand.Hunt;
                destination = prey;
            }
            else
            {
                Errand = Night || trips % 3 == 0 ? HerdErrand.Rest : HerdErrand.Roam;
                float angle = trips * 2.4f + phase;
                Vector2 point = center + new Vector2(Mathf.Sin(angle) * radius.x, Mathf.Cos(angle) * radius.y) * (aerial ? 1 : 2.2f);
                if (IslandLayout.ShoreDistance(point) < 25 || IslandLayout.LagoonDistance(point) > -15)
                    point = center;
                destination = IslandSites.Ground(point);
            }
            deadline = Time.time + 70 + TriceratopsLife.Distance(Middle, destination) / 1.6f;
        }

        private Vector3 Post(Dinosaur animal)
        {
            Vector2 place = formation[Mathf.Min(animal.HerdSlot, formation.Length - 1)];
            Vector3 side = Vector3.Cross(Vector3.up, heading);
            if (!Arrived)
            {
                if (animal == Leader)
                    return destination;
                Vector2 lead = formation[Mathf.Min(Leader.HerdSlot, formation.Length - 1)];
                return Leader.transform.position + side * (place.x - lead.x) - heading * (depth + place.y + 4);
            }
            switch (Errand)
            {
                case HerdErrand.Drink:
                    Vector3 inland = IslandSites.Inland(destination);
                    return destination + Vector3.Cross(Vector3.up, inland) * place.x +
                        inland * ((place.y + depth) * .7f + (aerial ? 5 : 0));
                case HerdErrand.Graze:
                    float turn = animal.HerdSlot * 2 * Mathf.PI / Mathf.Max(3, members.Count);
                    return destination + new Vector3(Mathf.Sin(turn), 0, Mathf.Cos(turn)) * (9 + animal.Length * .36f);
                default:
                    return destination + side * place.x + heading * place.y;
            }
        }

        internal Vector3 Steer(Dinosaur animal, out bool run, out LifeActivity activity, out bool hold)
        {
            bool escaping = Fleeing;
            run = aerial || escaping;
            activity = LifeActivity.None;
            hold = false;
            Vector3 here = animal.transform.position;
            if (Leader == null)
                return Vector3.zero;
            Vector3 gap = TriceratopsRoute.Horizontal(Post(animal) - here);
            if (escaping && animal != Leader)
            {
                // En la huida nadie vuelve hacia el peligro para ocupar su lugar: corre con el guía y se va acomodando de costado.
                Vector3 lane = gap - heading * Vector3.Dot(gap, heading);
                gap = heading * 30 + Vector3.ClampMagnitude(lane, 12);
            }
            float distance = gap.magnitude;
            if (Arrived)
            {
                if (distance > (aerial ? (animal.IsFlying ? 16 : 45) : 3 + animal.Length * .14f))
                    return Vector3.ClampMagnitude(gap * (aerial ? .03f : .2f), .82f);
                activity = Errand switch
                {
                    HerdErrand.Drink => LifeActivity.Drink, HerdErrand.Graze => LifeActivity.Browse,
                    HerdErrand.Rest => Night ? LifeActivity.Sleep : LifeActivity.Rest, _ => LifeActivity.None
                };
                hold = Errand is HerdErrand.Drink or HerdErrand.Graze;
                if (!hold)
                    return Vector3.zero;
                Vector3 look = Errand == HerdErrand.Drink ? -IslandSites.Inland(destination)
                    : TriceratopsRoute.Horizontal(destination - here).normalized;
                return look.sqrMagnitude > .01f ? look : animal.transform.forward;
            }
            if (animal == Leader)
            {
                bool waiting = !escaping && TriceratopsLife.Distance(here, Middle) > 16 + animal.Length * 1.4f;
                return distance > .1f ? gap / distance * (waiting ? .22f : escaping ? 1 : .8f) : Vector3.zero;
            }
            run |= distance > 20 + animal.Length || Errand == HerdErrand.Drink && distance > 25 ||
                commanded && distance > 6 && Leader.Motion.GaitWeight > 1.15f;
            return Vector3.ClampMagnitude(gap * .2f, run ? 1 : .86f);
        }

        private static bool Night => Shader.GetGlobalFloat("_ForestNight") > .6f;
        private static bool Active(Dinosaur creature) => creature != null && creature.IsPresent && !creature.IsDead;
    }
}
