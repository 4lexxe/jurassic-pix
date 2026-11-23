using UnityEngine;

namespace JurassicPix
{
    public sealed class FlightWind : MonoBehaviour
    {
        [SerializeField] private AudioSource wind;
        private Dinosaur previous;
        private Vector3 lastPosition;
        private float airspeed;

        public void SetSource(AudioSource source) => wind = source;

        internal void Mix(Dinosaur current, Vector3 cameraRight, float delta)
        {
            Vector3 movement = Vector3.zero;
            if (current != null)
            {
                Vector3 position = current.transform.position;
                if (current == previous && current.IsFlying && delta > 0)
                    movement = Vector3.ClampMagnitude((position - lastPosition) / delta, 32);
                lastPosition = position;
            }
            previous = current;
            airspeed = Mathf.Lerp(airspeed, movement.magnitude, 1 - Mathf.Exp(-delta * 5));
            float speed = Mathf.InverseLerp(2, 14, airspeed);
            wind.volume = speed * .48f;
            wind.pitch = Mathf.Lerp(.84f, 1.12f, speed);
            float direction = Vector3.Dot(cameraRight, movement.normalized);
            wind.panStereo = Mathf.Lerp(wind.panStereo, direction * .44f, 1 - Mathf.Exp(-delta * 5));
            if (speed > .02f && !wind.isPlaying)
                wind.Play();
            else if (speed <= .02f && wind.isPlaying)
                wind.Stop();
        }
    }
}
