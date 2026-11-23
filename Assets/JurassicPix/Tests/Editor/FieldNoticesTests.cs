using NUnit.Framework;

namespace JurassicPix
{
    public sealed class FieldNoticesTests
    {
        [Test]
        public void ShowsWaitedNoticesInOrderForTheirFullDuration()
        {
            var notices = new FieldNotices();
            notices.Enqueue("Nacimiento");
            notices.Enqueue("Juvenil");

            Assert.That(notices.Read(10, out float time), Is.EqualTo("Nacimiento"));
            Assert.That(time, Is.EqualTo(6));
            Assert.That(notices.Read(15.9f, out _), Is.EqualTo("Nacimiento"));
            Assert.That(notices.Read(18, out time), Is.EqualTo("Juvenil"));
            Assert.That(time, Is.EqualTo(6));
            Assert.That(notices.Read(24, out _), Is.Null);
        }

        [Test]
        public void NewLifeClearsVisibleAndPendingNotices()
        {
            var notices = new FieldNotices();
            notices.Enqueue("Vida anterior");
            notices.Read(2, out _);
            notices.Enqueue("Pendiente");
            notices.Clear();

            Assert.That(notices.Read(3, out _), Is.Null);
        }
    }
}
