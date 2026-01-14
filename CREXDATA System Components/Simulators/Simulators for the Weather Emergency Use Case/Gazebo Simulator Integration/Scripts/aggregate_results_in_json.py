import pandas as pd
import json
import math

def safe_cast(value, cast_type, default=None):
    try:
        if value is None or (isinstance(value, float) and math.isnan(value)):
            return default
        return cast_type(value)
    except:
        return default

def rm_main(kpi_df, wp_df, macros):
    optimization_criterion = macros.get("optimizationCriterion", None)

    flights = []

    for _, row in kpi_df.iterrows():
        fn = row["flight_no"]

        flight_info = {
            "flight_no": safe_cast(fn, int, fn),
            "BatteryConsumption": row.get("BatteryConsumption"),
            "FlightDuration": row.get("FlightDuration"),
            "Efficiency": row.get("Efficiency")
        }

        wp_subset = wp_df[wp_df["flight_no"] == fn]

        waypoints = []
        for _, w in wp_subset.iterrows():
            waypoints.append({
                "event": w.get("Event"),
                "latitude": safe_cast(w.get("Latitude"), float),
                "longitude": safe_cast(w.get("Longitude"), float),
                "altitude": safe_cast(w.get("Altitude"), float),
                "timestamp": safe_cast(w.get("Timestamp"), float),
                "battery": safe_cast(w.get("Battery"), float)
            })

        flight_info["Waypoints"] = waypoints
        flights.append(flight_info)

    result = {
        "cityTag": "Dortmund",
        "optimizationCriterion": optimization_criterion,
        "flights": flights
    }

    json_str = json.dumps(result, indent=2)

    out_df = pd.DataFrame([{"message": json_str}])

    return out_df