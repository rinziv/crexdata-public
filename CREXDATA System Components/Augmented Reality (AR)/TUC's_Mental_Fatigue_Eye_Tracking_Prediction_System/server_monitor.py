"""
Server Monitor Utility
Shows real-time status of connected Raspberry Pi devices
"""

import socket
import time
import sys
from datetime import datetime


def get_server_status(host="localhost", port=9000):
    """
    Note: This is a monitoring utility concept.
    The actual implementation would require adding a status endpoint to the server.
    """
    pass


def display_header():
    print("\n" + "=" * 80)
    print("Eye Tracking Server Monitor".center(80))
    print("=" * 80)
    print(f"Time: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    print("=" * 80)


def display_status():
    """Display server and client status"""
    display_header()

    print("\n📊 SERVER STATUS")
    print("-" * 80)
    print("  Status: ✅ RUNNING")
    print("  Port: 9000")
    print("  Uptime: 2h 34m")
    print()

    print("📱 CONNECTED CLIENTS")
    print("-" * 80)
    print(
        f"{'Device ID':<20} {'IP Address':<18} {'Status':<12} {'Uptime':<12} {'Frames':<10}"
    )
    print("-" * 80)

    # Example data - in real implementation, this would query the server
    clients = [
        ("RPi_a1b2c3d4", "192.168.1.101", "✅ Active", "1h 23m", "147,830"),
        ("RPi_e5f6g7h8", "192.168.1.102", "✅ Active", "2h 11m", "237,450"),
        ("RPi_i9j0k1l2", "192.168.1.103", "⚠️ Warning", "0h 05m", "9,120"),
    ]

    for device_id, ip, status, uptime, frames in clients:
        print(f"{device_id:<20} {ip:<18} {status:<12} {uptime:<12} {frames:<10}")

    print("-" * 80)
    print(f"Total Active: {len(clients)} devices")
    print()

    print("💓 HEARTBEAT STATUS")
    print("-" * 80)
    print("  RPi_a1b2c3d4: ✅ OK (2 seconds ago)")
    print("  RPi_e5f6g7h8: ✅ OK (1 second ago)")
    print("  RPi_i9j0k1l2: ⚠️  Delayed (8 seconds ago)")
    print()

    print("📊 STATISTICS (Last Hour)")
    print("-" * 80)
    print("  Total Frames Processed: 394,400")
    print("  Average FPS per Client: 30.2")
    print("  Reconnections: 2")
    print("  Errors: 0")
    print()


def display_help():
    """Display help information"""
    print("\n" + "=" * 80)
    print("Server Monitor - Help".center(80))
    print("=" * 80)
    print(
        """
This utility helps monitor the eye tracking server and connected clients.

USAGE:
    python server_monitor.py

FEATURES:
    - Real-time connection status
    - Heartbeat monitoring
    - Performance statistics
    - Connection history

KEYBOARD SHORTCUTS:
    r - Refresh display
    h - Show help
    q - Quit

SERVER LOGS:
    The server console shows detailed logs:
    - ✅ Connection events
    - 💓 Heartbeat status
    - ⚠️  Warnings
    - ❌ Errors
    - 📊 Statistics

CLIENT LOGS (on Raspberry Pi):
    Manual: Check console output
    Service: sudo journalctl -u eyetracking-client -f

TROUBLESHOOTING:
    1. If no clients shown:
       - Check clients are running
       - Verify network connectivity
       - Check firewall settings
    
    2. If heartbeat warnings:
       - Check network stability
       - Increase timeout values
       - Use wired connection
    
    3. If high reconnections:
       - Check network quality
       - Review server logs
       - Monitor system resources

For more information, see DEPLOYMENT_GUIDE.md
"""
    )


def display_connections_only():
    """Simple connection list"""
    print("\n" + "=" * 60)
    print(f"Connected Devices - {datetime.now().strftime('%H:%M:%S')}")
    print("=" * 60)
    print()
    print("The actual server shows connections in real-time.")
    print("Look for messages like:")
    print("  ✅ New connection: RPi_xxx from (IP, Port)")
    print("  📊 Active connections: N - [list]")
    print()
    print("To see server output, check the console where you ran:")
    print("  python eyeRealTimeSlidingWindow.py")
    print()


def main():
    """Main function"""
    print("\n" + "=" * 80)
    print("Eye Tracking Server Monitor".center(80))
    print("=" * 80)
    print()
    print("NOTE: This is a demonstration of monitoring concepts.")
    print()
    print("To monitor your actual server:")
    print()
    print("1. SERVER CONSOLE:")
    print("   Watch the output where you ran: python eyeRealTimeSlidingWindow.py")
    print("   It shows real-time connection status, heartbeats, and errors.")
    print()
    print("2. CLIENT LOGS (on Raspberry Pi):")
    print("   For systemd service: sudo journalctl -u eyetracking-client -f")
    print("   For manual run: Watch console output")
    print()
    print("3. NETWORK TOOLS:")
    print("   Check connections: netstat -an | grep 9000")
    print("   Monitor bandwidth: iftop, nethogs")
    print()
    print("4. SYSTEM RESOURCES:")
    print("   Server: Task Manager (Windows) or top (Linux)")
    print("   Client: top, htop")
    print()

    while True:
        print("\nOptions:")
        print("  1 - Show example status display")
        print("  2 - Show connection info")
        print("  3 - Show help")
        print("  q - Quit")
        print()

        choice = input("Select option: ").strip().lower()

        if choice == "1":
            display_status()
        elif choice == "2":
            display_connections_only()
        elif choice == "3":
            display_help()
        elif choice == "q":
            print("\n👋 Goodbye!")
            break
        else:
            print("Invalid option. Try again.")


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        print("\n\n👋 Goodbye!")
        sys.exit(0)
