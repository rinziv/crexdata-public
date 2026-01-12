# regress_from_merged_to_output.py
from pathlib import Path
import json
import numpy as np
import pandas as pd
from sklearn.linear_model import Ridge

# ========= USER CONFIG =========
# Point this to your merged file:
MERGED_CSV = (
    r"C:/Users/AlexisPC/Desktop/allCSV/CLUSTERED/merged_naive_scored_7features.csv"
)

FEATURES = [
    "peak saccade velocity",
    "mean saccade velocity",
    "blink count",
    "mean pupil diameter",
    "mean fixation duration",  # Fixed: added missing feature
    "mean saccade duration",  # Fixed: added missing feature
    "mean saccade amplitude",
]
TARGET = "ClusterRank"  # from your naive step
SUBJECT = "subject_id"  # added during merge

RIDGE_ALPHA = 1.0  # regularization (↑ for smoother weights)
BALANCE_SUBJECTS = True  # give each subject equal total weight

# --- OPTIONAL weight balancing (pick ONE or neither) ---
SMOOTH_ALPHA = 0.50  # None or 0..1; blend magnitudes toward equal (0=orig, 1=equal)
MIN_FLOOR = None  # None or 0..0.24; ensure each feature ≥ this share
# ===============================================


def zscore_within_subject(df: pd.DataFrame, features, subject_col):
    def _zblock(g: pd.DataFrame):
        X = g[features].astype(float)
        mu = X.mean(axis=0)
        sd = X.std(axis=0, ddof=0).replace(0, 1.0)
        g[features] = (X - mu) / sd
        return g

    return df.groupby(subject_col, group_keys=False).apply(_zblock)


def apply_weight_smoothing(beta: np.ndarray, smooth_alpha, min_floor):
    if smooth_alpha is not None and min_floor is not None:
        raise ValueError("Use either SMOOTH_ALPHA or MIN_FLOOR, not both.")
    m = np.abs(beta)
    if m.sum() == 0:
        raise ValueError("All-zero coefficients; cannot smooth.")
    m = m / m.sum()
    if smooth_alpha is not None:
        a = float(smooth_alpha)
        if not (0.0 <= a <= 1.0):
            raise ValueError("SMOOTH_ALPHA must be in [0,1].")
        m = (1 - a) * m + a * np.full_like(m, 1.0 / len(m))
    elif min_floor is not None:
        eps = float(min_floor)
        if not (0.0 <= eps < 0.25):
            raise ValueError("MIN_FLOOR should be in [0, 0.24].")
        m = eps + (1 - len(m) * eps) * m
    return m  # sums to 1


def main():
    p = Path(MERGED_CSV)
    df = pd.read_csv(p)

    # checks
    missing = [c for c in [SUBJECT, TARGET] + FEATURES if c not in df.columns]
    if missing:
        raise SystemExit(f"Missing required columns in merged file: {missing}")

    # 1) within-subject z-scores
    dfz = zscore_within_subject(df.copy(), FEATURES, SUBJECT)

    # 2) data
    Z = dfz[FEATURES].values.astype(float)
    y = dfz[TARGET].astype(float).values

    # optional subject balancing
    sample_weight = None
    if BALANCE_SUBJECTS:
        counts = dfz.groupby(SUBJECT)[SUBJECT].transform("count")
        sample_weight = 1.0 / counts
        sample_weight *= len(dfz) / sample_weight.sum()

    # 3) fit ridge
    model = Ridge(alpha=RIDGE_ALPHA)
    model.fit(Z, y, sample_weight=sample_weight)
    beta = model.coef_.astype(float)

    # 4) weights (optionally balanced)
    if (SMOOTH_ALPHA is not None) or (MIN_FLOOR is not None):
        magnitudes = apply_weight_smoothing(beta, SMOOTH_ALPHA, MIN_FLOOR)
        signs = np.sign(beta)
        signs[signs == 0] = 1.0
        w = signs * magnitudes
        w = w / np.sum(np.abs(w))
    else:
        L1 = float(np.sum(np.abs(beta)))
        if L1 == 0:
            raise SystemExit(
                "All coefficients are zero. Lower RIDGE_ALPHA or check data."
            )
        w = beta / L1

    # 5) index + orientation
    index = Z @ w
    dfz["FatigueIndex"] = index
    means_by_rank = dfz.groupby(TARGET)["FatigueIndex"].mean().sort_index().values
    if len(means_by_rank) >= 2 and means_by_rank[0] > means_by_rank[-1]:
        w = -w
        dfz["FatigueIndex"] = -dfz["FatigueIndex"]
        means_by_rank = means_by_rank[::-1]

    # z-score the index
    idx_sd = float(dfz["FatigueIndex"].std(ddof=0))
    dfz["FatigueIndex_z"] = dfz["FatigueIndex"] / (idx_sd if idx_sd != 0 else 1.0)

    # thresholds (midpoints between rank means)
    m = means_by_rank
    thresholds = (
        [(m[i] + m[i + 1]) / 2.0 for i in range(len(m) - 1)] if len(m) >= 2 else []
    )

    def bin_from_thresholds(v, thr):
        b = 1
        for t in thr:
            if v > t:
                b += 1
        return b

    dfz["FatigueBin"] = [
        bin_from_thresholds(v, thresholds) for v in dfz["FatigueIndex"].values
    ]

    # 6) save to output/ next to merged file
    out_dir = p.parent / "output"
    out_dir.mkdir(parents=True, exist_ok=True)

    weights_path = out_dir / f"{p.stem}_weights.csv"
    scored_path = out_dir / f"{p.stem}_scored.csv"
    summary_path = out_dir / f"{p.stem}_summary.json"

    pd.DataFrame({"feature": FEATURES, "weight_L1": w, "beta_raw": beta}).to_csv(
        weights_path, index=False
    )
    dfz.to_csv(scored_path, index=False)

    rank_means = dfz.groupby(TARGET)["FatigueIndex"].mean().sort_index().to_dict()
    summary = {
        "features": FEATURES,
        "alpha": RIDGE_ALPHA,
        "balanced_by_subject": bool(BALANCE_SUBJECTS),
        "weights_L1_sum_1": {f: float(wi) for f, wi in zip(FEATURES, w)},
        "beta_raw": {f: float(bi) for f, bi in zip(FEATURES, beta)},
        "rank_mean_index_low_to_high": [float(x) for x in m],
        "thresholds_on_index": [float(t) for t in thresholds],
        "index_std_dev": idx_sd,
        "rows": int(len(dfz)),
        "subjects": int(dfz[SUBJECT].nunique()),
        "outputs": {
            "weights_csv": str(weights_path),
            "merged_scored_csv": str(scored_path),
            "summary_json": str(summary_path),
        },
        "notes": "Within-subject z-scoring → Ridge on ClusterRank → (optional) weight balancing; Index oriented so higher = more fatigue.",
    }
    with open(summary_path, "w", encoding="utf-8") as f:
        json.dump(summary, f, indent=2)

    print("\n== Done ==")
    print(f"Weights  → {weights_path}")
    print(f"Scored   → {scored_path}")
    print(f"Summary  → {summary_path}")
    print("\nFinal weights (|w|=1):")
    for feat, wi in zip(FEATURES, w):
        print(f"  {feat}: {wi:+.4f}")


if __name__ == "__main__":
    main()
