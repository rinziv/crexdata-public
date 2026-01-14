import itertools
import math
import json
import pandas as pd

def rm_main():
    # Waypoints from macro 'waypoints'
    # This is expected to be a JSON string like: [[x,y,z],[x,y,z],...]
    points_str = r"%{waypoints}"
    points = json.loads(points_str)
    # First element of the list is the start point
    start = points[0]
    # Last element of the list is the end point
    end = points[-1]
    # All elements in between are the "middle" waypoints to be permuted
    middle = points[1:-1]
    # Number of shortest routes to keep (macro 'number_routes')
    number_routes_str = r"%{number_routes}"
    try:
        number_routes = int(number_routes_str)
    except:
        number_routes = 50  # fallback default
    # Distance function between two 3D points
    def distance(a, b):
        return math.sqrt((a[0]-b[0])**2 + (a[1]-b[1])**2 + (a[2]-b[2])**2)
    # Route length = sum of segment distances
    def route_length(route):
        return sum(distance(route[i], route[i+1]) for i in range(len(route)-1))
    # Build all permutations of the middle waypoints
    routes_with_length = []
    for perm in itertools.permutations(middle):
        route = [start] + list(perm) + [end]
        length = route_length(route)
        routes_with_length.append((length, route))
    # Keep the n shortest routes
    routes_with_length.sort(key=lambda x: x[0])
    routes_only = [route for _, route in routes_with_length[:number_routes]]
    # Create JSON output (same format expected downstream)
    lines = []
    lines.append('{"routes": [')
    for idx, route in enumerate(routes_only):
        line = "  " + json.dumps(route)
        if idx < len(routes_only) - 1:
            line += ","
        lines.append(line)
    lines.append("]}")
    json_body = "\n".join(lines)
    # Return as DataFrame so RapidMiner can consume it
    return pd.DataFrame([{"routes_json": json_body}])
 