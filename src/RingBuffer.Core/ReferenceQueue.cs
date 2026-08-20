using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace RingBuffer.Core;

/// <summary>
/// A trivially-correct <see cref="IRingBuffer"/>: a list of byte arrays.
///
/// <b>Deliberately slow and obviously right.</b> It copies on publish and on
/// consume, allocates freely, and has no wrap, no padding and no capacity
/// arithmetic. That is the point — it is the oracle the real rings are checked
/// against, so it must be correct by inspection rather than by testing.
/// </summary>
/// <remarks>
/// <para>
/// It exists to solve a bootstrapping problem: acceptance fixtures are produced by
/// the replay harness, which needs a ring, which is what we are trying to test.
/// This queue lets the corpus exist before either real ring does.
/// </para>
/// <para>
/// <b>Its limits, stated plainly.</b> It is an oracle for <em>payload
/// preservation</em> — message in, same message out, in order, exactly once. It is
/// <em>not</em> an oracle for ring semantics: it has no wrap, no padding records and
/// no capacity bound, so it can never disagree with a real ring about those. Those
/// are pinned by the exhaustive algebra tests in Phase 2 instead.
/// </para>
/// </remarks>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix",
    Justification = "It is a queue, and is named that throughout the knowledge bundle and " +
                    "IMPL/PLAN.md. Renaming it to satisfy the suffix heuristic would make " +
                    "the code disagree with the documentation that explains why it exists.")]
public sealed class ReferenceQueue : IRingBuffer
{
    private readonly Queue<(int MsgTypeId, byte[] Payload)> _messages = new();
    private readonly List<byte[]> _pending = new();
    private readonly List<int> _pendingTypeIds = new();
    private bool _disposed;

    public ReferenceQueue(int capacity = 1 << 20)
    {
        if (!Align.IsPowerOfTwo(capacity))
        {
            throw new ArgumentOutOfRangeException(
                nameof(capacity), capacity, "capacity must be a positive power of two");
        }

        Capacity = capacity;
    }

    public int Capacity { get; }

    /// <summary>Messages published and not yet consumed. For tests only.</summary>
    public int PendingCount => _messages.Count;

    public bool TryClaim(int maxPayloadLength, int msgTypeId, out Claim claim)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        RecordDescriptor.CheckTypeId(msgTypeId);

        if (maxPayloadLength < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxPayloadLength), maxPayloadLength, "must not be negative");
        }

        // Deliberately unbounded: this queue is an oracle, not a ring. Rejecting on
        // capacity here would make it disagree with a real ring for reasons that
        // have nothing to do with payload preservation.
        // ALLOW-ALLOC: this queue is the deliberately-slow oracle, not a ring. Allocating
        // freely is what makes it obviously correct by inspection, which is its whole job.
        var scratch = new byte[maxPayloadLength]; // ALLOW-ALLOC: oracle, never on the hot path
        _pending.Add(scratch);
        _pendingTypeIds.Add(msgTypeId);

        claim = new Claim(this, scratch, _pending.Count - 1);
        return true;
    }

    public void CommitClaim(int token, int actualPayloadLength)
    {
        var scratch = _pending[token];
        if (actualPayloadLength < 0 || actualPayloadLength > scratch.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(actualPayloadLength), actualPayloadLength,
                "must be between 0 and the claimed length");
        }

        var payload = new byte[actualPayloadLength]; // ALLOW-ALLOC: oracle, never on the hot path
        Array.Copy(scratch, payload, actualPayloadLength);
        _messages.Enqueue((_pendingTypeIds[token], payload));
        _pending[token] = Array.Empty<byte>();
    }

    public void AbortClaim(int token) => _pending[token] = Array.Empty<byte>();

    public int Read(MessageHandler handler, int messageCountLimit)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var read = 0;
        while (read < messageCountLimit && _messages.Count > 0)
        {
            var (msgTypeId, payload) = _messages.Dequeue();
            handler(msgTypeId, payload);
            read++;
        }

        return read;
    }

    public void Dispose()
    {
        _disposed = true;
        _messages.Clear();
        _pending.Clear();
        _pendingTypeIds.Clear();
    }
}
