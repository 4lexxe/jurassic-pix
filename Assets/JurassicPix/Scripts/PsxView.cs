using UnityEngine;

namespace JurassicPix
{
    [RequireComponent(typeof(Camera))]
    public sealed class PsxView : MonoBehaviour
    {
        [SerializeField] private Shader colorShader;
        [SerializeField, Range(0, 1), Tooltip("Intensidad del pixelado, la paleta reducida y las tramas. 1 es el aspecto PSX completo.")]
        private float retro = 0.15f;
        [SerializeField, Range(120, 720)] private int lines = 240;
        [SerializeField, Range(4, 256)] private int colorLevels = 32;
        [SerializeField, Range(0, 1)] private float dither = 1f;
        [SerializeField, Range(0, 1), Tooltip("Trama de puntos cuadrados en sombras y medios tonos.")]
        private float stipple = 0.14f;
        [SerializeField, Range(0.5f, 2)] private float gamma = 0.8f;
        [SerializeField, Range(0, 0.3f)] private float scanlines;
        [SerializeField, Range(0, 1.5f), Tooltip("Rayos y destellos de lente cuando el sol entra en cuadro.")]
        private float sunFlare = 1;

        private Camera worldCamera;
        private RenderTexture pixels;
        private Material colorMaterial;
        private int screenHeight;

        public static Rect Viewport
        {
            get
            {
                float scale = Mathf.Min(Screen.width / 16f, Screen.height / 9f);
                Vector2 size = new(16 * scale, 9 * scale);
                return new Rect((Screen.width - size.x) / 2, (Screen.height - size.y) / 2, size.x, size.y);
            }
        }

        private void OnEnable()
        {
            worldCamera = GetComponent<Camera>();
            worldCamera.allowHDR = false;
            worldCamera.allowMSAA = false;
            colorMaterial = new Material(colorShader);
            colorMaterial.SetFloat("_Retro", retro);
            Resize();
            colorMaterial.SetFloat("_Levels", colorLevels);
            colorMaterial.SetFloat("_Dither", dither);
            colorMaterial.SetFloat("_Stipple", stipple);
            colorMaterial.SetFloat("_Gamma", gamma);
            colorMaterial.SetFloat("_Scanlines", scanlines);
        }

        private void Resize()
        {
            if (pixels != null)
            {
                worldCamera.targetTexture = null;
                pixels.Release();
                Destroy(pixels);
            }
            screenHeight = Screen.height;
            float native = Mathf.Max(lines, Viewport.height);
            int rows = Mathf.RoundToInt(native / Mathf.Lerp(1, native / lines, retro));
            pixels = new RenderTexture(Mathf.RoundToInt(rows * 16f / 9f), rows, 24, RenderTextureFormat.ARGB32)
            {
                name = "PSX View", filterMode = retro > .6f ? FilterMode.Point : FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp, antiAliasing = 1
            };
            worldCamera.targetTexture = pixels;
        }

        private void Update()
        {
            if (Screen.height != screenHeight)
                Resize();
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            Vector3 sun = Shader.GetGlobalVector("_ForestSunDisk");
            Vector3 spot = worldCamera.WorldToViewportPoint(worldCamera.transform.position + sun * 1000);
            float day = 1 - Shader.GetGlobalFloat("_ForestNight");
            float edge = Mathf.Min(Mathf.Min(spot.x, 1 - spot.x), Mathf.Min(spot.y, 1 - spot.y));
            float strength = spot.z <= 0 ? 0 : sunFlare * day * day * (1 - Shader.GetGlobalVector("_ValleyWeather").y) *
                Mathf.InverseLerp(-.12f, .04f, edge) * Mathf.InverseLerp(-.02f, .1f, sun.y);
            colorMaterial.SetVector("_SunScreen", new Vector4(spot.x, spot.y, strength, worldCamera.aspect));
            colorMaterial.SetVector("_SunTint", Vector4.Lerp(new Vector4(1, .62f, .3f), new Vector4(1, .93f, .78f), Mathf.Clamp01(sun.y * 2.2f)));
            Graphics.Blit(source, destination, colorMaterial);
        }

        public void DrawViewport()
        {
            GUI.DrawTexture(Viewport, pixels, ScaleMode.StretchToFill, false);
        }

        private void OnDisable()
        {
            worldCamera.targetTexture = null;
            pixels.Release();
            Destroy(pixels);
            Destroy(colorMaterial);
        }
    }
}
