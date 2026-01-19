import json

# Load your JSON data
with open('fire_nrt_SV-C2_518678.json', 'r') as file:
    fire_data = json.load(file)

# Define the area of interest for Greece
latitude_min = 34.5
latitude_max = 41.8
longitude_min = 19.4
longitude_max = 28.2

# Filter the data for the specific area and extract important information
filtered_fires = []
for fire in fire_data:
    if (latitude_min <= fire['latitude'] <= latitude_max and
        longitude_min <= fire['longitude'] <= longitude_max):
        
        # Extract important information
        filtered_fire = {
            'latitude': fire['latitude'],
            'longitude': fire['longitude'],
            'acq_date': fire['acq_date'],
            'acq_time': fire['acq_time'],
            'brightness': fire['brightness'],
            'frp': fire['frp']
        }
        filtered_fires.append(filtered_fire)

# Save the filtered data to a new JSON file
with open('filtered_fire_data.json', 'w') as outfile:
    json.dump(filtered_fires, outfile, indent=4)

print(f"Filtered {len(filtered_fires)} fires in the specified area.")