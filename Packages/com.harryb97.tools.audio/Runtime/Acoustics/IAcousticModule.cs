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
        /// Writes the module's own output for each query (same index). The results arrive neutral: write the full output, not a
        /// change on top of other modules. The simulator keeps the last output of every module per voice and, each tick, reduces all of them
        /// in Order: volumes multiply, the lowest low-pass and the highest high-pass win, and the perceived position comes from the last
        /// module that sets one. A module skipped by its interval keeps contributing its last output.
        /// </summary>
        void Evaluate(ReadOnlySpan<AcousticQuery> _queries, Span<AcousticResult> _results);
    }
}
