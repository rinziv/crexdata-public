import pandas as pd
import numpy as np
from datetime import datetime, timedelta
from math import nan
from classes.findingClosest import BufferProc
from datetime import datetime
from scipy.spatial import cKDTree
from sklearn.preprocessing import MinMaxScaler
from classes.dfTransferer import slidingWindow
import os

FPS = 50
ROWS_BEFORE = 6000
ROWS_AFTER = 0
# path = "C:/Users/AlexisPC/Desktop/alexiTestMF.csv"
path = "C:/Users/AlexisPC/Desktop/INDUCING_MENTAL_FATIGUE/Alexis2/alexiTestMF.csv"
path2 = "C:/Users/AlexisPC/Desktop/INDUCING_MENTAL_FATIGUE/Alexis2/reakTime60fps.csv"

df = pd.read_csv(path)

# Name of the column you're checking
target_columns = [
    "StartTime",
    "AXCPTtrialsLoop1.thisRepN",
    "NBackTrialsLoop1.thisRepN",
    "searchLoop1.thisRepN",
    "mentRotTrialsLoop1.thisRepN",
    "NBackTrialsLoop2.thisRepN",
    "AXCPTtrialsLoop2.thisRepN",
    "searchLoop2.thisRepN",
    "mentRotTrialsLoop2.thisRepN",
]

user_inputs = []
stop_input = False

for col in target_columns:
    if not stop_input:
        value = input(f"Enter a value for '{col}' (leave blank to stop): ").strip()
        if value == "":
            stop_input = True
            user_inputs.append(None)
        else:
            user_inputs.append(value)
    else:
        user_inputs.append(None)

# Create a DataFrame to store results
print(user_inputs)
df.columns = df.columns.str.strip()
input_df = pd.DataFrame({"column": target_columns, "user_value": user_inputs})
original_str = df.at[2, "date"]
start_time = datetime.strptime(original_str.strip(), "%Y-%m-%d_%Hh%M.%S.%f")
print(value)
print("\nUser inputs:")


# Create a new column in input_df to store date values
input_df["task_start_date"] = None
input_df["task_end_date"] = None

for idx, row in input_df.iterrows():
    col = row["column"]
    user_value = row["user_value"]

    if pd.notna(user_value) and col in df.columns and "date" in df.columns:
        series = df[col]
        active = series.notna() & (series.astype(str).str.strip() != "")

        if active.any():
            if col == "AXCPTtrialsLoop1.thisRepN":
                # First non-null index
                start_index = active.idxmax()
                # Find when it becomes null again after that
                following = active.loc[start_index:]
                try:
                    end_index = following[~following].index[0] - 1
                except IndexError:
                    end_index = following.index[-1]

                # Assign AXCPT trial's start and end
                input_df.at[idx, "task_start_date"] = start_time + timedelta(
                    seconds=df.at[start_index, "elapsed.time"]
                )
                input_df.at[idx, "task_end_date"] = start_time + timedelta(
                    seconds=df.at[end_index, "elapsed.time"]
                )

                match_idx = input_df.index[input_df["column"] == "StartTime"]
                if not match_idx.empty:
                    input_df.at[match_idx[0], "task_end_date"] = start_time + timedelta(
                        seconds=df.at[start_index, "elapsed.time"]
                    )

            elif col != "StartTime" and col != "AXCPTtrialsLoop1.thisRepN":
                # For other columns, find the last index before it turns null again
                first_block = active[active].index

                if not first_block.empty:
                    # Now check where it becomes inactive again after the first valid
                    start_index = first_block[0]
                    following = active.loc[start_index:]
                    try:
                        end_index = following[~following].index[0] - 1
                    except IndexError:
                        end_index = following.index[-1]

                    input_df.at[idx, "task_end_date"] = start_time + timedelta(
                        seconds=df.at[end_index, "elapsed.time"]
                    )

print(input_df)

eyeTrackingData = pd.read_csv(path2)
eyeTrackingData.columns = eyeTrackingData.columns.str.strip()
eyeTrackingData["Real Date Time"] = pd.to_datetime(
    eyeTrackingData["Real Date Time"], errors="coerce"
)
input_df["task_start_date"] = pd.to_datetime(
    input_df["task_start_date"], errors="coerce"
)
input_df["task_end_date"] = pd.to_datetime(input_df["task_end_date"], errors="coerce")
input_df = input_df.dropna(subset=["task_end_date"])
# Convert timestamps to datetime and then to microseconds

eyeTrackingTimeStamps = pd.to_datetime(eyeTrackingData["Real Date Time"])
eyeTrackingData["FrameWallClock"] = eyeTrackingTimeStamps.astype("int64")
frame_wall_clock_reshaped = eyeTrackingData["FrameWallClock"].values.reshape(-1, 1)
frame_wall_clock_df = pd.DataFrame(
    frame_wall_clock_reshaped, columns=["FrameWallClock"]
)

frame_wall_clock_df.to_csv("frame_wall_clock_reshaped.csv", index=False)
# Build the KDTree using 2D array
tree = cKDTree(eyeTrackingData["FrameWallClock"].values.reshape(-1, 1))
pavloviaEnd = pd.to_datetime(input_df["task_end_date"])
# input_df["start_Unix"] = pavloviaStart.astype("int64")
input_df["end_Unix"] = pavloviaEnd.astype("int64")

# Query the closest match from the eye-tracking data
# _, startIndex = tree.query(input_df["start_Unix"].values.reshape(-1, 1), k=1)
_, endIndex = tree.query(input_df["end_Unix"].values.reshape(-1, 1), k=1)
print("End Index:", endIndex)
input_df["end_index"] = endIndex
windows = []  # list to collect DataFrames
for i, idx in enumerate(input_df["end_index"]):
    if pd.notna(idx):
        idx = int(idx)
        user_val = input_df.iloc[i]["user_value"]
        start = max(idx - ROWS_BEFORE, 0)
        if i == 0:
            end = idx
        else:
            end = min(
                idx + ROWS_AFTER, len(eyeTrackingData)
            )  # ensure we stay within bounds
        window_df = eyeTrackingData.iloc[start:end].copy()
        window_df["from_task"] = idx  # tag the source index
        if pd.notna(user_val):
            try:
                user_val = float(user_val)
                normalized = ((user_val - 0) / 4) * 10
                print("Normalized:", normalized)
                if normalized < 3:
                    normalized = 1
                elif normalized <= 8:
                    normalized = 2
                else:
                    normalized = 3
            except ValueError:
                print("Invalid value:", user_val)

        window_df["user_value"] = normalized  # <--- tag with user_value too
        windows.append(window_df)

statisticalTrainingDF = pd.DataFrame()
trainingDF = pd.DataFrame()

file_exists = os.path.exists("STATISTICAL_trainingDF_export.csv")

for df in windows:
    # Clean the DataFrame
    cleanedDF = df.drop(
        columns=[
            "datetime",
            "timeStamp",
            "from_task",
            "FrameWallClock",
            "eyeProb",
            "blinkProb",
        ],
        errors="ignore",
    )
    print("Cleaned DataFrame:", cleanedDF)
    # Create sliding window features
    slidingWindowDF = slidingWindow(cleanedDF, FPS)
    slidingWindowDF["user_value"] = df["user_value"].unique()[0]
    print("Sliding Window DataFrame:", slidingWindowDF)
    # Append to CSV - write headers only if file doesn't exist
    slidingWindowDF.to_csv(
        "STATISTICAL_trainingDF_export.csv",
        mode="a",  # Always append
        index=False,
        header=not file_exists,  # Write header only if file doesn't exist
    )

    # After first write, file will exist
    file_exists = True

statisticalTrainingDF = statisticalTrainingDF.drop(
    ["frameN", "Real Date Time", "label"], axis=1
)
# trainingDF = trainingDF.drop(["frameN", "Real Date Time"], axis=1)
# desktop_path = os.path.join(os.path.expanduser("~"), "Desktop")
# file_path = os.path.join(desktop_path, "STATISTICAL_trainingDF_export.csv")
# statisticalTrainingDF.to_csv(file_path, sep="\t", index=False)
