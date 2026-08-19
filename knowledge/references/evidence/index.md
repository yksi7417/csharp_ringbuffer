# Research evidence

Throwaway probes, kept so the feasibility claims in [findings](/findings/index.md) can be
**re-run rather than trusted**. None of this is the project — it is the proof that the
project is buildable.

# Files

* [SbeCsharpGen.java](SbeCsharpGen.java) - The ~18-line shim that reaches `CSharpGenerator`. See [F2](/findings/f2-csharp-codegen-requires-shim.md).
* [probe-schema.xml](probe-schema.xml) - FIX-style schema: nested repeating group, var-length data, composite, enum, char array.
* [RoundTripProof.cs](RoundTripProof.cs) - Encode into `NativeMemory`, decode from the same address. See [F5](/findings/f5-end-to-end-roundtrip-proof.md).

# Reproduce

```bash
apt-get update && apt-get install -y dotnet-sdk-8.0     # dotnet-install.sh is egress-blocked -- F1
export PATH=$PATH:/usr/lib/dotnet

curl -sSL -o sbe-all-1.39.0.jar \
  https://repo1.maven.org/maven2/uk/co/real-logic/sbe-all/1.39.0/sbe-all-1.39.0.jar

# 1. The documented CLI path fails for C#. This is expected, and IS the finding:
java -Dsbe.target.language=CSharp -jar sbe-all-1.39.0.jar probe-schema.xml
#   -> IllegalArgumentException: No code generator for name: CSharp

# 2. The shim works:
javac -cp sbe-all-1.39.0.jar -d out SbeCsharpGen.java
java  -cp sbe-all-1.39.0.jar:out SbeCsharpGen probe-schema.xml ./gen
```

Then compile `RoundTripProof.cs` together with `./gen/**/*.cs` and the vendored
`csharp/sbe-dll/*.cs` from the SBE repo at tag `1.39.0`.

# Observed output

```
encoded bytes = 96
templateId=1 blockLength=8 transactTime=1700000000000
mdEntries count=2
  BID   ESZ6     px=5432e-2 sz=10 role=7 text=bid
  OFFER ESZ6     px=5433e-2 sz=20 role=8 text=ask
head32=08000100010000000068e5cf8b0100001a0002003045535a3620202020381500
```
