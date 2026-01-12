import pandas as pd

NAME = input("Enter the name : ").strip()
while not NAME:  # If user just pressed Enter (empty input)
    NAME = input("Please Enter the correct name : ").strip()

path = f"C:/Users/AlexisPC/Desktop/Calibration/{NAME}/{NAME}Calibration.csv"

while True:
    try:
        left = int(input("Enter the LEFT frame: "))
        middle1 = int(input("Enter the MIDDLE frame (horizontal): "))
        right = int(input("Enter the RIGHT frame: "))

        up = int(input("Enter the UP frame: "))
        middle2 = int(input("Enter the MIDDLE frame (vertical): "))
        down = int(input("Enter the DOWN frame: "))
        break  # Exit loop if conversion successful
    except ValueError:
        print("Invalid input! Please enter a valid integer.")

# Load CSV and extract coordinates
try:
    df = pd.read_csv(path)
    print(f"\n{'='*80}")
    print(f"Loaded: {NAME}Calibration.csv ({len(df)} rows)")
    print(f"{'='*80}\n")

    # Dictionary of calibration points (using tuple to preserve order and show both middles)
    calibration_points = [
        ("LEFT", left),
        ("MIDDLE (H)", middle1),
        ("RIGHT", right),
        ("UP", up),
        ("MIDDLE (V)", middle2),
        ("DOWN", down),
    ]

    # Extract and display coordinates for each point
    for point_name, frame_num in calibration_points:
        matching_rows = df[df["frameN"] == frame_num]

        if not matching_rows.empty:
            row = matching_rows.iloc[0]
            pupCntr_x = row["pupCntr_x"]
            pupCntr_y = row["pupCntr_y"]
            print(
                f"{point_name:12} (Frame {frame_num:4}): x = {pupCntr_x}, y = {pupCntr_y}"
            )
        else:
            print(f"{point_name:12} (Frame {frame_num:4}): ❌ NOT FOUND")

    print(f"\n{'='*80}")

except FileNotFoundError:
    print(f"\n❌ Error: File not found at {path}")
except Exception as e:
    print(f"\n❌ Error: {e}")
