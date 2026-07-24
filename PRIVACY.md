# Privacy

Linkora Local does not include analytics, advertising, crash reporting, or
behavioral telemetry.

## Data that stays on the computer

- the complete list of listening local ports;
- process identifiers and process names;
- HTTP page titles discovered during local probing;
- the Linkora desktop access token, stored in Windows Credential Manager;
- tunnel process output.

## Data sent to Linkora

Only after an explicit user action:

- the computer label used for device authorization;
- the selected hostname or requested hostname label;
- the selected loopback origin, such as `http://127.0.0.1:3318`;
- normal API request metadata, including the source IP address and user agent.

Linkora stores security-relevant device authorization and tunnel actions in its
audit log.

## Data sent through Cloudflare

After Publish, traffic for the selected public hostname is carried through
Cloudflare Tunnel to the chosen local service. Cloudflare's terms and privacy
policy apply to that traffic.

## Source code

Linkora Local does not read or upload project source files, `.env` files,
repository contents, editor history, or terminal history.
