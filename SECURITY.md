# Security Policy

## Supported Versions

Security fixes target the latest published stable release of TrafficLens.

| Version | Supported |
|---|---|
| v0.1.6 (latest stable) | Yes |

Older versions are not maintained. Please update to the latest release before
reporting an issue against them.

## Reporting a Vulnerability

**Please do not report security vulnerabilities as public GitHub Issues.**

TrafficLens has [GitHub Private Vulnerability Reporting](https://github.com/nabilety008/TrafficLens/security/advisories/new)
enabled. Use **Security → Report a vulnerability** on the repository page, or the
link above, to submit a report confidentially.

This opens a private advisory visible only to you and the maintainers, so a
vulnerability is not made public before it has been reviewed.

Please include, where applicable:

- The affected TrafficLens version
- The affected component or page
- Steps to reproduce the issue
- The impact you observed
- Proof-of-concept details, where safe to share
- A suggested mitigation, if you have one

Please do not disclose the vulnerability publicly until it has been reviewed and a
fix or guidance has been provided.

## Scope note

TrafficLens is a local, read-only network monitor. It does not capture packet
payload contents, and it does not block, throttle, or filter traffic. Please keep
reports within that scope.
