using UnityEngine;

namespace JurassicPix
{
    [DefaultExecutionOrder(-60)]
    public sealed partial class Expedition : MonoBehaviour
    {
        [System.Serializable]
        public class Habitat
        {
            public string Name;
            public string Route;
            public Color Fog;
            public Color Light;
            public Vector2 FogRange;
        }

        [SerializeField] private Camera eyes;
        [SerializeField] private ForestLighting lighting;
        [SerializeField] private ExpeditionNetwork network;
        [SerializeField] private ValleyWeather weather;
        [SerializeField] private GroundTremor tremor;
        [SerializeField] private FlightWind flightWind;
        [SerializeField] private AudioSource interfaceSound;
        [SerializeField] private Texture2D map;
        [SerializeField] private Habitat[] habitats;
        [SerializeField] private Sighting[] sightings;
        [SerializeField] private Sighting[] wildlife;

        private PsxView psxView;
        private Dinosaur[] creatures;
        private Dinosaur[] population;
        private DinosaurAudio[] voices;
        private Dinosaur active;
        private CoopMenu coopMenu;
        private int habitatIndex;
        private int lastCreature;
        private float orbitYaw, orbitPitch = 16;
        private float cameraArm;
        private Vector3 cameraPivot;
        private Color fog, light;
        private Vector2 fogRange;
        private int moveFinger = -1;
        private int lookFinger = -1;
        private Vector2 moveOrigin, movePosition;
        private bool panel, attackRequested, flightRequested;
        private int mapMode;
        private bool leading;
        private float touchLift;
        private bool touchGuard, touchDeed;
        private DinosaurDeed chance;
        private Vector3 waypoint;
        private bool hasWaypoint;

        // Supervivencia: cuánto lleva viva la criatura del jugador y en qué etapa está.
        private bool wasDead;
        private int stage = -1;
        private float lifeStart, lifeEnd;
        private readonly FieldNotices notices = new();

        private static readonly Rect PanelButton = new(868, 20, 30, 26);
        private static readonly Rect ScanButton = new(795, 416, 143, 47);
        private static readonly Rect FlightButton = new(795, 359, 143, 47);
        private static readonly Rect TimeButton = new(906, 20, 30, 26);
        private static readonly Rect MapButton = new(24, 388, 114, 114);
        private static readonly Rect LeadButton = new(24, 50, 300, 20);
        private static readonly Rect GuardButton = new(645, 416, 142, 47);
        private static readonly Rect DeedButton = new(645, 359, 142, 47);
        private static readonly Rect FlightUp = new(795, 309, 68, 39);
        private static readonly Rect FlightDown = new(870, 309, 68, 39);
        private static readonly Rect DeathCard = new(290, 204, 380, 104);
        private static readonly string[] Stages = { "cría", "juvenil", "adulto" };

        private bool Playing => active != null && !menu && !panel && !coopMenu.IsOpen;
        private IPlayerControl PlayerControl => network.Online ? network : active;

        private void Awake()
        {
            psxView = eyes.GetComponent<PsxView>();
            useGUILayout = false;
            Habitat first = habitats[0];
            fog = first.Fog;
            light = first.Light;
            fogRange = first.FogRange;
            lighting.Apply(fog, light, fogRange, 0);
            Dinosaur.Focus = eyes.transform.position;
            if (!Application.isMobilePlatform && !Application.isEditor &&
                System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-screen-fullscreen") < 0)
                SetFullscreen(true);
        }

        private void Start()
        {
            creatures = System.Array.ConvertAll(sightings, sighting => sighting.GetComponent<Dinosaur>());
            var census = new Sighting[sightings.Length + wildlife.Length];
            sightings.CopyTo(census, 0);
            wildlife.CopyTo(census, sightings.Length);
            population = System.Array.ConvertAll(census, sighting => sighting.GetComponent<Dinosaur>());
            voices = System.Array.ConvertAll(creatures, creature => creature.GetComponent<DinosaurAudio>());
            var herds = new System.Collections.Generic.HashSet<TriceratopsHerd>();
            var groups = new System.Collections.Generic.HashSet<WildlifeHerd>();
            IslandSites.Communities.Clear();
            IslandSites.Herds.Clear();
            foreach (Dinosaur creature in population)
            {
                creature.SetNeighbors(population);
                creature.Motion.Observer = eyes.transform;
                creature.GetComponent<DinosaurAudio>().SetListener(eyes.transform);
                if (creature.Life != null)
                    herds.Add(creature.Life.Community);
                if (creature.Herd != null)
                    groups.Add(creature.Herd);
            }
            foreach (TriceratopsHerd herd in herds)
                herd.Bind(population);
            foreach (WildlifeHerd group in groups)
                group.Bind(population);
            tremor.Bind(population);
            network.Initialize(population, creatures.Length, lighting, weather);
            network.SeatChanged += OnSeatChanged;
            coopMenu = new CoopMenu(network);
            network.AutoStart();
            // La partida empieza en el menú de especies; un servidor dedicado solo mira.
            menu = !network.Dedicated;
            menuCut = true;
            RefreshPointer();
        }

        private void Update()
        {
            bool touch = Application.isMobilePlatform;
            chance = Playing ? active.Opportunity() : DinosaurDeed.None;
            if (!touch)
                ReadDesktop();
            else if (!coopMenu.IsOpen)
                ReadTouch();
            if (awaitingSeat)
            {
                // En red el lugar lo otorga el anfitrión: se insiste hasta tenerlo o hasta que otro lo ocupe.
                if (network.LocalSeat == menuIndex)
                    Begin();
                else if (!network.Online || network.IsOccupied(menuIndex))
                    awaitingSeat = false;
                else if (Time.unscaledTime >= nextRequest)
                {
                    nextRequest = Time.unscaledTime + 1;
                    network.Select(menuIndex);
                }
            }

            if (active != null)
            {
                if (Playing)
                {
                    Vector2 movement = touch
                        ? Vector2.ClampMagnitude((movePosition - moveOrigin) / (Screen.height * 0.13f), 1)
                        : Vector2.ClampMagnitude(new Vector2(
                            (Held(KeyCode.D) || Held(KeyCode.RightArrow) ? 1 : 0) - (Held(KeyCode.A) || Held(KeyCode.LeftArrow) ? 1 : 0),
                            (Held(KeyCode.W) || Held(KeyCode.UpArrow) ? 1 : 0) - (Held(KeyCode.S) || Held(KeyCode.DownArrow) ? 1 : 0)), 1);
                    bool running = touch ? movement.magnitude > 0.91f : Held(KeyCode.LeftShift) || Held(KeyCode.RightShift);
                    DriveCreature(movement, running, touch);
                }
                else
                    SendInput(default);
                TrackLife();
            }

            attackRequested = flightRequested = false;
            UpdateAtmosphere();
        }

        private void LateUpdate()
        {
            if (menu || active == null)
                Showcase();
            else
                FollowCreature(false);
            Vector3 vibration = tremor.Sample(eyes.transform.position, Time.deltaTime);
            eyes.transform.position += eyes.transform.TransformDirection(vibration);
            eyes.transform.rotation *= Quaternion.Euler(-vibration.y * 3.3f, 0, vibration.x * 5.7f);
            flightWind.Mix(menu ? null : active, eyes.transform.right, Time.deltaTime);
            Dinosaur.Focus = eyes.transform.position;
            Dinosaur.Sight = lighting.Sight;
            Dinosaur.Throttle = !network.Online;
        }

        private void OnDestroy()
        {
            if (network != null)
                network.SeatChanged -= OnSeatChanged;
        }

        private static bool Held(KeyCode key) => Input.GetKey(key);

        private static void LockPointer(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void RefreshPointer()
        {
            if (!Application.isMobilePlatform)
                LockPointer(Playing);
        }

        private static void SetFullscreen(bool fullscreen)
        {
            if (Application.isEditor)
                return;

            int width = Display.main.systemWidth;
            int height = Display.main.systemHeight;
            if (fullscreen)
                Screen.SetResolution(width, height, FullScreenMode.FullScreenWindow);
            else
            {
                int windowWidth = Mathf.Min(1280, Mathf.RoundToInt(Mathf.Min(width, height * 16f / 9) * .88f));
                Screen.SetResolution(windowWidth, Mathf.RoundToInt(windowWidth * 9f / 16), FullScreenMode.Windowed);
            }
        }

        private void ReadDesktop()
        {
            if (Input.GetKeyDown(KeyCode.F11))
                SetFullscreen(!Screen.fullScreen);
            if (Input.GetKeyDown(KeyCode.F2) || coopMenu.IsOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                ToggleCoopMenu();
                return;
            }
            if (coopMenu.IsOpen)
                return;
            if (menu || active == null)
            {
                ReadMenuKeys();
                return;
            }
            if (Input.GetKeyDown(KeyCode.N))
                network.RequestDayNight();
            if (Input.GetKeyDown(KeyCode.H))
                network.RequestWeather();
            if (Input.GetKeyDown(KeyCode.M))
                CycleMap();
            if (Input.GetKeyDown(KeyCode.L) && !panel)
                SetLeading(!leading);
            if (Input.GetKeyDown(KeyCode.V))
            {
                OpenMenu();
                return;
            }
            if (Input.GetKeyDown(KeyCode.Tab))
                TogglePanel();
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (panel)
                    TogglePanel();
                else
                    LockPointer(false);
            }
            bool locked = Cursor.lockState == CursorLockMode.Locked;
            if (!locked && Input.GetMouseButtonDown(0))
            {
                Vector3 mouse = Input.mousePosition;
                ReadHudPress(new Vector2(mouse.x, Screen.height - mouse.y));
                RefreshPointer();
                return;
            }
            if (panel)
                return;
            if (locked)
                Look(new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 2.2f);

            attackRequested |= Input.GetKeyDown(KeyCode.F) || (locked && Input.GetMouseButtonDown(0));
            flightRequested |= Input.GetKeyDown(KeyCode.Space);
        }

        private void ReadTouch()
        {
            touchLift = 0;
            touchGuard = touchDeed = false;
            bool busy = panel || menu || active == null;
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                Vector2 point = new(touch.position.x, Screen.height - touch.position.y);
                bool pressed = touch.phase is not (TouchPhase.Ended or TouchPhase.Canceled);
                if (!busy && pressed && chance != DinosaurDeed.None && ControlRect(DeedButton).Contains(point))
                {
                    touchDeed = true;
                    continue;
                }
                if (!busy && pressed && ControlRect(GuardButton).Contains(point))
                {
                    touchGuard = true;
                    continue;
                }
                if (!busy && pressed && active.IsFlying)
                {
                    if (ControlRect(FlightUp).Contains(point))
                    {
                        touchLift += 1;
                        continue;
                    }
                    if (ControlRect(FlightDown).Contains(point))
                    {
                        touchLift -= 1;
                        continue;
                    }
                }
                if (touch.phase == TouchPhase.Began)
                {
                    Rect viewport = PsxView.Viewport;
                    if (!viewport.Contains(point))
                        continue;

                    if (ReadHudPress(point))
                    {
                        if (coopMenu.IsOpen)
                            return;
                        busy = panel || menu || active == null;
                        continue;
                    }
                    if (busy)
                        continue;
                    if (ControlRect(ScanButton).Contains(point))
                        attackRequested = true;
                    else if (CanTakeOff && ControlRect(FlightButton).Contains(point))
                        flightRequested = true;
                    else if (point.x < viewport.x + viewport.width * 0.45f && moveFinger < 0)
                    {
                        moveFinger = touch.fingerId;
                        moveOrigin = movePosition = touch.position;
                    }
                    else if (lookFinger < 0)
                        lookFinger = touch.fingerId;
                }
                if (!pressed)
                {
                    if (touch.fingerId == moveFinger)
                    {
                        moveFinger = -1;
                        movePosition = moveOrigin;
                    }
                    if (touch.fingerId == lookFinger)
                        lookFinger = -1;
                    continue;
                }
                if (touch.fingerId == moveFinger)
                    movePosition = touch.position;
                if (touch.fingerId == lookFinger && !busy)
                    Look(touch.deltaPosition * (145f / Screen.height));
            }
        }

        private void Look(Vector2 movement)
        {
            orbitYaw = Mathf.Repeat(orbitYaw + movement.x, 360);
            orbitPitch = Mathf.Clamp(orbitPitch - movement.y, -13, 68);
        }

        private bool ReadHudPress(Vector2 point)
        {
            if (menu || active == null)
                return ReadMenuPress(point);
            if (Application.isMobilePlatform && ControlRect(PanelButton).Contains(point))
            {
                TogglePanel();
                return true;
            }
            if (panel)
            {
                for (int action = 0; action < 5; action++)
                {
                    if (!ControlRect(PanelAction(action)).Contains(point))
                        continue;
                    if (action == 1)
                        network.RequestDayNight();
                    else if (action == 2)
                        network.RequestWeather();
                    else
                    {
                        TogglePanel();
                        if (action == 0)
                            OpenMenu();
                        else if (action == 3)
                            ToggleCoopMenu();
                    }
                    return true;
                }
                return false;
            }
            if (active.IsDead && ControlRect(DeathCard).Contains(point))
            {
                OpenMenu();
                return true;
            }
            if (mapMode < 3)
            {
                // El botón de la lupa cambia la vista; tocar el mapa marca o quita un punto.
                if (ControlRect(MapZoomButton()).Contains(point))
                {
                    CycleMap();
                    return true;
                }
                if (ControlRect(MapArea()).Contains(point))
                {
                    float k = PsxView.Viewport.width / 960f;
                    Vector2 logical = (point - PsxView.Viewport.position) / k;
                    if (hasWaypoint && (ExpeditionMap.PointAt(waypoint) - logical).magnitude < 11)
                        hasWaypoint = false;
                    else
                    {
                        waypoint = ExpeditionMap.Unproject(logical);
                        hasWaypoint = true;
                    }
                    Page();
                    return true;
                }
            }
            if (HasGroup && active.Grown && ControlRect(LeadButton).Contains(point))
            {
                SetLeading(!leading);
                return true;
            }
            if (ControlRect(TimeButton).Contains(point))
            {
                network.RequestDayNight();
                return true;
            }
            return false;
        }

        // Elegir una especie es nacer: la criatura arranca como cría junto a su grupo.
        private void ChooseCreature(int index)
        {
            if (active != null && active != creatures[index])
                active.SetControl(false);
            creatures[index].SetControl(true);
            creatures[index].BeBorn();
            FocusCreature(index);
        }

        private void OnSeatChanged(int seat)
        {
            if (seat >= 0)
            {
                FocusCreature(seat);
                return;
            }
            if (active != null)
                voices[lastCreature].SetFocus(false);
            active = null;
            leading = false;
            if (!network.Online)
                OpenMenu();
        }

        private bool HasGroup => active != null && !active.IsDead && (active.Life != null || active.Herd != null);

        private bool CanTakeOff => active != null && active.Traits.CanFly;

        private void SetLeading(bool lead)
        {
            lead &= HasGroup && active.Grown;
            if (lead == leading)
                return;
            leading = lead;
            if (active == null)
                return;
            PlayerControl.SetLeading(lead);
        }

        private void FocusCreature(int index)
        {
            Dinosaur selection = creatures[index];
            if (active == selection)
                return;
            if (active != null)
                voices[lastCreature].SetFocus(false);
            leading = false;
            lastCreature = index;
            active = selection;
            voices[index].SetFocus(true);
            orbitYaw = active.transform.eulerAngles.y - 24;
            orbitPitch = active.Traits.CanFly ? 11 : 17.5f;
            lifeStart = Time.time;
            stage = -1;
            wasDead = active.IsDead;
            if (!menu)
                FollowCreature(true);
        }

        private void TrackLife()
        {
            bool dead = active.IsDead;
            int now = active.Maturity < Dinosaur.Juvenile ? 0 : active.Grown ? 2 : 1;
            if (dead && !wasDead)
            {
                notices.Clear();
                lifeEnd = Time.time;
                leading = false;
            }
            else if (!dead && (wasDead || stage > now))
            {
                lifeStart = Time.time;
                Notify("Naciste de nuevo. Buscá agua, comida y la compañía de los tuyos.");
            }
            else if (!dead && stage >= 0 && now > stage)
            {
                Notify(now == 2
                    ? HasGroup ? "Ya sos adulto: podés liderar a tu grupo con L." : "Ya sos adulto: el territorio es tuyo."
                    : "Ya sos juvenil: más fuerte y más veloz.");
            }
            wasDead = dead;
            stage = now;
        }

        private void Notify(string text) => notices.Enqueue(text);

        private void DriveCreature(Vector2 movement, bool running, bool touch)
        {
            if (active.IsDead)
            {
                SendInput(default);
                return;
            }
            Quaternion direction = Quaternion.Euler(0, orbitYaw, 0);
            float lift = touch ? Mathf.Clamp(touchLift, -1, 1) : (Held(KeyCode.R) ? 1 : 0) - (Held(KeyCode.C) ? 1 : 0);
            bool guard = touch ? touchGuard : Held(KeyCode.G) ||
                Cursor.lockState == CursorLockMode.Locked && Input.GetMouseButton(1);
            bool crouch = !touch && (Held(KeyCode.LeftControl) || !active.IsFlying && Held(KeyCode.C));
            SendInput(new DinosaurInput(direction * new Vector3(movement.x, 0, movement.y), running && !crouch, lift, guard, crouch,
                touch ? touchDeed : Held(KeyCode.E)));
            IPlayerControl control = PlayerControl;
            if (flightRequested)
                control.ToggleFlight();
            if (attackRequested && !guard)
                control.Attack();
        }

        private void SendInput(DinosaurInput input) => PlayerControl.SetInput(input);

        private void ToggleCoopMenu()
        {
            coopMenu.Toggle();
            moveFinger = lookFinger = -1;
            movePosition = moveOrigin;
            RefreshPointer();
        }

        private void TogglePanel()
        {
            panel = !panel;
            Page();
            RefreshPointer();
        }

        private void Page() => interfaceSound.PlayOneShot(interfaceSound.clip, .9f);

        private void FollowCreature(bool cut)
        {
            float grown = Mathf.InverseLerp(.36f, 1, active.Size);
            Vector3 target = active.transform.position +
                Vector3.up * (active.Traits.CameraHeight * Mathf.Lerp(active.Size * 1.5f, 1, grown));
            // De cría la cámara se acerca, pero nunca tanto como para que el cuerpo tape la vista.
            float arm = Mathf.Max(active.Traits.CameraDistance * Mathf.Lerp(.46f, 1, grown), active.Length * 1.6f * (1 - grown));
            PlaceCamera(target, Quaternion.Euler(orbitPitch, orbitYaw, 0), arm, 0, cut, 35);
        }

        private void PlaceCamera(Vector3 target, Quaternion rotation, float arm, float shift, bool cut, float pivotSpeed = 15)
        {
            float smoothing = cut ? 1 : 1 - Mathf.Exp(-Time.deltaTime * 15);
            cameraPivot = cut ? target : Vector3.Lerp(cameraPivot, target, 1 - Mathf.Exp(-Time.deltaTime * pivotSpeed));
            Vector3 back = rotation * Vector3.back;
            if (Physics.SphereCast(cameraPivot, 0.28f, back, out RaycastHit hit, arm, Dinosaur.Scenery))
                arm = Mathf.Max(0.85f, hit.distance - 0.20f);
            cameraArm = cut || arm < cameraArm ? arm : Mathf.Lerp(cameraArm, arm, smoothing);
            eyes.transform.SetPositionAndRotation(cameraPivot + back * cameraArm - rotation * Vector3.right * (cameraArm * shift), rotation);
        }

        private void UpdateAtmosphere()
        {
            Dinosaur subject = active != null && !menu ? active : creatures[menuIndex];
            Vector3 place = subject.transform.position;
            habitatIndex = IslandLayout.RegionAt(new Vector2(place.x, place.z));
            Habitat current = habitats[habitatIndex];
            float blend = 1 - Mathf.Exp(-Time.deltaTime * 1.65f);
            float air = 1;
            if (subject.IsFlying)
            {
                float water = IslandLayout.SurfaceLevel(new Vector2(place.x, place.z));
                float ground = Physics.Raycast(place + Vector3.up * 10, Vector3.down, out RaycastHit hit, 230,
                    1 << Dinosaur.TerrainLayer, QueryTriggerInteraction.Ignore) ? Mathf.Max(water, hit.point.y) : water;
                air = Mathf.Lerp(1, 2.15f, Mathf.InverseLerp(8, 65, place.y - ground));
            }
            fog = Color.Lerp(fog, current.Fog, blend);
            light = Color.Lerp(light, current.Light, blend);
            fogRange = Vector2.Lerp(fogRange, current.FogRange * air, blend);
            lighting.Apply(fog, light, fogRange, Time.deltaTime, !network.IsGuest, weather.Rain, weather.Flash);
        }

        private void OnGUI()
        {
            if (coopMenu == null)
                return;

            bool repaint = Event.current.type == EventType.Repaint;
            if (repaint)
                psxView.DrawViewport();
            Rect view = PsxView.Viewport;
            float scale = view.width / 960;
            Matrix4x4 original = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(view.position, Quaternion.identity, new Vector3(scale, scale, 1));
            bool wasOpen = coopMenu.IsOpen;
            if (repaint)
            {
                InitializeStyles();
                if (menu)
                    DrawMenu();
                else if (active == null)
                    DrawSpectator();
                else if (panel)
                    DrawPanel();
                else
                    DrawFieldHud();
            }
            if (wasOpen)
                coopMenu.Draw(menu ? menuIndex : lastCreature);
            GUI.matrix = original;
            if (wasOpen && !coopMenu.IsOpen)
                RefreshPointer();
        }

        private void DrawFieldHud()
        {
            bool touch = Application.isMobilePlatform;
            // Una sombra suave en las esquinas alcanza para leer el texto sin encerrarlo en recuadros.
            Tint(new Rect(-440, -140, 880, 280), cornerShade, new Color(0, 0, 0, .6f));
            Tint(new Rect(830, -76, 260, 152), cornerShade, new Color(0, 0, 0, .5f));
            DrawVitals();
            DrawSun(TimeButton.center, lighting.NightBlend > .5f);
            if (touch)
            {
                for (int line = 0; line < 3; line++)
                    Fill(new Rect(PanelButton.x + 8, PanelButton.y + 8 + line * 5, 14, 1.6f), Paper);
            }
            if (mapMode < 3)
            {
                Rect area = MapArea();
                ExpeditionMap.Draw(map, area, population, active, transform, mapMode,
                    eyes.transform.eulerAngles.y, icons, network, hasWaypoint ? waypoint : (Vector3?)null);
                Round(MapZoomButton(), Panel, 8);
                GUI.Label(MapZoomButton(), mapMode < 2 ? "+" : "–", tagName);
            }
            if (active.Life != null)
            {
                active.Life.Community.CountYoung(out int calves, out int juveniles, out int eggs);
                DrawHerd(new Vector2(MapButton.xMax + 16, 496), Quiet);
                Ink(new Rect(MapButton.xMax + 30, 489, 200, 14), $"{calves} crías  ·  {juveniles} juv.  ·  {eggs} huevos", small);
            }
            if (active.IsDead)
                DrawDeath();
            else if (active.AttackActive)
            {
                Round(new Rect(420, 462, 120, 22), Panel, 11);
                GUI.Label(new Rect(420, 462, 120, 22), active.AttackConnected ? "IMPACTO" : active.Traits.AttackName.ToUpperInvariant(), centered);
            }
            else if (active.Deed != DinosaurDeed.None)
            {
                Round(new Rect(420, 462, 120, 22), Panel, 11);
                GUI.Label(new Rect(420, 462, 120, 22), active.Deed switch
                {
                    DinosaurDeed.Drink => "BEBIENDO", DinosaurDeed.Graze => "PASTANDO", _ => "COMIENDO"
                }, centered);
            }
            else if (!touch && chance != DinosaurDeed.None &&
                (chance != DinosaurDeed.Graze || active.Hunger > .5f || active.HealthRatio < 1))
            {
                Round(new Rect(438, 462, 84, 22), Panel, 11);
                Key(new Rect(450, 467, 13, 13), "E", Accent);
                GUI.Label(new Rect(470, 465, 48, 15), DeedName(chance), small);
            }

            if (!active.IsDead && notices.Read(Time.unscaledTime, out float remaining) is string notice)
            {
                float width = body.CalcSize(new GUIContent(notice)).x + 28;
                float fade = Mathf.Clamp01(remaining / .6f);
                Round(new Rect(480 - width / 2, 84, width, 24), new Color(Panel.r, Panel.g, Panel.b, Panel.a * fade), 12);
                GUI.color = new Color(1, 1, 1, fade);
                GUI.Label(new Rect(480 - width / 2, 84, width, 24), notice, centeredBody);
                GUI.color = Color.white;
            }

            DrawPlayerNames();
            DrawWaypoint();
            if (touch)
                DrawTouchControls();
            else
            {
                Key(new Rect(848, 499, 25, 13), "TAB");
                Ink(new Rect(878, 498, 40, 15), "Panel", small);
                Key(new Rect(916, 499, 13, 13), "M");
            }
        }

        private void CycleMap() => mapMode = (mapMode + 1) % 4;

        // Modos del mapa: cercano y medio en la esquina, isla completa en grande y oculto.
        private Rect MapArea() => mapMode == 2 ? new Rect(325, 105, 310, 310) : MapButton;
        private Rect MapZoomButton() => new(MapArea().xMax - 19, MapArea().y + 3, 16, 16);

        // La marca del mapa se dibuja también sobre el mundo, con la distancia al paso.
        private void DrawWaypoint()
        {
            if (!hasWaypoint)
                return;
            Rect view = PsxView.Viewport;
            Vector3 point = eyes.WorldToScreenPoint(waypoint + Vector3.up * 1.4f);
            if (point.z <= 0)
                return;
            float x = (point.x - view.x) * 960 / view.width;
            float y = (Screen.height - point.y - view.y) * 540 / view.height;
            if (x < 8 || x > 952 || y < 8 || y > 532)
                return;
            Vector3 flat = active != null ? active.transform.position : eyes.transform.position;
            float distance = new Vector2(waypoint.x - flat.x, waypoint.z - flat.z).magnitude;
            Ink(new Rect(x - 60, y - 11, 120, 16), $"◆ {distance:0} m", tagName);
        }

        // Cada jugador conectado lleva su nombre flotando sobre su dinosaurio.
        private void DrawPlayerNames()
        {
            if (!network.Online)
                return;
            Rect view = PsxView.Viewport;
            for (int i = 0; i < creatures.Length; i++)
            {
                string name = network.SeatName(i);
                if (name == null || !creatures[i].IsPresent)
                    continue;
                Vector3 point = eyes.WorldToScreenPoint(creatures[i].transform.position +
                    Vector3.up * (creatures[i].Height + .9f));
                if (point.z <= 0 || point.x < view.x || point.x > view.xMax || point.y < view.y || point.y > view.yMax)
                    continue;
                float x = (point.x - view.x) * 960 / view.width;
                float y = (Screen.height - point.y - view.y) * 540 / view.height;
                Ink(new Rect(x - 60, y - 11, 120, 16), name, tagName);
            }
        }

        // Arriba a la izquierda queda solo lo vital: silueta, nombre con etapa y cuatro líneas finas sin recuadro.
        private void DrawVitals()
        {
            int seat = System.Array.IndexOf(creatures, active);
            Tint(new Rect(25, 21, 26, 26), icons[seat], new Color(0, 0, 0, .5f));
            Tint(new Rect(24, 20, 26, 26), icons[seat], Accent);
            string name = ShortName(active.Species);
            Ink(new Rect(58, 18, 200, 16), name, strong);
            float after = 58 + strong.CalcSize(new GUIContent(name)).x + 7;
            int phase = active.Maturity < Dinosaur.Juvenile ? 0 : active.Grown ? 2 : 1;
            Ink(new Rect(after, 18, 160, 16), phase == 2 ? Stages[2] : $"{Stages[phase]}  {active.Maturity * 100:0} %", small);

            var bar = new Rect(58, 37, 132, 4);
            Round(bar, new Color(0, 0, 0, .42f), 2);
            Color health = active.HurtRecently ? Warn : Accent;
            if (active.Health > 0)
                Round(new Rect(bar.x, bar.y, Mathf.Max(4, bar.width * active.HealthRatio), bar.height), health, 2);
            var breath = new Rect(58, 43, 132, 2);
            Round(breath, new Color(0, 0, 0, .36f), 1);
            if (active.Stamina > .01f)
                Round(new Rect(breath.x, breath.y, Mathf.Max(2, breath.width * active.Stamina), breath.height),
                    active.Winded ? Warn : Paper, 1);
            Need(new Rect(198, 37, 30, 3), "SED", active.Thirst, new Color(.47f, .76f, .95f));
            Need(new Rect(198, 43, 30, 3), "HAMBRE", active.Hunger, new Color(.93f, .76f, .40f));

            string alert = active.IsDead ? null : active.Drowning ? "Te estás ahogando: salí del agua" :
                active.Toppled ? "Derribado" : active.Thirst >= 1 ? "Necesitás agua: la sed te debilita" :
                active.Hunger >= 1 ? "Necesitás comer: el hambre te debilita" : active.Winded ? "Sin aliento" : null;
            if (alert != null)
            {
                Color ink = small.normal.textColor;
                small.normal.textColor = Warn;
                Ink(new Rect(58, 51, 320, 15), alert, small);
                small.normal.textColor = ink;
                return;
            }
            string objective = Objective();
            if (objective != null)
                Ink(new Rect(58, 51, 420, 15), objective, small);
        }

        private void DrawDeath()
        {
            Round(DeathCard, Panel, 12);
            Color ink = heading.normal.textColor;
            heading.normal.textColor = Warn;
            heading.alignment = TextAnchor.MiddleCenter;
            GUI.Label(new Rect(DeathCard.x, DeathCard.y + 12, DeathCard.width, 26), "No sobreviviste", heading);
            heading.alignment = TextAnchor.UpperLeft;
            heading.normal.textColor = ink;
            GUI.Label(new Rect(DeathCard.x, DeathCard.y + 42, DeathCard.width, 18),
                $"Viviste {Clock(lifeEnd - lifeStart)} como {ShortName(active.Species)} {Stages[Mathf.Max(0, stage)]}", centeredBody);
            int wait = Mathf.Max(0, Mathf.CeilToInt(12 - (Time.time - lifeEnd)));
            GUI.Label(new Rect(DeathCard.x, DeathCard.y + 62, DeathCard.width, 16), $"Renacés como cría en {wait} s", centeredSmall);
            if (Application.isMobilePlatform)
                GUI.Label(new Rect(DeathCard.x, DeathCard.y + 80, DeathCard.width, 16), "Tocá para elegir otra especie", centeredSmall);
            else
            {
                Key(new Rect(DeathCard.center.x - 74, DeathCard.y + 82, 13, 13), "V", Accent);
                GUI.Label(new Rect(DeathCard.center.x - 56, DeathCard.y + 80, 160, 16), "elegir otra especie", small);
            }
        }

        private static string Clock(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{total / 60}:{total % 60:00}";
        }

        private void DrawTouchControls()
        {
            Vector2 stick = moveFinger >= 0
                ? new Vector2((moveOrigin.x - PsxView.Viewport.x) * 960 / PsxView.Viewport.width,
                    (Screen.height - moveOrigin.y - PsxView.Viewport.y) * 540 / PsxView.Viewport.height)
                : new Vector2(190, 418);
            Round(new Rect(stick.x - 33, stick.y - 33, 66, 66), Panel, 33);
            Vector2 offset = Vector2.ClampMagnitude((movePosition - moveOrigin) / Screen.height * 250, 22);
            Round(new Rect(stick.x - 9 + offset.x, stick.y - 9 - offset.y, 18, 18), Accent, 9);
            Round(ScanButton, Panel, 10);
            GUI.Label(ScanButton, "ATACAR", centered);
            Round(GuardButton, active.Guarding ? new Color(.15f, .27f, .20f, .95f) : Panel, 10);
            GUI.Label(GuardButton, "DEFENSA", centered);
            if (chance != DinosaurDeed.None)
            {
                Round(DeedButton, active.Deed != DinosaurDeed.None ? new Color(.15f, .27f, .20f, .95f) : Panel, 10);
                GUI.Label(DeedButton, DeedName(chance).ToUpperInvariant(), centered);
            }
            if (!CanTakeOff)
                return;
            Round(FlightButton, Panel, 10);
            GUI.Label(FlightButton, active.IsFlying ? "ATERRIZAR" : "DESPEGAR", centered);
            if (!active.IsFlying)
                return;
            Round(FlightUp, Panel, 10);
            Round(FlightDown, Panel, 10);
            GUI.Label(FlightUp, "SUBIR", centered);
            GUI.Label(FlightDown, "BAJAR", centered);
        }

        private string Objective()
        {
            if (active.IsDead)
            {
                leading = false;
                return null;
            }
            if (active.Latched)
                return "Colgado de la presa  ·  Ctrl para soltarte";
            string status = GroupStatus();
            if (!HasGroup || !active.Grown)
                return status;
            bool touch = Application.isMobilePlatform;
            bool danger = active.Life != null && active.Life.Community.HasThreat;
            if (leading && !danger)
                return (active.Life != null || active.Traits.Nature == Dinosaur.Temperament.Herd ? "Liderás la manada" : "Liderás el grupo") +
                    (touch ? "  ·  tocá para soltar" : "  ·  L para soltar");
            return (status ?? "Tu grupo") + (leading ? "" : touch ? "  ·  tocá para liderar" : "  ·  L para liderar");
        }

        private string GroupStatus()
        {
            if (active.Life != null)
            {
                TriceratopsHerd community = active.Life.Community;
                return community.HasThreat ? "La manada está en peligro" : community.Sleeping ? "La manada duerme" :
                    community.Errand switch
                    {
                        HerdErrand.Drink => "La manada va a beber",
                        HerdErrand.Graze => "La manada va a pastar",
                        _ => "La manada recorre el territorio"
                    };
            }
            if (active.Herd != null && active.Herd.Leader != null)
            {
                bool pack = active.Traits.Nature == Dinosaur.Temperament.Pack;
                string group = pack || active.Traits.CanFly ? "El grupo" : "La manada";
                if (active.Herd.Foe != null)
                    return $"{group} ataca a un {ShortName(active.Herd.Foe.Species)}";
                if (active.Herd.Meal != null)
                    return $"{group} come";
                return active.Herd.Errand switch
                {
                    HerdErrand.Drink => $"{group} va a beber",
                    HerdErrand.Graze => $"{group} va a los árboles gigantes",
                    HerdErrand.Hunt => $"{group} sale de caza",
                    HerdErrand.Rest => $"{group} descansa",
                    _ => $"{group} recorre el territorio"
                };
            }
            if (active.Traits.Nature == Dinosaur.Temperament.Herd)
                return null;
            IslandSites.PreyHerds(active, quarry);
            string nearest = null;
            float range = float.PositiveInfinity;
            foreach ((Vector3 center, string name, object _) in quarry)
            {
                float distance = TriceratopsLife.Distance(center, active.transform.position);
                if (distance >= range)
                    continue;
                range = distance;
                nearest = name;
            }
            return nearest == null ? null : $"Presa más cercana  ·  {ShortName(nearest)} a {range:0} m";
        }

        // Sed y hambre: una línea fina que se llena; el rótulo aparece recién cuando la necesidad aprieta.
        private void Need(Rect rect, string name, float level, Color tone)
        {
            Color color = level > .75f ? Warn : tone;
            Round(rect, new Color(0, 0, 0, .42f), 1.5f);
            if (level > .02f)
                Round(new Rect(rect.x, rect.y, Mathf.Max(3, rect.width * Mathf.Clamp01(level)), rect.height), color, 1.5f);
            if (level <= .5f)
                return;
            Color ink = tiny.normal.textColor;
            tiny.normal.textColor = color;
            Ink(new Rect(rect.xMax + 5, rect.y - 4, 60, 10), name, tiny);
            tiny.normal.textColor = ink;
        }

        private static string DeedName(DinosaurDeed deed) =>
            deed == DinosaurDeed.Drink ? "Beber" : deed == DinosaurDeed.Graze ? "Pastar" : "Comer";

        private static string ShortName(string species) => species == "Tyrannosaurus rex" ? "T. rex" : species;

        private static Rect ControlRect(Rect control)
        {
            Rect view = PsxView.Viewport;
            float scale = view.width / 960;
            return new Rect(view.position + control.position * scale, control.size * scale);
        }
    }
}
