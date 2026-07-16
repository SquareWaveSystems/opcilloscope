#!/usr/bin/env bash
set -euo pipefail

# Reproducible promotional captures for opcilloscope.
# Requires Linux/Hyprland: dotnet, foot, tmux, grim, jq, ffmpeg.

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
output_dir="${1:-$repo_root/docs/media}"
work_dir="$(mktemp -d -t opcilloscope-capture.XXXXXX)"
server_session="opcilloscope-media-server-$$"
app_session="opcilloscope-media-app-$$"
app_id="opcilloscope-media-$$"
fps=10
port=$((14840 + ($$ % 1000)))

cleanup() {
  tmux kill-session -t "$app_session" 2>/dev/null || true
  tmux kill-session -t "$server_session" 2>/dev/null || true
  rm -rf "$work_dir"
}
trap cleanup EXIT INT TERM

for command in dotnet foot tmux grim jq ffmpeg hyprctl; do
  command -v "$command" >/dev/null || {
    echo "Missing required command: $command" >&2
    exit 1
  }
done

if [[ -z "${WAYLAND_DISPLAY:-}" ]] || [[ "${XDG_CURRENT_DESKTOP:-}" != *Hyprland* ]]; then
  echo "Run this script from a Hyprland desktop session." >&2
  exit 1
fi

mkdir -p "$output_dir"

if [[ -n "${OPCILLOSCOPE_CAPTURE_BINARY:-}" ]]; then
  binary="$OPCILLOSCOPE_CAPTURE_BINARY"
else
  binary="$work_dir/publish/opcilloscope"
  dotnet publish "$repo_root/Opcilloscope.csproj" -c Release -r linux-x64 \
    -o "$(dirname "$binary")" -p:DebugType=none
fi

if [[ -z "${CAPTURE_ONLY:-}" ]]; then
  for scene in ui-live-monitor scope-sine scope-multi-wave; do
    OPCILLOSCOPE_CAPTURE_BINARY="$binary" CAPTURE_ONLY="$scene" "$0" "$output_dir"
  done
  exit 0
fi

config="$work_dir/promotional-demo.cfg"
jq --arg endpoint "opc.tcp://localhost:$port/UA/OpcilloscopeTest" \
  '.server.endpointUrl = $endpoint' \
  "$repo_root/scripts/promotional-demo.cfg" >"$config"

server_binary="$repo_root/Tests/Opcilloscope.TestServer/bin/Release/net10.0/Opcilloscope.TestServer"
if [[ ! -x "$server_binary" ]]; then
  dotnet build "$repo_root/Tests/Opcilloscope.TestServer/Opcilloscope.TestServer.csproj" \
    -c Release
fi

tmux new-session -d -s "$server_session" -x 120 -y 40 \
  "exec '$server_binary' --port '$port'"

for _ in {1..150}; do
  if tmux capture-pane -pt "$server_session" 2>/dev/null | grep -q "Server started successfully"; then
    break
  fi
  tmux has-session -t "$server_session" 2>/dev/null || {
    echo "Demo server exited during startup." >&2
    exit 1
  }
  sleep 0.1
done
tmux capture-pane -pt "$server_session" 2>/dev/null | grep -q "Server started successfully" || {
  echo "Demo server did not start:" >&2
  tmux capture-pane -pt "$server_session" >&2
  exit 1
}

window_geometry() {
  hyprctl clients -j | jq -r --arg app_id "$app_id" '
    first(.[] | select(.class == $app_id)) |
    "\(.at[0]),\(.at[1]) \(.size[0])x\(.size[1])"'
}

wait_for_window() {
  for _ in {1..100}; do
    geometry="$(window_geometry 2>/dev/null || true)"
    if [[ -n "$geometry" && "$geometry" != "null" ]]; then
      printf '%s\n' "$geometry"
      return 0
    fi
    sleep 0.1
  done
  echo "Capture terminal did not appear." >&2
  return 1
}

start_app() {
  tmux kill-session -t "$app_session" 2>/dev/null || true
  tmux new-session -d -s "$app_session" -x 116 -y 36 \
    "TERM=xterm-256color exec '$binary' '$config'"
  foot -a "$app_id" -T "opcilloscope — live OPC UA signals" -W 116x36 \
    -f "JetBrainsMono Nerd Font:size=13" tmux attach-session -t "$app_session" &
  foot_pid=$!
  geometry="$(wait_for_window)"

  for _ in {1..150}; do
    if tmux capture-pane -pt "$app_session" | grep -q "Configuration loaded: 4 nodes"; then
      return 0
    fi
    tmux has-session -t "$app_session" 2>/dev/null || {
      echo "opcilloscope exited while loading the capture scene." >&2
      exit 1
    }
    sleep 0.1
  done

  echo "opcilloscope did not finish loading the capture scene:" >&2
  tmux capture-pane -pt "$app_session" >&2
  exit 1
}

open_scope() {
  local scene="$1"

  # Terminal.Gui may tab through the frame and its child control separately.
  # Keep advancing until the context-aware status bar proves that Monitored
  # Variables owns focus, instead of relying on a fixed number of Tab presses.
  for _ in {1..8}; do
    tmux send-keys -t "$app_session" Tab
    sleep 0.2
    if tmux capture-pane -pt "$app_session" | tail -n 2 | grep -q "Unsub"; then
      break
    fi
  done
  tmux capture-pane -pt "$app_session" | tail -n 2 | grep -q "Unsub" || {
    echo "Could not focus Monitored Variables for capture scene '$scene':" >&2
    tmux capture-pane -pt "$app_session" >&2
    exit 1
  }

  tmux send-keys -t "$app_session" Home Space

  if [[ "$scene" == "scope-multi-wave" ]]; then
    for _ in {1..3}; do
      tmux send-keys -t "$app_session" Down Space
    done
  fi

  tmux send-keys -t "$app_session" s

  for _ in {1..50}; do
    if tmux capture-pane -pt "$app_session" | grep -q "SCOPE"; then
      return 0
    fi
    sleep 0.1
  done

  echo "Scope did not open for capture scene '$scene':" >&2
  tmux capture-pane -pt "$app_session" >&2
  exit 1
}

stop_app() {
  tmux send-keys -t "$app_session" C-q 2>/dev/null || true
  for _ in {1..30}; do
    kill -0 "$foot_pid" 2>/dev/null || return 0
    sleep 0.1
  done
  tmux kill-session -t "$app_session" 2>/dev/null || true
}

record_frames() {
  local name="$1"
  local seconds="$2"
  local frame_dir="$work_dir/$name"
  local total=$((seconds * fps))
  mkdir -p "$frame_dir"

  for ((frame = 0; frame < total; frame++)); do
    grim -g "$geometry" "$frame_dir/$(printf '%04d' "$frame").png"
    sleep 0.1
  done

  ffmpeg -hide_banner -loglevel error -y -framerate "$fps" \
    -i "$frame_dir/%04d.png" -vf \
    "fps=$fps,scale=1200:-2:flags=lanczos,split[s0][s1];[s0]palettegen=max_colors=128:stats_mode=diff[p];[s1][p]paletteuse=dither=bayer:bayer_scale=3:diff_mode=rectangle" \
    -loop 0 "$output_dir/$name.gif"

  ffmpeg -hide_banner -loglevel error -y -framerate "$fps" \
    -i "$frame_dir/%04d.png" -vf "fps=$fps,scale=1200:-2:flags=lanczos" \
    -c:v libwebp_anim -lossless 0 -quality 82 -compression_level 6 -loop 0 \
    "$output_dir/$name.webp"
}

case "$CAPTURE_ONLY" in
  ui-live-monitor)
    echo "Capturing live monitor overview..."
    start_app
    record_frames "ui-live-monitor" 6
    ;;
  scope-sine|scope-multi-wave)
    echo "Capturing ${CAPTURE_ONLY#scope-} scope..."
    start_app
    open_scope "$CAPTURE_ONLY"
    sleep 2
    record_frames "$CAPTURE_ONLY" 8
    ;;
  *)
    echo "Unknown capture scene: $CAPTURE_ONLY" >&2
    exit 1
    ;;
esac

echo "Created:"
find "$output_dir" -maxdepth 1 -type f \( -name '*.gif' -o -name '*.webp' \) \
  -printf '  %f (%k KiB)\n' | sort
