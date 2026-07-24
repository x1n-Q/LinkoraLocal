# Security policy

## Reporting a vulnerability

Do not open a public issue for an unpatched vulnerability. Send the report to
`security@linkora.top` with:

- the affected Linkora Local version;
- reproduction steps;
- the expected and observed behavior;
- any proof of concept that does not contain another user's data.

Reports concerning malicious hostnames or published content belong at
`abuse@linkora.top`.

## Client security boundaries

Linkora Local:

- never requests or stores a Linkora password;
- accepts device access only after browser approval with a matching code;
- stores the desktop token in Windows Credential Manager;
- does not store the Cloudflare tunnel credential;
- invokes `cloudflared` without a command shell;
- publishes only explicit HTTP or HTTPS loopback URLs;
- excludes known sensitive ports and system, database, browser, and editor
  processes;
- requires an explicit Publish action;
- stops the connector when the user chooses Stop sharing or quits the app.

The local Windows user can inspect processes owned by that same user, including
their command lines. A compromised Windows account is outside the protection
boundary of Linkora Local.

## Server security boundaries

- Device codes contain 256 bits of random data and expire after ten minutes.
- Human confirmation codes are rate limited and cannot issue a token without
  the secret device code.
- Desktop tokens are stored as SHA-256 hashes by the Linkora API.
- Tokens expire after 30 days and can be revoked from the app.
- Desktop bearer tokens have an API route allowlist.
- Tunnel connection requests must belong to the authenticated member.
- Local service targets must be loopback addresses with explicit ports.
- Security-sensitive actions are written to the Linkora audit log.

## Release requirements

Official releases should:

1. build from a tagged public commit;
2. use a clean CI worker;
3. include a pinned official `cloudflared` binary;
4. publish checksums and an SBOM;
5. be signed by Microsoft Store or the Linkora code-signing identity;
6. pass the repository's source and secret checks.
