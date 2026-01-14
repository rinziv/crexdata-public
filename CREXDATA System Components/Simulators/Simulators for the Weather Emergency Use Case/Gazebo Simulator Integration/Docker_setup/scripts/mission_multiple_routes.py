#!/usr/bin/env python3
import rospy
import time
import math
import os
import json
import csv
import cv2
import tkinter as tk
import os, signal, subprocess
from tkinter import font
from datetime import datetime
from threading import Thread
from geometry_msgs.msg import PoseStamped
from mavros_msgs.srv import CommandBool, SetMode
from mavros_msgs.srv import ParamSet
from mavros_msgs.msg import ParamValue
from nav_msgs.msg import Odometry
from sensor_msgs.msg import BatteryState
from sensor_msgs.msg import NavSatFix
from mavros_msgs.msg import Altitude
from mavros_msgs.msg import ExtendedState
from cv_bridge import CvBridge
from sensor_msgs.msg import Image
from std_srvs.srv import Empty

json_path = os.path.expanduser('/home/catkin_ws/src/automatic_launch/config/routes.json')
try:
    with open(json_path, 'r') as f:
        data = json.load(f)
    routes = data["routes"]
    rospy.loginfo(f"Loaded {len(routes)} routes from {json_path}")
except Exception as e:
    rospy.logerr(f"Could not load routes from {json_path}: {e}")
    # Optional: fallback or exit
    routes = []

WAYPOINT_TOLERANCE = 2.0

# Global parameters
current_position = None
local_pose = None
global_position = None
battery_data = []
battery_level = None
altitude_data = None
first_timestamp = None
flight_duration = None
initial_battery_percentage = None
last_image = None
landed_state = None

def kill_simulation():
    # roslaunch PID
    os.system("pkill -9 -f roslaunch")
    # PX4
    os.system("pkill -9 px4")
    # Gazebo
    os.system("pkill -f gazebo")

def is_display_available():
    return bool(os.environ.get("DISPLAY"))

def extended_state_cb(msg):
    global landed_state
    landed_state = msg.landed_state

def image_callback(msg):
    global last_image
    bridge = CvBridge()
    try:
        # transform ROS messages in OpenCV format
        last_image = bridge.imgmsg_to_cv2(msg, "bgr8")
    except Exception as e:
        rospy.logerr(f"Error converting image: {e}")

def show_camera_feed():
    cv2.namedWindow("Drone Camera", cv2.WINDOW_NORMAL)
    cv2.setWindowProperty("Drone Camera", cv2.WND_PROP_TOPMOST, 1)

    # Set window size with specific corner
    window_width = 550
    window_height = 440
    root = tk.Tk()
    root.withdraw()
    screen_width = root.winfo_screenwidth()
    screen_height = root.winfo_screenheight()
    x_offset = 50
    y_offset = 120
    x_position = x_offset
    y_position = screen_height - window_height - y_offset  # To avoid overlapping

    cv2.resizeWindow("Drone Camera", window_width, window_height)
    cv2.moveWindow("Drone Camera", x_position, y_position)

    while not rospy.is_shutdown():
        if last_image is not None:
            cv2.imshow("Drone Camera", last_image)

        key = cv2.waitKey(1)
        if key == 27:
            break

    cv2.destroyAllWindows()

def battery_callback(msg):
    global battery_level
    battery_level = {
        "voltage": msg.voltage,
        "current": msg.current,
        "percentage": msg.percentage
    }
    if local_pose and global_position and altitude_data:
        timestamp = rospy.Time.now().to_sec()
        battery_data.append({
            "timestamp": timestamp,
            "battery": battery_level,
            "local_pose": {
                "position": {
                    "x": local_pose.pose.pose.position.x,
                    "y": local_pose.pose.pose.position.y,
                    "z": local_pose.pose.pose.position.z
                },
                "orientation": {
                    "x": local_pose.pose.pose.orientation.x,
                    "y": local_pose.pose.pose.orientation.y,
                    "z": local_pose.pose.pose.orientation.z,
                    "w": local_pose.pose.pose.orientation.w
                }
            },
            "global_position": global_position,
            "altitude": altitude_data
        })
        with open(log_filename, 'w') as file:
            json.dump(battery_data, file, indent=4)

def update_gui():
    if battery_level and local_pose and global_position and altitude_data:
        battery_label.config(text=f"Battery level: {battery_level['percentage']*100:.2f}% ({battery_level['voltage']}V)")
        # set color of battery level
        if battery_level['percentage'] > 0.80:
            battery_label.config(fg="green")
        elif 0.20 <= battery_level['percentage'] <= 0.80:
            battery_label.config(fg="orange")
        else:
            battery_label.config(fg="red")
        local_label_x.config(text=f"x: {local_pose.pose.pose.position.x:.2f}")
        local_label_y.config(text=f"y: {local_pose.pose.pose.position.y:.2f}")
        local_label_z.config(text=f"z: {local_pose.pose.pose.position.z:.2f}")
        global_label_lat.config(text=f"Lat: {global_position['latitude']:.6f}")
        global_label_lon.config(text=f"Lon: {global_position['longitude']:.6f}")
        global_label_altitude.config(text=f"Alt: {altitude_data['amsl']:.2f} m")
    root.after(500, update_gui)  # update rate 500 ms

def setup_gui():
    global root, battery_label, local_label_x, local_label_y, local_label_z
    global global_label_lat, global_label_lon, global_label_altitude
    root = tk.Tk()
    root.title("Drone status")
    root.attributes("-topmost", True)
    window_width = 550
    window_height = 220
    screen_width = root.winfo_screenwidth()
    screen_height = root.winfo_screenheight()
    x_offset = 50
    y_offset = 120
    x_position = screen_width - window_width - x_offset
    y_position = screen_height - window_height - y_offset
    # set window size and position
    root.geometry(f"{window_width}x{window_height}+{x_position}+{y_position}")
    label_font = font.Font(size=14)
    battery_label = tk.Label(root, text="Battery level: Loading...", font=label_font)
    battery_label.grid(row=0, column=0, columnspan=2)
    windspeed_label = tk.Label(root, text="Wind speed: 10 m/s", font=label_font)
    windspeed_label.grid(row=1, column=0, columnspan=2)
    winddirection_label = tk.Label(root, text="Wind direction: 90°", font=label_font)
    winddirection_label.grid(row=2, column=0, columnspan=2)
    local_label = tk.Label(root, text="Local position:", font=label_font)
    local_label.grid(row=3, column=0)
    local_label_x = tk.Label(root, text="x: Loading...", font=label_font)
    local_label_x.grid(row=4, column=0)
    local_label_y = tk.Label(root, text="y: Loading...", font=label_font)
    local_label_y.grid(row=5, column=0)
    local_label_z = tk.Label(root, text="z: Loading...", font=label_font)
    local_label_z.grid(row=6, column=0)
    global_label = tk.Label(root, text="Global position:", font=label_font)
    global_label.grid(row=3, column=1)
    global_label_lat = tk.Label(root, text="Lat: Loading...", font=label_font)
    global_label_lat.grid(row=4, column=1)
    global_label_lon = tk.Label(root, text="Lon: Loading...", font=label_font)
    global_label_lon.grid(row=5, column=1)
    global_label_altitude = tk.Label(root, text="Alt: Loading...", font=label_font)
    global_label_altitude.grid(row=6, column=1)
    update_gui()
    root.mainloop()

def position_callback(msg):
    global current_position
    current_position = msg.pose.pose.position

def local_position_callback(msg):
    global local_pose
    local_pose = msg

def global_position_callback(msg):
    global global_position
    global_position = {
        "latitude": msg.latitude,
        "longitude": msg.longitude,
    }

def altitude_callback(msg):
    global altitude_data
    altitude_data = {
        "amsl": msg.amsl,
        "relative": msg.relative,
    }

def distance_to_waypoint(target):
    if current_position is None:
        return float('inf')
    return math.sqrt(
        (target.pose.position.x - current_position.x) ** 2 +
        (target.pose.position.y - current_position.y) ** 2 +
        (target.pose.position.z - current_position.z) ** 2
    )

def set_mode(mode):
    rospy.wait_for_service('/mavros/set_mode')
    try:
        set_mode_srv = rospy.ServiceProxy('/mavros/set_mode', SetMode)
        response = set_mode_srv(base_mode=0, custom_mode=mode)
        return response.mode_sent
    except rospy.ServiceException as e:
        rospy.logerr(f"Set mode failed: {e}")
        return False

def arm_drone():
    rospy.wait_for_service('/mavros/cmd/arming')
    try:
        arm_srv = rospy.ServiceProxy('/mavros/cmd/arming', CommandBool)
        response = arm_srv(value=True)
        return response.success
    except rospy.ServiceException as e:
        rospy.logerr(f"Arming failed: {e}")
        return False

def calculate_heading(current, target):
    if current is None:
        return 0.0

    dx = target.pose.position.x - current.x
    dy = target.pose.position.y - current.y
    heading = math.atan2(dy, dx)
    return heading

def set_sim_bat_min_pct(value):
    rospy.wait_for_service('/mavros/param/set')
    try:
        param_set = rospy.ServiceProxy('/mavros/param/set', ParamSet)
        param_value = ParamValue(real=float(value))
        response = param_set(param_id="SIM_BAT_MIN_PCT", value=param_value)
        if response.success:
            rospy.loginfo(f"Set SIM_BAT_MIN_PCT to {value}")
            return True
        else:
            rospy.logwarn("Failed to set SIM_BAT_MIN_PCT")
            return False
    except rospy.ServiceException as e:
        rospy.logerr(f"Param set service call failed: {e}")
        return False

def run_mission(flight_no, waypoints):
    global current_position, first_timestamp, initial_battery_percentage, battery_level, local_pose, global_position, altitude_data, landed_state, log_filename

    rospy.loginfo(f"Starting mission flight no: {flight_no}")

    csv_dir = os.path.expanduser("/home/flight_data")
    os.makedirs(csv_dir, exist_ok=True)
    csv_filename = os.path.join(csv_dir, f"flight_{global_timestamp}_{flight_no}.csv")
    csv_file = open(csv_filename, mode='w', newline='')
    csv_writer = csv.writer(csv_file)
    csv_writer.writerow(["flight_no", "Event", "Latitude", "Longitude", "Altitude", "Timestamp", "Battery"])

    log_dir = os.path.expanduser("/home/battery_data")
    os.makedirs(log_dir, exist_ok=True)
    log_filename = os.path.join(log_dir, f"battery_log_{global_timestamp}_{flight_no}.json")

    # initialize MAVROS publisher und subs

    pub = rospy.Publisher('/mavros/setpoint_position/local', PoseStamped, queue_size=10)

    rospy.loginfo("Waiting for position data...")
    time.sleep(5)

    initial_target = PoseStamped()
    initial_target.header.frame_id = "map"
    initial_target.pose.position.x = waypoints[0][0]
    initial_target.pose.position.y = waypoints[0][1]
    initial_target.pose.position.z = waypoints[0][2]
    initial_target.pose.orientation.w = 1.0

    rate = rospy.Rate(10)
    for _ in range(30):
        initial_target.header.stamp = rospy.Time.now()
        pub.publish(initial_target)
        rate.sleep()

    rospy.loginfo("Switching to OFFBOARD mode...")
    if not set_mode("OFFBOARD"):
        rospy.logerr("OFFBOARD mode could not be activated!")
        return False

    rospy.loginfo("Arming the drone...")
    if not arm_drone():
        rospy.logerr("Drone could not be armed!")
        return False

    first_timestamp = rospy.Time.now().to_sec()

    if battery_level:
        initial_battery_percentage = battery_level["percentage"]
    else:
        initial_battery_percentage = 1.0

    csv_writer.writerow([
        flight_no,
        "Start",
        global_position['latitude'] if global_position else "",
        global_position['longitude'] if global_position else "",
        altitude_data['amsl'] if altitude_data else "",
        rospy.Time.now().to_sec(),
        battery_level["percentage"] if battery_level else ""
    ])

    battery_data = []

    # main flight
    for point in waypoints:
        target = PoseStamped()
        target.header.frame_id = "map"
        target.pose.position.x = point[0]
        target.pose.position.y = point[1]
        target.pose.position.z = point[2]
        current_heading = calculate_heading(current_position, target)
        target.pose.orientation.z = math.sin(current_heading / 2)
        target.pose.orientation.w = math.cos(current_heading / 2)

        rospy.loginfo(f"Flight {flight_no}: going to waypoint {point}")

        while distance_to_waypoint(target) > WAYPOINT_TOLERANCE and not rospy.is_shutdown():
            target.header.stamp = rospy.Time.now()
            pub.publish(target)
            rate.sleep()

            # collect battery levels in json
            if local_pose and global_position and altitude_data and battery_level:
                timestamp = rospy.Time.now().to_sec()
                battery_data.append({
                    "timestamp": timestamp,
                    "battery": battery_level,
                    "local_pose": {
                        "position": {
                            "x": local_pose.pose.pose.position.x,
                            "y": local_pose.pose.pose.position.y,
                            "z": local_pose.pose.pose.position.z,
                        },
                        "orientation": {
                            "x": local_pose.pose.pose.orientation.x,
                            "y": local_pose.pose.pose.orientation.y,
                            "z": local_pose.pose.pose.orientation.z,
                            "w": local_pose.pose.pose.orientation.w,
                        }
                    },
                    "global_position": global_position,
                    "altitude": altitude_data,
                })

        #check if battery simulation works
        if battery_level is None or battery_level.get("percentage", 1.0) >= 0.999:
            rospy.logwarn("Battery still at 100%, aborting flight and retrying...")
            kill_simulation()
            sys.exit(1)
            return False

        # Waypoint reached - log in csv
        if global_position and altitude_data and battery_level:
            csv_writer.writerow([
                flight_no,
                "Waypoint",
                global_position['latitude'],
                global_position['longitude'],
                altitude_data['amsl'],
                rospy.Time.now().to_sec(),
                battery_level["percentage"]
            ])
        rospy.loginfo(f"Waypoint reached: {point}")

    # landing after last waypoint
    rospy.loginfo("Switching to MANUAL mode...")
    set_mode("MANUAL")

    rospy.loginfo("Landing...")
    set_mode("AUTO.LAND")

    landing_start = time.time()
    max_landing_time = 60
    while not rospy.is_shutdown() and (time.time() - landing_start) < max_landing_time:
        if landed_state == 1:
            rospy.loginfo("Drone landed successfully")
            break
        rospy.sleep(0.5)
    else:
        rospy.logwarn("Landing timeout")

    flight_duration = rospy.Time.now().to_sec() - first_timestamp
    energy_consumed = initial_battery_percentage - (battery_level["percentage"] if battery_level else initial_battery_percentage)

    csv_writer.writerow([
        flight_no,
        "Landing",
        global_position['latitude'] if global_position else "",
        global_position['longitude'] if global_position else "",
        altitude_data['amsl'] if altitude_data else "",
        rospy.Time.now().to_sec(),
        battery_level["percentage"] if battery_level else ""
    ])
    csv_writer.writerow([])

    csv_file.close()

    with open(log_filename, 'w') as f:
        json.dump(battery_data, f, indent=4)

    rospy.loginfo(f"Flight {flight_no} complete. CSV saved to {csv_filename}, JSON saved to {log_filename}")

    return True

if __name__ == "__main__":
    rospy.init_node('waypoint_mission_multi', anonymous=True)

    rospy.Subscriber('/mavros/local_position/odom', Odometry, position_callback)
    rospy.Subscriber('/mavros/global_position/local', Odometry, local_position_callback)
    rospy.Subscriber('/mavros/global_position/global', NavSatFix, global_position_callback)
    rospy.Subscriber('/mavros/altitude', Altitude, altitude_callback)
    rospy.Subscriber('/mavros/battery', BatteryState, battery_callback)
    rospy.Subscriber('/mavros/extended_state', ExtendedState, extended_state_cb)
    rospy.Subscriber('/camera/color/image_raw', Image, image_callback)

    import argparse, sys
    parser = argparse.ArgumentParser()
    parser.add_argument("--route_index", type=int, required=True,
                        help="Welche Route aus routes.json (Index 0..N-1)")
    parser.add_argument("--flight_no", type=int, required=True,
                        help="Flugnummer für Logging")
    parser.add_argument("--sim_timestamp", type=str, required=True,
                        help="Globaler Timestamp der gesamten Simulationsreihe")
    args = parser.parse_args()

    global global_timestamp
    global_timestamp = args.sim_timestamp

    # select route
    try:
        route = routes[args.route_index]
    except IndexError:
        rospy.logerr(f"Route index {args.route_index} nicht vorhanden!")
        sys.exit(1)

    flight_no_str = f"{args.flight_no:03d}"
    success = run_mission(flight_no_str, route)
    sys.exit(0 if success else 1)