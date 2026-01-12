import cv2
import time


class Reconnect:
    def __init__(self, VIDEOSOURCE):
        self.VIDEOSOURCE = VIDEOSOURCE

    def reconnect(source, retries=5, delay=2.0):
        for attempt in range(1, retries + 1):
            print(f"Reconnect attempt {attempt}/{retries}...")
            cap = cv2.VideoCapture(source, cv2.CAP_FFMPEG)
            if cap.isOpened():
                print("✅ Reconnected!")
                return cap
            else:
                print("Reconnect failed, waiting...")
                cap.release()
                time.sleep(delay)
        raise RuntimeError("Failed to reconnect after several attempts.")
