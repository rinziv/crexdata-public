# merge_naive_scored.py
# Usage examples:
#   python merge_naive_scored.py --base "C:/Users/AlexisPC/Desktop/allCSV/CLUSTERED"
#   python merge_naive_scored.py --base "C:/Users/AlexisPC/Desktop/allCSV/CLUSTERED" --subjects yvonni alex rafail
#   python merge_naive_scored.py --base "C:/Users/AlexisPC/Desktop/allCSV/CLUSTERED" --out merged_all.csv

from pathlib import Path
import argparse
import pandas as pd


def find_subjects(base_dir: Path, subjects_cli):
    if subjects_cli:
        return [s for s in subjects_cli]
    # auto-discover subjects = immediate subfolders of base_dir
    return [p.name for p in base_dir.iterdir() if p.is_dir()]


def main():
    ap = argparse.ArgumentParser(
        description="Merge *_naive_scored.csv files and add subject_id/dataset_id."
    )
    ap.add_argument(
        "--base",
        default="C:/Users/AlexisPC/Desktop/allCSV/CLUSTERED",  # Added default
        help="Base folder containing subject folders.",
    )
    ap.add_argument(
        "--subjects",
        nargs="*",
        default=None,
        help="Optional list of subject folder names to include. If omitted, all subfolders are scanned.",
    )
    ap.add_argument(
        "--out",
        default=None,
        help="Output filename (CSV). Defaults to <base>/merged_naive_scored.csv",
    )
    args = ap.parse_args()

    base_dir = Path(args.base)
    if not base_dir.exists():
        raise SystemExit(f"Base directory not found: {base_dir}")

    subjects = find_subjects(base_dir, args.subjects)
    if not subjects:
        raise SystemExit("No subject folders found.")

    all_frames = []
    per_subject_counts = {}

    for subj in subjects:
        # subj_dir = base_dir / subj / "naiveForRegression"
        subj_dir = base_dir / subj / "naiveForRegressionSevenEqualWeights"

        if not subj_dir.exists():
            print(f"⚠️  Skipping {subj}: missing folder {subj_dir}")
            continue

        files = list(subj_dir.glob("*_naive_scored.csv"))
        if not files:
            print(f"⚠️  Skipping {subj}: no *_naive_scored.csv files in {subj_dir}")
            continue

        cnt = 0
        for fp in files:
            try:
                df = pd.read_csv(fp)
                df["subject_id"] = subj
                # dataset_id = file stem without the trailing "_naive_scored"
                stem = fp.stem
                dataset_id = (
                    stem[: -len("_naive_scored")]
                    if stem.endswith("_naive_scored")
                    else stem
                )
                df["dataset_id"] = dataset_id
                all_frames.append(df)
                cnt += len(df)
            except Exception as e:
                print(f"!! Error reading {fp}: {e}")
        per_subject_counts[subj] = cnt
        print(f"✓ {subj}: {cnt} rows merged from {len(files)} files.")

    if not all_frames:
        raise SystemExit("No rows merged. Check paths/subjects.")

    merged = pd.concat(all_frames, ignore_index=True)

    # out_path = Path(args.out) if args.out else (base_dir / "merged_naive_scored.csv")
    out_path = (
        Path(args.out) if args.out else (base_dir / "merged_naive_scored_7features.csv")
    )

    out_path.parent.mkdir(parents=True, exist_ok=True)
    merged.to_csv(out_path, index=False)

    print("\n== Merge complete ==")
    print(f"Output: {out_path}")
    print(f"Total rows: {len(merged)}")
    print("Rows per subject:")
    for s, n in per_subject_counts.items():
        print(f" - {s}: {n}")
    # quick sanity: ClusterRank present?
    if "ClusterRank" not in merged.columns:
        print(
            "⚠️  Note: 'ClusterRank' not found in merged columns. Did you run the naive scoring first?"
        )


if __name__ == "__main__":
    main()
