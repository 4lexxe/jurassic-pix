using System.Collections.Generic;

namespace JurassicPix
{
    internal sealed class FieldNotices
    {
        private const float Duration = 6;
        private readonly Queue<string> pending = new();
        private string current;
        private float until;

        internal void Enqueue(string text) => pending.Enqueue(text);

        internal string Read(float now, out float remaining)
        {
            if (current == null || now >= until)
            {
                current = pending.TryDequeue(out string next) ? next : null;
                until = now + Duration;
            }
            remaining = current == null ? 0 : until - now;
            return current;
        }

        internal void Clear()
        {
            pending.Clear();
            current = null;
            until = 0;
        }
    }
}
