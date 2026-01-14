import pandas as pd
import ast
from pyproj import Proj, Transformer

def gps_to_relative_enu(waypoints):
    if not waypoints:
        return []
    lat0, lon0, alt0 = waypoints[0]
    proj_enu = Proj(proj="tmerc", lat_0=lat0, lon_0=lon0, ellps="WGS84")
    # Neuere pyproj-Versionen: proj_enu.crs statt proj_enu.srs
    transformer = Transformer.from_crs("epsg:4326", proj_enu.crs, always_xy=True)

    rel_coords = []
    e0, n0 = None, None
    for lat, lon, alt in waypoints:
        e, n = transformer.transform(lon, lat)
        if e0 is None:
            e0, n0 = e, n
        # x,y relativ, z absolut
        rel_coords.append([e - e0, n - n0, alt])
    return rel_coords

def rm_main(data):
    """
    Erwartet: DataFrame aus RapidMiner, erste Spalte: String mit Koordinatenliste
              z.B. "[[47.15,11.31,30],[47.16,11.32,30],...]"
    """
    coords_str = data.iloc[0, 0]
    waypoints = ast.literal_eval(coords_str)
    rel = gps_to_relative_enu(waypoints)
    rel_str = str(rel)  # [[0.0,0.0,30.0],[...]]
    return pd.DataFrame({"list_of_coordinates": [rel_str]})