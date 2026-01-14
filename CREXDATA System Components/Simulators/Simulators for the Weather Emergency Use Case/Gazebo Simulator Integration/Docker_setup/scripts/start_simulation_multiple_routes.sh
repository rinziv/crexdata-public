#!/bin/bash
set -e

#source ROS environment
source /home/catkin_ws/devel/setup.bash

# read parameter from json file
CONFIG_JSON=/home/catkin_ws/src/automatic_launch/config/sim_speed.json # path to json with parameters
SIM_SPEED_FACTOR=$(jq '.sim_speed_factor' "$CONFIG_JSON")

# define common timestamp for simulation outputs
SIM_TIMESTAMP=$(date +"%Y%m%d_%H%M%S")
echo "Simulation Timestamp: $SIM_TIMESTAMP"

# identify number of routes to be simulated
ROUTES_JSON=/home/catkin_ws/src/automatic_launch/config/routes.json # path to routes
NUM_ROUTES=$(jq ".routes | length" $ROUTES_JSON)

echo "Start PX4 simulation ..."
# path to PX4 repository
PX4_DIR=/home/catkin_ws/src/PX4-Autopilot
cd $PX4_DIR

echo "Building PX4 SITL..."
DONT_RUN=1 make px4_sitl_default gazebo-classic_iris

echo "Setting up Gazebo environment..."
source $PX4_DIR/Tools/simulation/gazebo-classic/setup_gazebo.bash $PX4_DIR $PX4_DIR/build/px4_sitl_default

export ROS_PACKAGE_PATH=$ROS_PACKAGE_PATH:$PX4_DIR
export ROS_PACKAGE_PATH=$ROS_PACKAGE_PATH:$PX4_DIR/Tools/simulation/gazebo-classic/sitl_gazebo-classic
export PX4_SIM_SPEED_FACTOR=$SIM_SPEED_FACTOR

success=0
while [ $success -lt $NUM_ROUTES ]; do
  FLIGHT_NO=$((success+1))
  echo "=== Starting flight $FLIGHT_NO (Route Index $success) ==="

  roslaunch px4 mavros_posix_sitl.launch gui:=false &
  LAUNCH_PID=$!

  # Wait for MAVROS connection
  while true; do
    connected=$(rostopic echo -n 1 /mavros/state | grep connected | awk '{print $2}')
    if [ "$connected" == "True" ]; then
      echo "MAVROS connected!"
      break
    fi
    echo "Waiting for MAVROS connection..."
    sleep 1
  done

  # Wait until battery simulation is initialized
  while true; do
    batt=$(rostopic echo -n 1 /mavros/battery 2>/dev/null | grep percentage | awk '{print $2}')
    if [ ! -z "$batt" ]; then
      echo "Battery initialised at $batt"
      break
    fi
    echo "Waiting for battery data..."
    sleep 1
  done

  set +e
  #run python script for drone mission
  python3 /home/catkin_ws/src/automatic_launch/scripts/mission_multiple_routes.py \
      --route_index $success --flight_no $FLIGHT_NO --sim_timestamp $SIM_TIMESTAMP
  MISSION_RC=$?
  set -e

  echo "Mission $FLIGHT_NO finished, stopping PX4..."
  if kill -0 $LAUNCH_PID 2>/dev/null; then
    kill $LAUNCH_PID 2>/dev/null || true
    wait $LAUNCH_PID 2>/dev/null || true
  else
    echo "roslaunch already stopped"
  fi


  pkill -9 px4 || true
  pkill -f gazebo || true
  pkill -f roslaunch || true
  sleep 3
  while pgrep -f "px4|gazebo|roslaunch" >/dev/null; do
    echo "Waiting for old processes to exit..."
    sleep 1
  done

  if [ $MISSION_RC -eq 0 ]; then
    echo "Flight $FLIGHT_NO successful."
    success=$((success+1))  # only count successful executions
  else
    echo "Flight $FLIGHT_NO failed, retrying..."
    sleep 2
  fi

done

echo "=== All $NUM_ROUTES missions completed ==="
wait $LAUNCH_PID 2>/dev/null

echo "Simulation finished cleanly."