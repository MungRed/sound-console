# Security Policy

## Supported versions

| Version | Supported |
| --- | --- |
| 1.x | Yes |
| < 1.0 | No |

## Reporting a vulnerability

Please do **not** open a public issue for a vulnerability.

Use GitHub's private vulnerability reporting:

1. Open the repository's **Security** tab.
2. Choose **Report a vulnerability**.
3. Include a description, reproduction steps, affected version, and impact.

If private reporting is unavailable, open a minimal public issue asking the
maintainer to establish a private channel. Do not include exploit details or
personal data in that issue.

## Scope

Sound Console is a local Windows desktop application. It does not make network
requests, does not include telemetry, and does not record audio. Areas worth
scrutinizing include:

- Memory-safety issues in native/COM interop or unsafe audio conversion code.
- Path handling in settings, logging, or the data-directory buttons.
- Denial of service through malformed audio-device data.
- Supply-chain issues in NuGet dependencies or GitHub Actions.
- Accidental disclosure of local paths, device names, or user data in logs or
  release artifacts.

UI spoofing, social engineering, and issues that require an attacker to already
have local code execution or administrator rights are generally out of scope.

## Response

Reports are handled on a best-effort basis. Confirmed issues will be fixed in a
new release and credited in the release notes if the reporter wishes.

## Dependency updates

Dependabot is configured for NuGet packages and GitHub Actions. Security updates
should be kept small and reviewed with the normal CI checks.
