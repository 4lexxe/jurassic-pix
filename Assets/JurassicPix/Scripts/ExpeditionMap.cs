using System.Collections.Generic;
using UnityEngine;

namespace JurassicPix
{
    internal static class ExpeditionMap
    {
        private const int Side = 128;
        private static readonly Color Player = new(.98f, .95f, .42f);
        private static readonly Color Outline = new(.03f, .05f, .04f);
        private static readonly Color Back = new(.035f, .064f, .052f, .91f);
        private static readonly Dictionary<WildlifeHerd, (Vector3 Sum, int Count, Dinosaur Sample)> Groups = new();
        private static readonly List<(Vector3 Center, string Name, object Key)> Quarry = new();
        private static Texture2D disc;
        private static Color32[] source, pixels;
        private static int sourceSide;
        private static float nextRefresh, shownSpan, scale, radius;
        private static Vector2 shown, hub;
        private static GUIStyle tag;

        // mode: 0 vista cercana, 1 media isla, 2 isla completa grande.
        public static void Draw(Texture2D terrain, Rect area, Dinosaur[] animals, Dinosaur active, Transform observer, int mode,
            float look, Texture2D[] icons, ExpeditionNetwork network, Vector3? mark)
        {
            bool follow = mode < 2;
            Transform self = active != null ? active.transform : observer;
            Rect bounds = IslandLayout.Bounds;
            float span = mode >= 2 ? bounds.width : mode == 0 ? (active != null && active.IsFlying ? 980 : 560) :
                (active != null && active.IsFlying ? 1600 : 1400);
            Refresh(terrain, bounds, follow ? new Vector2(self.position.x, self.position.z) : bounds.center, span);
            hub = area.center;
            radius = area.width * .5f;
            scale = area.width / shownSpan;
            Disc(new Rect(area.x - 2, area.y - 2, area.width + 4, area.height + 4), Back, 0);
            GUI.DrawTexture(area, disc);
            Disc(area, new Color(1, 1, 1, .24f), 1.2f);

            Groups.Clear();
            foreach (Dinosaur animal in animals)
            {
                if (animal == active || animal.IsDead || !animal.IsPresent)
                    continue;
                Vector3 place = animal.transform.position;
                if (animal.Herd == null)
                {
                    Badge(place, animal, active, icons, follow);
                    continue;
                }
                Groups.TryGetValue(animal.Herd, out (Vector3 Sum, int Count, Dinosaur Sample) group);
                Groups[animal.Herd] = (group.Sum + place, group.Count + 1, group.Count == 0 ? animal : group.Sample);
                Vector2 point = Project(place);
                if (follow && (point - hub).sqrMagnitude < (radius - 3) * (radius - 3))
                    Disc(new Rect(point.x - 1.3f, point.y - 1.3f, 2.6f, 2.6f), Tone(animal, active), 0);
            }
            foreach ((Vector3 Sum, int Count, Dinosaur Sample) group in Groups.Values)
                Badge(group.Sum / group.Count, group.Sample, active, icons, follow);

            if (active != null)
            {
                if (active.Traits.Nature != Dinosaur.Temperament.Herd)
                {
                    IslandSites.PreyHerds(active, Quarry);
                    foreach ((Vector3 center, string _, object _) in Quarry)
                        Pin(center, new Color(1, .55f, .25f), false);
                }
                if (active.Life != null)
                    Pin(active.Life.Community.Camp, Player, true);
                else if (active.Herd != null && active.Herd.Leader != null)
                    Pin(active.Herd.Goal, Player, true);
            }
            // Los demás jugadores conectados aparecen con su nombre y un pin claro.
            if (network != null && network.Online)
            {
                tag ??= new GUIStyle(GUI.skin.label)
                {
                    fontSize = mode >= 2 ? 11 : 8, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white }
                };
                for (int seat = 0; seat < 5 && seat < animals.Length; seat++)
                {
                    string name = network.SeatName(seat);
                    if (name == null || animals[seat] == active || animals[seat].IsDead || !animals[seat].IsPresent)
                        continue;
                    Vector2 point = Clamp(Project(animals[seat].transform.position), 10);
                    Pin(animals[seat].transform.position, new Color(.62f, .9f, 1), true);
                    float width = mode >= 2 ? 90 : 60;
                    GUI.Label(new Rect(point.x - width * .5f, point.y + (mode >= 2 ? 4 : 2), width, mode >= 2 ? 15 : 11), name, tag);
                }
            }
            if (mark.HasValue)
                Pin(mark.Value, new Color(.55f, .95f, 1), true);
            GUI.Label(new Rect(hub.x - 4, area.y + 1, 14, 16), "N");
            Needle(Clamp(Project(self.position), 13), self.eulerAngles.y, look);
        }

        private static void Refresh(Texture2D terrain, Rect bounds, Vector2 middle, float span)
        {
            if (disc == null)
            {
                disc = new Texture2D(Side, Side, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave
                };
                pixels = new Color32[Side * Side];
                source = terrain.GetPixels32();
                sourceSide = terrain.width;
                shownSpan = 0;
            }
            if (span == shownSpan && (Time.unscaledTime < nextRefresh || (middle - shown).sqrMagnitude < 1))
                return;
            nextRefresh = Time.unscaledTime + .05f;
            shown = middle;
            shownSpan = span;
            for (int y = 0; y < Side; y++)
            for (int x = 0; x < Side; x++)
            {
                float dx = (x + .5f) / Side * 2 - 1, dy = (y + .5f) / Side * 2 - 1;
                float edge = (1 - Mathf.Sqrt(dx * dx + dy * dy)) * Side * .5f;
                Color32 color = default;
                if (edge > 0)
                {
                    int column = Mathf.FloorToInt((middle.x + dx * span * .5f - bounds.xMin) / bounds.width * sourceSide);
                    int row = Mathf.FloorToInt((middle.y + dy * span * .5f - bounds.yMin) / bounds.height * sourceSide);
                    color = column < 0 || row < 0 || column >= sourceSide || row >= sourceSide
                        ? new Color32(20, 61, 77, 255) : source[row * sourceSide + column];
                    color.a = (byte)Mathf.Min(255, edge * 255);
                }
                pixels[y * Side + x] = color;
            }
            disc.SetPixels32(pixels);
            disc.Apply(false);
        }

        private static Vector2 Project(Vector3 world) => hub + new Vector2(world.x - shown.x, shown.y - world.z) * scale;

        // Posición sobre el disco del mapa (en coordenadas lógicas de 960 × 540) → punto del mundo a ras de suelo.
        internal static Vector2 PointAt(Vector3 world) => Project(world);

        internal static Vector3 Unproject(Vector2 logical)
        {
            Vector3 place = new(shown.x + (logical.x - hub.x) / scale, 0, shown.y - (logical.y - hub.y) / scale);
            place.y = Physics.Raycast(new Vector3(place.x, 240, place.z), Vector3.down, out RaycastHit hit, 480)
                ? hit.point.y
                : IslandLayout.SurfaceLevel(new Vector2(place.x, place.z));
            return place;
        }

        private static Vector2 Clamp(Vector2 point, float margin) => hub + Vector2.ClampMagnitude(point - hub, radius - margin);

        private static int Kind(Dinosaur animal) => animal.Life != null ? 0 :
            animal.Traits.Nature == Dinosaur.Temperament.Pack ? 1 : animal.Traits.TailStrike ? 2 : animal.Traits.CanFly ? 3 : 4;

        private static Color Tone(Dinosaur animal, Dinosaur active)
        {
            bool ally = active != null && (animal.Herd != null && animal.Herd == active.Herd ||
                animal.Life != null && active.Life != null && animal.Life.Community == active.Life.Community);
            return ally ? new Color(.60f, .95f, .55f) :
                animal.Traits.Nature is Dinosaur.Temperament.Pack or Dinosaur.Temperament.Solitary ? new Color(.96f, .42f, .33f) :
                animal.Traits.CanFly ? new Color(.62f, .85f, .93f) :
                animal.Traits.TailStrike ? new Color(.94f, .74f, .45f) : new Color(.84f, .90f, .68f);
        }

        private static void Badge(Vector3 world, Dinosaur sample, Dinosaur active, Texture2D[] icons, bool follow)
        {
            Vector2 point = Project(world);
            float size = follow ? 15 : 10;
            if ((point - hub).sqrMagnitude > (radius - size * .5f) * (radius - size * .5f))
                return;
            Disc(new Rect(point.x - size * .5f - 1.5f, point.y - size * .5f - 1.5f, size + 3, size + 3), new Color(.03f, .05f, .04f, .78f), 0);
            GUI.DrawTexture(new Rect(point.x - size * .5f, point.y - size * .5f, size, size), icons[Kind(sample)],
                ScaleMode.ScaleToFit, true, 0, Tone(sample, active), 0, 0);
        }

        private static void Pin(Vector3 world, Color color, bool goal)
        {
            Vector2 point = Clamp(Project(world), 5);
            float half = goal ? 3.5f : 4.5f;
            Matrix4x4 previous = GUI.matrix;
            if (goal)
                Turn(45, point);
            Frame(new Rect(point.x - half - 1, point.y - half - 1, half * 2 + 2, half * 2 + 2), Outline);
            Frame(new Rect(point.x - half, point.y - half, half * 2, half * 2), color);
            GUI.matrix = previous;
        }

        internal static void Turn(float degrees, Vector2 pivot) =>
            GUI.matrix *= Matrix4x4.TRS(pivot, Quaternion.Euler(0, 0, degrees), Vector3.one) * Matrix4x4.Translate(-pivot);

        private static void Needle(Vector2 point, float heading, float look)
        {
            Matrix4x4 previous = GUI.matrix;
            float reach = Mathf.Clamp(radius - (point - hub).magnitude, 8, 30);
            for (int ray = -6; ray <= 6; ray++)
            {
                Turn(look + ray * 4.5f, point);
                Fill(new Rect(point.x - 1.2f, point.y - reach, 2.4f, reach), new Color(1, 1, 1, Mathf.Abs(ray) == 6 ? .6f : .13f));
                GUI.matrix = previous;
            }
            Turn(heading, point);
            for (int step = 0; step < 6; step++)
            {
                float width = 1.4f + step * 1.1f;
                Fill(new Rect(point.x - width * .5f - 1, point.y - 12 + step * 2 - 1, width + 2, 4), Outline);
            }
            for (int step = 0; step < 6; step++)
            {
                float width = 1.4f + step * 1.1f;
                Fill(new Rect(point.x - width * .5f, point.y - 12 + step * 2, width, 2), Player);
            }
            GUI.matrix = previous;
            Disc(new Rect(point.x - 2.5f, point.y - 2.5f, 5, 5), Outline, 0);
            Disc(new Rect(point.x - 1.5f, point.y - 1.5f, 3, 3), Player, 0);
        }

        private static void Disc(Rect rect, Color color, float outline) =>
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, color, outline, rect.width * .5f);

        private static void Frame(Rect rect, Color color) =>
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, color, 1.4f, 0);

        private static void Fill(Rect rect, Color color) =>
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, color, 0, 0);
    }
}
