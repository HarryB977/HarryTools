using System;

namespace HarryTools.Audio {
    /// <summary>
    /// Computes acoustic effects (occlusion, reverb, ...) for the voices the simulator picked. The core has no geometry code:
    /// modules are registered with <see cref="Audio.RegisterModule"/>. Evaluate must not allocate if the game wants zero GC.
    /// A module that throws is logged and unregistered.
    /// </summary>
    public interface IAcousticModule {
        /// <summary> Evaluation order; lower runs first. Ties keep the registration order. </summary>
        int Order { get; }

        /// <summary> Seconds between evaluations; 0 evaluates every tick. Between evaluations the last result of each voice keeps being applied. </summary>
        float UpdateInterval { get; }

        /// <summary>
        /// Modifies the results in place, one per query (same index). The results arrive holding what the previous modules produced
        /// (neutral for the first), so a module combines with them: multiply the volume, take the lowest low-pass, and so on.
        /// A module skipped by its interval re-applies the output it had the last time it ran.
        /// </summary>
        void Evaluate(ReadOnlySpan<AcousticQuery> _queries, Span<AcousticResult> _results);
    }
}
