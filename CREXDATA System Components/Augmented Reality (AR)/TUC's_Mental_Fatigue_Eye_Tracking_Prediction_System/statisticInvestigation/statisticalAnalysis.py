import pandas as pd
import numpy as np
from scipy import stats
from pathlib import Path

# ----------------------- Config -----------------------
BASE_DIR = Path("C:/Users/AlexisPC/Desktop/allCSV/CLUSTERED")


# Auto-discover all subject folders
def find_subjects(base_dir):
    return [p.name for p in base_dir.iterdir() if p.is_dir()]


SUBJECTS = find_subjects(BASE_DIR)
print(f"Found subjects: {SUBJECTS}\n")

cluster_col = "cluster"

# Features to analyze - ONLY the ones with defined weights
features = [
    "mean pupil diameter",
    "blink count",
    "peak saccade velocity",
    "mean saccade amplitude",
]

# Direction of fatigue effect from 4 weight Naive Clustering:
#   peak saccade velocity: +0.1259
#   mean saccade velocity: -0.4497
#   blink count: -0.1401
#   mean pupil diameter: -0.2842


# Direction of fatigue effect from 7 weight Naive Clustering:
# peak saccade velocity: -0.3045
# mean saccade velocity: -0.1916
# blink count: +0.1673
# mean pupil diameter: -0.3367


sign_map = {
    "mean pupil diameter": -0.2616,
    "blink count": +0.1299,
    "peak saccade velocity": -0.2366,
    "mean saccade amplitude": -0.3720,
}


# If no feature is significant in Kruskal, do we fall back to all features?
FALLBACK_TO_ALL_IF_NONE = False  # Changed to False - use ONLY these 4 features

# Store all processed dataframes for final merge
all_processed_dfs = []

# ----------------------- Process each SUBJECT -----------------------
for NAME in SUBJECTS:
    print(f"\n{'#'*80}")
    print(f"PROCESSING SUBJECT: {NAME}")
    print(f"{'#'*80}\n")

    paths = [
        BASE_DIR / NAME / f"{NAME}CLUSTERED_AXCPT_and_NBack.csv",
        BASE_DIR / NAME / f"{NAME}CLUSTERED_mentRot.csv",
        BASE_DIR / NAME / f"{NAME}CLUSTERED_searchLoop.csv",
    ]

    # ----------------------- Process each file for this subject -----------------------
    for path2 in paths:
        if not path2.exists():
            print(f"⚠️  Skipping missing file: {path2}")
            continue

        print(f"\n{'='*80}")
        print(f"Processing: {path2}")
        print(f"{'='*80}\n")

        # ----------------------- Load -------------------------
        df = pd.read_csv(path2)
        df[cluster_col] = df[cluster_col].astype(str)

        print("Dataset preview:")
        print(df.head())
        print("\nAvailable columns:")
        print(df.columns)

        # ----------------------- Identify clusters -----------------------
        # Get all unique cluster labels (like "0","1","2","3" or "0.0","1.0",...)
        clusters_present_raw = sorted(
            df[cluster_col].unique(),
            key=lambda x: float(x) if x.replace(".", "", 1).isdigit() else x,
        )

        # Convert them to ints (0,1,2,3,...) so we can index rows
        cluster_ids_numeric = []
        for c in clusters_present_raw:
            try:
                cluster_ids_numeric.append(int(float(c)))
            except:
                pass

        unique_sorted_cluster_ids = sorted(set(cluster_ids_numeric))
        print("\nDetected cluster IDs:", unique_sorted_cluster_ids)

        # Validation: Check if clusters start from 1 instead of 0
        if unique_sorted_cluster_ids and min(unique_sorted_cluster_ids) == 1:
            print("✅ Clusters start from 1 (not 0-indexed)")
        elif unique_sorted_cluster_ids and min(unique_sorted_cluster_ids) == 0:
            print("⚠️  Clusters start from 0 (0-indexed)")

        # Map: actual cluster id (like 0 or 2) -> row index (0..K-1 in arrays)
        cluster_id_to_row = {
            cid: idx for idx, cid in enumerate(unique_sorted_cluster_ids)
        }

        K = len(unique_sorted_cluster_ids)
        C = len(features)

        print(f"\nK (number of clusters) = {K}")
        print(f"C (number of features) = {C}")

        # ----------------------- Kruskal–Wallis: pick features that differ across clusters -----------------------
        kept_features = []
        print("\n=== Kruskal–Wallis per feature ===")

        for feature in features:
            if feature not in df.columns:
                print(f"⚠️  Skipping missing column: {feature}")
                continue

            # collect values per cluster
            groups = []
            for clabel in clusters_present_raw:
                vals = df[df[cluster_col] == clabel][feature]
                if len(vals) > 0:
                    groups.append(vals)

            # need at least 2 non-empty groups
            if len(groups) < 2:
                print(f"{feature}: not enough data across clusters → not significant")
                continue

            try:
                H, p = stats.kruskal(*groups)
            except Exception as e:
                print(f"{feature}: Kruskal error ({e}) → not significant")
                continue

            sig = p < 0.05
            flag = "✅" if sig else "❌"
            print(f"{feature}: H={H:.4f}, p={p:.4f} {flag}")
            if sig:
                kept_features.append(feature)

        print("\nSignificant features kept:", kept_features)

        if not kept_features:
            if FALLBACK_TO_ALL_IF_NONE:
                print("⚠️  No significant features. Falling back to ALL features.")
                kept_features = features.copy()
            else:
                print(f"⚠️  No significant features for {path2}, skipping")
                continue

        # ----------------------- Compute per-cluster medians for kept features -----------------------
        # X will be shape (K, n_kept): each row is a cluster, each col is one kept feature
        n_kept = len(kept_features)
        X = np.zeros((K, n_kept), dtype=float)

        print("\n=== Cluster medians (kept features only) ===")
        for col_idx, feature in enumerate(kept_features):
            medians = df.groupby(cluster_col)[feature].median()

            print(f"\n{feature} medians:")
            for cluster_label, median_val in medians.items():
                # map e.g. "2.0" -> 2 -> row index
                try:
                    cid = int(float(cluster_label))
                except:
                    continue

                if cid in cluster_id_to_row and np.isfinite(median_val):
                    row_idx = cluster_id_to_row[cid]
                    X[row_idx, col_idx] = float(median_val)
                    print(
                        f"  Cluster {cluster_label} -> row {row_idx}: {median_val:.4f}"
                    )
                else:
                    print(
                        f"  Cluster {cluster_label}: {median_val:.4f} (not mapped / skipped)"
                    )

        print("\nMatrix X (rows = clusters in order of unique_sorted_cluster_ids):")
        print("kept_features =", kept_features)
        print(X)

        # ----------------------- Z-score across clusters -----------------------
        # We normalize each feature across clusters so they're comparable
        means = X.mean(axis=0)
        stds = X.std(axis=0, ddof=1)

        safe_stds = np.where((stds == 0) | ~np.isfinite(stds), 1.0, stds)
        z_scores = (X - means) / safe_stds  # shape (K, n_kept)

        print("\nFeature means:", means)
        print("Feature stds :", stds)
        print("Z-scores:\n", z_scores)

        # ----------------------- Build weight vector -----------------------
        # Each feature has equal absolute weight, and total abs(weights) = 1.
        # So base magnitude = 1 / n_kept
        base_weight_mag = 1.0 / n_kept

        # Apply direction from sign_map
        weight_vector = np.array(
            [sign_map[f] * base_weight_mag for f in kept_features], dtype=float
        )

        print("\nWeight vector (signed):")
        for f, w in zip(kept_features, weight_vector):
            print(f"  {f}: {w:.6f}")
        print("Sum of ABS(weights):", np.abs(weight_vector).sum())
        print("Sum of signed weights:", weight_vector.sum())

        # ----------------------- Fatigue score per cluster -----------------------
        # FatigueScore(cluster k) = sum_over_features( z(k,f) * signed_weight(f) )
        fatigue_scores = z_scores.dot(weight_vector)  # shape (K,)

        df_scores = pd.DataFrame(
            {
                "ClusterID": unique_sorted_cluster_ids,
                "FatigueScore": fatigue_scores,
            }
        )

        # ----------------------- Assign fatigue levels -----------------------
        # Goal:
        #   - Most fatigued → label = 4 (or generally = K)
        #   - Least fatigued → label = 1
        #   - No duplicates unless exact same FatigueScore

        # 1. Sort clusters by fatigue DESC (highest score = most fatigued)
        df_scores = df_scores.sort_values(
            by="FatigueScore", ascending=False
        ).reset_index(drop=True)

        # 2. ordinal_rank: 1,2,3,... where 1 = most fatigued
        df_scores["ordinal_rank"] = df_scores.index + 1

        # 3. Convert ordinal_rank to fatigue_label where most fatigued gets K, least gets 1
        #    fatigue_label = K - ordinal_rank + 1
        K_current = len(df_scores)
        df_scores["fatigue_label"] = K_current - df_scores["ordinal_rank"] + 1

        # 4. Go back to natural cluster id order for readability
        df_scores = df_scores.sort_values(by="ClusterID").reset_index(drop=True)

        print("\nFatigue Scores per Cluster (final):")
        print(df_scores[["ClusterID", "FatigueScore", "fatigue_label"]])

        # ----------------------- Map fatigue_label back to df -----------------------
        # We will store fatigue_label in df["rank"]
        clusterid_to_label = dict(
            zip(df_scores["ClusterID"], df_scores["fatigue_label"])
        )

        def lookup_label(cluster_label):
            try:
                cid = int(float(cluster_label))
            except Exception:
                return np.nan
            return clusterid_to_label.get(cid, np.nan)

        df["rank"] = df[cluster_col].apply(lookup_label)

        print("\nPreview with assigned 'rank':")
        print(df[[cluster_col, "rank"]].head())

        # Add subject_id and dataset_id for merged file
        df["subject_id"] = NAME
        df["dataset_id"] = path2.stem  # filename without extension

        # Store for final merge
        all_processed_dfs.append(df)

        # ----------------------- Save -----------------------
        out_dir = BASE_DIR / NAME / "clusteringMeaningRegressionWeights"
        out_dir.mkdir(parents=True, exist_ok=True)

        if "search" in str(path2):
            out = out_dir / f"{NAME}ClusterMeaning_searchLoop_equalWeights.csv"
        elif "mentRot" in str(path2):
            out = out_dir / f"{NAME}ClusterMeaning_mentRot_equalWeights.csv"
        else:
            out = out_dir / f"{NAME}ClusterMeaning_AXCPT_and_NBack_equalWeights.csv"

        df.to_csv(out, index=False)
        print(f"✅ Saved: {out}")

# ----------------------- Merge all processed files -----------------------
if all_processed_dfs:
    print(f"\n{'='*80}")
    print("MERGING ALL PROCESSED FILES")
    print(f"{'='*80}\n")

    merged_df = pd.concat(all_processed_dfs, ignore_index=True)
    merged_output = BASE_DIR / "merged_all_subjects_regression_weights.csv"
    merged_df.to_csv(merged_output, index=False)

    print(f"✅ Merged file saved: {merged_output}")
    print(f"Total rows: {len(merged_df)}")
    print(f"Subjects included: {merged_df['subject_id'].nunique()}")
    print(f"Subjects: {sorted(merged_df['subject_id'].unique())}")

print(f"\n{'='*80}")
print("✅ ALL FILES PROCESSED AND MERGED SUCCESSFULLY!")
print(f"{'='*80}")
