import os
import json
import time
import math
import numpy as np
import pandas as pd
import cv2 as cv
from datetime import datetime
import tensorflow as tf
from tensorflow import keras

from keras.optimizers import RMSprop
from collections import defaultdict

from classes.frame_labeler import FrameLabeler
from classes.buffer_proc import BufferProc
from classes.dfTransferer import DataFrameTransferer
from classes.convertions import Convertions
from classes.morphProcessing import MorphProcessing

# ---------------- CONFIG ----------------
# ✅ BETTER GPU CHECK - replace the deprecated line
print("🔍 GPU Detection:")
print(f"TensorFlow version: {tf.__version__}")
gpus = tf.config.list_physical_devices("GPU")
print(f"GPUs Available: {gpus}")

if gpus:
    try:
        # Enable memory growth to prevent TensorFlow from allocating all GPU memory at once
        for gpu in gpus:
            tf.config.experimental.set_memory_growth(gpu, True)

        # Optional: Set visible GPU devices (use GPU 0)
        tf.config.set_visible_devices(gpus[0], "GPU")

        print(f"✅ GPU Configured: {gpus[0].name}")
        print(f"   Memory growth enabled")
    except RuntimeError as e:
        print(f"⚠️ GPU configuration error: {e}")
else:
    print("❌ No GPU detected - running on CPU")

print("-" * 50)

from classes.frame_labeler import FrameLabeler


model_path = "C:/Users/AlexisPC/Desktop/pythonEYE/meye-segmentation_i128_s4_c1_f16_g1_a-relu.hdf5"

# ✅ ADD: Video file path configuration
VIDEO_FILE_PATH = (
    "C:/Users/AlexisPC/Desktop/test_video.mp4"  # Change this to your video path
)

ROIWIDTH = 400
ROIHEIGHT = 400
TIMEWINDOW = 50
BLINK_THRESHOLD = 0.8
THRESHOLD = 0.65
IMCLOSING = 13
INVERTIMAGE = False

Y_FOV = 67  # degrees
X_FOV = 102  # degrees
VERTICAL = 0.5156
HORIZONTAL = 0.4222

# Fake data mode - set to True to bypass model and publish random labels
USE_FAKE_DATA = False  # Change to True to enable fake data mode
FAKE_DATA_INTERVAL = 1.0  # seconds between fake data publishes

# Time-based weight progression (shifts toward higher labels over time)
# At 0 min: favor label 1, At 50 min: favor label 4
MINUTES_FOR_FULL_FATIGUE = 50  # Minutes until maximum fatigue (label 4 most likely)


# --------- Helpers ----------
def load_nn(path):
    # ✅ Force model to load on GPU
    with tf.device("/GPU:0"):
        model = keras.models.load_model(path, compile=False)
        model.compile(
            optimizer=RMSprop(learning_rate=1e-4), loss="categorical_crossentropy"
        )
    return model


def init_empty_df():
    return pd.DataFrame(
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


def fakeDataProcess(raspberry_id="desktop_video"):
    """
    Time-based weighted fake data publishing loop
    - Publishes random label (1-4) every second with time-based weights
    - At start (0 min): Higher probability for label 1 (normal)
    - Over time: Gradually shifts toward label 4 (fatigue)
    - At 30 min: Highest probability for label 4

    Useful for testing Kafka pipeline with realistic fatigue progression
    Also records video stream if ENABLE_VIDEO_RECORDING is True
    """
    print(f"🎭 Starting FAKE DATA mode for {raspberry_id}")
    print(f"   Publishing weighted labels (1-4) every {FAKE_DATA_INTERVAL} second(s)")
    print(
        f"   Weight progression: {MINUTES_FOR_FULL_FATIGUE} minutes to maximum fatigue"
    )

    # Initialize DataFrameTransferer for Kafka publishing
    DataFrameTransf = DataFrameTransferer(TIMEWINDOW, raspberry_id)

    publish_count = 0
    start_time = time.time()

    try:
        while True:
            # Calculate elapsed time in minutes
            elapsed_minutes = (time.time() - start_time) / 60.0

            # Calculate fatigue progression (0.0 at start -> 1.0 at MINUTES_FOR_FULL_FATIGUE)
            fatigue_progress = min(elapsed_minutes / MINUTES_FOR_FULL_FATIGUE, 1.0)

            # Wave-based weights: dominant label shifts sequentially 1 -> 2 -> 3 -> 4
            # Divides time into 4 phases, each phase favors one label
            # Phase 1 (0-25%):   Label 1 dominant
            # Phase 2 (25-50%):  Label 2 dominant
            # Phase 3 (50-75%):  Label 3 dominant
            # Phase 4 (75-100%): Label 4 dominant

            # Determine which phase we're in (0-3)
            phase = int(fatigue_progress * 4)  # 0, 1, 2, or 3
            if phase >= 4:
                phase = 3  # Cap at phase 3 (label 4)

            # Calculate position within current phase (0.0 to 1.0)
            phase_progress = (fatigue_progress * 4) % 1.0

            # Create wave using Gaussian-like peaks centered on each phase
            # Higher peak_width = more gradual transition between phases
            peak_width = 0.5  # Adjust this (0.5-1.5) for sharper/smoother transitions

            weights = []
            for label_idx in range(4):
                # Distance from current phase to this label's phase
                distance = abs(label_idx - (phase + phase_progress))
                # Gaussian-like decay: closer labels get higher probability
                weight = np.exp(-(distance**2) / (2 * peak_width**2))
                weights.append(weight)

            # Normalize weights to ensure they sum to 1.0
            total = sum(weights)
            weights = [w / total for w in weights]

            # Generate weighted random label from 1-4
            random_label = np.random.choice([1, 2, 3, 4], p=weights)

            # Publish fake data with the weighted label
            data = DataFrameTransf.publish_fake_label(random_label)

            publish_count += 1
            print(
                f"📤 [{raspberry_id}] #{publish_count} (t={elapsed_minutes:.1f}min, "
                f"progress={fatigue_progress*100:.0f}%): Label={random_label} "
                f"| Weights: 1={weights[0]:.2f}, 2={weights[1]:.2f}, "
                f"3={weights[2]:.2f}, 4={weights[3]:.2f}"
            )

            # Wait before next publish
            time.sleep(FAKE_DATA_INTERVAL)

    except KeyboardInterrupt:
        print(f"\n🛑 Fake data publishing interrupted")
    except Exception as e:
        print(f"❌ Error in fake data process: {e}")
        import traceback

        traceback.print_exc()
    finally:
        print(f"🔚 Fake data publishing ended")
        print(f"   Total runtime: {(time.time() - start_time) / 60:.1f} minutes")


def mainProcess(video_path, device_id="desktop_video"):
    """Main processing loop for local video file"""
    print(f"🔄 Starting video processing from: {video_path}")

    # ✅ Open video file with cv.VideoCapture
    cap = cv.VideoCapture(video_path)

    if not cap.isOpened():
        print(f"❌ Failed to open video file: {video_path}")
        return

    # Get video properties
    fps = cap.get(cv.CAP_PROP_FPS)
    total_frames = int(cap.get(cv.CAP_PROP_FRAME_COUNT))
    width = int(cap.get(cv.CAP_PROP_FRAME_WIDTH))
    height = int(cap.get(cv.CAP_PROP_FRAME_HEIGHT))

    print(f"📹 Video Info:")
    print(f"   Resolution: {width}x{height}")
    print(f"   FPS: {fps:.2f}")
    print(f"   Total Frames: {total_frames}")
    print(f"   Duration: {total_frames/fps:.2f} seconds")

    # ---- Prepare state ----
    model = None
    MorProc = None
    Frame_labeler = None
    Conv = None
    DataFrameTransf = None
    bufferProc = None

    df = init_empty_df()
    slidingDF = df.copy()
    buffer = init_empty_df()

    prevFrame = -1
    prevPupCntr = np.array([np.nan, np.nan], dtype=float)
    prevTime = np.nan
    angDistVelAcc = [np.nan, np.nan, np.nan]
    fixationsInRow = 0
    getInBuffer = False
    firstElementIsBlinkOrFlicker = False
    bufferHelpingRow = pd.DataFrame(
        {
            "Real Date Time": [
                pd.Timestamp.now().strftime("%Y-%m-%d %H:%M:%S.%f")[:-3]
            ],
            "datetime": [pd.Timestamp.now().strftime("%Y-%m-%d %H:%M:%S")],
            "timeStamp": [0.0],
            "frameN": [0.0],
            "pupilSize": [0.0],
            "pupCntr_x": [float(ROIWIDTH // 2)],
            "pupCntr_y": [float(ROIWIDTH // 2)],
            "eyeProb": [np.float32(1.0)],
            "blinkProb": [np.float32(0.0)],
            "angDistance": [0.0],
            "angVelocity": [0.0],
            "angAcceleration": [0.0],
            "label": ["normal"],
        }
    )

    frame_count = 0
    start_time = time.time()
    successful_frames = 0

    print("🎬 Starting frame processing...")

    try:
        while True:
            # ✅ Read frame from video file
            ret, frame = cap.read()

            if not ret:
                print("✅ Reached end of video")
                break

            frame_count += 1
            successful_frames += 1

            # Progress indicator
            if frame_count % 100 == 0:
                progress = (frame_count / total_frames) * 100
                elapsed = time.time() - start_time
                current_fps = frame_count / elapsed if elapsed > 0 else 0
                print(
                    f"📊 Progress: {progress:.1f}% | Frame: {frame_count}/{total_frames} | FPS: {current_fps:.2f}"
                )

            # ✅ Load model on first frame
            if model is None:
                print(f"🧠 Loading ML model...")
                model = load_nn(model_path)
                MorProc = MorphProcessing(THRESHOLD, IMCLOSING)
                Frame_labeler = FrameLabeler(BLINK_THRESHOLD)
                Conv = Convertions(VERTICAL, HORIZONTAL)
                DataFrameTransf = DataFrameTransferer(TIMEWINDOW, device_id)

                print(f"📊 Video FPS: {fps:.2f}")
                saccadeFrameLimit = math.ceil(fps / 10)
                blinkFrameLimit = math.ceil(0.4 * fps)
                bufferProc = BufferProc(
                    saccadeFrameLimit,
                    blinkFrameLimit,
                    angVelocity_threshold=240,
                    angAcceleration_threshold=3000,
                )

            frame_idx = frame_count

            # ---------- preprocessing ----------
            if INVERTIMAGE:
                frame = cv.bitwise_not(frame)
            gray = frame[..., 0] if frame.ndim == 3 else frame
            _, mas = cv.threshold(gray, 220, 255, cv.THRESH_BINARY)
            gray = cv.inpaint(gray, mas, inpaintRadius=10, flags=cv.INPAINT_TELEA)

            # ---------- model inference ----------
            inp = (gray.astype(np.float32) / 255.0)[None, :, :, None]
            mask, info = model(inp)
            prediction = mask[0, :, :, 0]
            eyeProbability = float(np.round(info[0, 0].numpy(), 1))
            blinkProbability = float(np.round(info[0, 1].numpy(), 1))

            morphedMask, _ = MorProc.morphProcessing(prediction)
            resized_mask = cv.resize(
                morphedMask, (ROIWIDTH, ROIHEIGHT), interpolation=cv.INTER_NEAREST
            )
            morphedMask2, centroid2 = MorProc.morphProcessing(resized_mask)

            # ---------- angular metrics ----------
            t_now = frame_idx / fps
            if not np.isnan(centroid2).any() and np.isnan(prevPupCntr).any():
                prevPupCntr = np.array([centroid2[0], centroid2[1]], dtype=float)
                prevTime = t_now
                angDistVelAcc = [0.0, 0.0, 0.0]
            elif not np.isnan(centroid2).any() and not np.isnan(prevPupCntr).any():
                angDistVelAcc = Conv.pxl2deg(
                    prevPupCntr, centroid2, prevTime, t_now, angDistVelAcc[1]
                )
                prevPupCntr = np.array([centroid2[0], centroid2[1]], dtype=float)
                prevTime = t_now

            frame_label = Frame_labeler.label_frame(
                blinkProbability, centroid2, angDistVelAcc
            )

            new_row = pd.DataFrame(
                {
                    "Real Date Time": [pd.Timestamp.now()],
                    "datetime": [pd.Timestamp.now()],
                    "timeStamp": [t_now],
                    "frameN": [frame_idx],
                    "pupilSize": [float(np.sum(morphedMask2) / 255.0)],
                    "pupCntr_x": [float(centroid2[0])],
                    "pupCntr_y": [float(centroid2[1])],
                    "eyeProb": [np.float32(eyeProbability)],
                    "blinkProb": [np.float32(blinkProbability)],
                    "angDistance": [
                        float(angDistVelAcc[0]) if frame_label != "blink" else np.nan
                    ],
                    "angVelocity": [
                        float(angDistVelAcc[1]) if frame_label != "blink" else np.nan
                    ],
                    "angAcceleration": [
                        float(angDistVelAcc[2]) if frame_label != "blink" else np.nan
                    ],
                    "label": [frame_label],
                }
            )

            # ---------- buffer/df logic ----------
            if not getInBuffer:
                if frame_label == "fixation":
                    if fixationsInRow != 0:
                        df = pd.concat([df, new_row], ignore_index=True)
                        if len(df) >= TIMEWINDOW:
                            df, bufferHelpingRow, _, slidingDF = (
                                DataFrameTransf.transferSlidingWindowToTopic(
                                    df, 0, slidingDF
                                )
                            )
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

                elif frame_label in ("blink", "flicker"):
                    fixationsInRow = 0
                    firstElementIsBlinkOrFlicker = True
                    rev_df = df[::-1]  # reversed DataFrame
                    mask_ok = (
                        rev_df["pupCntr_x"].notnull() & rev_df["pupCntr_y"].notnull()
                    )
                    filtered_df = rev_df[mask_ok]
                    if not filtered_df.empty:
                        bufferHelpingRow = pd.DataFrame([filtered_df.iloc[0]])
                    buffer = pd.concat([buffer, new_row], ignore_index=True)

                else:
                    firstElementIsBlinkOrFlicker = False
                    df = pd.concat([df, new_row], ignore_index=True)
                    if len(df) >= TIMEWINDOW:
                        df, bufferHelpingRow, _, slidingDF = (
                            DataFrameTransf.transferSlidingWindowToTopic(
                                df, 0, slidingDF
                            )
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
                        df, buffer = DataFrameTransf.transferBufferToMainDF(buffer, df)
                        df, bufferHelpingRow, _, slidingDF = (
                            DataFrameTransf.transferSlidingWindowToTopic(
                                df, 0, slidingDF
                            )
                        )

                elif frame_label in ("saccade", "flicker", "blink"):
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
                    df, bufferHelpingRow, _, slidingDF = (
                        DataFrameTransf.transferSlidingWindowToTopic(df, 0, slidingDF)
                    )
        # end while loop

    except KeyboardInterrupt:
        print(f"\n🛑 Processing interrupted")
    except Exception as e:
        print(f"❌ Error in video processing: {e}")
        import traceback

        traceback.print_exc()
    finally:
        cap.release()
        total_time = time.time() - start_time
        final_fps = frame_count / total_time if total_time > 0 else 0
        print(f"\n📊 Final Stats:")
        print(f"   Total Frames Processed: {frame_count}")
        print(f"   Total Time: {total_time:.2f}s")
        print(f"   Average Processing FPS: {final_fps:.2f}")
        print(f"🔚 Video processing complete")


# ---------------- MAIN ----------------

if __name__ == "__main__":
    if USE_FAKE_DATA:
        print("🎭 Running in FAKE DATA mode")
        fakeDataProcess()
    else:
        print("🎥 Running in VIDEO PROCESSING mode")
        if not os.path.exists(VIDEO_FILE_PATH):
            print(f"❌ Video file not found: {VIDEO_FILE_PATH}")
            print("Please update VIDEO_FILE_PATH in the config section")
        else:
            mainProcess(VIDEO_FILE_PATH)
