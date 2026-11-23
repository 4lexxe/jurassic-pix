using System.Collections.Generic;
using UnityEngine;

namespace JurassicPix
{
    internal static class IslandSites
    {
        internal static readonly List<TriceratopsHerd> Communities = new();
        internal static readonly List<WildlifeHerd> Herds = new();
        private static readonly List<Vector3> giants = new();
        private static readonly List<(Vector3 Center, string Name, object Key)> scratch = new();
        private static Transform grove;

        internal static Vector3 Ground(Vector2 point)
        {
            return Physics.Raycast(new Vector3(point.x, 900, point.y), Vector3.down, out RaycastHit hit, 1000,
                1 << Dinosaur.TerrainLayer, QueryTriggerInteraction.Ignore)
                ? hit.point : new Vector3(point.x, IslandLayout.SurfaceLevel(point), point.y);
        }

        internal static Vector3 Water(Vector3 from)
        {
            Vector2 start = new(from.x, from.z);
            Vector2 middle = IslandLayout.NearestLake(start);
            Vector2 lake = start - middle;
            lake = lake.sqrMagnitude > 1 ? lake.normalized : Vector2.up;
            Vector2 bank = middle;
            for (float reach = 20; reach < 700 && IslandLayout.LagoonDistance(bank) > -2; reach += 2)
                bank = middle + lake * reach;
            Vector2 sea = start - IslandLayout.Center;
            sea = sea.sqrMagnitude > 1 ? sea.normalized : Vector2.down;
            Vector2 beach = start;
            for (float reach = 5; reach < 4400 && IslandLayout.ShoreDistance(beach) > 4; reach += 3)
                beach = start + sea * reach;
            return Ground((bank - start).sqrMagnitude < (beach - start).sqrMagnitude ? bank : beach);
        }

        // Suelo firme más cercano tierra adentro, para que nadie nazca bajo el agua.
        internal static Vector3 Dry(Vector3 from)
        {
            var point = new Vector2(from.x, from.z);
            Vector3 inland = Inland(from);
            for (int step = 0; step < 80; step++)
            {
                Vector3 ground = Ground(point);
                if (ground.y > IslandLayout.SurfaceLevel(point) + .4f)
                    return ground + Vector3.up * .1f;
                point += new Vector2(inland.x, inland.z) * 4;
            }
            return Ground(point) + Vector3.up * .1f;
        }

        internal static Vector3 Inland(Vector3 site)
        {
            Vector2 point = new(site.x, site.z);
            Vector2 way = IslandLayout.LagoonDistance(point) > -40 ? point - IslandLayout.NearestLake(point) : IslandLayout.Center - point;
            return new Vector3(way.x, 0, way.y).normalized;
        }

        internal static bool GiantTree(Vector3 from, Vector3 home, Vector3 last, out Vector3 tree)
        {
            if (grove == null)
            {
                giants.Clear();
                GameObject found = GameObject.Find("Arboleda de gigantes");
                grove = found != null ? found.transform : null;
                if (grove != null)
                {
                    foreach (Transform giant in grove)
                        if (giant.childCount > 0)
                            giants.Add(giant.GetChild(0).position);
                }
            }
            tree = from;
            float best = float.PositiveInfinity;
            foreach (Vector3 giant in giants)
            {
                float score = TriceratopsLife.Distance(from, giant) + .6f * TriceratopsLife.Distance(home, giant);
                if (score >= best || TriceratopsLife.Distance(giant, last) < 5)
                    continue;
                best = score;
                tree = giant;
            }
            return best < float.PositiveInfinity;
        }

        internal static void PreyHerds(Dinosaur hunter, List<(Vector3 Center, string Name, object Key)> result)
        {
            result.Clear();
            Dinosaur.Temperament nature = hunter.Traits.Nature;
            foreach (TriceratopsHerd community in Communities)
                if (community.Living > 0)
                    result.Add((community.Center, "Triceratops", community));
            if (nature == Dinosaur.Temperament.Pack)
                return;
            foreach (WildlifeHerd herd in Herds)
            {
                Dinosaur sample = herd.Sample;
                if (herd == hunter.Herd || sample == null || herd.Count == 0 || sample.Traits.CanFly)
                    continue;
                if (sample.Traits.Nature == Dinosaur.Temperament.Pack ||
                    nature == Dinosaur.Temperament.Solitary && herd.Weakest < .4f)
                    result.Add((herd.Middle, sample.Species, herd));
            }
        }

        internal static bool Prey(Dinosaur hunter, Vector3 from, object shunned, out Vector3 center, out object key)
        {
            PreyHerds(hunter, scratch);
            center = from;
            key = null;
            float best = float.PositiveInfinity;
            bool flyer = hunter.Traits.Nature == Dinosaur.Temperament.Flyer;
            foreach ((Vector3 place, string name, object herd) in scratch)
            {
                float score = TriceratopsLife.Distance(from, place) + (name == "Triceratops" == flyer ? 300 : 0) +
                    (herd == shunned ? 900 : 0);
                if (score >= best)
                    continue;
                best = score;
                center = place;
                key = herd;
            }
            return key != null;
        }
    }
}
