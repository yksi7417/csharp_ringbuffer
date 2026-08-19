---
type: Concept
title: SBE encoding and FIX repeating groups
description: How the flyweight codecs work, and the shared-limit trap that nested groups expose.
tags: [sbe, fix, codec, core]
generated: { by: process:claude-code-session, at: 2026-08-19T00:00:00Z }
status: stable
---

# The flyweight model

SBE codecs are **flyweights**: they hold no data. A codec instance wraps a buffer at an
offset and reads or writes through to it. This is what makes zero copy possible, and it is
why codec instances can be reused across messages without allocating.

```csharp
encoder.WrapForEncodeAndApplyHeader(buffer, offset, headerEncoder);
decoder.WrapForDecodeAndApplyHeader(buffer, offset, headerDecoder);
```

# Fixed block, then groups, then var-data

An SBE message is laid out in three regions, **in this order**:

1. The **fixed block** — every scalar field, at a compile-time-known offset. Random access.
2. **Repeating groups** — a `groupSizeEncoding` dimension (`blockLength`, `numInGroup`)
   followed by that many entries. Sequential access only.
3. **Variable-length data** — a length prefix followed by bytes.

Regions 2 and 3 are why the encoded length is not known up front, which is the whole of
[D1](/decisions/d1-claim-commit-with-padding.md).

# The shared-limit trap

This is the bug that nested groups exist to catch, and the reason
[D8](/decisions/d8-schema-selection.md) insists on a nested group in the schema.

Groups and var-data are read through a **`Limit` cursor on the parent message object**, not
through per-group state. Walking a group advances that shared cursor. So:

- Walking an **inner** group advances the **outer** message's limit.
- Reading fields in the wrong order, or re-reading a group, silently returns garbage —
  there is no exception, just wrong values.
- The order in which you read must match the order the schema declares.

A single-level group hides this, because there is only one cursor user. A nested group
exposes it immediately. Verified generating and round-tripping correctly in
[F5](/findings/f5-end-to-end-roundtrip-proof.md).

# Schema evolution

The `MessageHeader` carries `schemaId` and `version`. A decoder wrapped with an
`actingVersion` lower than its own compiled version knows which fields were absent, and
returns the null value for them.

This is why v2 may only **append** fields and groups. Reordering or removing breaks every
message ever written. The v1/v2 pair in [D8](/decisions/d8-schema-selection.md) turns this
into an executable test rather than a rule people remember.
