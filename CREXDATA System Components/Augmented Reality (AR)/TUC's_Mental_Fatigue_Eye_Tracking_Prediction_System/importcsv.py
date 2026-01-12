import csv
import datetime

# Get the current datetime
now = datetime.datetime.now()

# Format the datetime to include milliseconds.
# The "%f" directive gives microseconds; slicing off the last three digits leaves milliseconds.
formatted_datetime = now.strftime("%Y-%m-%d %H:%M:%S.%f")[:-3]

# Alternatively, get the Unix timestamp in milliseconds (since epoch)
timestamp_ms = int(now.timestamp() * 1000)

# Print values (optional)
print("Current Timestamp (ms):", timestamp_ms)
print("Formatted Datetime with ms:", formatted_datetime)

# Export the results to a CSV file
with open('current_datetime.csv', 'w', newline='') as csvfile:
    csvwriter = csv.writer(csvfile)
    # Write header row
    csvwriter.writerow(['Timestamp (ms)', 'Formatted Datetime'])
    # Write data row
    csvwriter.writerow([timestamp_ms, formatted_datetime])
