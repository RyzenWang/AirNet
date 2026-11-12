# Security policy

## Supported versions

Security fixes are provided for the latest published `0.2.x` release. Older portable
archives are kept for reproducibility but are not actively patched.

## Reporting a vulnerability

Please do **not** open a public issue for a vulnerability.

Use GitHub's private vulnerability reporting:

1. Open the repository's **Security** tab.
2. Choose **Report a vulnerability**.
3. Include the affected version, platform, reproduction steps, and impact.

If private reporting is unavailable, contact the maintainer listed on the repository
profile and ask for a private channel before sharing details.

## Scope

AirNet is a local network diagnostic tool. It does not upload measurements, does not
run a background service, and does not install a driver. Relevant reports include:

- command injection or argument injection in subprocess calls;
- privilege escalation or unsafe UAC handling in the DNS/MTU helpers;
- unintended network egress or secret leakage;
- unsafe archive/installation behavior in a release asset;
- crashes caused by hostile DNS responses, traceroute output, or site metadata.

The following are not security issues by themselves:

- a measurement being wrong because the local network is unusual;
- Google/YouTube/X being unreachable in a restricted network;
- warnings from Windows SmartScreen or macOS Gatekeeper for unsigned builds;
- the app requiring a desktop session (X11/Wayland) on Linux.

## Release artifacts

Release assets are built by CI from the tagged commit. The portable packages are not
code-signed, so verify the SHA256 published in the release notes before running them.
