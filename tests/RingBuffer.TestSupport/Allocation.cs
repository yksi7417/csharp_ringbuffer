using System;

namespace RingBuffer.TestSupport;

/// <summary>
/// Task 1.9. Measures managed allocation on the calling thread.
///
/// Allocation count is <em>deterministic</em>, unlike latency, which is why it can
/// block a PR without ever being flaky — and why it, rather than a benchmark, is
/// what actually protects the zero-copy claim (S2). See
/// knowledge/concepts/zero-copy-in-dotnet.md.
/// </summary>
public static class Allocation
{
    /// <summary>
    /// Bytes allocated by <paramref name="action"/> per iteration, after warmup.
    /// </summary>
    /// <remarks>
    /// Warmup matters and is easy to get wrong. The first calls JIT the method,
    /// build generic dictionaries and populate statics — all of which allocate,
    /// none of which is the code under test. Measuring without warmup reports a
    /// few hundred bytes for an allocation-free loop and teaches everyone to
    /// distrust the check.
    /// </remarks>
    public static long BytesPerIteration(Action action, int warmup = 200, int iterations = 1000)
    {
        ArgumentNullException.ThrowIfNull(action);

        for (var i = 0; i < warmup; i++)
        {
            action();
        }

        // A collection between the two reads does not distort the result:
        // GetAllocatedBytesForCurrentThread is cumulative allocation, not live heap.
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < iterations; i++)
        {
            action();
        }
        var after = GC.GetAllocatedBytesForCurrentThread();

        return (after - before) / iterations;
    }

    /// <summary>Total bytes allocated by <paramref name="action"/>, warmed up first.</summary>
    public static long Bytes(Action action, int warmup = 200)
    {
        ArgumentNullException.ThrowIfNull(action);

        for (var i = 0; i < warmup; i++)
        {
            action();
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        action();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }
}
