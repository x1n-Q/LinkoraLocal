# Contributing

Contributions are welcome.

1. Open an issue for a substantial behavior or security change.
2. Create a focused branch.
3. Keep account authorization in the system browser.
4. Do not add telemetry, embedded service credentials, or shell-based tunnel
   execution.
5. Run a Release build and `scripts/Test-Source.ps1`.
6. Describe security and privacy effects in the pull request.

UI changes should remain usable at 100%, 125%, 150%, and 200% Windows scaling.
Never make a sensitive service publishable merely to improve automatic
detection.
