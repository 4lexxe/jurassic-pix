using UnityEngine;

namespace JurassicPix
{
    internal sealed class DinosaurVitals
    {
        public int Maximum { get; private set; }
        public int Health { get; private set; }
        public bool Dead => Health == 0;
        public float Ratio => (float)Health / Maximum;

        public DinosaurVitals(int maximum)
        {
            Maximum = maximum;
            Health = maximum;
        }

        public int Hit(int damage, float resistance, float guardMultiplier)
        {
            int previous = Health;
            int effective = Mathf.Max(1, Mathf.RoundToInt(damage * (1 - resistance) * guardMultiplier));
            SetHealth(Health - effective);
            return previous - Health;
        }

        public void SetHealth(int health) => Health = Mathf.Clamp(health, 0, Maximum);

        public void Resize(int limit)
        {
            if (limit == Maximum)
                return;
            int current = Dead ? 0 : Mathf.Max(1, Mathf.RoundToInt(Ratio * limit));
            Maximum = limit;
            SetHealth(current);
        }
    }
}
