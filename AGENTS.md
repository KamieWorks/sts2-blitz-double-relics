# Working rules

- Blitz Double Relics is an independent rules mod for consenting co-op players. Keep the manifest marked `affects_gameplay: true` so every participant needs the same version. Keep standard and solo treasure behavior intact.
- Build against the local game's managed assemblies. Publish original source and compiled mod output. Keep game files, third-party mod files, save data, raw logs, and machine-specific paths out of Git.
- Before claiming a multiplayer repair is complete, verify the deployed version on the host and client and retest the actual Steam join and chest room entry. The core cases are two independent picks in each round, contested picks and consolation, skipping, correct inventories, and normal room progression. Changes to the four-player tray also require a four-player UI check.
