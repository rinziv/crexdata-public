import socket
import threading
import cv2
import time
import numpy as np
import pandas as pd
import math

# Import processing classes
from classes.frame_labeler import FrameLabeler
from classes.buffer_proc import BufferProc
from classes.dfTransferer import DataFrameTransferer
from classes.convertions import Convertions
from classes.morphProcessing import MorphProcessing

# Import the NN model
import tensorflow  # type: ignore
from tensorflow.keras.models import load_model  # type: ignore
from tensorflow.keras.optimizers import RMSprop  # type: ignore

# Configuration parameters
CONFIG = {
    "model_path": "meye-segmentation_i128_s4_c1_f16_g1_a-relu.hdf5",
    "ROIWIDTH": 400,
    "ROIHEIGHT": 400,
    "TIMEWINDOW": 50,
    "THRESHOLD": 0.65,
    "IMCLOSING": 13,
    "INVERTIMAGE": False,
    "BLINK_THRESHOLD": 0.8,
    "Y_FOV": 67,
    "X_FOV": 102,
    "VERTICAL": 0.864,  # deg/px
    "HORIZONTAL": 0.186,  # deg/px
}


def handle_client(conn, addr, client_id):
    print(f"Connection from {addr}")
    print("Client ID:", client_id)

    # Tell client to start its camera stream, then close the handshake socket
    try:
        conn.sendall(b"start")
    finally:
        conn.close()

    # The sender is listening on its own TCP 1234; we connect to it by IP.
    stream_url = f"tcp://{addr[0]}:1234"
    window_title = f"Stream from {client_id}"

    # Load model
    model = load_model(CONFIG["model_path"], compile=False)
    optimizer = RMSprop(learning_rate=0.0001)
    model.compile(optimizer=optimizer, loss="categorical_crossentropy")
    print("Model loaded successfully")

    # Optional: keep trying for a bit in case the sender needs time to spin up FFmpeg
    backoff = 0.5
    max_backoff = 5.0

    print(f"Opening video stream at {stream_url}…")
    cap = None

    # Initialize processing objects and variables
    Conv = Convertions(CONFIG["VERTICAL"], CONFIG["HORIZONTAL"])
    MorProc = MorphProcessing(CONFIG["THRESHOLD"], CONFIG["IMCLOSING"])
    DataFrameTransf = DataFrameTransferer(CONFIG["TIMEWINDOW"])
    Frame_labeler = FrameLabeler(CONFIG["BLINK_THRESHOLD"])
    
    # Initialize DataFrame
    df = pd.DataFrame(
        {
            "Real Date Time": pd.Series(dtype="string"),
            "datetime": pd.Series(dtype="string"),
            "timeStamp": pd.Series(dtype="float64"),
            "frameN": pd.Series(dtype="float64"),
            "pupilSize": pd.Series(dtype="float64"),
            "pupCntr_x": pd.Series(dtype="float64"),
            "pupCntr_y": pd.Series(dtype="float64"),
            "eyeProb": pd.Series(dtype="float32"),
            "blinkProb": pd.Series(dtype="float32"),
            "angDistance": pd.Series(dtype="float64"),
            "angVelocity": pd.Series(dtype="float64"),
            "angAcceleration": pd.Series(dtype="float64"),
            "label": pd.Series(dtype="object"),
        }
    )
    buffer = df.copy()
    
    # Initialize buffer helping row
    bufferHelpingRow = pd.DataFrame(
        {
            "Real Date Time": [pd.Timestamp.now().strftime("%Y-%m-%d %H:%M:%S.%f")[:-3]],
            "datetime": [pd.Timestamp.now().strftime("%Y-%m-%d %H:%M:%S.%f")[:-3]],
            "timeStamp": [0.0],
            "frameN": [0.0],
            "pupilSize": [0.0],
            "pupCntr_x": [float(CONFIG["ROIWIDTH"] // 2)],
            "pupCntr_y": [float(CONFIG["ROIHEIGHT"] // 2)],
            "eyeProb": [np.float32(1.0)],
            "blinkProb": [np.float32(0.0)],
            "angDistance": [0.0],
            "angVelocity": [0.0],
            "angAcceleration": [0.0],
            "label": ["normal"],
        }
    )
    
    # State variables
    frame_idx = 0
    prevPupCntr = np.nan
    prevTime = np.nan
    angDistVelAcc = [np.nan, np.nan, np.nan]
    saccadesInRow = 0
    fixationsInRow = 0
    blinksInRow = 0
    startEye = False
    getInBuffer = False
    transfers = 0
    prevFrame = 0
    fps = None
    bufferProc = None

    while True:
        if cap is None:
            cap = cv2.VideoCapture(stream_url, cv2.CAP_FFMPEG)
            cap.set(cv2.CAP_PROP_BUFFERSIZE, 1)

        if not cap.isOpened():
            print("Stream not ready. Retrying...")
            cap.release()
            cap = None
            time.sleep(backoff)
            backoff = min(max_backoff, backoff * 1.5)
            continue

        # Get FPS once stream is opened
        if fps is None:
            fps = cap.get(cv2.CAP_PROP_FPS)
            if fps == 0:
                fps = 30.0  # Default fallback
            print(f"Stream FPS: {fps}")
            saccadeFrameLimit = math.ceil(fps / 10)
            blinkFrameLimit = math.ceil(0.4 * fps)
            bufferProc = BufferProc(
                saccadeFrameLimit,
                blinkFrameLimit,
                angVelocity_threshold=240,
                angAcceleration_threshold=3000,
            )

        print("Stream opened. Processing frames. Press 'q' to quit.")
        
        while True:
            ret, frame = cap.read()
            if not ret or frame is None:
                print("Stream ended or read error. Reconnecting...")
                cap.release()
                cap = None
                fps = None
                time.sleep(1.0)
                break

            frame_idx += 1
            
            # Check for frame discontinuity
            if frame_idx != prevFrame + 1 and prevFrame != 0:
                # Reset all state
                df = df.iloc[0:0]
                buffer = buffer.iloc[0:0]
                bufferHelpingRow = pd.DataFrame(
                    {
                        "Real Date Time": [pd.Timestamp.now().strftime("%Y-%m-%d %H:%M:%S.%f")[:-3]],
                        "datetime": [pd.Timestamp.now().strftime("%Y-%m-%d %H:%M:%S.%f")[:-3]],
                        "timeStamp": [0.0],
                        "frameN": [0.0],
                        "pupilSize": [0.0],
                        "pupCntr_x": [float(CONFIG["ROIWIDTH"] // 2)],
                        "pupCntr_y": [float(CONFIG["ROIHEIGHT"] // 2)],
                        "eyeProb": [np.float32(1.0)],
                        "blinkProb": [np.float32(0.0)],
                        "angDistance": [0.0],
                        "angVelocity": [0.0],
                        "angAcceleration": [0.0],
                        "label": ["normal"],
                    }
                )
                angDistVelAcc = [np.nan, np.nan, np.nan]
                prevPupCntr = np.nan
                prevTime = np.nan
                saccadesInRow = 0
                fixationsInRow = 0
                blinksInRow = 0
                startEye = False
                getInBuffer = False
                
            prevFrame = frame_idx
            
            try:
                # Process frame
                if CONFIG["INVERTIMAGE"]:
                    frame = cv2.bitwise_not(frame)
                    
                frame_gray = frame[..., 0] if len(frame.shape) == 3 else frame
                
                # Inpaint bright spots
                _, mas = cv2.threshold(frame_gray, 220, 255, cv2.THRESH_BINARY)
                frame_gray = cv2.inpaint(frame_gray, mas, inpaintRadius=10, flags=cv2.INPAINT_TELEA)
                
                # Prepare for network
                networkInput = frame_gray.astype(np.float32) / 255.0
                networkInput = networkInput[None, :, :, None]
                
                # Run model
                mask, info = model(networkInput)
                prediction = mask[0, :, :, 0]
                eyeProbability = round(info[0, 0].numpy(), 1)
                blinkProbability = round(info[0, 1].numpy(), 1)
                
                # Morphological processing
                morphedMask, _ = MorProc.morphProcessing(prediction)
                resized_mask = cv2.resize(
                    morphedMask, (CONFIG["ROIWIDTH"], CONFIG["ROIHEIGHT"]), interpolation=cv2.INTER_NEAREST
                )
                morphedMask2, centroid2 = MorProc.morphProcessing(resized_mask)
                
                # Calculate angular metrics
                if not np.isnan(centroid2).any() and np.isnan(prevPupCntr).any():
                    prevPupCntr = [centroid2[0], centroid2[1]]
                    prevTime = frame_idx / fps
                    angDistVelAcc = [0, 0, 0]
                elif not np.isnan(centroid2).any() and not np.isnan(prevPupCntr).any():
                    angDistVelAcc = Conv.pxl2deg(
                        prevPupCntr,
                        centroid2,
                        prevTime,
                        frame_idx / fps,
                        angDistVelAcc[1],
                    )
                    prevPupCntr = [centroid2[0], centroid2[1]]
                    prevTime = frame_idx / fps
                    
                # Label frame
                frame_label = Frame_labeler.label_frame(
                    blinkProbability, centroid2, angDistVelAcc
                )
                
                # Create new row
                new_row = pd.DataFrame(
                    {
                        "Real Date Time": [pd.Timestamp.now().strftime("%Y-%m-%d %H:%M:%S.%f")[:-3]],
                        "datetime": [pd.Timestamp.now().strftime("%Y-%m-%d %H:%M:%S.%f")[:-3]],
                        "timeStamp": [frame_idx / fps],
                        "frameN": [frame_idx],
                        "pupilSize": [np.sum(morphedMask2) / 255],
                        "pupCntr_x": [centroid2[0]],
                        "pupCntr_y": [centroid2[1]],
                        "eyeProb": [round(eyeProbability, 1)],
                        "blinkProb": [blinkProbability],
                        "angDistance": [float(angDistVelAcc[0]) if frame_label != "blink" else np.nan],
                        "angVelocity": [float(angDistVelAcc[1]) if frame_label != "blink" else np.nan],
                        "angAcceleration": [float(angDistVelAcc[2]) if frame_label != "blink" else np.nan],
                        "label": [frame_label],
                    }
                )
                
                # Buffer logic from multiprocRealTime.py
                if getInBuffer == False:
                    if frame_label == "fixation":
                        if fixationsInRow != 0:
                            df = pd.concat([df, new_row], ignore_index=True)
                            if len(df) >= CONFIG["TIMEWINDOW"]:
                                df, bufferHelpingRow, transfers = DataFrameTransf.transferToServer(df, transfers)
                        else:
                            firstElementIsBlinkOrFlicker = False
                            fixationsInRow = 1
                            getInBuffer = True
                            buffer = pd.concat([buffer, new_row], ignore_index=True)
                    elif frame_label == "saccade":
                        fixationsInRow = 0
                        firstElementIsBlinkOrFlicker = False
                        getInBuffer = True
                        buffer = pd.concat([buffer, new_row], ignore_index=True)
                    elif frame_label == "blink" or frame_label == "flicker":
                        fixationsInRow = 0
                        firstElementIsBlinkOrFlicker = True
                        getInBuffer = True
                        filtered_df = df[::-1][df["pupCntr_x"].notnull() & df["pupCntr_y"].notnull()]
                        if not filtered_df.empty:
                            bufferHelpingRow = pd.DataFrame([filtered_df.iloc[0]])
                            filtered_df = filtered_df.drop(filtered_df.index)
                        buffer = pd.concat([buffer, new_row], ignore_index=True)
                    else:
                        firstElementIsBlinkOrFlicker = False
                        df = pd.concat([df, new_row], ignore_index=True)
                        if len(df) >= CONFIG["TIMEWINDOW"]:
                            df, bufferHelpingRow, transfers = DataFrameTransf.transferToServer(df, transfers)
                else:
                    if frame_label == "fixation":
                        fixationsInRow += 1
                        buffer = pd.concat([buffer, new_row], ignore_index=True)
                        if fixationsInRow == saccadeFrameLimit:
                            getInBuffer = False
                            buffer = bufferProc.buffer_proc(buffer, firstElementIsBlinkOrFlicker, bufferHelpingRow)
                            df, buffer = DataFrameTransf.transferBufferToMainDF(buffer, df)
                            df, bufferHelpingRow, transfers = DataFrameTransf.transferToServer(df, transfers)
                    elif frame_label == "saccade" or frame_label == "flicker" or frame_label == "blink":
                        fixationsInRow = 0
                        buffer = pd.concat([buffer, new_row], ignore_index=True)
                    else:
                        getInBuffer = False
                        buffer = pd.concat([buffer, new_row], ignore_index=True)
                        buffer = bufferProc.buffer_proc(buffer, firstElementIsBlinkOrFlicker, bufferHelpingRow)
                        df, buffer = DataFrameTransf.transferBufferToMainDF(buffer, df)
                        df, bufferHelpingRow, transfers = DataFrameTransf.transferToServer(df, transfers)
                
                # Optional: Display frame
                cv2.imshow(window_title, frame)
                
            except Exception as e:
                print(f"Error processing frame {frame_idx}: {e}")
            
            key = cv2.waitKey(1) & 0xFF
            if (
                key == ord("q")
                or cv2.getWindowProperty(window_title, cv2.WND_PROP_VISIBLE) < 1
            ):
                print("User requested exit.")
                if cap is not None:
                    cap.release()
                cv2.destroyAllWindows()
                return

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
