from pathlib import Path
import json
import numpy as np
import pandas as pd
from sklearn.preprocessing import StandardScaler

# ---------- USER INPUT ----------
# Ask for subject name from keyboard
NAME = input("Enter subject name (e.g., alex, yvonni, rafail): ").strip()

input_files = [
    f"C:/Users/AlexisPC/Desktop/allCSV/CLUSTERED/{NAME}/{NAME}CLUSTERED_AXCPT_and_NBack.csv",
    f"C:/Users/AlexisPC/Desktop/allCSV/CLUSTERED/{NAME}/{NAME}CLUSTERED_mentRot.csv",
    f"C:/Users/AlexisPC/Desktop/allCSV/CLUSTERED/{NAME}/{NAME}CLUSTERED_searchLoop.csv",
]

CLUSTER_COL = "cluster"
FEATURES = [
    "peak saccade velocity",
    "mean saccade velocity",
    "blink count",
    "mean pupil diameter",
    "mean fixation duration",
    "mean saccade duration",
    "mean saccade amplitude",
]
# Physiology sign vector: saccade -, blink +, pupil -
SIGNS = np.array([-1, -1, +1, -1, -1, +1, -1], dtype=float)
# --------------------------------


def process_one(csv_path: str):
    p = Path(csv_path)
    print(f"\n== Processing {p} ==")

    df = pd.read_csv(p)

    # sanity checks
    missing = [c for c in [CLUSTER_COL] + FEATURES if c not in df.columns]
    if missing:
        raise ValueError(f"{p.name} missing required columns: {missing}")

    # 1) z-score features inside THIS dataset
    X = df[FEATURES].values.astype(float)
    scaler = StandardScaler().fit(X)
    Z = scaler.transform(X)
    Zdf = pd.DataFrame(Z, columns=FEATURES).assign(
        **{CLUSTER_COL: df[CLUSTER_COL].values}
    )

    # 2) centroids per cluster (mean z per feature)
    centroids = Zdf.groupby(CLUSTER_COL)[FEATURES].mean()

    # 3) naive cluster scores (signed sum of centroid z's)
    cluster_scores = centroids.values @ SIGNS
    centroids_out = centroids.copy()
    centroids_out["naive_score"] = cluster_scores

    # 4) order clusters low → high fatigue
    order_idx = np.argsort(cluster_scores)
    ordered_clusters = centroids.index.values[order_idx].tolist()
    rank_map = {c: i + 1 for i, c in enumerate(ordered_clusters)}

    # 5) per-row NaiveIndex (signed sum of row z-scores) + z of the index
    naive_index = Z @ SIGNS
    idx_sd = naive_index.std(ddof=0)
    naive_index_z = naive_index / (idx_sd if idx_sd != 0 else 1.0)

    # 6) attach to original df
    df["NaiveIndex"] = naive_index
    df["NaiveIndex_z"] = naive_index_z
    df["ClusterRank"] = df[CLUSTER_COL].map(rank_map).astype(int)

    # 7) outputs in "naiveForRegressionSevenEqualWeights" subfolder next to the input file
    naive_folder = (
        p.parent / "naiveForRegressionSevenEqualWeights"
    )  # Changed folder name
    naive_folder.mkdir(exist_ok=True)  # Create folder if it doesn't exist

    base_name = p.stem  # Filename without extension
    scored_path = naive_folder / f"{base_name}_naive_scored.csv"
    centroids_path = naive_folder / f"{base_name}_naive_centroids.csv"
    summary_path = naive_folder / f"{base_name}_naive_summary.json"

    # save files
    df.to_csv(scored_path, index=False)
    centroids_out.reset_index().to_csv(centroids_path, index=False)

    mean_idx_by_cluster = (
        df.groupby(CLUSTER_COL)["NaiveIndex"].mean().sort_values().to_dict()
    )
    summary = {
        "file": str(p),
        "features": FEATURES,
        "sign_vector": {f: float(s) for f, s in zip(FEATURES, SIGNS)},
        "ordered_clusters_low_to_high_fatigue": ordered_clusters,
        "cluster_to_rank": {str(k): int(v) for k, v in rank_map.items()},
        "cluster_mean_NaiveIndex_sorted": {
            str(k): float(v) for k, v in mean_idx_by_cluster.items()
        },
        "index_std_dev": float(idx_sd),
        "notes": "Naive approach: signed sum of z-scored metrics; equal weights by sign only.",
    }
    with open(summary_path, "w", encoding="utf-8") as f:
        json.dump(summary, f, indent=2)

    print(f"  → Scored rows:   {scored_path}")
    print(f"  → Centroids:     {centroids_path}")
    print(f"  → Summary:       {summary_path}")
    print(f"  Order (low→high): {ordered_clusters}")


if __name__ == "__main__":
    errors = []
    for fp in input_files:
        try:
            process_one(fp)
        except Exception as e:
            errors.append((fp, str(e)))
            print(f"!! ERROR on {fp}: {e}")

    if errors:
        print("\nCompleted with errors on these files:")
        for fp, msg in errors:
            print(f" - {fp}: {msg}")
    else:
        print("\n✅ All files processed successfully.")
