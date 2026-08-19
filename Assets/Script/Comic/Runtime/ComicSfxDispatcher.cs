using System.Collections.Generic;

namespace Game.Comic
{
    /// <summary>
    /// Owns delayed-start and beat-ranged SFX playback for one comic playback context, driving an
    /// injected <see cref="IComicSfxBackend"/> to actually make sound. Pure-function-of-beat,
    /// mirroring <c>ComicPageView.ApplyBeat</c>'s contract: call <see cref="Reconcile"/> every time
    /// the current beat changes — one step forward in real Play Mode, or an arbitrary jump from the
    /// Comic Editor's scrub bar — and it starts/stops/cancels exactly what belongs at that beat,
    /// regardless of how playback got there. This one class is used by both <c>ComicPlayer</c> and
    /// <c>ComicEditorWindow</c>, each owning its own instance (never shared) with its own backend,
    /// so the two stay behaviorally identical without duplicated reconciliation logic.
    /// </summary>
    public class ComicSfxDispatcher
    {
        private readonly Dictionary<ComicBeatEvent, float> _pendingElapsed = new();
        private readonly Dictionary<ComicBeatEvent, object> _activeRange = new();
        private readonly IComicSfxBackend _backend;

        // Guards against re-firing one-shots (which, unlike range/pending SFX, have no ongoing
        // state to dedupe against) when Reconcile is called again for the exact same (page, beat)
        // it was just called for — e.g. the Comic Editor re-reconciling at an unchanged beat after
        // an unrelated field edit triggers a rebuild. StopAll() resets this, so a genuine context
        // reset (page transition, or a rebuild after editing this event's own SFX fields) still
        // re-fires fresh; only a bare repeat call is suppressed.
        private ComicPage _lastReconciledPage;
        private int _lastReconciledBeat;
        private bool _lastReconcileValid;

        public ComicSfxDispatcher(IComicSfxBackend backend)
        {
            _backend = backend;
        }

        /// <summary>Advances all pending delay timers by dt and fires any that have elapsed.
        /// Call once per frame, unscaled/real time, while a page is loaded — comics can play
        /// with Time.timeScale == 0 (PauseStack), so a scaled delay would never elapse.</summary>
        private readonly List<ComicBeatEvent> _tickKeysScratch = new();

        public void Tick(float dt)
        {
            if (_pendingElapsed.Count == 0) return;

            // Snapshot keys first — writing to _pendingElapsed[key] via the indexer below, even
            // for a key that already exists, bumps Dictionary's version stamp and would throw
            // "Collection was modified" if done while still foreach-ing the dictionary itself.
            _tickKeysScratch.Clear();
            _tickKeysScratch.AddRange(_pendingElapsed.Keys);

            List<ComicBeatEvent> toFire = null;
            foreach (var e in _tickKeysScratch)
            {
                float elapsed = _pendingElapsed[e] + dt;
                _pendingElapsed[e] = elapsed;
                if (elapsed >= e.sfxDelay)
                    (toFire ??= new List<ComicBeatEvent>()).Add(e);
            }
            if (toFire == null) return;

            foreach (var e in toFire)
            {
                _pendingElapsed.Remove(e);
                Fire(e);
            }
        }

        /// <summary>Call whenever the current beat changes — a normal +1 Advance, a fresh page's
        /// beat 0, or an editor scrub jump of any size. Cancels pending delayed starts and stops
        /// active range instances that are no longer valid for <paramref name="beat"/>, then
        /// starts (or schedules, if delayed) anything newly valid — including SFX whose range
        /// window was jumped into mid-span rather than entered at its start beat.</summary>
        public void Reconcile(ComicPage page, int beat)
        {
            if (page == null) return;
            if (_lastReconcileValid && ReferenceEquals(page, _lastReconciledPage) && beat == _lastReconciledBeat)
                return; // exact repeat of the immediately-preceding call — nothing to do
            _lastReconciledPage = page;
            _lastReconciledBeat = beat;
            _lastReconcileValid = true;

            if (_pendingElapsed.Count > 0)
            {
                List<ComicBeatEvent> stale = null;
                foreach (var e in _pendingElapsed.Keys)
                    if (!e.IsActiveAtBeat(beat)) (stale ??= new List<ComicBeatEvent>()).Add(e);
                if (stale != null)
                    foreach (var e in stale) _pendingElapsed.Remove(e);
            }

            if (_activeRange.Count > 0)
            {
                List<ComicBeatEvent> ended = null;
                foreach (var e in _activeRange.Keys)
                    if (!e.IsActiveAtBeat(beat)) (ended ??= new List<ComicBeatEvent>()).Add(e);
                if (ended != null)
                {
                    foreach (var e in ended)
                    {
                        _backend.StopRange(_activeRange[e]);
                        _activeRange.Remove(e);
                    }
                }
            }

            for (int i = 0; i < page.beatEvents.Count; i++)
            {
                var e = page.beatEvents[i];
                if (string.IsNullOrEmpty(e.sfxName)) continue;
                if (!e.IsActiveAtBeat(beat)) continue;
                if (_pendingElapsed.ContainsKey(e) || _activeRange.ContainsKey(e)) continue;

                if (e.sfxDelay > 0f) _pendingElapsed[e] = 0f;
                else Fire(e);
            }
        }

        /// <summary>Stops and cancels everything. Call on page transitions and sequence end/skip
        /// so a range or delayed SFX never bleeds into a page it doesn't belong to.</summary>
        public void StopAll()
        {
            _pendingElapsed.Clear();
            foreach (var handle in _activeRange.Values) _backend.StopRange(handle);
            _activeRange.Clear();
            _lastReconcileValid = false;
        }

        private void Fire(ComicBeatEvent e)
        {
            if (e.IsRangeMode)
            {
                var handle = _backend.StartRange(e.sfxName);
                if (handle != null) _activeRange[e] = handle;
            }
            else
            {
                _backend.PlayOneShot(e.sfxName);
            }
        }
    }
}
