import numpy as np
import pandas as pd
import os
import time
import joblib
import json
from kafka import KafkaProducer


# from .kafkaProducer import kafka_producer

ONE_MINUTE_IN_SECONDS = 60


class DataFrameTransferer:

    # ❌ COMMENT OUT the offline version (1 argument)
    def __init__(self, timewindow):
        self.timewindow = timewindow
        self.first_batch = True
        self.csv_file = os.path.join(
            os.path.expanduser("~"),
            "Desktop",
            "rosaMetricsFromVideosCorrectConv.csv",
            # "rosaCalibration.csv",
        )

        # self.knn_model = joblib.load("knn_model.pkl")
        # self.scaler = joblib.load("scaler.pkl")
        # self.label_encoder = joblib.load("label_encoder.pkl")

    # ✅ UNCOMMENT the Kafka version (2 arguments)
    def __init__(self, timewindow, raspberryID):
        self.raspberryID = raspberryID
        self.timewindow = timewindow
        self.first_batch = True
        # self.csv_file = os.path.join(
        #     os.path.expanduser("~"),
        #     "Desktop",
        #     "ramoMetricsFromVideosCorrectConv.csv",
        #     # "ramoClibration.csv",
        # )
        # ✅ Use full paths to model files
        model_dir = "C:/Users/AlexisPC/Desktop/saved_models/knn"
        self.knn_model = joblib.load(os.path.join(model_dir, "knn_fatigue_model.pkl"))
        self.scaler = joblib.load(os.path.join(model_dir, "knn_scaler.pkl"))
        self.label_encoder = joblib.load(
            os.path.join(model_dir, "knn_label_encoder.pkl")
        )
        # Kafka configuration
        self.producer = KafkaProducer(
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

    def transferToServer(self, df, transfers):
        last_row = df.iloc[[-1]].reset_index(drop=True)

        while len(df) >= self.timewindow:
            batch = df.iloc[: self.timewindow].copy()

            # Iterate through batch (excluding last row)
            for i in range(len(batch) - 1):
                current_label = batch.loc[i, "label"]
                next_label = batch.loc[i + 1, "label"]

                if current_label == "blink" and next_label != "blink":
                    batch.loc[i + 1, "label"] = "normal"
                    # Do NOT touch angDistance

            batch.to_csv(self.csv_file, mode="a", index=False, header=self.first_batch)
            # Send the batch to Kafka using the initialized producer (JSON-serializable)
            # self.producer.send(
            #     "raw",
            #     {
            #         "raspberryID": str(self.raspberryID),
            #         "transfers": transfers,
            #         "batch": batch.to_dict(orient="records"),
            #     },
            # )
            # self.producer.flush()

            self.first_batch = False
            transfers += 1
            df = df.iloc[self.timewindow :].reset_index(drop=True)
            print("TIME OF TRANSFER: ", time.time())

        return df, last_row, transfers

    def transferSlidingWindowToTopic(self, df, transfers, slidingDF):
        slidingWindow = ONE_MINUTE_IN_SECONDS * self.timewindow
        last_row = df.iloc[[-1]].reset_index(drop=True)

        while len(df) >= self.timewindow:
            batch = df.iloc[: self.timewindow].copy()

            # Iterate through batch (excluding last row)
            for i in range(len(batch) - 1):
                current_label = batch.loc[i, "label"]
                next_label = batch.loc[i + 1, "label"]

                if current_label == "blink" and next_label != "blink":
                    batch.loc[i + 1, "label"] = "normal"
            slidingDF = pd.concat([slidingDF, batch], ignore_index=True)
            # print(slidingDF)
            while slidingDF.shape[0] >= slidingWindow:
                inputDF = slidingDF.head(slidingWindow).reset_index(drop=True)
                row = compute_metrics_for_window(inputDF, self.timewindow)
                last_dt = row["last real_datetime"].iloc[0]
                row = row.drop(columns=["last real_datetime"])
                knnResult = self.knn_model.predict(self.scaler.transform(row))
                label = self.label_encoder.inverse_transform(knnResult)[0]

                data = {
                    "last_real_datetime": str(last_dt),
                    "label": str(label),
                    "raspberryID": str(self.raspberryID),
                }
                self.producer.send("mental", value=data)

                slidingDF = slidingDF.iloc[self.timewindow :].reset_index(drop=True)
                # ❌ REMOVE THIS LINE - it's broken and unnecessary
                # KafkaProducer(batch, self.producerConfig)

            self.first_batch = False
            transfers += 1
            df = df.iloc[self.timewindow :].reset_index(drop=True)
            # print("TIME OF TRANSFER: ", time.time())

        return df, last_row, transfers, slidingDF

    def publish_fake_label(self, label):
        """
        Publish simple fake data with just a random label (1-4)

        Args:
            label: Integer label from 1-4

        Returns:
            dict: The data that was published
        """
        data = {
            "last_real_datetime": str(pd.Timestamp.now()),
            "label": str(label),
            "raspberryID": str(self.raspberryID),
        }

        self.producer.send("mental", value=data)
        self.producer.flush()  # Ensure message is sent immediately

        return data

    def transferBufferToMainDF(self, bufferDF, mainDF):
        # Concatenate the entire buffer DataFrame to mainDF at once
        mainDF = pd.concat([mainDF, bufferDF], ignore_index=True)
        # Clear the buffer
        bufferDF = bufferDF.iloc[0:0]
        return mainDF, bufferDF


def compute_metrics_for_window(window: pd.DataFrame, fps: int) -> pd.DataFrame:
    """
    Calculate metrics for exactly ONE dataframe window.
    Returns a single-row DataFrame.
    """

    columns = [
        "last real_datetime",
        "mean pupil diameter",
        "fixation count",
        "saccade count",
        "blink count",
        "mean fixation duration",
        "mean saccade duration",
        "peak saccade velocity",
        "mean saccade velocity",
        "mean saccade amplitude",
    ]
    metrics_df = pd.DataFrame(columns=columns)

    frameTime = 1 / fps

    fixationCount = 0
    saccadeCount = 0
    fixationRows = 0
    saccadeRows = 0
    maxVelocity = 0
    saccadeVelocity = 0
    saccadeAngularDistance = 0
    blinkcount = 0

    mean_pupil_size = window["pupilSize"].mean()
    last_real_datetime = window["Real Date Time"].iloc[-1]

    N = len(window)
    for i in range(N - 1):
        label = str(window.iloc[i]["label"]).strip().lower()
        next_label = str(window.iloc[i + 1]["label"]).strip().lower()

        if i == 0:
            if label == "fixation":
                fixationCount += 1
            elif label == "saccade":
                saccadeCount += 1
            elif label == "blink":
                blinkcount += 1

        if label == "fixation":
            fixationRows += 1

        if label == "saccade":
            saccadeRows += 1
            v = abs(window.iloc[i]["angVelocity"])
            saccadeVelocity += v
            saccadeAngularDistance += window.iloc[i]["angDistance"]
            if v > maxVelocity:
                maxVelocity = v

        # transitions
        if label != "fixation" and next_label == "fixation":
            fixationCount += 1
        if label != "saccade" and next_label == "saccade":
            saccadeCount += 1
        if label != "blink" and next_label == "blink":
            blinkcount += 1

    fixationMeanDuration = (
        (fixationRows * frameTime) / fixationCount if fixationCount else 0
    )
    saccadeMeanDuration = (
        (saccadeRows * frameTime) / saccadeCount if saccadeCount else 0
    )
    meanSaccadeVelocity = (saccadeVelocity / saccadeRows) if saccadeRows else 0
    meanSaccadeAmplitude = (saccadeAngularDistance / saccadeRows) if saccadeRows else 0

    metrics_df.loc[0] = [
        last_real_datetime,
        mean_pupil_size,
        fixationCount,
        saccadeCount,
        blinkcount,
        fixationMeanDuration,
        saccadeMeanDuration,
        maxVelocity,
        meanSaccadeVelocity,
        meanSaccadeAmplitude,
    ]

    return metrics_df


def slidingWindowAlternate(df, fps):
    count = 0
    columns = [
        "last real_datetime",
        "mean pupil diameter",
        "fixation count",
        "saccade count",
        "blink count",
        "mean fixation duration",
        "mean saccade duration",
        "peak saccade velocity",
        "mean saccade velocity",
        "mean saccade amplitude",
    ]
    metrics_df = pd.DataFrame(columns=columns)

    oneMinute = ONE_MINUTE_IN_SECONDS * fps  # Window size in frames
    frameTime = 1 / fps  # Time per frame in seconds
    start = 0

    while start + oneMinute <= len(df):

        fixationCount = 0
        saccadeCount = 0
        fixationRows = 0
        saccadeRows = 0
        maxVelocity = 0
        saccadeVelocity = 0
        saccadeAngularDistance = 0
        meanSaccadeAmplitude = 0
        blinkcount = 0

        window = df.iloc[start : start + oneMinute]

        mean_pupil_size = window["pupilSize"].mean()
        last_real_datetime = window["Real Date Time"].iloc[-1]

        i = 0
        while i < oneMinute - 1:
            label = str(window.iloc[i]["label"]).strip().lower()

            if i == 0:
                if label == "fixation":
                    fixationCount += 1
                elif label == "saccade":
                    saccadeCount += 1
                elif label == "blink":
                    blinkcount += 1
            else:
                if label == "fixation":
                    fixationRows += 1

                if label == "saccade":
                    saccadeRows += 1
                    saccadeVelocity += abs(window.iloc[i]["angVelocity"])
                    saccadeAngularDistance += window.iloc[i]["angDistance"]
                    if maxVelocity < abs(window.iloc[i]["angVelocity"]):
                        maxVelocity = abs(window.iloc[i]["angVelocity"])

                # Detect transitions for counting events
                if (
                    label != "fixation"
                    and str(window.iloc[i + 1]["label"]).strip().lower() == "fixation"
                ):
                    fixationCount += 1
                if (
                    label != "saccade"
                    and str(window.iloc[i + 1]["label"]).strip().lower() == "saccade"
                ):
                    saccadeCount += 1
                if (
                    label != "blink"
                    and str(window.iloc[i + 1]["label"]).strip().lower() == "blink"
                ):
                    blinkcount += 1

            i += 1

        # Mean durations
        fixationMeanDuration = (
            (fixationRows * frameTime) / fixationCount if fixationCount else 0
        )
        saccadeMeanDuration = (
            (saccadeRows * frameTime) / saccadeCount if saccadeCount else 0
        )

        # ✅ FIXED: divide by saccadeRows, not saccadeCount
        meanSaccadeVelocity = saccadeVelocity / saccadeRows if saccadeRows else 0

        meanSaccadeAmplitude = (
            saccadeAngularDistance / saccadeRows if saccadeRows else 0
        )

        values = [
            [
                last_real_datetime,
                mean_pupil_size,
                fixationCount,
                saccadeCount,
                blinkcount,
                fixationMeanDuration,
                saccadeMeanDuration,
                maxVelocity,
                meanSaccadeVelocity,
                meanSaccadeAmplitude,
            ]
        ]

        new_row = pd.DataFrame(values, columns=columns)
        metrics_df = pd.concat([metrics_df, new_row], ignore_index=True)

        start += fps
        count += 1

    return metrics_df


def slidingWindow(df, fps):
    count = 0
    columns = [
        "mean pupil diameter",
        "fixation count",
        "saccade count",
        "blink count",
        "mean fixation duration",
        "mean saccade duration",
        "peak saccade velocity",
        "mean saccade velocity",
        "mean saccade amplitude",
    ]
    metrics_df = pd.DataFrame(columns=columns)
    # print(len(df))

    oneMinute = ONE_MINUTE_IN_SECONDS * fps  # Define window size (1 minute of frames)
    frameTime = ONE_MINUTE_IN_SECONDS / fps  # Time per frame
    start = 0  # Initialize window start index
    while start + oneMinute <= len(df):  # Ensure we don’t exceed df size

        fixationCount = 0
        saccadeCount = 0
        fixationRows = 0
        saccadeRows = 0
        maxVelocity = 0
        saccadeVelocity = 0
        saccadeAngularDistance = 0
        meanSaccadeAmplitude = 0
        blinkcount = 0

        # Select the current window
        window = df.iloc[start : start + oneMinute]
        ########### Mean Pupil Size ###########
        mean_pupil_size = window["pupilSize"].mean()
        last_real_datetime = df.iloc[start : start + oneMinute]["Real Date Time"].iloc[
            -1
        ]

        ########### Fixation & Saccade Count ###########
        i = 0
        while i < oneMinute - 1:  # Process only within the current window
            label = str(window.iloc[i]["label"]).strip().lower()

            if i == 0:

                if str(window.iloc[i]["label"]).strip().lower() == "fixation":
                    fixationCount += 1
                elif str(window.iloc[i]["label"]).strip().lower() == "saccade":
                    saccadeCount += 1
                elif str(window.iloc[i]["label"]).strip().lower() == "blink":
                    blinkcount += 1
            else:
                if str(window.iloc[i]["label"]).strip().lower() == "fixation":
                    fixationRows += 1

                if str(window.iloc[i]["label"]).strip().lower() == "saccade":
                    saccadeRows += 1
                    saccadeVelocity += np.abs(window.iloc[i]["angVelocity"])
                    saccadeAngularDistance += window.iloc[i]["angDistance"]
                    if maxVelocity < np.abs(window.iloc[i]["angVelocity"]):
                        maxVelocity = np.abs(window.iloc[i]["angVelocity"])

                if (
                    str(window.iloc[i]["label"]).strip().lower() != "fixation"
                    and str(window.iloc[i + 1]["label"]).strip().lower() == "fixation"
                ):
                    fixationCount += 1  # Count transition to fixation

                if (
                    str(window.iloc[i]["label"]).strip().lower() != "saccade"
                    and str(window.iloc[i + 1]["label"]).strip().lower() == "saccade"
                ):
                    saccadeCount += 1  # Count transition to saccade

                if (
                    str(window.iloc[i]["label"]).strip().lower() != "blink"
                    and str(window.iloc[i + 1]["label"]).strip().lower() == "blink"
                ):
                    blinkcount += 1  # Count transition to saccade
            i += 1  # Move to next row

        # Compute Mean Durations
        fixationMeanDuration = (
            (fixationRows * frameTime) / fixationCount if fixationCount else 0
        )
        saccadeMeanDuration = (
            (saccadeRows * frameTime) / saccadeCount if saccadeCount else 0
        )
        meanSaccadeVelocity = saccadeVelocity / saccadeCount if saccadeCount else 0
        meanSaccadeAmplitude = (
            saccadeAngularDistance / saccadeCount if saccadeCount else 0
        )
        # Print results for each window (you can store them in a list)

        # Move window by step_size (sliding forward)
        values = [
            [
                mean_pupil_size,
                fixationCount,
                saccadeCount,
                blinkcount,
                fixationMeanDuration,
                saccadeMeanDuration,
                maxVelocity,
                meanSaccadeVelocity,
                meanSaccadeAmplitude,
            ]
        ]

        new_row = pd.DataFrame(values, columns=columns)
        metrics_df = pd.concat([metrics_df, new_row], ignore_index=True)

        # metrics_df.to_csv(
        #     "metrics_data.csv",
        #     mode="a",
        #     index=False,
        #     header=not os.path.exists("metrics_data.csv"),
        # )

        # print(metrics_df)
        start += fps
        count += 1

    return metrics_df
    print("start: ", start)
    print("fps: ", fps)
    print("oneMinute: ", oneMinute)
    print("start - fps + oneMinute: ", start - fps + oneMinute)
    print(len(df) - (start - fps + oneMinute))
    print(count)
    return df.iloc[(start - fps + oneMinute) :]
