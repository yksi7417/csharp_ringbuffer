---
type: Finding
title: "F1: dotnet-install.sh is egress-blocked; bootstrap must use apt"
description: builds.dotnet.microsoft.com returns 403 on CONNECT in the sandboxed environment.
tags: [toolchain, ci, environment]
generated: { by: process:claude-code-session, at: 2026-08-19T00:00:00Z }
status: stable
---

# What was observed

```
curl -sSL https://dot.net/v1/dotnet-install.sh
  -> curl: (56) CONNECT tunnel failed, response 403
```

The proxy status endpoint names the host:

```json
{ "kind": "connect_rejected", "host": "builds.dotnet.microsoft.com:443",
  "detail": "gateway answered 403 to CONNECT (policy denial or upstream failure)" }
```

# What works

```bash
apt-get update && apt-get install -y dotnet-sdk-8.0    # -> 8.0.130 at /usr/lib/dotnet
```

`apt-get update` is **required first** — without it the install fails with 404s on stale
package URLs, which looks like a broken mirror rather than a stale index.

Both `api.nuget.org` and `repo1.maven.org` are reachable (HTTP 200), so package restore
and the SBE jar download are fine. Only the SDK installer host is blocked.

# Consequence

`scripts/bootstrap.sh` uses apt, not the Microsoft install script, and a `SessionStart`
hook runs it. Otherwise web-based agent sessions cannot build the project at all. See
[the bootstrap playbook](/playbooks/bootstrap-environment.md).
