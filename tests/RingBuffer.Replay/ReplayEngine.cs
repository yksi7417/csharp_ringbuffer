using System;
using System.Collections.Generic;
using System.IO;
using RingBuffer.Core;

namespace RingBuffer.Replay;

/// <summary>
/// A pure function from input journal to output journal.
///
/// No network, no clock, no filesystem beyond the two paths it is given. Every
/// source of ambient variation is closed off, because byte-for-byte diffing is only
/// meaningful if the output is a function of the input alone — otherwise the diff
/// produces false failures and gets disabled, which is how byte-exact suites die.
///
/// See knowledge/concepts/deterministic-replay.md.
/// </summary>
public sealed class ReplayEngine
{
    private readonly Func<IRingBuffer> _ringFactory;

    public ReplayEngine(Func<IRingBuffer> ringFactory) =>
        _ringFactory = ringFactory ?? throw new ArgumentNullException(nameof(ringFactory));

    /// <summary>
    /// Publishes every input frame into a ring and drains it, returning what came out.
    /// </summary>
    /// <remarks>
    /// The drain is deliberately <b>single-threaded</b>. This layer tests that the
    /// format and the logic are stable, not that the ring is thread-safe — that is
    /// L3's job, and mixing the two would make the corpus flaky and the concurrency
    /// results unreadable.
    /// </remarks>
    public IReadOnlyList<byte[]> Run(IReadOnlyList<byte[]> inputFrames, long seed = 0)
    {
        ArgumentNullException.ThrowIfNull(inputFrames);

        // Constructed but not yet consumed by any transform. They exist so a
        // transform added later cannot reach for DateTime.Now instead.
        _ = new SeededClock(seed);
        _ = new SeededIds(seed);

        var output = new List<byte[]>(inputFrames.Count);
        using var ring = _ringFactory();

        foreach (var frame in inputFrames)
        {
            if (!ring.TryClaim(frame.Length, msgTypeId: 1, out var claim))
            {
                // Drain and retry once: a full ring is a legitimate state, and the
                // replay must make progress rather than silently dropping a frame.
                ring.Read((_, payload) => output.Add(payload.ToArray()), int.MaxValue);
                if (!ring.TryClaim(frame.Length, msgTypeId: 1, out claim))
                {
                    throw new InvalidOperationException(
                        $"ring cannot accept a {frame.Length}-byte frame even when empty; " +
                        "the capacity is too small for this case");
                }
            }

            frame.CopyTo(claim.Span);
            claim.Commit(frame.Length);
        }

        ring.Read((_, payload) => output.Add(payload.ToArray()), int.MaxValue);
        return output;
    }

    /// <summary>The CLI contract: input path in, output path out, nothing else touched.</summary>
    public void Run(string inputPath, string outputPath, long seed = 0)
    {
        List<byte[]> input;
        using (var source = File.OpenRead(inputPath))
        {
            input = new List<byte[]>(Journal.ReadFrames(source));
        }

        var output = Run(input, seed);

        using var destination = File.Create(outputPath);
        foreach (var frame in output)
        {
            Journal.WriteFrame(destination, frame);
        }
    }
}
