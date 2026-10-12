using System.IO;
using NUnit.Framework;
using TrashCats.Infrastructure;

namespace TrashCats.Tests
{
    public sealed class JsonFileSaveStorageTests
    {
        private string _dir;
        private JsonFileSaveStorage _storage;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "trashcats-tests-" + System.Guid.NewGuid().ToString("N"));
            _storage = new JsonFileSaveStorage(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir))
                Directory.Delete(_dir, true);
        }

        [Test]
        public void NothingSaved_ReadsNull()
        {
            Assert.That(_storage.ReadMain(), Is.Null);
            Assert.That(_storage.ReadBackup(), Is.Null);
        }

        [Test]
        public void Write_ReplacesMainAndKeepsPreviousAsBackup()
        {
            _storage.Write("{\"n\":1}");
            Assert.That(_storage.ReadMain(), Is.EqualTo("{\"n\":1}"));
            Assert.That(_storage.ReadBackup(), Is.Null);

            _storage.Write("{\"n\":2}");
            Assert.That(_storage.ReadMain(), Is.EqualTo("{\"n\":2}"));
            Assert.That(_storage.ReadBackup(), Is.EqualTo("{\"n\":1}"));
            Assert.That(Directory.GetFiles(_dir, "*.tmp.json"), Is.Empty);
        }

        [Test]
        public void UnicodeContent_SurvivesRoundTrip()
        {
            _storage.Write("{\"name\":\"Пончик 🐱\"}");
            Assert.That(_storage.ReadMain(), Is.EqualTo("{\"name\":\"Пончик 🐱\"}"));
        }

        [Test]
        public void LeftoverTempFile_DoesNotAffectSaves()
        {
            _storage.Write("{\"n\":1}");
            File.WriteAllText(Path.Combine(_dir, "save.tmp.json"), "garbage from a crash");

            _storage.Write("{\"n\":2}");

            Assert.That(_storage.ReadMain(), Is.EqualTo("{\"n\":2}"));
        }

        [Test]
        public void WriteAside_AndDeleteAll()
        {
            _storage.Write("a");
            _storage.Write("b");
            _storage.WriteAside("../save.future.v9.json", "future");

            Assert.That(File.ReadAllText(Path.Combine(_dir, "save.future.v9.json")), Is.EqualTo("future"));

            _storage.DeleteAll();
            Assert.That(_storage.ReadMain(), Is.Null);
            Assert.That(_storage.ReadBackup(), Is.Null);
        }
    }
}
