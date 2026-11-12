## Summary

What changed and why?

## Verification

- [ ] `dotnet build AirNet.slnx -c Release -warnaserror`
- [ ] `dotnet test tests/AirNet.Core.Tests/AirNet.Core.Tests.csproj -c Release`
- [ ] Manual UI check (describe) or screenshot attached
- [ ] `packaging/pack-all.ps1` considered for release-visible changes

## Checklist

- [ ] No real public IPs, SSIDs, DNS servers, or local history files added
- [ ] New dependency documented in `THIRD_PARTY_NOTICES.md`
- [ ] `CHANGELOG.md` updated for user-visible changes
- [ ] Windows-only code is guarded with `OperatingSystem.IsWindows()`
- [ ] No `bin/`, `obj/`, or `artifacts/` files included
