using UnityEngine;

namespace JurassicPix
{
    public sealed class Sighting : MonoBehaviour
    {
        public string Species;
        public string FieldNote;
        public Transform Focus;
        public float Range = 18;
        public Sighting JournalSource;
        public Sighting JournalEntry => JournalSource != null ? JournalSource : this;
    }
}
