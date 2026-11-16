---
name: airnet
description: Read-only local network diagnostics with AirNet. Use for latency, jitter, loss, DNS/TTFB, IPv4/IPv6 and optional bandwidth checks. Never changes DNS or other system settings.
---

# AirNet

Use the headless CLI, not the desktop window.

```bash
airnet-cli scan --json
airnet-cli watch --json --interval 10 --count 3
airnet-cli history --json --limit 10
```

Rules:

1. Never run privileged commands or change DNS/proxy/hosts/firewall.
2. Ask before `--bandwidth`; it uploads about 4 MB.
3. `scan`/`watch` do not persist history.
4. Missing bandwidth means "not measured", not 0 Mbps.
5. Treat IPs, SSIDs and hostnames as private.
6. Report the evidence, not only the score. Read `llms.txt` for the full contract.
