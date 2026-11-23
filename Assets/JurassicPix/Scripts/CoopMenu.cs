using UnityEngine;

namespace JurassicPix
{
    internal sealed class CoopMenu
    {
        private readonly ExpeditionNetwork network;
        private string address = "127.0.0.1";
        private string port = ExpeditionNetwork.DefaultPort.ToString();
        private string code = "";
        private string name;
        private string error;
        private GUIStyle statusStyle, codeStyle;
        private bool showDirect, copied;
        private bool southAmerica = PlayerPrefs.GetInt("jp.relay.sudamerica", 1) != 0;

        public bool IsOpen { get; private set; }

        public CoopMenu(ExpeditionNetwork network) => this.network = network;

        public void Toggle()
        {
            IsOpen = !IsOpen;
            error = null;
            copied = false;
        }

        public void Draw(int preferredSeat)
        {
            var panel = new Rect(210, 46, 540, 448);
            Color previousColor = GUI.color;
            GUI.color = new Color(.07f, .08f, .07f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = previousColor;
            GUI.Box(panel, "Multijugador");
            statusStyle ??= new GUIStyle(GUI.skin.label) { wordWrap = true };
            GUI.Label(new Rect(232, 77, 496, 40), error ?? network.Status, statusStyle);

            if (network.Online)
                DrawSession();
            else
                DrawConnection(preferredSeat);

            if (GUI.Button(new Rect(232, 446, 200, 34), "Volver al juego"))
                Toggle();
        }

        private void DrawSession()
        {
            if (!network.IsGuest && network.JoinCode != null)
            {
                GUI.Label(new Rect(232, 122, 496, 23), "Código para tus invitados");
                codeStyle ??= new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 22,
                    fontStyle = FontStyle.Bold
                };
                GUI.Box(new Rect(232, 148, 260, 36), network.JoinCode, codeStyle);
                if (GUI.Button(new Rect(504, 148, 224, 36), copied ? "Código copiado" : "Copiar código"))
                {
                    GUIUtility.systemCopyBuffer = network.JoinCode;
                    copied = true;
                }
                if (network.HostAddress != network.JoinCode)
                    GUI.Label(new Rect(232, 192, 496, 40), $"Compartilo con quienes estén en tu misma red. Dirección: {network.HostAddress}", statusStyle);
            }
            else if (!network.IsGuest)
                GUI.Label(new Rect(232, 122, 496, 40), "Conectate a una red local para generar un código para otros equipos.", statusStyle);

            if (GUI.Button(new Rect(232, 249, 200, 38), "Salir de la partida"))
            {
                copied = false;
                network.Leave();
            }
        }

        private void DrawConnection(int preferredSeat)
        {
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && !network.PreparingInvite;
            GUI.Label(new Rect(232, 119, 240, 22), "Tu nombre");
            name = GUI.TextField(new Rect(232, 143, 240, 30), name ?? network.LocalName, 12);
            network.LocalName = name;

            GUI.Label(new Rect(232, 182, 496, 22), "Código de invitación");
            code = GUI.TextField(new Rect(232, 207, 264, 36), code, 64);
            if (GUI.Button(new Rect(502, 207, 72, 36), "Pegar"))
                code = (GUIUtility.systemCopyBuffer ?? "").Trim();
            if (GUI.Button(new Rect(580, 207, 148, 36), "Unirse con código"))
            {
                error = null;
                network.JoinByCode(code);
            }

            if (GUI.Button(new Rect(232, 260, 240, 36), "Crear partida local"))
            {
                error = null;
                network.Host(preferredSeat, ExpeditionNetwork.DefaultPort);
            }
            if (network.InternetAvailable && GUI.Button(new Rect(488, 260, 240, 36), "Crear partida por internet"))
            {
                error = null;
                network.HostInternet(preferredSeat, southAmerica);
            }
            GUI.Label(new Rect(232, 301, 248, 22), "El código local funciona en la misma red.");
            if (network.InternetAvailable && GUI.Button(new Rect(488, 300, 240, 26),
                    southAmerica ? "Región de internet · São Paulo" : "Región de internet · Automática"))
            {
                southAmerica = !southAmerica;
                PlayerPrefs.SetInt("jp.relay.sudamerica", southAmerica ? 1 : 0);
            }

            if (GUI.Button(new Rect(232, 330, 240, 32), showDirect ? "Ocultar IP y puerto" : "Conexión por IP y puerto"))
            {
                showDirect = !showDirect;
                error = null;
            }
            if (showDirect)
                DrawDirect(preferredSeat);
            GUI.enabled = enabled;
        }

        private void DrawDirect(int preferredSeat)
        {
            GUI.Label(new Rect(232, 368, 145, 19), "IP");
            GUI.Label(new Rect(389, 368, 54, 19), "Puerto");
            address = GUI.TextField(new Rect(232, 390, 145, 31), address, 63);
            port = GUI.TextField(new Rect(389, 390, 54, 31), port, 5);
            if (GUI.Button(new Rect(455, 390, 80, 31), "Alojar") && TryPort(out ushort hostPort))
                network.Host(preferredSeat, hostPort);
            if (GUI.Button(new Rect(541, 390, 86, 31), "Unirse") && TryPort(out ushort joinPort))
                network.Join(address.Trim(), joinPort);
            if (GUI.Button(new Rect(633, 390, 95, 31), "Servidor") && TryPort(out ushort servePort))
                network.Serve(servePort);
        }

        private bool TryPort(out ushort value)
        {
            bool valid = ushort.TryParse(port, out value) && value != 0;
            error = valid ? null : "El puerto debe estar entre 1 y 65535.";
            return valid;
        }
    }
}
