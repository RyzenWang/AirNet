# AirNet for agents

AirNet is read-only by default. The safe path is the headless CLI, which does not initialize
Avalonia and does not append to history.

## Commands

```bash
# build once
dotnet build src/AirNet.Cli/AirNet.Cli.csproj -c Release

# one measurement as JSON on stdout
dotnet run --project src/AirNet.Cli -c Release -- scan --json

# continuous NDJSON, one line per cycle
dotnet run --project src/AirNet.Cli -c Release -- watch --json --interval 10 --count 3

# recent local history
dotnet run --project src/AirNet.Cli -c Release -- history --json --limit 10
```

The packaged Windows executable is `airnet-cli.exe` when the CLI is published separately.
The desktop app and the CLI share the same `AirNet.Core` measurement stack.

## Output contract

- `scan --json` and `watch --json` use `ProbeJson`, schema version 1, camelCase properties.
- `watch --json` is NDJSON: each line is a complete object; blank/partial lines are not
  emitted.
- `scan` exit code carries the verdict; do not parse the score to decide whether a command
  failed. A tool/runtime failure is code 3.
- `downloadMbps`/`uploadMbps` are 0 when bandwidth was not requested or failed. Explain
  that as "not measured", not "0 Mbps".

## Boundaries

Do not:
- run `ApplyDns`, `RestoreDns`, or any elevated command;
- alter DNS, proxy, hosts, firewall or adapter settings;
- run `--bandwidth` without explicit permission;
- upload a diagnostic bundle or history without explicit permission.

Do:
- prefer a single `scan` for a question, and `watch` only when the user asks for monitoring;
- show the user the verdict and the evidence (latency/jitter/loss/timing), not just a score;
- distinguish "ICMP blocked" from "packet loss" using the `interface`/`diagnostics` data;
- keep SSIDs, local addresses and public egress IPs out of public summaries.

## Text-only smoke test

```bash
airnet-cli scan --json | jq '{score, grade, latency, downloadMbps, uploadMbps}'
airnet-cli watch --json --count 2 | jq -c '.score'
```
