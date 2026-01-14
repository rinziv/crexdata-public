#!/usr/bin/env bash
set -euo pipefail

# Source system ROS (if installed)
if [ -f "/opt/ros/noetic/setup.bash" ]; then
  . /opt/ros/noetic/setup.bash
fi

# Source a mounted workspace (optional)
if [ -f "/workspace/ros_ws/devel/setup.bash" ]; then
  . /workspace/ros_ws/devel/setup.bash
fi

# If arguments supplied, execute them; otherwise spawn bash
if [ "$#" -gt 0 ]; then
  exec "$@"
else
  exec /bin/bash -c "exec \"$SHELL\""
fi