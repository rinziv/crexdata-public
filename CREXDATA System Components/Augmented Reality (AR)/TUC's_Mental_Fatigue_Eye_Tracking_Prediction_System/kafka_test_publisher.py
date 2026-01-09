#!/usr/bin/env python3
"""
Dual Raspberry Pi Kafka publisher for testing the 'mental' topic.
Controls two virtual Raspberry Pi devices with keyboard commands.
"""

import json
import time
import threading
from datetime import datetime
from kafka import KafkaProducer
import sys
import os
import random

# Windows-specific imports for non-blocking keyboard input
if os.name == "nt":
    import msvcrt
else:
    import select
    import tty
    import termios


def create_producer():
    """Create and return a Kafka producer with the same config as your main script."""
    return KafkaProducer(
        bootstrap_servers="localhost:9092",
        acks="all",  # wait for leader + replicas (safer than default "1")
        retries=5,  # retry up to 5 times on errors
        linger_ms=10,  # wait 10ms to batch more messages (throughput boost)
        batch_size=32_768,  # up to 32KB per batch
        value_serializer=lambda v: json.dumps(v).encode(
            "utf-8"
        ),  # dict -> JSON -> bytes
        key_serializer=lambda k: k.encode("utf-8") if isinstance(k, str) else k,
    )


def get_key():
    """Get a single key press without Enter (Windows compatible)"""
    if os.name == "nt":  # Windows
        if msvcrt.kbhit():
            return msvcrt.getch().decode("utf-8").lower()
    else:  # Unix/Linux/Mac
        if select.select([sys.stdin], [], [], 0) == ([sys.stdin], [], []):
            return sys.stdin.read(1).lower()
    return None


class RaspberryPiSender:
    def __init__(self, raspberry_id, device_name, producer):
        self.raspberry_id = raspberry_id
        self.device_name = device_name
        self.producer = producer
        self.active = False
        self.label_counter = 0
        self.last_send_time = 0
        self.random_labels = True

    def send_message(self):
        """Send a message if this device is active and enough time has passed"""
        current_time_ms = time.time()

        if self.active and (current_time_ms - self.last_send_time >= 1.0):
            # Create current timestamp in the same format
            timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S.%f")

            # Create the message data
            # choose label either by cycling or randomly
            if self.random_labels:
                label_value = str(random.randint(1, 4))
            else:
                label_value = str(self.label_counter % 4 + 1)

            data = {
                "last_real_datetime": timestamp,
                "label": label_value,
                "raspberryID": self.raspberry_id,
            }

            # Send message to Kafka
            try:
                self.producer.send("mental", value=data)
                # print(f"[{self.device_name}] Sent: {json.dumps(data)}")
                self.label_counter += 1
                self.last_send_time = current_time_ms
            except Exception as e:
                print(f"[{self.device_name}] Error sending: {e}")

    def start(self):
        """Start this device"""
        self.active = True
        print(f"[{self.device_name}] STARTED (ID: {self.raspberry_id})")

    def stop(self):
        """Stop this device"""
        self.active = False
        print(f"[{self.device_name}] STOPPED")

    def set_random_labels(self, enabled: bool):
        """Enable or disable random label selection for this sender."""
        self.random_labels = enabled
        print(f"[{self.device_name}] Random labels set to {self.random_labels}")


def menu():
    """Display the control menu"""
    print("\nControl Menu:")
    print("  z - Start Pi-1 (ID: 1000000000ea781d)")
    print("  q - Stop Pi-1")
    print("  x - Start Pi-2 (ID: 2000000000fb892e)")
    print("  w - Stop Pi-2")
    print("  c - Start Pi-3 (ID: 3000000000ea781d)")
    print("  e - Stop Pi-3")
    print("  h - Show this menu")
    print("  ESC - Exit\n")


def main():
    """Main function to manage dual Raspberry Pi senders."""
    menu()

    try:
        # Create producer
        producer = create_producer()

        # Create two virtual Raspberry Pi devices (both start inactive)
        pi1 = RaspberryPiSender("1000000000ea781d", "Pi-1", producer)
        pi2 = RaspberryPiSender("2000000000fb892e", "Pi-2", producer)
        pi3 = RaspberryPiSender("3000000000ea781d", "Pi-3", producer)

        print("Both devices are initially STOPPED")
        print("Press keys to control devices...")

        while True:
            # Check for keyboard input
            key = get_key()

            if key:
                if key == "z":
                    pi1.start()
                elif key == "q":
                    pi1.stop()
                elif key == "x":
                    pi2.start()
                elif key == "c":
                    pi3.start()
                elif key == "w":
                    pi2.stop()
                elif key == "e":
                    pi3.stop()
                elif key == "h":
                    menu()
                elif key == "r":
                    # toggle random labels for all devices
                    new_state = not (
                        pi1.random_labels or pi2.random_labels or pi3.random_labels
                    )
                    pi1.set_random_labels(new_state)
                    pi2.set_random_labels(new_state)
                    pi3.set_random_labels(new_state)
                    print(f"Random labels toggled -> {new_state}")
                elif key == "\x1b":  # ESC key
                    print("\nExiting...")
                    break

            # Send messages from active devices
            pi1.send_message()
            pi2.send_message()
            pi3.send_message()

            # Small sleep to prevent high CPU usage
            time.sleep(0.1)

    except KeyboardInterrupt:
        print("\nStopping publisher...")
    except Exception as e:
        print(f"Error: {e}")
    finally:
        # Close producer
        if "producer" in locals():
            producer.close()
            print("Producer closed.")


if __name__ == "__main__":
    main()
