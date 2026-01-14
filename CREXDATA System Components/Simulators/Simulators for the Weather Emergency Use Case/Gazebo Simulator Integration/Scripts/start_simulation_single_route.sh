#!/bin/bash
set -e
#source ROS environment
source /home/catkin_ws/devel/setup.bash
# read parameter from json file
CONFIG_JSON=/home/catkin_ws/src/automatic_launch/config/sim_speed.json #path to json file with parameters
SIM_SPEED_FACTOR=$(jq '.sim_speed_factor' "$CONFIG_JSON")

echo "Start PX4 simulation ..."
#path to PX4 repository
PX4_DIR=/home/catkin_ws/src/PX4-Autopilot
cd $PX4_DIR

echo "Building PX4 SITL..."
DONT_RUN=1 make px4_sitl_default gazebo-classic_iris

echo "Setting up Gazebo environment..."
source $PX4_DIR/Tools/simulation/gazebo-classic/setup_gazebo.bash $PX4_DIR $PX4_DIR/build/px4_sitl_default

export ROS_PACKAGE_PATH=$ROS_PACKAGE_PATH:$PX4_DIR
export ROS_PACKAGE_PATH=$ROS_PACKAGE_PATH:$PX4_DIR/Tools/simulation/gazebo-classic/sitl_gazebo-classic
export PX4_SIM_SPEED_FACTOR=$SIM_SPEED_FACTOR

echo "Starting roslaunch..."
roslaunch px4 mavros_posix_sitl.launch gui:=false&
LAUNCH_PID=$!

# wait until MAVROS connected, i.e., /mavros/state topic is available and connected = true
while true; do
  connected=$(rostopic echo -n 1 /mavros/state | grep connected | awk '{print $2}')
  if [ "$connected" == "True" ]; then
    echo "MAVROS connected!"
    break
  fi
  echo "Waiting for MAVROS connection..."
  sleep 1
done

echo "Starting Python script..."
python3 /home/catkin_ws/src/automatic_launch/scripts/mission_single_route.py #path to Python script for drone control

echo "Python script finished. Killing roslaunch..."
kill $LAUNCH_PID
wait $LAUNCH_PID 2>/dev/null

echo "Simulation finished cleanly."