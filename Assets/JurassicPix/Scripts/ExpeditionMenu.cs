using System.Collections.Generic;
using UnityEngine;

namespace JurassicPix
{
    // Menú de inicio para elegir especie, panel de la partida y los recursos de dibujo de la interfaz.
    public sealed partial class Expedition
    {
        private static readonly string[] Diets =
        {
            "HERBÍVORO  ·  VIVE EN MANADA", "CARNÍVORO  ·  CAZA EN GRUPO", "HERBÍVORO  ·  MANADA DE GIGANTES",
            "CARNÍVORO  ·  VOLADOR", "CARNÍVORO  ·  CAZADOR SOLITARIO"
        };
        private static readonly string[] Kinds = { "Herbívoro", "Cazador en grupo", "Herbívoro gigante", "Volador", "Cazador solitario" };
        private static readonly string[] Blurbs =
        {
            "Nacés en una manada que te defiende. Pastá, tomá agua y no te alejes de los adultos hasta tener tus cuernos.",
            "Chiquito y veloz. De cría te conviene esconderte detrás de tu grupo; de grande cazás con ellos.",
            "El gigante de la isla. De cría sos presa del T. rex; de adulto casi nadie se anima a acercarse.",
            "Empezás en tierra, frágil y a pie. Cuando sos juvenil aprendés a volar y la isla entera es tuya.",
            "Nacés solo, sin manada que te cuide. Buscá carroña y presas chicas hasta ser el mayor depredador."
        };
        private static readonly string[] BirthTips =
        {
            "Naciste. Quedate cerca de la manada: los adultos te defienden.",
            "Naciste. Seguí a tu grupo y no enfrentes nada más grande que vos.",
            "Naciste. Caminá con la manada: el T. rex busca crías rezagadas.",
            "Naciste. Todavía no volás: mantenete lejos de los cazadores.",
            "Naciste solo. Buscá agua y restos para comer antes de cazar."
        };
        private static readonly int[] Difficulty = { 0, 1, 1, 1, 2 };
        private static readonly string[] DifficultyNames = { "Tranquila", "Media", "Difícil" };
        private static readonly Rect BirthButton = new(50, 362, 236, 34);
        private static readonly Rect NetworkButton = new(800, 32, 112, 24);
        private static readonly Rect BackButton = new(696, 32, 96, 24);

        private bool menu = true, awaitingSeat, menuCut = true;
        private int menuIndex;
        private float menuTurn, nextRequest;

        private GUIStyle small, faint, strong, body, tiny, heading, note, centered, keycap, title, display, dark;
        private GUIStyle centeredBody, centeredSmall, tagName;
        private Texture2D[] icons;
        private Texture2D fadeRight, fadeDown, cornerShade;
        private readonly List<(Vector3 Center, string Name, object Key)> quarry = new();
        private static readonly Color Paper = new(0.96f, 0.92f, 0.76f);
        private static readonly Color Accent = new(0.81f, 0.94f, 0.46f);
        private static readonly Color Panel = new(0.035f, 0.064f, 0.052f, 0.91f);
        private static readonly Color Quiet = new(0.67f, 0.74f, 0.65f);
        private static readonly Color Warn = new(.95f, .46f, .34f);
        private static readonly Color Veil = new(.012f, .024f, .019f, 1);

        private static Rect CardRect(int index) => new(48 + index * 176, 446, 168, 70);

        private void OpenMenu()
        {
            menu = true;
            panel = awaitingSeat = false;
            menuIndex = lastCreature;
            menuCut = true;
            Page();
            RefreshPointer();
        }

        private void CloseMenu()
        {
            menu = awaitingSeat = false;
            if (active != null)
                FollowCreature(true);
            RefreshPointer();
        }

        private void Browse(int index)
        {
            if (index == menuIndex)
                return;
            menuIndex = index;
            menuCut = true;
            awaitingSeat = false;
            Page();
        }

        private void ConfirmSpecies()
        {
            if (network.Dedicated || network.IsOccupied(menuIndex))
                return;
            Page();
            if (network.Online)
            {
                // El anfitrión decide; Update cierra el menú cuando el lugar ya es nuestro.
                awaitingSeat = true;
                nextRequest = Time.unscaledTime + 1;
                network.Select(menuIndex);
                return;
            }
            ChooseCreature(menuIndex);
            Begin();
        }

        private void Begin()
        {
            menu = panel = awaitingSeat = false;
            lifeStart = Time.time;
            stage = -1;
            wasDead = false;
            if (active != null)
                FollowCreature(true);
            notices.Clear();
            Notify(BirthTips[lastCreature]);
            RefreshPointer();
        }

        private void ReadMenuKeys()
        {
            int count = creatures.Length;
            int move = (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D) ? 1 : 0) -
                (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A) ? 1 : 0);
            if (move != 0)
                Browse((menuIndex + move + count) % count);
            for (int i = 0; i < count; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                    Browse(i);
            }
            if (!menu)
            {
                if (Input.GetKeyDown(KeyCode.V) && !network.Dedicated)
                    OpenMenu();
                return;
            }
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
                ConfirmSpecies();
            else if (Input.GetKeyDown(KeyCode.Escape) && active != null)
                CloseMenu();
            else if (Input.GetMouseButtonDown(0))
            {
                Vector3 mouse = Input.mousePosition;
                ReadMenuPress(new Vector2(mouse.x, Screen.height - mouse.y));
            }
        }

        private bool ReadMenuPress(Vector2 point)
        {
            if (!menu)
                return false;
            if (ControlRect(NetworkButton).Contains(point))
            {
                ToggleCoopMenu();
                return true;
            }
            if (active != null && ControlRect(BackButton).Contains(point))
            {
                CloseMenu();
                return true;
            }
            if (ControlRect(BirthButton).Contains(point))
            {
                ConfirmSpecies();
                return true;
            }
            for (int i = 0; i < creatures.Length; i++)
            {
                Rect card = CardRect(i);
                card.yMin -= 10;
                if (!ControlRect(card).Contains(point))
                    continue;
                if (i == menuIndex)
                    ConfirmSpecies();
                else
                    Browse(i);
                return true;
            }
            return false;
        }

        // La cámara gira despacio alrededor de la especie elegida, que sigue con su vida en la isla.
        private void Showcase()
        {
            Dinosaur subject = creatures[menuIndex];
            if (menuCut)
                menuTurn = subject.transform.eulerAngles.y + 152;
            menuTurn += Time.unscaledDeltaTime * 5;
            float grown = Mathf.InverseLerp(.36f, 1, subject.Size);
            Vector3 target = subject.transform.position +
                Vector3.up * (subject.Traits.CameraHeight * Mathf.Lerp(subject.Size * 1.2f, .9f, grown));
            PlaceCamera(target, Quaternion.Euler(subject.IsFlying ? 4 : 9, menuTurn, 0),
                subject.Traits.CameraDistance * Mathf.Lerp(.5f, 1.08f, grown), menu ? .3f : 0, menuCut);
            menuCut = false;
        }

        private static Vector2 Pointer()
        {
            Rect view = PsxView.Viewport;
            Vector3 mouse = Input.mousePosition;
            return new Vector2(mouse.x - view.x, Screen.height - mouse.y - view.y) * (960 / view.width);
        }

        private void DrawMenu()
        {
            bool touch = Application.isMobilePlatform;
            Vector2 pointer = touch ? new Vector2(-1, -1) : Pointer();
            Dinosaur subject = creatures[menuIndex];
            Dinosaur.Profile traits = subject.Traits;
            GUI.DrawTexture(new Rect(0, 0, 640, 540), fadeRight, ScaleMode.StretchToFill, true, 0, new Color(Veil.r, Veil.g, Veil.b, .93f), 0, 0);
            GUI.DrawTexture(new Rect(0, 350, 960, 190), fadeDown, ScaleMode.StretchToFill, true, 0, new Color(Veil.r, Veil.g, Veil.b, .9f), 0, 0);

            GUI.Label(new Rect(48, 30, 520, 46), "JURASSIC PIX", title);
            Fill(new Rect(50, 80, 38, 3), Accent);
            GUI.Label(new Rect(50, 90, 420, 16), "Nacés como cría. Crecé, cuidate y sobreviví.", body);

            GUI.Label(new Rect(50, 136, 360, 12), Diets[menuIndex], tiny);
            GUI.Label(new Rect(49, 148, 420, 32), sightings[menuIndex].Species, display);
            GUI.Label(new Rect(50, 182, 400, 15), $"{traits.SizeLabel} de adulto  ·  {traits.AttackName}", small);
            GUI.Label(new Rect(50, 202, 322, 50), Blurbs[menuIndex], note);

            (string Name, float Value)[] stats =
            {
                ("Vida", traits.MaxHealth / 420f), ("Ataque", traits.Damage / 82f),
                ("Velocidad", Mathf.Max(traits.Run, traits.FlightSpeed * .48f) / 10.4f), ("Aguante", traits.Stamina / 30f)
            };
            for (int i = 0; i < stats.Length; i++)
            {
                float y = 262 + i * 17;
                GUI.Label(new Rect(50, y, 70, 14), stats[i].Name, small);
                Round(new Rect(124, y + 6, 150, 3), new Color(1, 1, 1, .14f), 1.5f);
                Round(new Rect(124, y + 6, Mathf.Max(4, 150 * Mathf.Clamp01(stats[i].Value)), 3), Paper, 1.5f);
            }
            GUI.Label(new Rect(50, 332, 70, 14), "Infancia", small);
            for (int pip = 0; pip < 3; pip++)
                Round(new Rect(124 + pip * 11, 336, 7, 7), pip <= Difficulty[menuIndex] ? Accent : new Color(1, 1, 1, .16f), 3.5f);
            GUI.Label(new Rect(164, 332, 120, 14), DifficultyNames[Difficulty[menuIndex]], small);

            bool taken = network.IsOccupied(menuIndex);
            bool ready = !taken && !network.Dedicated;
            bool over = BirthButton.Contains(pointer);
            Round(BirthButton, !ready ? new Color(1, 1, 1, .14f) : over ? Color.Lerp(Accent, Color.white, .35f) : Accent, 17);
            GUI.Label(BirthButton, taken ? "OCUPADO POR OTRO JUGADOR" : awaitingSeat ? "ESPERANDO AL ANFITRIÓN…" :
                $"NACER COMO {ShortName(sightings[menuIndex].Species).ToUpperInvariant()}", dark);
            if (!touch)
            {
                Key(new Rect(BirthButton.xMax + 10, BirthButton.y + 10, 34, 14), "ENTER", Paper);
                GUI.Label(new Rect(BirthButton.xMax + 52, BirthButton.y + 9, 200, 15), "o clic para nacer", small);
            }
            if (network.Online)
                GUI.Label(new Rect(50, 402, 420, 15), network.Status, small);

            for (int i = 0; i < creatures.Length; i++)
            {
                Rect card = CardRect(i);
                bool chosen = i == menuIndex, occupied = network.IsOccupied(i);
                bool hover = card.Contains(pointer);
                if (chosen)
                    card.y -= 8;
                Round(card, chosen ? new Color(.07f, .12f, .09f, .96f) : new Color(Panel.r, Panel.g, Panel.b, hover ? .9f : .72f), 9);
                if (chosen)
                    Round(card, Accent, 9, 1.5f);
                Color ink = occupied ? new Color(1, 1, 1, .25f) : chosen ? Accent : hover ? Paper : Quiet;
                Tint(new Rect(card.x + 10, card.y + 15, 40, 40), icons[i], ink);
                GUI.Label(new Rect(card.x + 58, card.y + 17, 106, 16), ShortName(sightings[i].Species), occupied ? faint : strong);
                GUI.Label(new Rect(card.x + 58, card.y + 35, 106, 15), occupied ? "Ocupado" : Kinds[i], occupied ? faint : small);
                if (!touch)
                    GUI.Label(new Rect(card.xMax - 15, card.y + 6, 10, 12), (i + 1).ToString(), tiny);
            }

            Pill(NetworkButton, touch ? "Multijugador" : "F2  ·  Multijugador", NetworkButton.Contains(pointer));
            if (active != null)
                Pill(BackButton, touch ? "Volver" : "Esc  ·  Volver", BackButton.Contains(pointer));
            if (!touch)
                GUI.Label(new Rect(560, 524, 360, 12), "←  →  elegir especie", centeredSmall);
        }

        private void Pill(Rect rect, string text, bool hover)
        {
            Round(rect, new Color(Panel.r, Panel.g, Panel.b, hover ? .95f : .7f), rect.height * .5f);
            GUI.Label(rect, text, centeredSmall);
        }

        private void DrawSpectator()
        {
            string text = network.Dedicated ? "Servidor dedicado  ·  ← →  cambia la vista  ·  F2  red" : network.Status;
            float width = small.CalcSize(new GUIContent(text)).x + 28;
            Round(new Rect(480 - width / 2, 22, width, 24), Panel, 12);
            GUI.Label(new Rect(480 - width / 2, 22, width, 24), text, centeredSmall);
        }

        private static Rect PanelAction(int index) => new(520, 132 + index * 32, 236, 27);

        private void DrawPanel()
        {
            bool touch = Application.isMobilePlatform;
            int seat = System.Array.IndexOf(creatures, active);
            int phase = active.Maturity < Dinosaur.Juvenile ? 0 : active.Grown ? 2 : 1;
            Round(new Rect(180, 72, 600, 396), Panel, 10);
            GUI.Label(new Rect(204, 92, 280, 12), "TU DINOSAURIO", tiny);
            Tint(new Rect(204, 106, 30, 30), icons[seat], Accent);
            GUI.Label(new Rect(242, 108, 250, 24), active.Species, heading);

            GUI.Label(new Rect(204, 148, 140, 15), $"Etapa  ·  {Stages[phase]}", body);
            GUI.Label(new Rect(344, 148, 150, 15), active.IsDead ? "sin vida" : $"con vida hace {Clock(Time.time - lifeStart)}", small);
            var growth = new Rect(204, 170, 290, 4);
            Round(growth, new Color(1, 1, 1, .14f), 2);
            Round(new Rect(growth.x, growth.y, Mathf.Max(4, growth.width * active.Maturity), growth.height), Accent, 2);
            Fill(new Rect(growth.x + growth.width * Dinosaur.Juvenile, growth.y - 2, 1, 8), new Color(1, 1, 1, .45f));
            GUI.Label(new Rect(204, 178, 100, 12), "CRÍA", tiny);
            GUI.Label(new Rect(growth.x + growth.width * Dinosaur.Juvenile + 4, 178, 100, 12), "JUVENIL", tiny);
            GUI.Label(new Rect(growth.xMax - 40, 178, 44, 12), "ADULTO", tiny);

            Meter(204, "Vida", active.HealthRatio, $"{active.Health}/{active.MaxHealth}", Accent);
            Meter(226, "Sed", active.Thirst, active.Thirst > .75f ? "urgente" : "", new Color(.47f, .76f, .95f));
            Meter(248, "Hambre", active.Hunger, active.Hunger > .75f ? "urgente" : "", new Color(.93f, .76f, .40f));
            Fill(new Rect(204, 276, 290, 1), new Color(1, 1, 1, .12f));

            GUI.Label(new Rect(204, 286, 290, 12), "CÓMO SOBREVIVIR", tiny);
            GUI.Label(new Rect(204, 302, 290, 150),
                $"{Blurbs[seat]}\n\nCrecés mientras no pases hambre ni sed. Mantené E para beber en la orilla y para " +
                (active.Traits.Nature == Dinosaur.Temperament.Herd ? "pastar." : "comer de un cadáver.") +
                (active.Traits.CanFly ? " Recién de juvenil podés volar." : "") +
                (active.Life != null || active.Herd != null ? " De adulto podés liderar a tu grupo con L." : ""), note);

            GUI.Label(new Rect(520, 92, 236, 12), "PANEL", tiny);
            GUI.Label(new Rect(520, 104, 236, 24), habitats[habitatIndex].Name, heading);
            string clock = lighting.TimeLabel.Substring(0, 1) + lighting.TimeLabel.Substring(1).ToLowerInvariant();
            (string Key, string Text)[] actions =
            {
                ("V", "Volver a nacer con otra especie"),
                ("N", network.IsGuest ? $"Hora del anfitrión  ·  {clock}" : $"Día / noche  ·  {clock}"),
                ("H", $"Clima  ·  {weather.Label.Substring(0, 1)}{weather.Label.Substring(1).ToLowerInvariant()}"),
                ("F2", network.Online ? "Partida en red" : "Multijugador"),
                ("TAB", "Volver al juego")
            };
            for (int i = 0; i < actions.Length; i++)
            {
                Rect row = PanelAction(i);
                Round(row, new Color(1, 1, 1, .07f), 6);
                float text = row.x + 10;
                if (!touch)
                {
                    float width = actions[i].Key.Length > 1 ? 10 + actions[i].Key.Length * 5 : 13;
                    Key(new Rect(row.x + 9, row.y + 7, width, 13), actions[i].Key, Accent);
                    text = row.x + 16 + width;
                }
                GUI.Label(new Rect(text, row.y + 6, row.xMax - text - 6, 16), actions[i].Text, body);
            }
            if (touch)
                return;
            string[] controls =
            {
                "WASD  mover   ·   Shift  correr", "Clic / F  atacar   ·   G / clic der.  defensa",
                "Ctrl / C  agacharse   ·   Espacio  vuelo   ·   R / C  altura", "E  beber, pastar o comer",
                "L  liderar al grupo (adulto)   ·   M  mapa"
            };
            GUI.Label(new Rect(520, 306, 236, 12), "CONTROLES", tiny);
            for (int i = 0; i < controls.Length; i++)
                GUI.Label(new Rect(520, 322 + i * 19, 240, 16), controls[i], small);
        }

        private void Meter(float y, string name, float level, string value, Color tone)
        {
            GUI.Label(new Rect(204, y, 60, 15), name, small);
            Round(new Rect(264, y + 6, 160, 4), new Color(1, 1, 1, .14f), 2);
            if (level > .01f)
                Round(new Rect(264, y + 6, Mathf.Max(4, 160 * Mathf.Clamp01(level)), 4), tone, 2);
            GUI.Label(new Rect(434, y, 64, 15), value, small);
        }

        private void InitializeStyles()
        {
            if (small != null)
                return;

            small = Style(12, Quiet, FontStyle.Normal);
            faint = Style(12, new Color(1, 1, 1, .34f), FontStyle.Normal);
            strong = Style(12, Color.white, FontStyle.Bold);
            body = Style(12, Paper, FontStyle.Normal);
            tiny = Style(9, Accent, FontStyle.Bold);
            heading = Style(17, Paper, FontStyle.Bold);
            display = Style(25, Color.white, FontStyle.Bold);
            title = Style(38, Paper, FontStyle.Bold);
            note = Style(11, Quiet, FontStyle.Normal);
            note.wordWrap = true;
            centered = Style(11, Paper, FontStyle.Bold);
            centered.alignment = TextAnchor.MiddleCenter;
            centeredBody = Style(12, Paper, FontStyle.Normal);
            centeredBody.alignment = TextAnchor.MiddleCenter;
            centeredSmall = Style(11, Quiet, FontStyle.Normal);
            centeredSmall.alignment = TextAnchor.MiddleCenter;
            tagName = Style(12, Color.white, FontStyle.Bold);
            tagName.alignment = TextAnchor.MiddleCenter;
            dark = Style(11, new Color(.05f, .09f, .05f), FontStyle.Bold);
            dark.alignment = TextAnchor.MiddleCenter;
            keycap = Style(8, Quiet, FontStyle.Bold);
            keycap.alignment = TextAnchor.MiddleCenter;
            icons = HudIcons.Build();
            fadeRight = Gradient(64, 1);
            fadeDown = Gradient(1, 64);
            cornerShade = new Texture2D(64, 64, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave
            };
            var shade = new Color32[64 * 64];
            for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                // Mancha opaca en el centro y transparente hacia los bordes; se dibuja centrada en una esquina de la pantalla.
                float fade = Mathf.Clamp01(1 - new Vector2(x / 31.5f - 1, y / 31.5f - 1).magnitude);
                shade[y * 64 + x] = new Color32(255, 255, 255, (byte)(255 * fade * fade * (3 - 2 * fade)));
            }
            cornerShade.SetPixels32(shade);
            cornerShade.Apply(false, true);
        }

        // Degradé de opaco a transparente: hacia la derecha o, en vertical, hacia arriba.
        private static Texture2D Gradient(int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave
            };
            int length = Mathf.Max(width, height);
            var pixels = new Color32[length];
            for (int i = 0; i < length; i++)
            {
                float fade = 1 - i / (length - 1f);
                pixels[i] = new Color32(255, 255, 255, (byte)(255 * fade * fade * (3 - 2 * fade)));
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static GUIStyle Style(int size, Color color, FontStyle weight)
        {
            GUIStyle style = new(GUI.skin.label) { fontSize = size, fontStyle = weight, padding = new RectOffset() };
            style.normal.textColor = color;
            return style;
        }

        private static void Fill(Rect rect, Color color) => Round(rect, color, 0);

        private static void Round(Rect rect, Color color, float radius, float outline = 0) =>
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, color, outline, radius);

        private static void Tint(Rect rect, Texture icon, Color color)
        {
            if (icon != null)
                GUI.DrawTexture(rect, icon, ScaleMode.StretchToFill, true, 0, color, 0, 0);
        }

        // Texto sin recuadro: una sombra corrida un píxel lo mantiene legible sobre el paisaje.
        private static void Ink(Rect rect, string text, GUIStyle style)
        {
            Color color = style.normal.textColor;
            style.normal.textColor = new Color(0, 0, 0, .62f * color.a);
            GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), text, style);
            style.normal.textColor = color;
            GUI.Label(rect, text, style);
        }

        private void Key(Rect rect, string text) => Key(rect, text, Quiet);

        private void Key(Rect rect, string text, Color color)
        {
            Round(rect, new Color(color.r, color.g, color.b, .75f), 3, 1);
            Color ink = keycap.normal.textColor;
            keycap.normal.textColor = color;
            GUI.Label(rect, text, keycap);
            keycap.normal.textColor = ink;
        }

        private static void DrawSun(Vector2 center, bool night)
        {
            if (night)
            {
                Round(new Rect(center.x - 5.5f, center.y - 5.5f, 11, 11), Paper, 5.5f);
                Round(new Rect(center.x - 2, center.y - 7.5f, 10, 10), new Color(.07f, .09f, .08f, 1), 5);
                return;
            }
            Round(new Rect(center.x - 3.6f, center.y - 3.6f, 7.2f, 7.2f), Paper, 3.6f, 1.3f);
            Matrix4x4 previous = GUI.matrix;
            for (int ray = 0; ray < 8; ray++)
            {
                ExpeditionMap.Turn(45, center);
                Round(new Rect(center.x - .65f, center.y - 8.2f, 1.3f, 2.7f), Paper, .6f);
            }
            GUI.matrix = previous;
        }

        private static void DrawHerd(Vector2 center, Color color)
        {
            for (int member = -1; member <= 1; member++)
            {
                float size = member == 0 ? 1 : .78f;
                var place = new Vector2(center.x + member * 5.2f, center.y + (member == 0 ? 0 : 1.2f));
                Round(new Rect(place.x - 1.9f * size, place.y - 4.6f * size, 3.8f * size, 3.8f * size), color, 1.9f * size);
                Round(new Rect(place.x - 3 * size, place.y, 6 * size, 4.4f * size), color, 2.6f * size);
            }
        }
    }
}
