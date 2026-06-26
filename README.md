# Sense

A [BepInEx](https://github.com/BepInEx/BepInEx) mod for **R.E.P.O.** that passively marks nearby enemies on the map in red.

## What This Mod Does

Sense is a **passive radar** — there is nothing to press. While you are in a level, every enemy within **30 meters** (configurable) of you is marked on the **map** in **red**, the same way valuables show up in yellow. Open the map and you can see at a glance what is around you and roughly where it is.

- Works **automatically** — no key, no cooldown. Enemies appear as they come into range and disappear as they leave it.
- Markers **follow the enemies** as they move, and fade when an enemy is on a different floor — exactly like the game's own valuable markers.
- **Client-side only** — only you see the markers; it sends no network traffic and costs nothing to other players or the host.

R.E.P.O. itself scaffolded enemy map markers but never finished them (`Map.AddEnemy` exists in the game but is empty). Sense fills that gap on your own client through the game's working custom-marker system.

## Usage

Just play. Open the map during a level and nearby enemies are the red blips.

## Configuration

Settings are in `BepInEx/config/headclef.Sense.cfg` or the in-game mod config menu:

| Key | Default | Range | Description |
|-----|---------|-------|-------------|
| Enabled | `on` | on/off | Master switch for the radar |
| Radius | `30` | 5–100 | Detection radius in meters |
| Marker Color | `FF0000` | hex RGB | Colour of the enemy map markers |

## Requirements

- [BepInEx 5.x](https://github.com/BepInEx/BepInEx) installed for R.E.P.O.

## Installation

1. Install via **Thunderstore** (recommended).
2. Or manually: place `Sense.dll` into your `BepInEx/plugins` folder.
3. Launch the game — the config file is generated on first run.

## Multiplayer

- Runs **per-client** — only you see your own markers.
- Sends no RPCs and does not affect other players or the host.

## Development

### Project Structure

```text
├── Sense.cs          # Plugin entry point & config; drives the radar from Update
├── EnemyRadar.cs     # Client-side red enemy markers (add/track/remove on the map)
└── README.md
```

### Building

This project is standalone (BepInEx only) and part of the `Repo.slnx` solution:

```bash
dotnet build ../Repo.slnx
```

## License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.
