import socket
import threading
import cv2
import time


def handle_client(conn, addr, id):
    print(f"Connection from {addr}")
    print("Raspberry serial number:", id)

    # Tell Raspberry to start its camera stream
    conn.sendall(b"start")
    conn.close()

    # Single‑shot OpenCV stream
    stream_url = "tcp://0.0.0.0:1234?listen"  # adjust to your stream URL
    print(f"Opening video stream at {stream_url}…")
    cap = cv2.VideoCapture(stream_url, cv2.CAP_FFMPEG)

    if not cap.isOpened():
        print("ERROR: Could not open video stream.")

    print("Stream opened. Press 'q' to quit.")
    window_name = f"Stream from {id}"
    cv2.namedWindow(window_name, cv2.WINDOW_NORMAL)
    while True:
        ret, frame = cap.read()
        if not ret:
            print("Stream ended or read error.")
            break
        # Get current window size
        _, _, win_w, win_h = cv2.getWindowImageRect(window_name)

        # Resize frame to match
        frame_resized = cv2.resize(
            frame, (win_w, win_h), interpolation=cv2.INTER_LINEAR
        )

        cv2.imshow(window_name, frame_resized)
        if cv2.waitKey(1) & 0xFF == ord("q"):
            print("User requested exit.")
            break

    cap.release()
    cv2.destroyAllWindows()


def start_server():
    server_socket = socket.socket()
    server_socket.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    server_socket.bind(("0.0.0.0", 8000))
    server_socket.listen(5)
    server_socket.settimeout(1.0)  # timeout after 1 second

    print("Server listening…")

    try:
        while True:
            try:
                conn, addr = server_socket.accept()
                try:
                    conn.sendall("welcome\n".encode())
                    chunk = conn.recv(1024)
                    if not chunk:
                        print("Server closed connection.")
                        conn.close()
                    else:
                        id = chunk.decode().strip()
                        t = threading.Thread(
                            target=handle_client, args=(conn, addr, id), daemon=True
                        )
                        t.start()
                        # stop_event.set()
                        # t.join()
                except Exception as e:
                    print(e)
            except socket.timeout:
                continue  # go back to top of loop and check for Ctrl+C
    except KeyboardInterrupt:
        print("\nShutting down server…")
    finally:
        server_socket.close()


start_server()
