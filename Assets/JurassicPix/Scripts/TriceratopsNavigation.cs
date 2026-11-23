using UnityEngine;
using UnityEngine.AI;

namespace JurassicPix
{
    public sealed class TriceratopsNavigation : MonoBehaviour
    {
        [SerializeField] private NavMeshData island;
        private NavMeshDataInstance registration;

        public void Configure(NavMeshData data) => island = data;

        private void OnEnable() => registration = NavMesh.AddNavMeshData(island);
        private void OnDisable() => registration.Remove();
    }

    internal sealed class TriceratopsRoute
    {
        private readonly NavMeshPath path = new();
        private Vector3[] points = new Vector3[30];
        private Vector3 requested;
        private float nextPlan;
        private int index, count;

        public void Reset()
        {
            count = index = 0;
            nextPlan = 0;
        }

        public Vector3 Steer(Vector3 position, Vector3 destination, bool urgent, int slot)
        {
            Vector3 direct = Horizontal(destination - position);
            if (direct.sqrMagnitude < 8 * 8 && ClearFooting(position, destination))
                return direct;

            if (Time.time >= nextPlan)
            {
                bool moved = Horizontal(destination - requested).sqrMagnitude > 4 * 4;
                if (moved || index >= count || !ClearFooting(position, points[index]))
                {
                    index = count = 0;
                    requested = destination;
                    if (NavMesh.SamplePosition(position, out NavMeshHit start, 5, NavMesh.AllAreas) &&
                        NavMesh.SamplePosition(destination, out NavMeshHit finish, 7, NavMesh.AllAreas) &&
                        NavMesh.CalculatePath(start.position, finish.position, NavMesh.AllAreas, path))
                    {
                        count = path.GetCornersNonAlloc(points);
                        index = Mathf.Min(count, 1);
                    }
                }
                nextPlan = Time.time + (urgent ? .28f : .78f) + slot * .043f;
            }

            while (index < count && Horizontal(points[index] - position).sqrMagnitude < 1.8f * 1.8f)
                index++;
            if (index < count)
                return Horizontal(points[index] - position);
            return ClearFooting(position, destination) ? direct : Vector3.zero;
        }

        private static bool ClearFooting(Vector3 position, Vector3 destination)
        {
            Vector3 horizontal = Horizontal(destination - position);
            float distance = horizontal.magnitude;
            Vector3 ahead = position + horizontal.normalized * Mathf.Min(distance, 5.5f);
            Vector2 point = new(ahead.x, ahead.z);
            return IslandLayout.ShoreDistance(point) > 3 && IslandLayout.LagoonDistance(point) < 0 &&
                !Physics.Linecast(position + Vector3.up, ahead + Vector3.up, 1, QueryTriggerInteraction.Ignore);
        }

        internal static Vector3 Horizontal(Vector3 value) => new(value.x, 0, value.z);
    }
}
