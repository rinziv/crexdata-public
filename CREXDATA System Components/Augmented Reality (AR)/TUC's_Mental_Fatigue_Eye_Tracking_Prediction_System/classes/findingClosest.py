import pandas as pd

class BufferProc:

    def __init__():
        pass
    
    def find_closest_index(value, target_df, target_col):
        # Ensure the column is datetime format
        target_df[target_col] = pd.to_datetime(target_df[target_col])

        # Ensure value is also in datetime format
        value = pd.to_datetime(value)

        # Perform subtraction safely
        return (target_df[target_col] - value).abs().idxmin()



    def find_closest_datetime(value, target_df, target_col):
        # Ensure the column is datetime format
        target_df[target_col] = pd.to_datetime(target_df[target_col])

        # Ensure value is also in datetime format
        value = pd.to_datetime(value)

        # Perform subtraction safely and get the closest datetime
        closest_index = (target_df[target_col] - value).abs().idxmin()
        return target_df.loc[closest_index, target_col]  # Return datetime instead of index