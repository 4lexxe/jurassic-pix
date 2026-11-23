using UnityEngine;

namespace JurassicPix
{
    internal static class HudIcons
    {
        // Side outlines in the order of the species roster, as x, y pairs with y pointing down.
        private static readonly float[][] Outlines =
        {
            new[]
            {
                .02f,.55f, .25f,.38f, .45f,.30f, .60f,.32f, .64f,.16f, .72f,.12f, .79f,.24f, .93f,.14f, .85f,.31f, .92f,.37f, .99f,.32f,
                .95f,.44f, .97f,.52f, .86f,.57f, .74f,.53f, .70f,.60f, .72f,.86f, .60f,.86f, .60f,.66f, .40f,.68f, .40f,.86f, .27f,.86f,
                .25f,.62f, .02f,.58f
            },
            new[]
            {
                .02f,.30f, .30f,.40f, .50f,.40f, .62f,.26f, .70f,.14f, .78f,.10f, .98f,.18f, .98f,.22f, .80f,.26f, .72f,.34f, .66f,.48f,
                .74f,.54f, .68f,.60f, .60f,.52f, .52f,.56f, .56f,.68f, .50f,.76f, .58f,.90f, .44f,.90f, .42f,.72f, .40f,.56f, .28f,.46f,
                .02f,.34f
            },
            new[]
            {
                0f,.62f, .22f,.50f, .38f,.44f, .55f,.44f, .72f,.28f, .84f,.12f, .90f,.08f, .99f,.12f, .98f,.17f, .90f,.18f, .80f,.32f,
                .68f,.52f, .68f,.90f, .60f,.90f, .60f,.66f, .46f,.68f, .46f,.90f, .37f,.90f, .36f,.62f, .22f,.56f, 0f,.65f
            },
            new[]
            {
                0f,.40f, .30f,.30f, .45f,.36f, .48f,.20f, .50f,.02f, .53f,.20f, .56f,.36f, .70f,.30f, 1f,.40f, .72f,.52f, .58f,.56f,
                .54f,.80f, .50f,.86f, .46f,.80f, .42f,.56f, .28f,.52f
            },
            new[]
            {
                .02f,.40f, .30f,.34f, .52f,.30f, .66f,.20f, .74f,.14f, .92f,.16f, .98f,.24f, .98f,.30f, .80f,.33f, .94f,.38f, .78f,.42f,
                .68f,.40f, .66f,.50f, .72f,.56f, .66f,.58f, .62f,.52f, .52f,.56f, .56f,.70f, .50f,.80f, .60f,.90f, .44f,.90f, .40f,.74f,
                .38f,.56f, .02f,.44f
            }
        };

        public static Texture2D[] Build()
        {
            const int side = 64;
            var icons = new Texture2D[Outlines.Length];
            for (int icon = 0; icon < icons.Length; icon++)
            {
                float[] outline = Outlines[icon];
                var pixels = new Color32[side * side];
                for (int row = 0; row < side; row++)
                for (int column = 0; column < side; column++)
                {
                    int covered = 0;
                    for (int sample = 0; sample < 9; sample++)
                    {
                        float x = (column + (sample % 3 + .5f) / 3) / side;
                        float y = 1 - (row + (sample / 3 + .5f) / 3) / side;
                        if (Inside(outline, x, y))
                            covered++;
                    }
                    pixels[row * side + column] = new Color32(255, 255, 255, (byte)(covered * 255 / 9));
                }
                icons[icon] = new Texture2D(side, side, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave
                };
                icons[icon].SetPixels32(pixels);
                icons[icon].Apply(false, true);
            }
            return icons;
        }

        private static bool Inside(float[] outline, float x, float y)
        {
            bool inside = false;
            for (int current = 0, previous = outline.Length - 2; current < outline.Length; previous = current, current += 2)
            {
                float ax = outline[current], ay = outline[current + 1], bx = outline[previous], by = outline[previous + 1];
                if (ay > y != by > y && x < (bx - ax) * (y - ay) / (by - ay) + ax)
                    inside = !inside;
            }
            return inside;
        }
    }
}
