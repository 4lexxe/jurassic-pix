using System.Threading.Tasks;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace JurassicPix
{
    // Envoltorio mínimo de Unity Relay: crea la asignación del anfitrión y resuelve códigos de invitación.
    // Requiere el proyecto vinculado a Unity Services (Edit > Project Settings > Services).
    internal static class ExpeditionRelay
    {
        private static Task starting;

        internal static Task Prepare() => starting == null || starting.IsCompleted
            ? starting = Initialize() : starting;

        private static async Task Initialize()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        // Crea una sala y devuelve el código que comparten los invitados.
        internal static async Task<string> Host(UnityTransport transport, int seats, bool southAmerica)
        {
            const string saoPaulo = "southamerica-east1";
            var relay = RelayService.Instance;
            string region = null;
            if (southAmerica && (await relay.ListRegionsAsync()).Exists(candidate => candidate.Id == saoPaulo))
                region = saoPaulo;

            Allocation allocation = await relay.CreateAllocationAsync(Mathf.Clamp(seats, 1, 32), region);
            Debug.Log($"Unity Relay · región {allocation.Region}");
            string code = await relay.GetJoinCodeAsync(allocation.AllocationId);
            transport.SetRelayServerData(new RelayServerData(allocation.RelayServer.IpV4,
                (ushort)allocation.RelayServer.Port, allocation.AllocationIdBytes, allocation.ConnectionData,
                allocation.ConnectionData, allocation.Key, false));
            return code;
        }

        internal static async Task Join(UnityTransport transport, string code)
        {
            JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(code);
            transport.SetRelayServerData(new RelayServerData(allocation.RelayServer.IpV4,
                (ushort)allocation.RelayServer.Port, allocation.AllocationIdBytes, allocation.ConnectionData,
                allocation.HostConnectionData, allocation.Key, false));
        }
    }
}
