using UnityEngine;

namespace JurassicPix
{
    public enum LifeActivity : byte
    {
        None, Dormant, Egg, Rest, Graze, Drink, Explore, Play, Follow, Alert, Shelter, Panic, Defend, Retreat, Nest, Browse, Court, Sleep
    }

    internal readonly struct DinosaurPose
    {
        public readonly Vector3 Position;
        public readonly float Yaw, Gait, AttackTime;
        public readonly bool Flying, Diving, Impact, Guard, Crouch, Toppled, Stagger, Feed;
        public readonly float Stamina;
        public readonly int Health;
        public readonly float LifeClock;
        public readonly LifeActivity Activity;

        public DinosaurPose(Vector3 position, float yaw, float gait, float attackTime, bool flying, bool diving, bool impact,
            int health, bool guard, float lifeClock, LifeActivity activity, bool crouch = false, bool toppled = false,
            bool stagger = false, bool feed = false, float stamina = 1)
        {
            Position = position;
            Yaw = yaw;
            Gait = gait;
            AttackTime = attackTime;
            Flying = flying;
            Diving = diving;
            Impact = impact;
            Health = health;
            Guard = guard;
            LifeClock = lifeClock;
            Activity = activity;
            Crouch = crouch;
            Toppled = toppled;
            Stagger = stagger;
            Feed = feed;
            Stamina = stamina;
        }

        public static DinosaurPose Blend(DinosaurPose from, DinosaurPose to, float fraction)
        {
            if ((from.Health == 0 && to.Health > 0) || (from.Position - to.Position).sqrMagnitude > 28 * 28 ||
                (from.Activity != to.Activity && (Inactive(from.Activity) || Inactive(to.Activity))))
                return to;
            float attack = from.AttackTime >= 0 && to.AttackTime >= from.AttackTime
                ? Mathf.Lerp(from.AttackTime, to.AttackTime, fraction) : to.AttackTime;
            return new DinosaurPose(Vector3.Lerp(from.Position, to.Position, fraction), Mathf.LerpAngle(from.Yaw, to.Yaw, fraction),
                Mathf.Lerp(from.Gait, to.Gait, fraction), attack, to.Flying, to.Diving, to.Impact, to.Health, to.Guard,
                to.LifeClock, to.Activity, to.Crouch, to.Toppled, to.Stagger, to.Feed, Mathf.Lerp(from.Stamina, to.Stamina, fraction));
        }

        private static bool Inactive(LifeActivity phase) => phase is LifeActivity.Egg or LifeActivity.Dormant;
    }

    internal readonly struct DinosaurInput
    {
        public readonly Vector3 Direction;
        public readonly bool Run;
        public readonly bool Guard, Crouch, Act;
        public readonly float Lift;

        public DinosaurInput(Vector3 direction, bool run, float lift, bool guard = false, bool crouch = false, bool act = false)
        {
            Direction = direction;
            Run = run;
            Lift = lift;
            Guard = guard;
            Crouch = crouch;
            Act = act;
        }
    }
}
