import os
import numpy as np
import pandas as pd
import matplotlib.pyplot as plt
import matplotlib.patches as ptch
import time
import multiprocessing
from datetime import datetime, timedelta
import math
import json


# from multiprocessing import Process, Queue, cpu_count
# matplotlib inline  # Remove or comment out this line
import cv2 as cv
from confluent_kafka import Producer
from kafka import KafkaProducer
from skimage.measure import label, regionprops
from classes.frame_labeler import FrameLabeler
from classes.buffer_proc import BufferProc
from classes.dfTransferer import DataFrameTransferer
from classes.reconnect import Reconnect  # Add this line to import Reconnect
from classes.convertions import Convertions
from classes.morphProcessing import MorphProcessing

# Import the NN model
import tensorflow  # type: ignore
from tensorflow.keras.models import load_model  # type: ignore
from tensorflow.keras.optimizers import RMSprop  # type: ignore

####################  SET UP PRODUCER ######################

# print("Starting Kafka Producer")
# conf = {"bootstrap.servers": "localhost:9092"}
# print("connecting to Kafka topic...")
# PRODUCER = Producer(conf)
NAME = "magda"  # change this to the name of the subject

videoSource = (
    # f"C:/Users/AlexisPC/Desktop/Calibration/{NAME}/{NAME}Cali.mp4"
    f"C:/Users/AlexisPC/Desktop/UPDATED_INDUCING_MENTAL_FATIGUE/{NAME}/{NAME}.mp4"
)
# videoSource = "udp://192.168.0.102:5000"
# source_type = "udp"
source_type = "file"  # Change to 'udp' for live video stream
model_path = "C:/Users/AlexisPC/Desktop/pythonEYE/meye-segmentation_i128_s4_c1_f16_g1_a-relu.hdf5"  # Update this path to the location of your model file
ROIWIDTH = 400
ROIHEIGHT = 400
TIMEWINDOW = 50  # time window for data transfer to server
start_time_str = "4/1/2025 4:31:23 PM"
start_time = datetime.strptime(start_time_str, "%m/%d/%Y %I:%M:%S %p")


def wait_for_non_zero(value):
    # Wait until the value is different from 0.0
    print("Waiting for value to become non-zero...")
    while value.value == 0.0:
        time.sleep(0.1)  # Sleep to prevent busy-waiting CPU usage
    print(f"Value is now non-zero: {value}")


def producer(queue, fps, Y_PIXELS, X_PIXELS, videoSource, source_type):
    Recon = Reconnect(videoSource)
    cap = cv.VideoCapture(videoSource, cv.CAP_FFMPEG)

    if not cap.isOpened():
        print("Error: Could not open video source.")
        exit()

    with fps.get_lock():
        fps.value = cap.get(cv.CAP_PROP_FPS)
    with Y_PIXELS.get_lock():
        Y_PIXELS.value = cap.get(cv.CAP_PROP_FRAME_HEIGHT)
    with X_PIXELS.get_lock():
        X_PIXELS.value = cap.get(cv.CAP_PROP_FRAME_WIDTH)

    try:
        while True:
            ret, frame = cap.read()
            if not ret:
                if source_type == "udp":
                    cap.release()
                    print("Lost connection, attempting to reconnect...")
                    cap = Recon.reinitialize_capture()

                    with fps.get_lock():
                        fps.value = cap.get(cv.CAP_PROP_FPS)
                    with Y_PIXELS.get_lock():
                        Y_PIXELS.value = cap.get(cv.CAP_PROP_FRAME_HEIGHT)
                    with X_PIXELS.get_lock():
                        X_PIXELS.value = cap.get(cv.CAP_PROP_FRAME_WIDTH)
                else:
                    print("Reached end of video file.")
                    break  # Exit loop if it's a stored local video file
            else:
                queue.put((frame, cap.get(cv.CAP_PROP_POS_FRAMES)))
    except Exception as e:
        print(f"An error occurred while processing frame: {e}")
    finally:
        cap.release()


def consumer(queue, fps, Y_PIXELS, X_PIXELS, model_path, meta_data_df):
    model = load_model(model_path, compile=False)
    optimizer = RMSprop(learning_rate=0.0001)
    model.compile(optimizer=optimizer, loss="categorical_crossentropy")
    print("Model loaded successfully")

    THRESHOLD = 0.65  # probability threshold for image binarization
    IMCLOSING = 13  # pixel radius of circular kernel for morphological closing
    INVERTIMAGE = False
    BLINK_THRESHOLD = 0.8  # Blink probability threshold
    Y_FOV = 67  # Vertical field of view of the camera in degrees
    X_FOV = 102  # Horizontal field of view of the camera in degrees
    transfers = 0
    wait_for_non_zero(fps)
    wait_for_non_zero(Y_PIXELS)
    wait_for_non_zero(X_PIXELS)
    saccadeFrameLimit = math.ceil(fps.value / 10)
    blinkFrameLimit = math.ceil(0.4 * fps.value)
    # Y_degs_per_pixel = Y_FOV / 1080
    # X_degs_per_pixel = X_FOV / 1920

    VERTICAL = 0.864
    HORIZONTAL = 0.186

    print("VERTICAL deg/px:", VERTICAL)
    print("HORIZONTAL deg/px:", HORIZONTAL)
    # requiredFrameSize = list(model.input.shape[1:3])
    Conv = Convertions(VERTICAL, HORIZONTAL)
    MorProc = MorphProcessing(THRESHOLD, IMCLOSING)
    DataFrameTransf = DataFrameTransferer(TIMEWINDOW)
    Frame_labeler = FrameLabeler(BLINK_THRESHOLD)
    df = pd.DataFrame(
        {
            "Real Date Time": pd.Series(dtype="string"),  # Define as string
            "datetime": pd.Series(dtype="string"),  # Define as string
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
    buffer = df
    new_row = df
    bufferHelpingRow = df
    bufferHelpingRow = pd.DataFrame(
        {
            "Real Date Time": [
                pd.Timestamp.now().strftime("%Y-%m-%d %H:%M:%S.%f")[:-3]
            ],  # Millisecond precision
            "datetime": [
                pd.Timestamp.now().strftime("%Y-,%m-%d %H:%M:%S.%f")[:-3]
            ],  # Millisecond precision
            "timeStamp": [0.0],  # Single float64 value
            "frameN": [0.0],  # Single float64 value
            "pupilSize": [0.0],  # Single float64 value
            "pupCntr_x": [float(ROIWIDTH // 2)],  # Single float64 value
            "pupCntr_y": [float(ROIWIDTH // 2)],  # Single float64 value
            "eyeProb": [np.float32(1.0)],  # Single float32 value
            "blinkProb": [np.float32(0.0)],  # Single float32 value
            "angDistance": [0.0],  # Single float64 value
            "angVelocity": [0.0],  # Single float64 value
            "angAcceleration": [0.0],  # Single float64 value
            "label": ["normal"],  # Single string value
        }
    )
    centroid = [np.nan, np.nan]  # Initialize centroid as a list with two elements
    angDistVelAcc = [
        np.nan,
        np.nan,
        np.nan,
    ]  # Initialize angDistance, angVelocity,bangAcceleration
    frame_idx = 0
    prevPupCntr = np.nan
    prevTime = np.nan
    total_time = 0  # Initialize total_time
    saccadesInRow = 0
    fixationsInRow = 0
    blinksInRow = 0
    startEye = False
    getInBuffer = False
    bufferProc = BufferProc(
        saccadeFrameLimit,
        blinkFrameLimit,
        angVelocity_threshold=240,
        angAcceleration_threshold=3000,
    )
    prevFrame = 0
    while True:
        try:
            frame, frame_idx = queue.get()
            if frame_idx != prevFrame + 1:
                prevFrame = 0
                df = df.iloc[0:0]

                buffer = buffer.iloc[0:0]
                bufferHelpingRow = pd.DataFrame(
                    {
                        "Real Date Time": [
                            pd.Timestamp.now().strftime("%Y-%m-%d %H:%M:%S.%f")[:-3]
                        ],  # Millisecond precision
                        "datetime": [
                            pd.Timestamp.now().strftime("%Y-%m-%d %H:%M:%S")
                        ],  # Datetime with millisecond precision
                        "timeStamp": [0.0],  # Single float64 value
                        "frameN": [0.0],  # Single float64 value
                        "pupilSize": [0.0],  # Single float64 value
                        "pupCntr_x": [float(ROIWIDTH // 2)],  # Single float64 value
                        "pupCntr_y": [float(ROIWIDTH // 2)],  # Single float64 value
                        "eyeProb": [np.float32(1.0)],  # Single float32 value
                        "blinkProb": [np.float32(0.0)],  # Single float32 value
                        "angDistance": [0.0],  # Single float64 value
                        "angVelocity": [0.0],  # Single float64 value
                        "angAcceleration": [0.0],  # Single float64 value
                        "label": ["normal"],  # Single string value
                    }
                )

                angDistVelAcc = [np.nan, np.nan, np.nan]
                prevPupCntr = np.nan
                prevTime = np.nan
                total_time = 0
                saccadesInRow = 0
                fixationsInRow = 0
                blinksInRow = 0
                startEye = False
                getInBuffer = False
            else:
                prevFrame = frame_idx
                if INVERTIMAGE:
                    frame = cv.bitwise_not(frame)
                frame = frame[..., 0]
                _, mas = cv.threshold(
                    frame, 220, 255, cv.THRESH_BINARY
                )  # <-- corrected to 255
                frame = cv.inpaint(frame, mas, inpaintRadius=10, flags=cv.INPAINT_TELEA)

                networkInput = (
                    frame.astype(np.float32) / 255.0
                )  # convert from uint8 [0, 255] to float32 [0, 1]
                networkInput = networkInput[
                    None, :, :, None
                ]  # add batch and channel dimensions
                mask, info = model(networkInput)
                prediction = mask[0, :, :, 0]
                np.set_printoptions(threshold=np.inf)
                eyeProbability = round(info[0, 0].numpy(), 1)
                blinkProbability = round(info[0, 1].numpy(), 1)
                ##################################################
                morphedMask, _ = MorProc.morphProcessing(prediction)
                resized_mask = cv.resize(
                    morphedMask, (ROIWIDTH, ROIHEIGHT), interpolation=cv.INTER_NEAREST
                )
                morphedMask2, centroid2 = MorProc.morphProcessing(resized_mask)
                if not np.isnan(centroid2).any() and np.isnan(prevPupCntr).any():
                    prevPupCntr = [centroid2[0], centroid2[1]]
                    prevTime = frame_idx / fps.value
                    angDistVelAcc = [0, 0, 0]
                elif not np.isnan(centroid2).any() and not np.isnan(prevPupCntr).any():

                    angDistVelAcc = Conv.pxl2deg(
                        prevPupCntr,
                        centroid2,
                        prevTime,
                        frame_idx / fps.value,
                        angDistVelAcc[1],
                    )
                    prevPupCntr = [centroid2[0], centroid2[1]]
                    prevTime = frame_idx / fps.value
                frame_label = Frame_labeler.label_frame(
                    blinkProbability, centroid2, angDistVelAcc
                )
            ###################################################
            # uncomment the following lines to visualize the results

            # falseColor = np.dstack((morphedMask, frame, frame))

            # if not np.isnan(centroid[0]):
            #     cY, cX = int(centroid[0]), int(centroid[1])
            #     cv.drawMarker(
            #         falseColor,
            #         (cX, cY),
            #         (0, 255, 0),
            #         markerType=cv.MARKER_CROSS,
            #         markerSize=12,
            #         thickness=2,
            #     )

            # # Convert original frame from grayscale to RGB to match falseColor channels
            # frame_rgb = cv.cvtColor(frame, cv.COLOR_GRAY2BGR)

            # # Horizontally stack original frame and falseColor image
            # side_by_side = np.hstack((frame_rgb, falseColor))

            # plt.imshow(cv.cvtColor(side_by_side, cv.COLOR_BGR2RGB))
            # plt.title("Original Frame (left) and Binary Pupil (right)")
            # plt.axis("off")
            # plt.show(block=False)
            # plt.pause(0.001)  # short pause to simulate video
            # plt.clf()

            #####################################################################
            new_row = pd.DataFrame(
                {
                    "Real Date Time": [
                        (
                            pd.Timestamp.now()
                            if source_type == "udp"
                            else meta_data_df.loc[
                                meta_data_df["Frame"] == frame_idx,
                                "FrameWallClock_datetime",
                            ].values[0]
                        )
                    ],
                    "datetime": [
                        pd.Timestamp.now()
                    ],  # Datetime with millisecond precision
                    "timeStamp": [frame_idx / fps.value],
                    "frameN": [frame_idx],
                    "pupilSize": np.sum(morphedMask2) / 255,
                    "pupCntr_x": centroid2[0],
                    "pupCntr_y": centroid2[1],
                    "eyeProb": round(eyeProbability, 1),
                    "blinkProb": blinkProbability,
                    "angDistance": [
                        float(angDistVelAcc[0]) if frame_label != "blink" else np.nan
                    ],  # Convert to float64
                    "angVelocity": [
                        float(angDistVelAcc[1]) if frame_label != "blink" else np.nan
                    ],  # Convert to float64
                    "angAcceleration": [
                        float(angDistVelAcc[2]) if frame_label != "blink" else np.nan
                    ],
                    "label": [frame_label],  # Convert label to string
                }
            )

            # new_row["label"] = new_row["label"].astype("string")
            ###################################################
            if getInBuffer == False:
                if frame_label == "fixation":
                    if fixationsInRow != 0:

                        df = pd.concat([df, new_row], ignore_index=True)

                        if len(df) >= TIMEWINDOW:
                            # print("DF NO BUFFER BEFORE:", df.dtypes)
                            df, bufferHelpingRow, transfers = (
                                DataFrameTransf.transferToServer(df, transfers)
                            )

                    else:
                        firstElementIsBlinkOrFlicker = False
                        fixationsInRow = 1
                        getInBuffer = True
                        # bufferLastRow = df[::-1][
                        #     df["pupCntr_x"].notnull() & df["pupCntr_y"].notnull()
                        # ].iloc[0]
                        buffer = pd.concat([buffer, new_row], ignore_index=True)
                elif frame_label == "saccade":
                    fixationsInRow = 0
                    firstElementIsBlinkOrFlicker = False
                    getInBuffer = True
                    # print("First saccade")
                    buffer = pd.concat([buffer, new_row], ignore_index=True)
                elif frame_label == "blink" or frame_label == "flicker":
                    fixationsInRow = 0
                    firstElementIsBlinkOrFlicker = True
                    getInBuffer = True
                    filtered_df = df[::-1][
                        df["pupCntr_x"].notnull() & df["pupCntr_y"].notnull()
                    ]
                    if not filtered_df.empty:
                        bufferHelpingRow = pd.DataFrame([filtered_df.iloc[0]])

                        filtered_df = filtered_df.drop(filtered_df.index)

                    buffer = pd.concat([buffer, new_row], ignore_index=True)
                else:
                    firstElementIsBlinkOrFlicker = False
                    df = pd.concat([df, new_row], ignore_index=True)
                    if len(df) >= TIMEWINDOW:

                        df, bufferHelpingRow, transfers = (
                            DataFrameTransf.transferToServer(df, transfers)
                        )

            else:
                if frame_label == "fixation":
                    fixationsInRow += 1
                    buffer = pd.concat([buffer, new_row], ignore_index=True)

                    if fixationsInRow == saccadeFrameLimit:
                        getInBuffer = False
                        buffer = bufferProc.buffer_proc(
                            buffer,
                            firstElementIsBlinkOrFlicker,
                            bufferHelpingRow,
                        )
                        # print("BUFFER 4:", buffer.dtypes)
                        df, buffer = DataFrameTransf.transferBufferToMainDF(buffer, df)
                        df, bufferHelpingRow, transfers = (
                            DataFrameTransf.transferToServer(df, transfers)
                        )
                        # print("BUFFER2 4:", buffer.dtypes)

                elif (
                    frame_label == "saccade"
                    or frame_label == "flicker"
                    or frame_label == "blink"
                ):
                    fixationsInRow = 0
                    buffer = pd.concat([buffer, new_row], ignore_index=True)
                else:
                    getInBuffer = False
                    buffer = pd.concat([buffer, new_row], ignore_index=True)

                    buffer = bufferProc.buffer_proc(
                        buffer,
                        firstElementIsBlinkOrFlicker,
                        bufferHelpingRow,
                    )
                    df, buffer = DataFrameTransf.transferBufferToMainDF(buffer, df)
                    df, bufferHelpingRow, transfers = DataFrameTransf.transferToServer(
                        df, transfers
                    )

        ###################################################
        except Exception as e:
            print(f"An error occurred while processing frame {frame_idx}: {e}")
        # if cv.waitKey(1) & 0xFF == ord("q"):
        #     break
        # print("END OF WHILE",df.dtypes)
        # print(df)


if __name__ == "__main__":
    # --- Build metadata DataFrame (from rpicam --metadata) ---
    meta_data_df = pd.DataFrame()  # safe default

    if source_type == "file":
        meta_path = f"C:/Users/AlexisPC/Desktop/UPDATED_INDUCING_MENTAL_FATIGUE/{NAME}/metadata.txt"
        # meta_path = f"C:/Users/AlexisPC/Desktop/Calibration/{NAME}/metadata.txt"

        # Read once; tolerate BOM. Detect JSON array vs NDJSON (one JSON per line).
        with open(meta_path, "r", encoding="utf-8-sig") as f:
            first = f.read(1)
            f.seek(0)
            if first == "[":  # JSON array of per-frame dicts
                data = json.load(f)
            else:  # NDJSON (one JSON object per line)
                data = [json.loads(line) for line in f if line.strip().startswith("{")]

        # Build DataFrame (Frame starts at 1)
        meta_data_df = pd.DataFrame(
            {
                "Frame": np.arange(1, len(data) + 1, dtype=np.int64),
                "FrameWallClock": [frame.get("FrameWallClock") for frame in data],
            }
        )

        # Convert FrameWallClock -> UTC datetime
        wc_ns = pd.to_numeric(meta_data_df["FrameWallClock"], errors="coerce")

        # Determine unit based on NAME
        if NAME == "katerina":
            # Katerina's timestamps are in MICROSECONDS (μs)
            meta_data_df["FrameWallClock_utc"] = pd.to_datetime(
                wc_ns, unit="us", utc=True, errors="coerce"
            )
        else:
            # Other subjects use NANOSECONDS (ns)
            meta_data_df["FrameWallClock_utc"] = pd.to_datetime(
                wc_ns, unit="ns", utc=True, errors="coerce"
            )

        # Then convert from UTC to Europe/Athens timezone (applies to BOTH cases)
        meta_data_df["FrameWallClock_datetime"] = (
            meta_data_df["FrameWallClock_utc"]
            .dt.tz_convert("Europe/Athens")
            .dt.tz_localize(None)  # Remove timezone info, keep Athens time
        )

        # Drop the intermediate UTC column
        meta_data_df = meta_data_df.drop(columns=["FrameWallClock_utc"])

        # Show the first few rows
        print(meta_data_df.head())

    shared_fps = multiprocessing.Value("f", 0.0)
    shared_Y_PIXELS = multiprocessing.Value("f", 0.0)
    shared_X_PIXELS = multiprocessing.Value("f", 0.0)

    # ...rest of your code...

    shared_fps = multiprocessing.Value("f", 0.0)
    shared_Y_PIXELS = multiprocessing.Value("f", 0.0)
    shared_X_PIXELS = multiprocessing.Value("f", 0.0)
    # videoSource = "udp://192.168.0.101:8554"
    frame_queue = multiprocessing.Queue(
        maxsize=10000
    )  # Bounded queue to prevent memory overuse

    producer_process = multiprocessing.Process(
        target=producer,
        args=(
            frame_queue,
            shared_fps,
            shared_Y_PIXELS,
            shared_X_PIXELS,
            videoSource,
            source_type,
        ),  # Change to 'file' for local video file
    )
    consumer_process = multiprocessing.Process(
        target=consumer,
        args=(
            frame_queue,
            shared_fps,
            shared_Y_PIXELS,
            shared_X_PIXELS,
            model_path,
            meta_data_df,
        ),
    )

    producer_process.start()
    consumer_process.start()

    # Wait for the processes to finish
    producer_process.join()
    consumer_process.join()
    cv.destroyAllWindows()


#   frame_idx = 0
#                 df = df.iloc[0:0]
#                 buffer = buffer.iloc[0:0]

#                 # Reinitialize bufferHelpingRow
#                 bufferHelpingRow = pd.DataFrame(
#                     {
#                         "timeStamp": [[float(0)]],
#                         "frameN": [0],  # frame index
#                         "pupilSize": 0,
#                         "pupCntr_x": ROIWIDTH
#                         // 2,  # Set pupil center X to half of ROI width
#                         "pupCntr_y": ROIWIDTH
#                         // 2,  # Set pupil center Y to half of ROI width
#                         "eyeProb": 1,  # Probability of eye detected
#                         "blinkProb": 0,  # Probability of blink
#                         "angDistance": [0.0],  # Angular distance
#                         "angVelocity": [0.0],  # Angular velocity
#                         "angAcceleration": [0.0],  # Angular acceleration
#                         "label": ["normal"],  # Label the state
#                     }
#                 )

#                 # Initialize angular metrics with NaN
#                 angDistVelAcc = [np.nan, np.nan, np.nan]

#                 # Reset other state variables
#                 prevPupCntr = np.nan
#                 prevTime = np.nan
#                 total_time = 0  # Total elapsed time
#                 saccadesInRow = 0
#                 fixationsInRow = 0
#                 blinksInRow = 0
#                 startEye = False
#                 getInBuffer = False
