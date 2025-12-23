import json
import pickle
import io
import numpy as np


class SerializerRegistry:
    def __init__(self):
        self._registry = {
            "json": (self._serialize_json, self._deserialize_json),
            "pickle": (self._serialize_pickle, self._deserialize_pickle),
            "ndarray": (self._serialize_ndarray, self._deserialize_ndarray),
            "ndarray_list": (
                self._serialize_ndarray_list,
                self._deserialize_ndarray_list,
            ),
        }

    def serialize(self, obj, fmt: str) -> bytes:
        return self._registry[fmt][0](obj)

    def deserialize(self, data: bytes, fmt: str):
        return self._registry[fmt][1](data)

    def safe_json_load(self, data: bytes):
        try:
            return json.loads(data.decode("utf-8"))
        except Exception:
            return data

    # --- Internal serialization methods ---
    def _serialize_json(self, obj):
        if not isinstance(obj, dict):
            raise TypeError("JSON serialization requires a dictionary.")
        return json.dumps(obj).encode("utf-8")

    def _deserialize_json(self, data):
        return json.loads(data.decode("utf-8"))

    def _serialize_pickle(self, obj):
        return pickle.dumps(obj)

    def _deserialize_pickle(self, data):
        return pickle.loads(data)

    def _serialize_ndarray(self, arr):
        if not isinstance(arr, np.ndarray):
            raise TypeError("NumPy serialization requires a NumPy array.")
        buffer = io.BytesIO()
        np.save(buffer, arr)
        return buffer.getvalue()

    def _deserialize_ndarray(self, data):
        return np.load(io.BytesIO(data), allow_pickle=True)

    def _serialize_ndarray_list(self, arr_list):
        if not isinstance(arr_list, list) or not all(
            isinstance(a, np.ndarray) for a in arr_list
        ):
            raise TypeError("Serialization requires a list of NumPy arrays.")
        buffer = io.BytesIO()
        np.savez(buffer, *arr_list)
        return buffer.getvalue()

    def _deserialize_ndarray_list(self, data):
        buffer = io.BytesIO(data)
        arrays = np.load(buffer, allow_pickle=True)
        return [arrays[key] for key in arrays]
