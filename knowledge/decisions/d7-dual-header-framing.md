---
type: Decision
title: "D7: Keep the ring record header and the SBE MessageHeader separate"
description: 8 bytes of transport framing plus 8 bytes of codec framing. 16 bytes per message, deliberately.
tags: [ring-buffer, sbe, framing]
generated: { by: process:claude-code-session, at: 2026-08-19T00:00:00Z }
status: stable
---

# Context

Both the ring and SBE want a header at the front of every message, and they overlap
slightly — both carry something length-like and something type-like. Collapsing them would
save 8 bytes per message.

# Decision

**Keep both, layered.**

| Bytes | Header | Owner | Contents |
|---|---|---|---|
| 0-7 | ring record header | transport | `length` (int32), `type` (int32) |
| 8-15 | SBE `MessageHeader` | codec | `blockLength`, `templateId`, `schemaId`, `version` |

The ring header is transport framing and stays **opaque to SBE**. The SBE header is the first
8 bytes of the ring's payload and is what makes schema evolution work.

# Consequences

- 16 bytes of overhead per message, a fixed cost worth documenting rather than optimising.
- **Collapsing them would couple the transport to the codec**, which would make the ring
  unusable for any non-SBE payload and would break the layering the teaching material
  depends on. The 8 bytes are buying a boundary.
- The ring's `type` field stays free for transport-level concerns (padding sentinel,
  future control records) rather than being overloaded with `templateId`.
