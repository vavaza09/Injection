using NUnit.Framework;
using Game.Persistence;
using Game.Tests.Fixtures;

namespace Game.Tests.Persistence
{
    public class SaveServiceTests
    {
        private FakeSaveStorage _storage;
        private FakeLogger _logger;
        private SaveService _sut;

        [SetUp]
        public void Setup()
        {
            _storage = new FakeSaveStorage();
            _logger  = new FakeLogger();
            _sut     = new SaveService(_storage, _logger);
        }

        [Test]
        public void HasSave_FalseWhenEmpty()
        {
            Assert.IsFalse(_sut.HasSave());
        }

        [Test]
        public void HasSave_TrueAfterSave()
        {
            _sut.Save(new SaveData());
            Assert.IsTrue(_sut.HasSave());
        }

        [Test]
        public void Save_ThenLoad_RoundTripsAllFields()
        {
            var data = new SaveData();
            data.checkpoint.roomId = "room_harbor";
            data.checkpoint.spawnPointId = "checkpoint_1";
            data.checkpoint.energy = 2;
            data.lastRoom.roomId = "room_caves";
            data.lastRoom.spawnPointId = "caves_entry";
            data.bossesDefeated.Add("crab_boss");

            _sut.Save(data);
            var loaded = _sut.Load();

            Assert.IsNotNull(loaded);
            Assert.AreEqual("room_harbor", loaded.checkpoint.roomId);
            Assert.AreEqual("checkpoint_1", loaded.checkpoint.spawnPointId);
            Assert.AreEqual(2, loaded.checkpoint.energy);
            Assert.AreEqual("room_caves", loaded.lastRoom.roomId);
            Assert.AreEqual("caves_entry", loaded.lastRoom.spawnPointId);
            Assert.AreEqual(1, loaded.bossesDefeated.Count);
            Assert.AreEqual("crab_boss", loaded.bossesDefeated[0]);
        }

        [Test]
        public void Save_WritesThroughToStorage()
        {
            _sut.Save(new SaveData());
            Assert.AreEqual(1, _storage.WriteCallCount);
        }

        [Test]
        public void Load_ReturnsNullWhenNoSave()
        {
            Assert.IsNull(_sut.Load());
        }

        [Test]
        public void Load_ReturnsNullOnVersionMismatch()
        {
            var bad = new SaveData { version = 999 };
            _storage.Write(bad);

            var result = _sut.Load();

            Assert.IsNull(result);
            Assert.AreEqual(1, _logger.Warnings.Count);
        }

        [Test]
        public void MarkBossDefeated_AddsId()
        {
            _sut.MarkBossDefeated("crab_boss");
            var data = _sut.Load();
            Assert.IsTrue(data.bossesDefeated.Contains("crab_boss"));
        }

        [Test]
        public void MarkBossDefeated_IsIdempotent()
        {
            _sut.MarkBossDefeated("crab_boss");
            _sut.MarkBossDefeated("crab_boss");
            var data = _sut.Load();
            Assert.AreEqual(1, data.bossesDefeated.Count);
        }

        [Test]
        public void MarkBossDefeated_PreservesExistingData()
        {
            var initial = new SaveData();
            initial.checkpoint.roomId = "room_harbor";
            initial.checkpoint.spawnPointId = "cp1";
            initial.checkpoint.energy = 3;
            _sut.Save(initial);

            _sut.MarkBossDefeated("boss_a");
            var data = _sut.Load();

            Assert.AreEqual("room_harbor", data.checkpoint.roomId);
            Assert.AreEqual("cp1", data.checkpoint.spawnPointId);
            Assert.AreEqual(3, data.checkpoint.energy);
        }

        [Test]
        public void IsBossDefeated_TrueAfterMark()
        {
            _sut.MarkBossDefeated("boss_a");
            Assert.IsTrue(_sut.IsBossDefeated("boss_a"));
        }

        [Test]
        public void IsBossDefeated_FalseOtherwise()
        {
            Assert.IsFalse(_sut.IsBossDefeated("boss_a"));
        }

        [Test]
        public void Delete_ClearsStorage()
        {
            _sut.Save(new SaveData());
            _sut.Delete();
            Assert.AreEqual(1, _storage.ClearCallCount);
        }

        [Test]
        public void Load_AfterDelete_ReturnsNull()
        {
            _sut.Save(new SaveData());
            _sut.Delete();
            Assert.IsNull(_sut.Load());
        }
    }
}
