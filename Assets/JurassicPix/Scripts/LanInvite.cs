using System.Net;
using System.Net.Sockets;

namespace JurassicPix
{
    internal static class LanInvite
    {
        private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
        private const string Prefix = "JP";
        private const int SymbolCount = 11;
        private const int ChecksumBits = 7;
        private const int ChecksumModulus = 127;

        internal static string Encode(IPAddress address, ushort port)
        {
            ulong endpoint = 0;
            foreach (byte octet in address.GetAddressBytes())
                endpoint = (endpoint << 8) | octet;
            endpoint = (endpoint << 16) | port;

            // Los siete bits de verificación detectan errores al copiar un código.
            ulong value = (endpoint << ChecksumBits) | (endpoint % ChecksumModulus);
            char[] symbols = new char[SymbolCount];
            for (int i = symbols.Length - 1; i >= 0; i--)
            {
                symbols[i] = Alphabet[(int)(value & 31)];
                value >>= 5;
            }
            return $"{Prefix}-{new string(symbols, 0, 5)}-{new string(symbols, 5, 6)}";
        }

        internal static bool LooksLocal(string code) =>
            code.StartsWith(Prefix, System.StringComparison.Ordinal) && code.Length != 6;

        // Recibe el código en mayúsculas, sin espacios ni guiones.
        internal static bool TryDecode(string code, out IPAddress address, out ushort port)
        {
            address = null;
            port = 0;
            if (code.Length != Prefix.Length + SymbolCount || !LooksLocal(code))
                return false;

            ulong value = 0;
            for (int i = Prefix.Length; i < code.Length; i++)
            {
                char symbol = code[i] switch { 'O' => '0', 'I' or 'L' => '1', _ => code[i] };
                int digit = Alphabet.IndexOf(symbol);
                if (digit < 0)
                    return false;
                value = (value << 5) | (uint)digit;
            }

            ulong endpoint = value >> ChecksumBits;
            if (value % (1 << ChecksumBits) != endpoint % ChecksumModulus)
                return false;
            port = (ushort)endpoint;
            address = new IPAddress(new[] { (byte)(endpoint >> 40), (byte)(endpoint >> 32),
                (byte)(endpoint >> 24), (byte)(endpoint >> 16) });
            return IsUsable(address) && port != 0;
        }

        internal static bool IsUsable(IPAddress address) => address != null
            && address.AddressFamily == AddressFamily.InterNetwork && !address.Equals(IPAddress.Any)
            && !address.Equals(IPAddress.Broadcast) && address.GetAddressBytes()[0] < 224;
    }
}
