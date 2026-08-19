# Vendored SBE C# runtime

**Do not edit these files.** They are upstream source, vendored verbatim.

| | |
|---|---|
| Upstream | https://github.com/aeron-io/simple-binary-encoding |
| Path | `csharp/sbe-dll/*.cs` |
| Tag | pinned in `/sbe-version.txt` |
| Licence | Apache-2.0, copyright Bill Segall, MarketFactory Inc, Adaptive Consulting |

## Why vendored rather than a NuGet reference

The published package is abandoned — `sbe-dll` last shipped 1.13.0 in ~2019, against a
current generator. Pairing them is version skew across a codegen boundary.
See [F3](../../knowledge/findings/f3-sbe-dll-nuget-stale.md).

## The rule

The generator jar version and this tag are **one decision, not two**. Bumping either alone
reintroduces exactly the skew we vendored to avoid.

To bump: change `/sbe-version.txt`, re-run `scripts/ci/checks/check_vendored_sbe.sh --update`,
and regenerate the codecs. `scripts/ci/gate.sh fast` verifies the two agree.
