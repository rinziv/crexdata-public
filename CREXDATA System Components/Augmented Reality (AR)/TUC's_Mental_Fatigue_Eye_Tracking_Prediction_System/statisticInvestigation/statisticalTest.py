import pandas as pd
from scipy.stats import shapiro
from sklearn.discriminant_analysis import StandardScaler


# Load your data
df = pd.read_csv(
    "C:/Users/AlexisPC/Desktop/UPDATED_INDUCING_MENTAL_FATIGUE/frosw/froswSlidingWindowResults.csv",
)

# Exclude specific columns
columns_to_exclude = ["last real_datetime", "Test Name"]

# Select only numeric columns and drop the excluded ones
numeric_cols = df.select_dtypes(include="number").drop(
    columns=columns_to_exclude, errors="ignore"
)

# Apply Shapiro-Wilk test to each remaining numeric column
for col in numeric_cols.columns:

    data = numeric_cols[col].dropna()  # drop missing values
    scaler = StandardScaler()  # calculate standard deviation
    data = scaler.fit_transform(
        data.values.reshape(-1, 1)
    ).flatten()  # standardize the data
    if 3 <= len(data) <= 5000:  # Shapiro-Wilk recommendation
        stat, p = shapiro(data)
        print(f'{col}: p-value = {p} -> {"Normal" if p > 0.05 else "Not normal"}')
    else:
        print(f"{col}: Skipped (Shapiro-Wilk requires 3 <= n <= 5000)")
