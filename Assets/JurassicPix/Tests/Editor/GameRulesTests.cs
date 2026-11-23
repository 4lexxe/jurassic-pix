using System.Net;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace JurassicPix
{
    public sealed class GameRulesTests
    {
        [Test]
        public void GuardAndArmorReduceDamage()
        {
            var health = new DinosaurVitals(100);
            Assert.That(health.Hit(40, .25f, .5f), Is.EqualTo(15));
            Assert.That(health.Health, Is.EqualTo(85));
        }

        [Test]
        public void ResizePreservesHealthFractionAndDoesNotRevive()
        {
            var health = new DinosaurVitals(100);
            health.SetHealth(25);
            health.Resize(200);
            Assert.That(health.Health, Is.EqualTo(50));
            health.SetHealth(-10);
            health.Resize(300);
            Assert.That(health.Dead, Is.True);
        }

        [Test]
        public void LanCodeRoundTripsAddressAndRejectsACopyingError()
        {
            var address = IPAddress.Parse("192.168.1.23");
            string code = LanInvite.Encode(address, 7777).Replace("-", "");

            Assert.That(LanInvite.TryDecode(code, out IPAddress result, out ushort port), Is.True);
            Assert.That(result, Is.EqualTo(address));
            Assert.That(port, Is.EqualTo(7777));
            string typo = code[..^1] + (code[^1] == '0' ? '1' : '0');
            Assert.That(LanInvite.TryDecode(typo, out _, out _), Is.False);
        }

        [Test]
        public void PacketRecoversInputAndRejectsNonFiniteValues()
        {
            var input = new DinosaurInput(new Vector3(.6f, 0, .8f), true, .5f, true, false, true);
            using var writer = new FastBufferWriter(ExpeditionPackets.InputSize, Allocator.Temp);
            ExpeditionPackets.WriteInput(writer, 2, input);
            using var reader = new FastBufferReader(writer, Allocator.Temp);

            Assert.That(ExpeditionPackets.TryReadInput(reader, out int seat, out DinosaurInput received), Is.True);
            Assert.That(seat, Is.EqualTo(2));
            Assert.That(received.Direction, Is.EqualTo(input.Direction));
            Assert.That(received.Lift, Is.EqualTo(input.Lift));
            Assert.That(received.Run && received.Guard && received.Act && !received.Crouch, Is.True);

            using var invalidWriter = new FastBufferWriter(ExpeditionPackets.InputSize, Allocator.Temp);
            ExpeditionPackets.WriteInput(invalidWriter, 0, new DinosaurInput(new Vector3(float.NaN, 0, 0), false, 0));
            using var invalidReader = new FastBufferReader(invalidWriter, Allocator.Temp);
            Assert.That(ExpeditionPackets.TryReadInput(invalidReader, out _, out _), Is.False);
        }

        [Test]
        public void PoseRoundTripsWithinQuantization()
        {
            var position = new Vector3(3.12f, 27.986f, IslandLayout.Center.y - 17.39f);
            var pose = new DinosaurPose(position, 347.1f, 1.25f, .17f, true, true, false, 132, false,
                38.62f, LifeActivity.Drink, true, false, true, false, .58f);
            using var writer = new FastBufferWriter(64, Allocator.Temp);
            ExpeditionPackets.WritePose(writer, pose);
            using var reader = new FastBufferReader(writer, Allocator.Temp);
            DinosaurPose result = ExpeditionPackets.ReadPose(reader);

            Assert.That(Vector3.Distance(result.Position, position), Is.LessThan(.065f));
            Assert.That(Mathf.DeltaAngle(result.Yaw, pose.Yaw), Is.EqualTo(0).Within(.003f));
            Assert.That(result.Health, Is.EqualTo(pose.Health));
            Assert.That(result.Activity, Is.EqualTo(pose.Activity));
            Assert.That(result.LifeClock, Is.EqualTo(pose.LifeClock).Within(.126f));
            Assert.That(result.Stamina, Is.EqualTo(pose.Stamina).Within(.002f));
            Assert.That(result.Flying && result.Diving && result.Crouch && result.Stagger && !result.Feed, Is.True);
        }

        [Test]
        public void SnapshotOfCurrentSceneFitsOneDatagram()
        {
            const int players = 5, fauna = 107, groups = 3;
            int largestGroup = (fauna + groups - 1) / groups;
            Assert.That(ExpeditionPackets.SnapshotSize(players, largestGroup), Is.LessThan(1000));
        }

        [Test]
        public void WetnessBuildsAndDriesAfterTheRain()
        {
            Assert.That(ValleyWeather.SurfaceWetness(false, 85), Is.Zero);
            Assert.That(ValleyWeather.SurfaceWetness(true, 61), Is.Zero);
            Assert.That(ValleyWeather.SurfaceWetness(true, 76), Is.InRange(.2f, .9f));
            Assert.That(ValleyWeather.SurfaceWetness(true, 85), Is.EqualTo(1));
            Assert.That(ValleyWeather.SurfaceWetness(true, 154), Is.InRange(.2f, .9f));
            Assert.That(ValleyWeather.SurfaceWetness(true, 174), Is.Zero);
        }
    }
}
