# Dockerfile (fixed)
FROM ros:noetic

ARG DEBIAN_FRONTEND=noninteractive
SHELL ["/bin/bash", "-o", "pipefail", "-c"]

# ---- Configurable user ----
ARG USER=simuser
ARG UID=1000
ARG GID=1000

ENV ROS_MASTER_URI=http://localhost:11311
ENV ROS_HOSTNAME=localhost
ENV MPLBACKEND=Agg

# Install build deps and Gazebo + MAVROS runtime deps in one RUN
USER root
RUN apt-get update \
 && apt-get install -y --no-install-recommends geographiclib-tools \
      curl wget gnupg2 lsb-release ca-certificates sudo \
      build-essential git python3-pip python3-rosdep locales \
      gazebo11 libgazebo11-dev ros-noetic-mavros ros-noetic-mavros-extras \
      python3-catkin-tools python3-empy ros-noetic-roslaunch \
      python3-vcstool \
 && locale-gen en_US.UTF-8 \
 && update-locale LANG=en_US.UTF-8 \
 && geographiclib-get-geoids egm96-5 \
 && apt-get clean \
 && rm -rf /var/lib/apt/lists/*

# Ensure rosdep initialized (no-op if already initialized)
RUN if [ ! -f /etc/ros/rosdep/sources.list.d/20-default.list ]; then rosdep init || true; fi \
 && rosdep update || true

# Create non-root user early so /home/${USER} exists
RUN id -u ${USER} >/dev/null 2>&1 || (groupadd -g ${GID} ${USER} || true && useradd -m -u ${UID} -g ${GID} -s /bin/bash ${USER} || true)

# Create catkin workspace directories as root then chown to user; guarantees path exists
RUN mkdir -p /home/${USER}/catkin_ws/src \
 && chown -R ${USER}:${USER} /home/${USER}/catkin_ws \
 && mkdir -p /home/${USER}/catkin_ws/src/automatic_launch

# Copy entrypoint, sanitize line endings and make executable
COPY entrypoint.sh /home/${USER}/entrypoint.sh
RUN sed -i '1s/^\xEF\xBB\xBF//' /home/${USER}/entrypoint.sh \
 && sed -i 's/\r$//' /home/${USER}/entrypoint.sh \
 && chmod +x /home/${USER}/entrypoint.sh \
 && chown ${USER}:${USER} /home/${USER}/entrypoint.sh

# Copy the script files into the container
COPY scripts /home/simuser/catkin_ws/src/automatic_launch/scripts
COPY config /home/simuser/catkin_ws/src/automatic_launch/config
RUN sed -i '1s/^\xEF\xBB\xBF//' /home/${USER}/catkin_ws/src/automatic_launch/scripts/start_simulation_multiple_runs.sh \
 && sed -i 's/\r$//' /home/${USER}/catkin_ws/src/automatic_launch/scripts/start_simulation_multiple_runs.sh \
 && chmod +x /home/${USER}/catkin_ws/src/automatic_launch/scripts/start_simulation_multiple_runs.sh \
 && chown ${USER}:${USER} /home/${USER}/catkin_ws/src/automatic_launch/scripts/start_simulation_multiple_runs.sh

RUN sed -i '1s/^\xEF\xBB\xBF//' /home/${USER}/catkin_ws/src/automatic_launch/scripts/start_simulation_multiple_runs_v002.sh \
 && sed -i 's/\r$//' /home/${USER}/catkin_ws/src/automatic_launch/scripts/start_simulation_multiple_runs_v002.sh \
 && chmod +x /home/${USER}/catkin_ws/src/automatic_launch/scripts/start_simulation_multiple_runs_v002.sh \
 && chown ${USER}:${USER} /home/${USER}/catkin_ws/src/automatic_launch/scripts/start_simulation_multiple_runs_v002.sh

# Give execute permission to the .sh file
RUN chmod +x /home/simuser/catkin_ws/src/automatic_launch/scripts/start_simulation_multiple_runs.sh \
 && chmod +x /home/simuser/catkin_ws/src/automatic_launch/scripts/start_simulation_multiple_runs_v002.sh \
 && cd /home/simuser/catkin_ws \
 && rm -rf devel \
 && rm -rf build \
 && rm -rf logs \
 && . /opt/ros/noetic/setup.bash \
 && catkin build

RUN apt-get update \
 && apt-get install -y --no-install-recommends jq \
 && rm -rf /var/lib/apt/lists/*

# Switch to non-root and perform initial catkin build (creates build/devel)
USER ${USER}
WORKDIR /home/${USER}
RUN /bin/bash -lc "source /opt/ros/noetic/setup.bash && cd catkin_ws && catkin build || true"

# Ensure ownership (defensive) then final user + entrypoint
USER root
RUN chown -R ${USER}:${USER} /home/${USER}/catkin_ws /home/${USER}/entrypoint.sh

USER ${USER}
WORKDIR /home/${USER}

USER root
# Install PX4
WORKDIR /home/simuser
RUN git clone https://github.com/PX4/PX4-Autopilot.git --recursive /home/simuser/catkin_ws/src/PX4-Autopilot \
 && cd /home/simuser/catkin_ws/src \
 && bash ./PX4-Autopilot/Tools/setup/ubuntu.sh \
 && cd /home/simuser/catkin_ws/src/PX4-Autopilot \
 && DONT_RUN=1 make px4_sitl gazebo-classic \
 && source Tools/simulation/gazebo-classic/setup_gazebo.bash $(pwd) $(pwd)/build/px4_sitl_default \
 && export ROS_PACKAGE_PATH=$ROS_PACKAGE_PATH:$(pwd) \
 && export ROS_PACKAGE_PATH=$ROS_PACKAGE_PATH:$(pwd)/Tools/simulation/gazebo-classic/sitl_gazebo-classic \
 && apt update \
 && apt install -y ros-noetic-gazebo-ros-pkgs ros-noetic-gazebo-ros-control \
 && cd /home/simuser/catkin_ws \
 && rosdep init 2>/dev/null || true \
 && rosdep update \
 && rosdep install --from-paths src --ignore-src -r -y \
 && source /opt/ros/noetic/setup.bash \
 && catkin build \
 && source devel/setup.bash \
 && apt-get update \
 && apt-get install -y python3-tk

RUN pip3 install kconfiglib


# fix possible ownership issues and mark safe for git
USER root
RUN chown -R simuser:simuser /home/simuser/catkin_ws/src/PX4-Autopilot || true
USER simuser
RUN git config --global --add safe.directory /home/simuser/catkin_ws/src/PX4-Autopilot || true
RUN git config --global --add safe.directory '*' || true

#RUN chown -R simuser:simuser /home/simuser/catkin_ws/src/PX4-Autopilot

ENTRYPOINT ["/home/simuser/entrypoint.sh"]
CMD ["tail", "-f", "/dev/null"]