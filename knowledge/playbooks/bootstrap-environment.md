---
type: Playbook
title: Bootstrap the environment
description: Get a machine or agent session able to build and test this repo.
tags: [toolchain, setup, ci]
generated: { by: process:claude-code-session, at: 2026-08-19T00:00:00Z }
status: stable
---

# Trigger

A fresh container, a new machine, or a web agent session where `dotnet` is missing.

# Steps

```bash
# 1. .NET SDK. Use apt -- dotnet-install.sh is egress-blocked. See F1.
apt-get update                      # REQUIRED first, or the install 404s on stale URLs
apt-get install -y dotnet-sdk-8.0
export PATH=$PATH:/usr/lib/dotnet

# 2. JDK 21 -- needed to run the SBE generator shim. See F2.
java -version

# 3. The SBE generator jar, at the pinned version.
curl -sSL -o tools/sbe-csharp-gen/sbe-all-<PINNED>.jar \
  https://repo1.maven.org/maven2/uk/co/real-logic/sbe-all/<PINNED>/sbe-all-<PINNED>.jar

# 4. Generate codecs and build.
scripts/generate-codecs.sh
dotnet build

# 5. Verify.
scripts/ci/gate.sh fast
```

# Gotchas

- **`apt-get update` first.** Without it the install fails with 404s that look like a broken
  mirror rather than a stale index. See [F1](/findings/f1-dotnet-install-egress-blocked.md).
- **Do not run `dotnet-install.sh`.** `builds.dotnet.microsoft.com` returns 403 on
  CONNECT behind the agent proxy.
- Java must be present **even though this is a C# project** — the codec generator is Java.
  This surprises people; see [F2](/findings/f2-csharp-codegen-requires-shim.md).
