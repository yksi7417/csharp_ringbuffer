using System;

namespace RingBuffer.Core;

/// <summary>
/// Invoked once per committed record during <see cref="IRingBuffer.Read"/>.
/// The span points <em>into the ring's memory</em> and is valid only for the
/// duration of the call — copying it out is the caller's decision, not the ring's.
/// </summary>
public delegate void MessageHandler(int msgTypeId, ReadOnlySpan<byte> payload);

/// <summary>
/// A ring buffer carrying opaque byte payloads.
///
/// Deliberately knows nothing about SBE: the ring is transport, the codec is a
/// payload concern (D7). That separation is what lets the same conformance suite
/// run against the SPSC ring, the MPSC ring, and a trivially-correct reference
/// queue — see knowledge/practices/triangulation.md.
/// </summary>
public interface IRingBuffer : IDisposable
{
    /// <summary>Bytes in the data region. Always a power of two.</summary>
    int Capacity { get; }

    /// <summary>
    /// Reserves room for up to <paramref name="maxPayloadLength"/> bytes.
    /// <b>Never blocks</b> — returns false when the buffer is full, and backpressure
    /// is the caller's policy (D3).
    /// </summary>
    /// <remarks>
    /// The claim is a <em>bound</em>, not a promise: SBE variable-length data means
    /// the true length is unknown until encoding finishes, so the producer claims
    /// generously and commits the truth (D1).
    /// </remarks>
    bool TryClaim(int maxPayloadLength, int msgTypeId, out Claim claim);

    /// <summary>
    /// Passes up to <paramref name="messageCountLimit"/> committed records to
    /// <paramref name="handler"/>. Returns how many were read.
    /// </summary>
    int Read(MessageHandler handler, int messageCountLimit);

    /// <summary>
    /// Publishes a claim. <b>Called by <see cref="Claim.Commit"/>, not directly.</b>
    /// </summary>
    void CommitClaim(int token, int actualPayloadLength);

    /// <summary>
    /// Abandons a claim, turning the whole reservation into padding.
    /// <b>Called by <see cref="Claim.Abort"/>, not directly.</b>
    /// </summary>
    void AbortClaim(int token);
}

/// <summary>
/// A reserved span of the ring that the producer may write into but has not yet
/// published.
/// </summary>
/// <remarks>
/// A <c>ref struct</c> deliberately: a claim that outlived its stack frame would be
/// a pointer into memory the consumer may already have reclaimed and zeroed. The
/// compiler prevents it — it cannot be boxed, stored in a field, or captured by a
/// lambda — which is a stronger guarantee than a code review.
/// </remarks>
public readonly ref struct Claim
{
    private readonly IRingBuffer _owner;
    private readonly Span<byte> _span;
    private readonly int _token;

    public Claim(IRingBuffer owner, Span<byte> span, int token)
    {
        _owner = owner;
        _span = span;
        _token = token;
    }

    /// <summary>Writable memory inside the ring. Encode straight into this — that is the zero copy.</summary>
    public Span<byte> Span => _span;

    /// <summary>
    /// Publishes <paramref name="actualPayloadLength"/> bytes. Any claimed space
    /// beyond that becomes a padding record (D1).
    /// </summary>
    public void Commit(int actualPayloadLength) => _owner.CommitClaim(_token, actualPayloadLength);

    /// <summary>Abandons the claim. The whole reservation becomes padding.</summary>
    public void Abort() => _owner.AbortClaim(_token);
}
