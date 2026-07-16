# Promotional media

The assets in `docs/media/` are reproducible recordings of the real opcilloscope
binary connected to the repository's local OPC UA test server. No public server,
mouse input, or manual timing is involved.

Run the capture from a Hyprland desktop session:

```bash
./scripts/capture-media.sh
```

The script creates three clips in both formats:

- `ui-live-monitor` — the main interface receiving four live signals
- `scope-sine` — a focused, high-resolution sine-wave scope
- `scope-multi-wave` — sine, triangle, square, and sawtooth signals together

GIF files are intended for social platforms. Animated WebP files contain the
same captures at a substantially smaller size and are preferred for the README.
The script uses an isolated tmux session and temporary configuration, starts and
stops the demo server itself, drives only normal terminal keyboard input, and
replaces existing generated assets. No capture-only behavior is compiled into
the production application.

Requirements: .NET SDK 10.0.109, Hyprland, foot, tmux, grim, jq, and ffmpeg.
