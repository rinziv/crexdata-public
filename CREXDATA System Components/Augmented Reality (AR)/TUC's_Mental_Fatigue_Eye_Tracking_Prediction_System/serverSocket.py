import socket
import threading
import cv2
import time


def handle_client(conn, addr, client_id):
    print(f"Connection from {addr}")
    print("Client ID:", client_id)

    # Tell client to start its camera stream, then close the handshake socket
    try:
        conn.sendall(b"start")
    finally:
        conn.close()

    # The sender is listening on its own TCP 1234; we connect to it by IP.
    # (No 'listen=1' here — that's only for the sender.)
    stream_url = f"tcp://{addr[0]}:1234"
    window_title = f"Stream from {client_id}"

    # Optional: keep trying for a bit in case the sender needs time to spin up FFmpeg
    backoff = 0.5
    max_backoff = 5.0

    print(f"Opening video stream at {stream_url}…")
    cap = None

    while True:
        if cap is None:
            cap = cv2.VideoCapture(stream_url, cv2.CAP_FFMPEG)
            # Try to keep latency low (may be ignored depending on build)
            cap.set(cv2.CAP_PROP_BUFFERSIZE, 1)

        if not cap.isOpened():
            print("Stream not ready. Retrying...")
            cap.release()
            cap = None
            time.sleep(backoff)
            backoff = min(max_backoff, backoff * 1.5)
            continue

        print("Stream opened. Press 'q' to quit this viewer.")
        while True:
            ret, frame = cap.read()
            if not ret or frame is None:
                print("Stream ended or read error. Reconnecting...")
                cap.release()
                cap = None
                time.sleep(1.0)
                break  # break inner loop to reconnect
            cv2.imshow(window_title, frame)
            key = cv2.waitKey(1) & 0xFF
            if (
                key == ord("q")
                or cv2.getWindowProperty(window_title, cv2.WND_PROP_VISIBLE) < 1
            ):
                print("User requested exit.")
                if cap is not None:
                    cap.release()
                cv2.destroyAllWindows()
                return  # leave the function

        # small delay before trying to reopen after a drop
        time.sleep(0.5)


def start_server():
    server_socket = socket.socket()
    server_socket.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    server_socket.bind(("0.0.0.0", 9000))
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
