# Findings

Verified facts about the toolchain, each one established by **running it in a container**
rather than by reading documentation. Three of them contradict what the public docs imply.

**Read these before fighting the build.** They are the answers to "why is this set up so
strangely".

# Critical path

* [F2: SBE's documented C# codegen path does not work](f2-csharp-codegen-requires-shim.md) - `-Dsbe.target.language=CSharp` cannot reach the generator on any current release. A Java shim is required. **This finding is why the research phase existed.**
* [F3: The sbe-dll NuGet package is abandoned](f3-sbe-dll-nuget-stale.md) - 1.13.0 from ~2019 against a 1.39.0 generator. Vendor the 1,851-line runtime at a pinned tag.
* [F4: DirectBuffer wraps a raw pointer with no GC handle](f4-directbuffer-native-pointer.md) - The API that makes the zero-copy claim reachable at all.
* [F5: The full chain works end to end](f5-end-to-end-roundtrip-proof.md) - Nested groups and var-length data, encoded into native memory and decoded from the same address.

# Environment

* [F1: dotnet-install.sh is egress-blocked](f1-dotnet-install-egress-blocked.md) - Bootstrap must use apt, or web agent sessions cannot build.
* [F6: All required NuGet packages are available](f6-package-availability.md) - xUnit v3, Reqnroll, Coyote, FsCheck, BenchmarkDotNet, Verify.
* [F7: Reqnroll pins xUnit v2](f7-reqnroll-pins-xunit-v2.md) - The acceptance project cannot use xunit.v3. Both versions coexist in the solution, but not in one project.
