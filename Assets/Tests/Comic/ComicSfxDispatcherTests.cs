using System.Collections.Generic;
using NUnit.Framework;
using Game.Comic;

namespace Game.Tests.Comic
{
    /// <summary>Records calls instead of playing anything — the same fake-backend pattern the
    /// project already uses for FakeDashHandler/FakeSaveStorage, made possible here because
    /// ComicSfxDispatcher depends only on IComicSfxBackend, never SoundManager directly.</summary>
    public class FakeComicSfxBackend : IComicSfxBackend
    {
        public readonly List<string> OneShotCalls = new();
        public readonly List<string> StartRangeCalls = new();
        public readonly List<object> StopRangeCalls = new();

        private int _nextHandle;

        public void PlayOneShot(string sfxName) => OneShotCalls.Add(sfxName);

        public object StartRange(string sfxName)
        {
            StartRangeCalls.Add(sfxName);
            return ++_nextHandle; // boxed int identity is enough to track "which instance"
        }

        public void StopRange(object handle) => StopRangeCalls.Add(handle);
    }

    public class ComicSfxDispatcherTests
    {
        private static ComicPage PageWith(params ComicBeatEvent[] events)
        {
            var page = new ComicPage();
            page.beatEvents.AddRange(events);
            return page;
        }

        [Test]
        public void Reconcile_LegacyOneShot_FiresImmediatelyOnItsBeat()
        {
            var backend = new FakeComicSfxBackend();
            var dispatcher = new ComicSfxDispatcher(backend);
            var e = new ComicBeatEvent { beatIndex = 2, sfxName = "JUMP" };
            var page = PageWith(e);

            dispatcher.Reconcile(page, 2);

            Assert.AreEqual(new[] { "JUMP" }, backend.OneShotCalls.ToArray());
            Assert.IsEmpty(backend.StartRangeCalls);
        }

        [Test]
        public void Reconcile_LegacyOneShot_DoesNotRefireOnRepeatedReconcileAtSameBeat()
        {
            var backend = new FakeComicSfxBackend();
            var dispatcher = new ComicSfxDispatcher(backend);
            var e = new ComicBeatEvent { beatIndex = 2, sfxName = "JUMP" };
            var page = PageWith(e);

            dispatcher.Reconcile(page, 2);
            dispatcher.Reconcile(page, 2);
            dispatcher.Reconcile(page, 2);

            Assert.AreEqual(1, backend.OneShotCalls.Count);
        }

        [Test]
        public void Reconcile_RangeMode_StartsOnFirstBeatAndStopsWhenLeavingWindow()
        {
            var backend = new FakeComicSfxBackend();
            var dispatcher = new ComicSfxDispatcher(backend);
            var e = new ComicBeatEvent { beatIndex = 3, sfxName = "ENGINE_LOOP", endBeatIndex = 6 };
            var page = PageWith(e);

            dispatcher.Reconcile(page, 3);
            Assert.AreEqual(1, backend.StartRangeCalls.Count);
            Assert.IsEmpty(backend.StopRangeCalls);

            dispatcher.Reconcile(page, 5); // still inside [3,6)
            Assert.AreEqual(1, backend.StartRangeCalls.Count, "should not restart while still in-range");
            Assert.IsEmpty(backend.StopRangeCalls);

            dispatcher.Reconcile(page, 6); // exclusive boundary — out of range
            Assert.AreEqual(1, backend.StopRangeCalls.Count);
        }

        [Test]
        public void Reconcile_RangeMode_JumpingStraightIntoMiddleOfWindowStillStartsIt()
        {
            var backend = new FakeComicSfxBackend();
            var dispatcher = new ComicSfxDispatcher(backend);
            var e = new ComicBeatEvent { beatIndex = 3, sfxName = "ENGINE_LOOP", endBeatIndex = 6 };
            var page = PageWith(e);

            // Never visits beat 3 — jumps straight to beat 4, still inside [3,6).
            dispatcher.Reconcile(page, 4);

            Assert.AreEqual(1, backend.StartRangeCalls.Count);
        }

        [Test]
        public void Reconcile_NoEndBeatSpecified_DefaultsToSingleBeatWindow()
        {
            var backend = new FakeComicSfxBackend();
            var dispatcher = new ComicSfxDispatcher(backend);
            // endBeatIndex left at -1 (legacy) vs explicitly 0 would both be "no range"; here we
            // simulate the editor's "range toggled on, no end typed" case: endBeatIndex == beatIndex.
            var e = new ComicBeatEvent { beatIndex = 5, sfxName = "ENGINE_LOOP", endBeatIndex = 5 };
            var page = PageWith(e);

            dispatcher.Reconcile(page, 5);
            Assert.AreEqual(1, backend.StartRangeCalls.Count);

            dispatcher.Reconcile(page, 6); // effective end clamps to beatIndex+1 = 6, exclusive
            Assert.AreEqual(1, backend.StopRangeCalls.Count);
        }

        [Test]
        public void Tick_FiresDelayedOneShotOnceElapsed()
        {
            var backend = new FakeComicSfxBackend();
            var dispatcher = new ComicSfxDispatcher(backend);
            var e = new ComicBeatEvent { beatIndex = 0, sfxName = "JUMP", sfxDelay = 1f };
            var page = PageWith(e);

            dispatcher.Reconcile(page, 0);
            Assert.IsEmpty(backend.OneShotCalls, "should not fire before the delay elapses");

            dispatcher.Tick(0.5f);
            Assert.IsEmpty(backend.OneShotCalls, "0.5s < 1s delay");

            dispatcher.Tick(0.5f);
            Assert.AreEqual(new[] { "JUMP" }, backend.OneShotCalls.ToArray());
        }

        [Test]
        public void Tick_MultiplePendingDelaysDoNotThrowOrInterfereWithEachOther()
        {
            // Regression test: an earlier version mutated the pending dictionary's values via the
            // indexer while foreach-ing it, which throws "Collection was modified" even though the
            // key set never changed. Two-plus concurrent pending delays reproduce it reliably.
            var backend = new FakeComicSfxBackend();
            var dispatcher = new ComicSfxDispatcher(backend);
            var a = new ComicBeatEvent { beatIndex = 0, sfxName = "JUMP", sfxDelay = 0.2f };
            var b = new ComicBeatEvent { beatIndex = 0, sfxName = "DASH", sfxDelay = 0.5f };
            var page = PageWith(a, b);

            dispatcher.Reconcile(page, 0);

            Assert.DoesNotThrow(() => dispatcher.Tick(0.3f));
            Assert.AreEqual(new[] { "JUMP" }, backend.OneShotCalls.ToArray());

            Assert.DoesNotThrow(() => dispatcher.Tick(0.3f));
            Assert.AreEqual(new[] { "JUMP", "DASH" }, backend.OneShotCalls.ToArray());
        }

        [Test]
        public void Reconcile_CancelsPendingDelayIfBeatLeavesBeforeItFires()
        {
            var backend = new FakeComicSfxBackend();
            var dispatcher = new ComicSfxDispatcher(backend);
            var e = new ComicBeatEvent { beatIndex = 0, sfxName = "JUMP", sfxDelay = 5f };
            var page = PageWith(e);

            dispatcher.Reconcile(page, 0);
            dispatcher.Reconcile(page, 1); // beat moved on well before the 5s delay could elapse

            dispatcher.Tick(10f); // more than enough time, but should be cancelled by now
            Assert.IsEmpty(backend.OneShotCalls, "delayed SFX must not fire late on the wrong beat");
        }

        [Test]
        public void StopAll_StopsActiveRangesAndCancelsPendingDelays()
        {
            var backend = new FakeComicSfxBackend();
            var dispatcher = new ComicSfxDispatcher(backend);
            var range = new ComicBeatEvent { beatIndex = 0, sfxName = "ENGINE_LOOP", endBeatIndex = 4 };
            var delayed = new ComicBeatEvent { beatIndex = 0, sfxName = "JUMP", sfxDelay = 5f };
            var page = PageWith(range, delayed);

            dispatcher.Reconcile(page, 0);
            dispatcher.StopAll();

            Assert.AreEqual(1, backend.StopRangeCalls.Count);

            dispatcher.Tick(10f);
            Assert.IsEmpty(backend.OneShotCalls, "StopAll must cancel pending delays too, not just active ranges");
        }
    }
}
