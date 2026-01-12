#!/usr/bin/env python3
"""
Raspberry Pi Eye Tracking Client
Connects to eyeRealTimeSlidingWindow.py and streams video using rpicam-vid + FFmpeg
"""

import socket
import subprocess
import time
import signal
import sys
import os
from datetime import datetime
import threading

# ============ CONFIGURATION ============
SERVER_IP = "147.27.41.144"  # CHANGE THIS to your server IP
SERVER_PORT = 9000
STREAM_PORT = 12345
RASPBERRY_ID = "raspi-01"  # CHANGE THIS for each device

# Camera configuration for rpicam-vid
CAMERA_WIDTH = 1920
CAMERA_HEIGHT = 1080
CAMERA_FPS = 50
OUTPUT_SIZE = 128  # Final size sent to server (128x128)
CROP_SIZE = 400  # Crop to 400x400 before scaling

# Connection settings
MAX_CONNECT_ATTEMPTS = 0  # 0 = infinite retries
INITIAL_RETRY_DELAY = 2.0  # seconds
HEARTBEAT_TIMEOUT = 3.0  # seconds

# Global state
stream_process = None
heartbeat_socket = None
is_running = True
connection_active = False


# ============ HELPER FUNCTIONS ============


def get_cpu_serial():
    """Get Raspberry Pi CPU serial number for unique ID"""
    try:
        with open("/proc/cpuinfo", "r") as f:
            for line in f:
                if line.startswith("Serial"):
                    serial = line.split(":")[1].strip()
                    return f"RPi_{serial[-8:]}"
    except Exception as e:
        print(f"⚠️  Could not read CPU serial: {e}")

    # Fallback to hostname
    try:
        import platform

        hostname = platform.node()
        return f"RPi_{hostname}"
    except:
        return RASPBERRY_ID


def signal_handler(sig, frame):
    """Handle Ctrl+C gracefully"""
    global is_running
    print("\n🛑 Shutdown signal received...")
    is_running = False
    cleanup_and_exit()


def cleanup_and_exit():
    """Clean up resources and exit"""
    global stream_process, heartbeat_socket

    print("🧹 Cleaning up...")

    # Stop video stream
    if stream_process:
        try:
            print("  Stopping video stream...")
            stream_process.terminate()
            stream_process.wait(timeout=5)
        except subprocess.TimeoutExpired:
            print("  Force killing stream...")
            stream_process.kill()
            stream_process.wait()
        except:
            pass

    # Close heartbeat socket
    if heartbeat_socket:
        try:
            heartbeat_socket.close()
        except:
            pass

    print("✅ Cleanup complete. Goodbye!")
    sys.exit(0)


def heartbeat_handler(sock, raspberry_id):
    """Handle heartbeat messages from server in separate thread"""
    global connection_active

    try:
        sock.settimeout(HEARTBEAT_TIMEOUT)
        print("💓 Heartbeat handler started")

        while is_running and connection_active:
            try:
                data = sock.recv(1024)
                if not data:
                    print("💔 Server closed connection")
                    connection_active = False
                    break

                message = data.decode().strip()
                if message == "HEARTBEAT":
                    # Respond to heartbeat
                    sock.sendall(b"ACK\n")
                else:
                    print(f"📨 Received: {message}")

            except socket.timeout:
                continue
            except Exception as e:
                print(f"❌ Heartbeat error: {e}")
                connection_active = False
                break
    finally:
        print("💔 Heartbeat handler stopped - will reconnect...")


def perform_handshake(raspberry_id, server_ip, server_port, max_attempts=10):
    """
    Perform handshake with eyeRealTimeSlidingWindow.py server:
    1. Server sends: WELCOME\n
    2. Client sends: <raspberry_id>\n
    3. Server sends: START\n

    Returns: (success: bool, socket: socket or None)
    """
    attempt = 0
    delay = INITIAL_RETRY_DELAY

    while is_running:
        attempt += 1

        # Check max attempts (if set)
        if max_attempts > 0 and attempt > max_attempts:
            print(f"❌ Max connection attempts ({max_attempts}) reached")
            return False, None

        try:
            # Create and connect socket
            print(f"🔌 Connection attempt {attempt}...")
            sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            sock.settimeout(10.0)
            sock.connect((server_ip, server_port))
            print(f"✅ Connected to {server_ip}:{server_port}")

            # Step 1: Receive WELCOME
            welcome_msg = sock.recv(1024).decode().strip()
            if welcome_msg != "WELCOME":
                print(f"⚠️  Expected 'WELCOME' but got: '{welcome_msg}'")
                sock.close()
                time.sleep(delay)
                delay = min(delay * 1.5, 10.0)
                continue

            print(f"📨 Received: {welcome_msg}")

            # Step 2: Send Raspberry ID
            print(f"📤 Sending ID: {raspberry_id}")
            sock.sendall(f"{raspberry_id}\n".encode())

            # Step 3: Receive START
            start_msg = sock.recv(1024).decode().strip()
            if start_msg != "START":
                print(f"⚠️  Expected 'START' but got: '{start_msg}'")
                sock.close()
                time.sleep(delay)
                delay = min(delay * 1.5, 10.0)
                continue

            print(f"📨 Received: {start_msg}")
            print("🎉 Handshake successful!")

            return True, sock

        except KeyboardInterrupt:
            raise
        except socket.timeout:
            print(f"⏰ Connection timeout (attempt {attempt})")
        except ConnectionRefusedError:
            print(
                f"❌ Connection refused by {server_ip}:{server_port} (attempt {attempt})"
            )
        except Exception as e:
            print(f"❌ Connection error (attempt {attempt}): {e}")

        try:
            sock.close()
        except:
            pass

        # Wait before retry with exponential backoff
        print(f"⏳ Retrying in {delay:.1f}s...")
        time.sleep(delay)
        delay = min(delay * 1.5, 10.0)  # Cap at 10 seconds

    return False, None


def start_camera_stream():
    """
    Start the camera stream pipeline using rpicam-vid and FFmpeg
    This creates a TCP server on port 12345 that the server will connect to
    """
    global stream_process

    # Check if port is already in use and kill any process using it
    try:
        print(f"🔍 Checking if port {STREAM_PORT} is available...")
        result = subprocess.run(
            ["fuser", "-k", f"{STREAM_PORT}/tcp"], capture_output=True, timeout=2
        )
        time.sleep(2)  # Wait for port to be released
    except:
        pass

    # Build the camera + FFmpeg pipeline command
    cmd = [
        "bash",
        "-c",
        (
            f"rpicam-vid -t 0 --width {CAMERA_WIDTH} --height {CAMERA_HEIGHT} "
            f"--autofocus-mode manual --lens-position 25 --shutter 20000 --gain 5 "
            f"--contrast 2 --brightness 0.15 --denoise cdn_fast --framerate {CAMERA_FPS} "
            f"--sharpness 5 --codec yuv420 --metadata metadata.txt -v 0 --output - 2>/tmp/rpicam_error.log | "
            f"ffmpeg -f rawvideo -pix_fmt yuv420p -s {CAMERA_WIDTH}x{CAMERA_HEIGHT} "
            f"-framerate {CAMERA_FPS} -i - "
            f'-vf "crop={CROP_SIZE}:{CROP_SIZE},transpose=2,scale={OUTPUT_SIZE}:{OUTPUT_SIZE},'
            f'hue=s=0,format=yuv420p" '
            f"-r {CAMERA_FPS} -c:v libx264 -preset ultrafast -tune zerolatency "
            f"-crf 23 -pix_fmt yuv420p -g {CAMERA_FPS} -b:v 2M -maxrate 2M -bufsize 4M "
            f"-f mpegts tcp://0.0.0.0:{STREAM_PORT}?listen=1"
        ),
    ]

    print("🎥 Starting camera stream pipeline...")
    print(f"   Output: {OUTPUT_SIZE}x{OUTPUT_SIZE} @ {CAMERA_FPS} FPS")
    print(f"   Stream port: {STREAM_PORT}")

    try:
        stream_process = subprocess.Popen(
            cmd, stdout=subprocess.DEVNULL, stderr=subprocess.PIPE, text=True
        )

        print(f"✅ Camera stream started (PID: {stream_process.pid})")

        # Wait longer and verify
        print("⏳ Waiting 10 seconds for stream to stabilize...")
        time.sleep(10)

        poll_result = stream_process.poll()
        if poll_result is not None:
            print(f"❌ Stream process failed with code: {poll_result}")
            # Read error logs
            print("\n📋 Error logs:")
            for log_file in ["/tmp/rpicam_error.log", "/tmp/ffmpeg_stream_error.log"]:
                try:
                    with open(log_file, "r") as f:
                        content = f.read()
                        if content:
                            print(f"\n{log_file}:")
                            print(content[:1000])  # First 1000 chars
                except:
                    pass
            return None

        print("✅ Camera stream is running and stable")
        return stream_process

    except Exception as e:
        print(f"❌ Failed to start camera stream: {e}")
        return None


def monitor_stream_process(process):
    """Monitor the stream process for errors"""
    global connection_active

    try:
        while is_running and connection_active:
            poll_result = process.poll()
            if poll_result is not None:
                print(f"⚠️  Stream process ended with code: {poll_result}")
                connection_active = False
                break

            time.sleep(0.5)

    except Exception as e:
        print(f"❌ Error monitoring stream: {e}")
        connection_active = False
    finally:
        print("🔚 Stream monitor ended")


def main():
    """Main client loop"""
    global is_running, stream_process, heartbeat_socket, connection_active

    # Setup signal handler for graceful shutdown
    signal.signal(signal.SIGINT, signal_handler)
    signal.signal(signal.SIGTERM, signal_handler)

    # Get device ID
    device_id = get_cpu_serial()

    print("=" * 70)
    print("🎥 Raspberry Pi Eye Tracking Client - Enhanced Version")
    print("=" * 70)
    print(f"Device ID: {device_id}")
    print(f"Server: {SERVER_IP}:{SERVER_PORT}")
    print(f"Stream Port: {STREAM_PORT}")
    print(f"Time: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    print("=" * 70)
    print()

    # Main reconnection loop - runs forever until Ctrl+C
    reconnect_delay = 5
    connection_attempt = 0

    while is_running:
        try:
            connection_attempt += 1
            print(f"\n{'='*70}")
            print(f"🔄 CONNECTION ATTEMPT #{connection_attempt}")
            print(f"{'='*70}")

            # Reset connection state
            connection_active = True

            # Step 1: Handshake with server
            print("🤝 Attempting to connect to server...")
            success, control_socket = perform_handshake(
                device_id, SERVER_IP, SERVER_PORT, max_attempts=MAX_CONNECT_ATTEMPTS
            )

            if not success or control_socket is None:
                print(f"❌ Handshake failed. Retrying in {reconnect_delay} seconds...")
                time.sleep(reconnect_delay)
                continue

            heartbeat_socket = control_socket

            # Step 2: Start heartbeat handler thread
            heartbeat_thread = threading.Thread(
                target=heartbeat_handler, args=(control_socket, device_id), daemon=True
            )
            heartbeat_thread.start()

            # Step 3: Start camera stream
            stream_process = start_camera_stream()

            if stream_process is None:
                print("❌ Failed to start camera stream. Closing connection...")
                connection_active = False
                try:
                    control_socket.close()
                except:
                    pass
                print(f"⏳ Retrying in {reconnect_delay} seconds...")
                time.sleep(reconnect_delay)
                continue

            # Step 4: Monitor stream process
            print("✅ System running. Press Ctrl+C to stop.")
            print("💓 Monitoring stream...")
            print()

            monitor_stream_process(stream_process)

            # If we get here, connection was lost or stream ended
            if connection_active:
                print("⚠️  Stream process ended unexpectedly.")
                connection_active = False
            else:
                print("⚠️  Connection lost to server.")

            # Wait for heartbeat thread to finish
            print("⏳ Waiting for heartbeat thread to finish...")
            heartbeat_thread.join(timeout=5)

            print(f"🔄 Reconnecting in {reconnect_delay} seconds...")

            # Cleanup current connection
            print("🧹 Cleaning up connection...")
            if stream_process:
                try:
                    print("  Terminating stream process...")
                    stream_process.terminate()
                    try:
                        stream_process.wait(timeout=5)
                        print("  Stream process terminated")
                    except subprocess.TimeoutExpired:
                        print("  Force killing stream process...")
                        stream_process.kill()
                        stream_process.wait()
                        print("  Stream process killed")
                except Exception as e:
                    print(f"  Error stopping stream: {e}")
                stream_process = None

            # Kill lingering processes
            try:
                print("  Cleaning up lingering camera processes...")
                subprocess.run(
                    ["pkill", "-9", "rpicam-vid"], timeout=2, capture_output=True
                )
                subprocess.run(
                    ["pkill", "-9", "ffmpeg"], timeout=2, capture_output=True
                )
                time.sleep(1)
            except Exception as e:
                print(f"  Note: {e}")

            if control_socket:
                try:
                    print("  Closing control socket...")
                    control_socket.close()
                except Exception as e:
                    print(f"  Error closing socket: {e}")
                heartbeat_socket = None

            # Wait before reconnection
            if is_running:
                print(f"⏳ Waiting {reconnect_delay} seconds before reconnecting...")
                time.sleep(reconnect_delay)

        except KeyboardInterrupt:
            print("\n🛑 Interrupted by user")
            is_running = False
            break
        except Exception as e:
            print(f"❌ Unexpected error: {e}")
            import traceback

            traceback.print_exc()
            time.sleep(reconnect_delay)

    # Final cleanup
    cleanup_and_exit()


if __name__ == "__main__":
    main()
