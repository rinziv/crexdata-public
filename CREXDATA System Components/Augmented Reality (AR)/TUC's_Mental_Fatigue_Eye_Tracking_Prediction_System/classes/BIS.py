import pandas as pd
import numpy as np

pd.set_option("display.max_rows", None)  # Display all rows
pd.set_option("display.max_columns", None)  # Display all columns if needed


class BIS:
    
    def calculate_bis(rts, pc):
        # Convert inputs to NumPy arrays
        rts = np.array(rts, dtype=float)
        pc = np.array(pc, dtype=float)

        if len(rts) != len(pc):
            raise ValueError("The 'rts' and 'pc' arrays must have the same length.")

        # Compute means
        mrt = rts.mean()
        mpc = pc.mean()

        # Compute standard deviations using ddof=1 (Matches MATLAB)
        srt = rts.std(ddof=1)  # Sample standard deviation (MATLAB default)
        spc = pc.std(ddof=1)  # Sample standard deviation (MATLAB default)

        # Handle division by zero
        zrt = np.zeros_like(rts) if srt == 0 else (rts - mrt) / srt
        zpc = np.zeros_like(pc) if spc == 0 else (pc - mpc) / spc

        # Compute BIS
        bis = zpc - zrt
        # Return as Pandas Series
        return pd.Series(bis, name="bis")


    def calculate_bis_corrected(rts, pc):
        rts = np.array(rts, dtype=float)
        pc = np.array(pc, dtype=float)

        if len(rts) != len(pc):
            raise ValueError("The 'rts' and 'pc' arrays must have the same length.")

        mrt = rts.mean()
        mpc = pc.mean()
        srt = rts.std(ddof=0)  # Population standard deviation
        spc = pc.std(ddof=0)  # Population standard deviation

        # Handle division by zero in standardization
        if srt == 0:
            zrt = np.zeros_like(rts)
        else:
            zrt = (rts - mrt) / srt

        if spc == 0:
            zpc = np.zeros_like(pc)
        else:
            zpc = (pc - mpc) / spc

        # Compute BIS
        bis = zpc - zrt
        return pd.Series(bis, name="bis_corrected")


    def z_normalization(data):
        mean = data.mean()
        std = data.std()
        z = (data - mean) / std
        return z


    def nearestNeighbor(preBrums, postBrums, bisMean):
        dpre = abs(preBrums - bisMean)
        dpost = abs(postBrums - bisMean)
        if dpre < dpost:
            return 0
        else:
            return 1
