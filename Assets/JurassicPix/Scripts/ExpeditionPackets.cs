using Unity.Netcode;
using UnityEngine;

namespace JurassicPix
{
    internal static class ExpeditionPackets
    {
        internal const int InputSize = 2 * sizeof(byte) + 3 * sizeof(float);
        internal const int CommandSize = 2 * sizeof(byte);
        internal const int SnapshotHeaderSize = sizeof(uint) + sizeof(byte);
        private const int PoseSize = 6 * sizeof(ushort) + 5 * sizeof(byte);
        private const float HorizontalPrecision = 12;
        private const float VerticalPrecision = 32;

        public static int SnapshotSize(int seats, int wildlife) =>
            SnapshotHeaderSize + 2 * sizeof(float) + seats * sizeof(ulong) + (seats + wildlife) * PoseSize;

        // Netcode has already advanced past the named message header.
        public static bool HasPayload(FastBufferReader reader, int size) => reader.Length - reader.Position == size;

        public static void WriteInput(FastBufferWriter writer, int seat, DinosaurInput input)
        {
            writer.WriteValueSafe((byte)seat);
            writer.WriteValueSafe(input.Direction.x);
            writer.WriteValueSafe(input.Direction.z);
            writer.WriteValueSafe(input.Lift);
            writer.WriteValueSafe((byte)((input.Run ? 1 : 0) | (input.Guard ? 2 : 0) | (input.Crouch ? 4 : 0) |
                (input.Act ? 8 : 0)));
        }

        public static bool TryReadInput(FastBufferReader reader, out int seat, out DinosaurInput input)
        {
            seat = -1;
            input = default;
            if (!HasPayload(reader, InputSize))
                return false;

            reader.ReadValueSafe(out byte index);
            reader.ReadValueSafe(out float x);
            reader.ReadValueSafe(out float z);
            reader.ReadValueSafe(out float lift);
            reader.ReadValueSafe(out byte flags);
            if (!float.IsFinite(x) || !float.IsFinite(z) || !float.IsFinite(lift) || flags > 15)
                return false;

            seat = index;
            var direction = new Vector3(Mathf.Clamp(x, -1, 1), 0, Mathf.Clamp(z, -1, 1));
            input = new DinosaurInput(Vector3.ClampMagnitude(direction, 1), (flags & 1) != 0, Mathf.Clamp(lift, -1, 1),
                (flags & 2) != 0, (flags & 4) != 0, (flags & 8) != 0);
            return true;
        }

        public static void WritePose(FastBufferWriter writer, DinosaurPose pose)
        {
            Vector2 center = IslandLayout.Center;
            writer.WriteValueSafe(EncodePosition(pose.Position.x - center.x, HorizontalPrecision));
            writer.WriteValueSafe(EncodePosition(pose.Position.y, VerticalPrecision));
            writer.WriteValueSafe(EncodePosition(pose.Position.z - center.y, HorizontalPrecision));
            writer.WriteValueSafe((ushort)Mathf.RoundToInt(Mathf.Repeat(pose.Yaw, 360) * (65535f / 360)));
            writer.WriteValueSafe((ushort)pose.Health);
            writer.WriteValueSafe((byte)Mathf.RoundToInt(Mathf.Clamp01(pose.Gait * .5f) * 255));
            writer.WriteValueSafe(pose.AttackTime < 0 ? (byte)0 : (byte)(1 + Mathf.Min(254, Mathf.RoundToInt(pose.AttackTime * 100))));
            writer.WriteValueSafe((byte)((pose.Flying ? 1 : 0) | (pose.Diving ? 2 : 0) | (pose.Impact ? 4 : 0) |
                (pose.Guard ? 8 : 0) | (pose.Crouch ? 16 : 0) | (pose.Toppled ? 32 : 0) | (pose.Stagger ? 64 : 0) |
                (pose.Feed ? 128 : 0)));
            writer.WriteValueSafe((ushort)Mathf.Clamp(Mathf.RoundToInt(pose.LifeClock * 4), 0, ushort.MaxValue));
            writer.WriteValueSafe((byte)pose.Activity);
            writer.WriteValueSafe((byte)Mathf.RoundToInt(Mathf.Clamp01(pose.Stamina) * 255));
        }

        public static DinosaurPose ReadPose(FastBufferReader reader)
        {
            reader.ReadValueSafe(out short x);
            reader.ReadValueSafe(out short y);
            reader.ReadValueSafe(out short z);
            reader.ReadValueSafe(out ushort yaw);
            reader.ReadValueSafe(out ushort health);
            reader.ReadValueSafe(out byte gait);
            reader.ReadValueSafe(out byte attack);
            reader.ReadValueSafe(out byte flags);
            reader.ReadValueSafe(out ushort lifeClock);
            reader.ReadValueSafe(out byte activity);
            reader.ReadValueSafe(out byte stamina);
            Vector2 center = IslandLayout.Center;
            var position = new Vector3(center.x + x / HorizontalPrecision, y / VerticalPrecision,
                center.y + z / HorizontalPrecision);
            return new DinosaurPose(position, yaw * (360f / 65535), gait * (2f / 255), attack == 0 ? -1 : (attack - 1) * .01f,
                (flags & 1) != 0, (flags & 2) != 0, (flags & 4) != 0, health, (flags & 8) != 0,
                lifeClock * .25f, (LifeActivity)activity, (flags & 16) != 0, (flags & 32) != 0, (flags & 64) != 0,
                (flags & 128) != 0, stamina / 255f);
        }

        private static short EncodePosition(float position, float precision) =>
            (short)Mathf.Clamp(Mathf.RoundToInt(position * precision), short.MinValue, short.MaxValue);
    }
}
