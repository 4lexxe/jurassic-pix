using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace JurassicPix
{
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(NetworkManager), typeof(UnityTransport))]
    public sealed class ExpeditionNetwork : MonoBehaviour, IPlayerControl
    {
        public const ushort DefaultPort = 7777;
        private const int TickRate = 30;
        private const float Interval = 1f / TickRate;
        private const int WildlifeGroups = 3;
        private const float InputTimeout = .45f;
        private const ushort ProtocolVersion = 9;
        private const ulong Unowned = ulong.MaxValue;
        private const int NameLength = 12;
        private const string InputMessage = "jp.input";
        private const string CommandMessage = "jp.command";
        private const string SnapshotMessage = "jp.snapshot";
        private const string BusyMessage = "jp.busy";
        private const string RosterMessage = "jp.roster";

        private enum Role { Offline, Host, Guest }
        private enum Command : byte { Select, Attack, Flight, Lead, Follow }

        private NetworkManager manager;
        private UnityTransport transport;
        private Role role;
        private IReplicatedCreature[] animals;
        private ForestLighting lighting;
        private ValleyWeather weather;
        private ulong[] owners;
        private float[] lastInputAt;
        private DinosaurPose[] displayed, incoming, blendFrom;
        private readonly float[] blendStarted = new float[WildlifeGroups + 1];
        private readonly float[] blendDuration = new float[WildlifeGroups + 1];
        private readonly List<ulong> recipients = new();
        private DinosaurInput localInput;
        private int localSeat = -1, hostPreference;
        private bool dedicated, urgentInput;
        private float nextSendAt;
        private uint sequence;
        private bool hasSnapshot, closing, quitting;
        private string[] names;
        private readonly Dictionary<ulong, string> clientNames = new();
        private string localName;

        internal bool Online => role != Role.Offline;
        internal bool IsGuest => role == Role.Guest;
        internal bool Dedicated => Online && dedicated;
        internal bool PreparingInvite { get; private set; }
        internal bool InternetAvailable => !string.IsNullOrEmpty(Application.cloudProjectId);
        private bool CanStart => !Online && !PreparingInvite && !manager.ShutdownInProgress;
        internal int LocalSeat => localSeat;
        internal string Status { get; private set; } = "Modo individual";
        internal string HostAddress { get; private set; }
        internal string JoinCode { get; private set; }
        internal string LocalName
        {
            get => localName;
            set
            {
                string clean = SanitizeName(value);
                if (clean == localName)
                    return;
                localName = clean;
                PlayerPrefs.SetString("jp.nombre", localName);
            }
        }
        internal event Action<int> SeatChanged;

        internal void Initialize(IReplicatedCreature[] creatures, int seats, ForestLighting clock, ValleyWeather conditions)
        {
            animals = creatures;
            lighting = clock;
            weather = conditions;
            owners = new ulong[seats];
            names = new string[seats];
            lastInputAt = new float[seats];
            localName = PlayerPrefs.GetString("jp.nombre", "");
            if (string.IsNullOrEmpty(localName))
                localName = SanitizeName(Environment.UserName);
            displayed = new DinosaurPose[animals.Length];
            incoming = new DinosaurPose[animals.Length];
            blendFrom = new DinosaurPose[animals.Length];
            Array.Fill(owners, Unowned);

            manager = GetComponent<NetworkManager>();
            transport = GetComponent<UnityTransport>();
            manager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                ConnectionApproval = true,
                EnableSceneManagement = false,
                ProtocolVersion = ProtocolVersion,
                TickRate = TickRate
            };
            manager.ConnectionApprovalCallback = Approve;
            manager.OnServerStarted += RegisterHost;
            manager.OnClientStarted += RegisterGuest;
            manager.OnClientConnectedCallback += ClientConnected;
            manager.OnClientDisconnectCallback += ClientDisconnected;
            manager.OnClientStopped += SessionStopped;
            manager.OnTransportFailure += TransportFailed;
        }

        internal bool IsOccupied(int seat) => Online && owners[seat] != Unowned && seat != localSeat;

        // Nombre del jugador asignado a cada especie; null cuando el lugar está libre u offline.
        internal string SeatName(int seat) =>
            Online && seat >= 0 && seat < owners.Length && owners[seat] != Unowned ? names[seat] ?? "…" : null;

        private static string SanitizeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Jugador";
            var clean = new System.Text.StringBuilder(NameLength);
            foreach (char letter in value.Trim())
            {
                if (clean.Length >= NameLength)
                    break;
                if (!char.IsControl(letter))
                    clean.Append(letter);
            }
            return clean.Length == 0 ? "Jugador" : clean.ToString();
        }

        // Sala por Unity Relay: funciona entre redes distintas sin abrir puertos. Requiere el proyecto
        // vinculado a Unity Services y devuelve un código corto para compartir.
        internal async void HostInternet(int preferredSeat, bool southAmerica)
        {
            if (!CanStart || !CheckInternet())
                return;

            PreparingInvite = true;
            Status = "Preparando la sala por internet…";
            try
            {
                await ExpeditionRelay.Prepare();
                if (this == null)
                    return;
                JoinCode = await ExpeditionRelay.Host(transport, owners.Length - 1, southAmerica);
                if (this == null)
                    return;

                role = Role.Host;
                dedicated = false;
                hostPreference = Mathf.Clamp(preferredSeat, 0, owners.Length - 1);
                HostAddress = JoinCode;
                Status = "Iniciando la partida…";
                if (!manager.StartHost())
                    EndSession("No se pudo abrir la partida.");
            }
            catch (Exception error)
            {
                if (this != null)
                    InviteFailed(error, "No se pudo crear la partida por internet.");
            }
            finally
            {
                PreparingInvite = false;
            }
        }

        internal void JoinByCode(string input)
        {
            if (!CanStart)
                return;
            string code = System.Text.RegularExpressions.Regex.Replace(input, @"[\s-]", "").ToUpperInvariant();
            if (code.Length == 0)
            {
                Status = "Ingresá el código de invitación del anfitrión.";
                return;
            }
            if (LanInvite.LooksLocal(code))
            {
                if (LanInvite.TryDecode(code, out IPAddress address, out ushort port))
                    Join(address.ToString(), port);
                else
                    Status = "El código local no es válido. Copialo de nuevo del anfitrión.";
                return;
            }
            if (code.Length != 6 || !Array.TrueForAll(code.ToCharArray(), c => c is >= 'A' and <= 'Z' or >= '0' and <= '9'))
            {
                Status = "Código inválido. Revisá que esté completo.";
                return;
            }
            if (CheckInternet())
                JoinInternet(code);
        }

        private bool CheckInternet()
        {
            if (InternetAvailable)
                return true;
            Status = "Internet no está habilitado en esta build. Podés jugar en la misma Wi-Fi con un código local.";
            return false;
        }

        private async void JoinInternet(string code)
        {
            PreparingInvite = true;
            Status = "Buscando la sala…";
            try
            {
                await ExpeditionRelay.Prepare();
                if (this == null)
                    return;
                await ExpeditionRelay.Join(transport, code);
                if (this != null)
                    StartGuest("Conectando con la partida…");
            }
            catch (Exception error)
            {
                if (this != null)
                    InviteFailed(error, "No se pudo entrar. Verificá el código, tu conexión y que el anfitrión siga en la partida.");
            }
            finally
            {
                PreparingInvite = false;
            }
        }

        private void InviteFailed(Exception error, string message)
        {
            Debug.LogWarning($"Unity Relay: {error}");
            if (Online)
                EndSession(message);
            else
                Status = message;
        }

        internal bool Host(int preferredSeat, ushort port)
        {
            if (!CanStart)
                return false;

            role = Role.Host;
            dedicated = false;
            hostPreference = Mathf.Clamp(preferredSeat, 0, owners.Length - 1);
            SetDirectHost(port);
            Status = "Iniciando la partida…";
            if (manager.StartHost())
                return true;

            EndSession("No se pudo abrir la partida. Probá otro puerto.");
            return false;
        }

        internal bool Serve(ushort port)
        {
            if (!CanStart)
                return false;

            role = Role.Host;
            dedicated = true;
            foreach (IReplicatedCreature animal in animals)
                animal.SetControl(false);
            SetDirectHost(port);
            Status = "Iniciando el servidor…";
            if (manager.StartServer())
            {
                Status = $"Servidor dedicado · puerto {port}";
                return true;
            }
            EndSession("No se pudo abrir el servidor. Probá otro puerto.");
            return false;
        }

        private void SetDirectHost(ushort port)
        {
            transport.SetConnectionData("127.0.0.1", port, "0.0.0.0");
            IPAddress address = FindLanAddress();
            ushort listeningPort = transport.ConnectionData.Port;
            HostAddress = $"{address}:{listeningPort}";
            JoinCode = IPAddress.IsLoopback(address) ? null : LanInvite.Encode(address, listeningPort);
        }

        internal void AutoStart()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            ushort port = DefaultPort;
            int at = Array.IndexOf(arguments, "-puerto");
            if (at >= 0 && at + 1 < arguments.Length)
                ushort.TryParse(arguments[at + 1], out port);
            if (port == 0)
                port = DefaultPort;
            at = Array.IndexOf(arguments, "-unirse");
            if (Array.IndexOf(arguments, "-servidor") >= 0)
                Serve(port);
            else if (at >= 0 && at + 1 < arguments.Length)
            {
                string[] target = arguments[at + 1].Split(':');
                if (target.Length > 1 && ushort.TryParse(target[1], out ushort named) && named != 0)
                    port = named;
                Join(target[0], port);
            }
        }

        private static IPAddress Resolve(string address)
        {
            if (IPAddress.TryParse(address, out IPAddress direct))
                return direct.AddressFamily == AddressFamily.InterNetwork ? direct : null;
            try
            {
                foreach (IPAddress found in Dns.GetHostAddresses(address))
                    if (found.AddressFamily == AddressFamily.InterNetwork)
                        return found;
            }
            catch (SocketException)
            {
            }
            catch (ArgumentException)
            {
            }
            return null;
        }

        internal bool Join(string address, ushort port)
        {
            if (!CanStart)
                return false;
            IPAddress ip = Resolve(address);
            if (!LanInvite.IsUsable(ip))
            {
                Status = "No se encontró ese servidor. Revisá la dirección o el nombre.";
                return false;
            }

            transport.SetConnectionData(ip.ToString(), port);
            return StartGuest($"Conectando con {ip}:{port}…");
        }

        private bool StartGuest(string status)
        {
            role = Role.Guest;
            foreach (IReplicatedCreature animal in animals)
                animal.SetReplica(true);
            manager.NetworkConfig.ConnectionData = System.Text.Encoding.ASCII.GetBytes(LocalName);
            Status = status;
            if (manager.StartClient())
                return true;

            EndSession("No se pudo iniciar la conexión.");
            return false;
        }

        internal void Leave()
        {
            if (!Online || closing)
                return;

            closing = true;
            Status = "Cerrando la partida…";
            manager.Shutdown();
        }

        internal void Select(int seat) => SendCommand(Command.Select, seat);
        internal void Attack() => SendCommand(Command.Attack, localSeat);
        internal void ToggleFlight() => SendCommand(Command.Flight, localSeat);
        internal void SetLeading(bool lead) => SendCommand(lead ? Command.Lead : Command.Follow, localSeat);

        void IPlayerControl.SetInput(DinosaurInput input) => SetInput(input);
        void IPlayerControl.Attack() => Attack();
        void IPlayerControl.ToggleFlight() => ToggleFlight();
        void IPlayerControl.SetLeading(bool lead) => SetLeading(lead);

        internal void SetInput(DinosaurInput input)
        {
            if (closing || localSeat < 0)
                return;
            if (role == Role.Host)
                ApplyInput(localSeat, input);
            else if (IsGuest)
            {
                urgentInput |= (input.Direction == Vector3.zero) != (localInput.Direction == Vector3.zero)
                    || (input.Direction - localInput.Direction).sqrMagnitude > .1f
                    || input.Run != localInput.Run || input.Lift != localInput.Lift
                    || input.Guard != localInput.Guard || input.Crouch != localInput.Crouch || input.Act != localInput.Act;
                localInput = input;
            }
        }

        internal void RequestDayNight()
        {
            if (IsGuest)
                Status = "El anfitrión controla la hora.";
            else
                lighting.ToggleDayNight();
        }

        internal void RequestWeather()
        {
            if (!IsGuest)
                weather.NextPhase();
        }

        private void RegisterHost()
        {
            var messages = manager.CustomMessagingManager;
            messages.RegisterNamedMessageHandler(InputMessage, ReceiveInput);
            messages.RegisterNamedMessageHandler(CommandMessage, ReceiveCommand);
        }

        private void RegisterGuest()
        {
            if (!IsGuest)
                return;

            var messages = manager.CustomMessagingManager;
            messages.RegisterNamedMessageHandler(SnapshotMessage, ReceiveSnapshot);
            messages.RegisterNamedMessageHandler(BusyMessage, ReceiveBusy);
            messages.RegisterNamedMessageHandler(RosterMessage, ReceiveRoster);
        }

        private void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            // El invitado envía su nombre en la solicitud de conexión.
            clientNames[request.ClientNetworkId] = request.Payload != null && request.Payload.Length > 0
                ? SanitizeName(System.Text.Encoding.ASCII.GetString(request.Payload)) : null;
            response.Approved = request.ClientNetworkId == NetworkManager.ServerClientId
                || Array.IndexOf(owners, Unowned) >= 0 && !closing;
            response.CreatePlayerObject = false;
            if (!response.Approved)
                response.Reason = "Partida llena o en cierre.";
        }

        private void ClientConnected(ulong client)
        {
            if (role == Role.Host)
            {
                if (dedicated && client == NetworkManager.ServerClientId)
                    return;
                int seat = client == NetworkManager.ServerClientId ? hostPreference : FreeSeat();
                if (seat < 0)
                    manager.DisconnectClient(client, "Partida llena.");
                else
                    Assign(client, seat);
                UpdateHostStatus();
            }
            else if (IsGuest && client == manager.LocalClientId)
                Status = "Conectado. Asignando dinosaurio…";
        }

        private int FreeSeat()
        {
            return Array.IndexOf(owners, Unowned);
        }

        private void ClientDisconnected(ulong client)
        {
            if (!quitting && role == Role.Host && client != NetworkManager.ServerClientId)
            {
                int seat = Array.IndexOf(owners, client);
                if (seat >= 0)
                    Release(seat);
                if (!closing)
                    UpdateHostStatus();
            }
        }

        private void TransportFailed() => Status = "Se interrumpió la conexión de red.";

        private void SessionStopped(bool wasHost)
        {
            if (quitting || !Online)
                return;

            string reason = manager.DisconnectReason;
            EndSession(closing ? "Modo individual" : !string.IsNullOrEmpty(reason) ? reason
                : wasHost ? "Se cerró la partida." : "Se perdió la conexión con el anfitrión.");
        }

        private void EndSession(string status)
        {
            if (IsGuest)
            {
                foreach (IReplicatedCreature animal in animals)
                    animal.SetReplica(false);
            }
            else
            {
                for (int i = 0; i < owners.Length; i++)
                    Release(i);
            }

            Array.Fill(owners, Unowned);
            Array.Fill(names, null);
            clientNames.Clear();
            role = Role.Offline;
            JoinCode = null;
            HostAddress = null;
            closing = hasSnapshot = false;
            urgentInput = false;
            Array.Clear(blendDuration, 0, blendDuration.Length);
            nextSendAt = 0;
            sequence = 0;
            Status = status;
            localSeat = -1;
            localInput = default;
            SeatChanged?.Invoke(-1);
        }

        private void Assign(ulong client, int target)
        {
            int old = Array.IndexOf(owners, client);
            if (old >= 0)
                Release(old);
            owners[target] = client;
            names[target] = client == NetworkManager.ServerClientId ? LocalName
                : clientNames.TryGetValue(client, out string who) && !string.IsNullOrEmpty(who) ? who : $"Jugador {client}";
            BroadcastRoster();
            // Quien toma una especie nace como cría; el anfitrión que ya la venía jugando conserva lo crecido.
            bool born = !animals[target].IsControlled;
            animals[target].SetControl(true);
            if (born)
                animals[target].BeBorn();
            lastInputAt[target] = Time.unscaledTime;
            if (client == NetworkManager.ServerClientId)
                ChangeLocalSeat(target);
        }

        private void Release(int seat)
        {
            owners[seat] = Unowned;
            names[seat] = null;
            animals[seat].SetControl(false);
            lastInputAt[seat] = 0;
            BroadcastRoster();
        }

        // El anfitrión mantiene el mapa de nombres y lo difunde cada vez que un lugar cambia de dueño.
        private void BroadcastRoster()
        {
            if (role != Role.Host || manager == null || !manager.IsListening)
                return;
            recipients.Clear();
            var clients = manager.ConnectedClientsIds;
            for (int i = 0; i < clients.Count; i++)
                if (clients[i] != NetworkManager.ServerClientId)
                    recipients.Add(clients[i]);
            if (recipients.Count == 0)
                return;

            using var writer = new FastBufferWriter(4 + owners.Length * (1 + 61), Allocator.Temp);
            writer.WriteValueSafe((byte)owners.Length);
            for (int i = 0; i < owners.Length; i++)
            {
                writer.WriteValueSafe((byte)i);
                writer.WriteValueSafe(names[i] != null ? new Unity.Collections.FixedString64Bytes(names[i])
                    : new Unity.Collections.FixedString64Bytes());
            }
            manager.CustomMessagingManager.SendNamedMessage(RosterMessage, recipients, writer, NetworkDelivery.Reliable);
        }

        private void ReceiveRoster(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId)
                return;
            reader.ReadValueSafe(out byte seats);
            for (int i = 0; i < seats; i++)
            {
                reader.ReadValueSafe(out byte seat);
                reader.ReadValueSafe(out Unity.Collections.FixedString64Bytes who);
                if (seat < names.Length)
                    names[seat] = who.Length == 0 ? null : who.ToString();
            }
        }

        private void ChangeLocalSeat(int seat)
        {
            if (localSeat == seat)
                return;

            localSeat = seat;
            localInput = default;
            urgentInput = seat >= 0;
            SeatChanged?.Invoke(seat);
        }

        private void UpdateHostStatus()
        {
            int players = 0;
            foreach (ulong owner in owners)
            {
                if (owner != Unowned)
                    players++;
            }
            Status = $"Anfitrión · {players}/{owners.Length} jugadores";
        }

        private void SendCommand(Command command, int seat)
        {
            if (closing || localSeat < 0 || seat < 0 || seat >= owners.Length)
                return;
            if (role == Role.Host)
                ExecuteCommand(NetworkManager.ServerClientId, command, seat);
            else if (IsGuest && manager.IsConnectedClient)
            {
                using var writer = new FastBufferWriter(ExpeditionPackets.CommandSize, Allocator.Temp);
                writer.WriteValueSafe((byte)command);
                writer.WriteValueSafe((byte)seat);
                manager.CustomMessagingManager.SendNamedMessage(CommandMessage, NetworkManager.ServerClientId, writer);
            }
        }

        private void ReceiveCommand(ulong client, FastBufferReader reader)
        {
            if (closing || !ExpeditionPackets.HasPayload(reader, ExpeditionPackets.CommandSize))
                return;

            reader.ReadValueSafe(out byte command);
            reader.ReadValueSafe(out byte seat);
            ExecuteCommand(client, (Command)command, seat);
        }

        private void ExecuteCommand(ulong client, Command command, int seat)
        {
            if (seat < 0 || seat >= owners.Length)
                return;
            if (command == Command.Select)
            {
                int previous = Array.IndexOf(owners, client);
                if (previous < 0)
                    return;
                if (previous == seat)
                    animals[seat].BeBorn();
                else if (owners[seat] == Unowned)
                    Assign(client, seat);
                else if (client == NetworkManager.ServerClientId)
                    Status = SelectionUnavailable(seat);
                else
                {
                    using var writer = new FastBufferWriter(sizeof(byte), Allocator.Temp);
                    writer.WriteValueSafe((byte)seat);
                    manager.CustomMessagingManager.SendNamedMessage(BusyMessage, client, writer);
                }
                return;
            }
            if (owners[seat] != client)
                return;
            if (command == Command.Attack)
                animals[seat].Attack();
            else if (command == Command.Flight)
                animals[seat].ToggleFlight();
            else if (command is Command.Lead or Command.Follow)
                animals[seat].SetLeading(command == Command.Lead);
        }

        private void ReceiveBusy(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId || !ExpeditionPackets.HasPayload(reader, sizeof(byte)))
                return;

            reader.ReadValueSafe(out byte seat);
            if (seat < owners.Length)
                Status = SelectionUnavailable(seat);
        }

        private string SelectionUnavailable(int seat) => $"{animals[seat].Species} está ocupado por otro jugador.";

        private void ReceiveInput(ulong client, FastBufferReader reader)
        {
            if (!closing && ExpeditionPackets.TryReadInput(reader, out int seat, out DinosaurInput input)
                && seat < owners.Length && owners[seat] == client)
                ApplyInput(seat, input);
        }

        private void ApplyInput(int seat, DinosaurInput input)
        {
            animals[seat].SetInput(input);
            lastInputAt[seat] = Time.unscaledTime;
        }

        private void Update()
        {
            if (!Online || closing)
                return;

            float now = Time.unscaledTime;
            if (role == Role.Host)
            {
                for (int i = 0; i < owners.Length; i++)
                {
                    if (owners[i] != Unowned && owners[i] != NetworkManager.ServerClientId && now - lastInputAt[i] > InputTimeout)
                        animals[i].SetInput(default);
                }
            }
            else if (hasSnapshot)
            {
                ApplyReplicas(0, 0, owners.Length, now);
                for (int group = 0; group < WildlifeGroups; group++)
                    ApplyReplicas(group + 1, WildlifeStart(group), WildlifeStart(group + 1), now);
            }
        }

        private int WildlifeStart(int group) => owners.Length + (animals.Length - owners.Length) * group / WildlifeGroups;

        private void ApplyReplicas(int group, int start, int end, float now)
        {
            float duration = blendDuration[group];
            if (duration <= 0)
                return;
            float elapsed = now - blendStarted[group];
            float fraction = Mathf.Clamp01(elapsed / duration);
            for (int i = start; i < end; i++)
            {
                float progress = i == localSeat ? Mathf.Clamp01(elapsed / (Interval * .5f)) : fraction;
                displayed[i] = DinosaurPose.Blend(blendFrom[i], incoming[i], progress);
                animals[i].ApplyReplica(displayed[i], Time.deltaTime);
            }
        }

        private void LateUpdate()
        {
            float now = Time.unscaledTime;
            if (closing || !Online || !manager.IsListening || (!urgentInput && now < nextSendAt))
                return;

            nextSendAt = urgentInput ? now + Interval : Mathf.Max(now - Interval, nextSendAt) + Interval;
            urgentInput = false;
            if (role == Role.Host)
                SendSnapshot();
            else if (localSeat >= 0 && manager.IsConnectedClient)
            {
                using var writer = new FastBufferWriter(ExpeditionPackets.InputSize, Allocator.Temp);
                ExpeditionPackets.WriteInput(writer, localSeat, localInput);
                manager.CustomMessagingManager.SendNamedMessage(InputMessage, NetworkManager.ServerClientId,
                    writer, NetworkDelivery.UnreliableSequenced);
            }
        }

        private void SendSnapshot()
        {
            recipients.Clear();
            var clients = manager.ConnectedClientsIds;
            for (int i = 0; i < clients.Count; i++)
            {
                if (clients[i] != NetworkManager.ServerClientId)
                    recipients.Add(clients[i]);
            }
            if (recipients.Count == 0)
                return;

            // Cada paquete lleva a todos los jugadores y a un tercio de la fauna.
            int group = (int)(sequence % WildlifeGroups);
            int start = WildlifeStart(group), end = WildlifeStart(group + 1);
            using var writer = new FastBufferWriter(ExpeditionPackets.SnapshotSize(owners.Length, end - start), Allocator.Temp);
            writer.WriteValueSafe(++sequence);
            writer.WriteValueSafe((byte)group);
            writer.WriteValueSafe(lighting.Hour);
            writer.WriteValueSafe(weather.Elapsed);
            foreach (ulong owner in owners)
                writer.WriteValueSafe(owner);
            for (int i = 0; i < owners.Length; i++)
                ExpeditionPackets.WritePose(writer, animals[i].CapturePose());
            for (int i = start; i < end; i++)
                ExpeditionPackets.WritePose(writer, animals[i].CapturePose());
            manager.CustomMessagingManager.SendNamedMessage(SnapshotMessage, recipients, writer,
                NetworkDelivery.UnreliableSequenced);
        }

        private void ReceiveSnapshot(ulong sender, FastBufferReader reader)
        {
            if (closing || sender != NetworkManager.ServerClientId ||
                reader.Length - reader.Position < ExpeditionPackets.SnapshotHeaderSize)
                return;

            reader.ReadValueSafe(out uint received);
            if (hasSnapshot && unchecked((int)(received - sequence)) <= 0)
                return;
            reader.ReadValueSafe(out byte group);
            if (group >= WildlifeGroups)
                return;
            int start = WildlifeStart(group), end = WildlifeStart(group + 1);
            int size = ExpeditionPackets.SnapshotSize(owners.Length, end - start);
            if (!ExpeditionPackets.HasPayload(reader, size - ExpeditionPackets.SnapshotHeaderSize))
                return;

            sequence = received;
            reader.ReadValueSafe(out float hour);
            lighting.Hour = hour;
            reader.ReadValueSafe(out float weatherTime);
            weather.Synchronize(weatherTime);
            for (int i = 0; i < owners.Length; i++)
                reader.ReadValueSafe(out owners[i]);
            float now = Time.unscaledTime;
            int next = Array.IndexOf(owners, manager.LocalClientId);
            ReadReplicas(reader, 0, 0, owners.Length, now, Interval, next != localSeat ? next : -1);
            ReadReplicas(reader, group + 1, start, end, now, Interval * WildlifeGroups);
            hasSnapshot = true;
            if (next >= 0 && next != localSeat)
                Status = $"Conectado · {animals[next].Species}";
            ChangeLocalSeat(next);
        }

        private void ReadReplicas(FastBufferReader reader, int group, int start, int end, float now, float interval,
            int changedSeat = -1)
        {
            bool first = blendDuration[group] <= 0;
            blendDuration[group] = first ? interval : Mathf.Clamp(now - blendStarted[group], interval, 2 * interval);
            blendStarted[group] = now;
            for (int i = start; i < end; i++)
            {
                incoming[i] = ExpeditionPackets.ReadPose(reader);
                bool cut = first || i == changedSeat;
                blendFrom[i] = cut ? incoming[i] : displayed[i];
                if (cut)
                {
                    displayed[i] = incoming[i];
                    animals[i].ApplyReplica(incoming[i], 0);
                }
            }
        }

        private static IPAddress FindLanAddress()
        {
            if (!Application.isMobilePlatform || Application.internetReachability == NetworkReachability.ReachableViaLocalAreaNetwork)
            {
                try
                {
                    // Un socket UDP elige la interfaz de salida sin enviar ningún paquete.
                    using var route = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                    route.Connect(new IPEndPoint(new IPAddress(new byte[] { 192, 0, 2, 1 }), DefaultPort));
                    if (route.LocalEndPoint is IPEndPoint endpoint && LanInvite.IsUsable(endpoint.Address))
                        return endpoint.Address;
                }
                catch (SocketException)
                {
                }
            }

            IPAddress local = null;
            try
            {
                foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (adapter.OperationalStatus != OperationalStatus.Up ||
                        adapter.NetworkInterfaceType is not (NetworkInterfaceType.Ethernet or NetworkInterfaceType.Wireless80211))
                        continue;
                    var properties = adapter.GetIPProperties();
                    foreach (var address in properties.UnicastAddresses)
                    {
                        if (!LanInvite.IsUsable(address.Address) || IPAddress.IsLoopback(address.Address))
                            continue;
                        local ??= address.Address;
                        if (properties.GatewayAddresses.Count > 0)
                            return address.Address;
                    }
                }
            }
            catch (Exception error) when (error is NetworkInformationException or NotSupportedException or NotImplementedException)
            {
                // Algunos dispositivos no exponen todos los datos del adaptador.
            }
            return local ?? IPAddress.Loopback;
        }

        private void OnApplicationQuit() => quitting = true;

        private void OnDestroy()
        {
            if (manager == null)
                return;

            manager.ConnectionApprovalCallback = null;
            manager.OnServerStarted -= RegisterHost;
            manager.OnClientStarted -= RegisterGuest;
            manager.OnClientConnectedCallback -= ClientConnected;
            manager.OnClientDisconnectCallback -= ClientDisconnected;
            manager.OnClientStopped -= SessionStopped;
            manager.OnTransportFailure -= TransportFailed;
        }
    }
}
