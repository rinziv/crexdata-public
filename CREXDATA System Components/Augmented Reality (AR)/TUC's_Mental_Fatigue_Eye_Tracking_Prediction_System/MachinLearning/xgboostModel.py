# -*- coding: utf-8 -*-
import os
import warnings

import numpy as np
import pandas as pd

import matplotlib.pyplot as plt
import seaborn as sns

from sklearn.model_selection import train_test_split
from sklearn.preprocessing import StandardScaler, LabelEncoder, label_binarize
from sklearn.metrics import (
    accuracy_score,
    precision_score,
    recall_score,
    f1_score,
    confusion_matrix,
    classification_report,
    roc_auc_score,
)
from sklearn.dummy import DummyClassifier

import xgboost as xgb
import joblib  # Add this import at the top

warnings.filterwarnings("ignore")

# ------------------------------------------------
# Load data
# ------------------------------------------------
path = "C:/Users/AlexisPC/Desktop/allCSV/CLUSTERED/READY_FOR_TRINING.csv"  # ✅ Changed path
df = pd.read_csv(path)
df.columns = df.columns.str.strip()  # remove accidental whitespace in column names

# Quick sanity view (optional)
print("cluster distribution (fraction of dataset):")
if "cluster" in df.columns:
    print(df["cluster"].value_counts() / float(len(df)))
else:
    print("Column 'cluster' not found; skipping distribution print.")

# ------------------------------------------------
# Features (X) and labels (y)
# ------------------------------------------------
# Drop columns that shouldn't be used for training
columns_to_drop = [
    "selfReportTest",
    "cluster",
    "Test Name",
    "NaiveIndex",
    "NaiveIndex_z",
    "ClusterRank",  # Target variable
    "subject_id",
    "dataset_id",
    "rank",
    "FatigueIndex",
    "FatigueIndex_z",
    "FatigueBin",
]

# Also drop any "Unnamed" columns
columns_to_drop.extend([col for col in df.columns if "Unnamed" in col])

# Filter to only drop columns that actually exist
columns_to_drop = [col for col in columns_to_drop if col in df.columns]

print("\nDropping columns:", columns_to_drop)

X = df.drop(columns_to_drop, axis=1)

# Encode y from original 'ClusterRank' to 0..n_classes-1
if "ClusterRank" not in df.columns:  # ✅ Changed from "rank" to "ClusterRank"
    raise ValueError(
        "Column 'ClusterRank' not found in the CSV. Please ensure it exists."
    )

le = LabelEncoder()
y = le.fit_transform(df["ClusterRank"])  # ✅ Changed from "rank" to "ClusterRank"
print(
    "Class mapping (original_rank -> encoded):",
    dict(zip(le.classes_, le.transform(le.classes_))),
)
print("Encoded class counts:\n", pd.Series(y).value_counts().sort_index())

print("\nFeatures used for training:")
print(X.columns.tolist())
print("Feature count:", len(X.columns))

# ------------------------------------------------
# Train / test split (stratified)
# ------------------------------------------------
X_train, X_test, y_train, y_test = train_test_split(
    X, y, test_size=0.30, random_state=42, stratify=y
)

# ------------------------------------------------
# Scale features (keep same columns)
# ------------------------------------------------
cols = X_train.columns
scaler = StandardScaler()
X_train = pd.DataFrame(scaler.fit_transform(X_train), columns=cols, index=X_train.index)
X_test = pd.DataFrame(scaler.transform(X_test), columns=cols, index=X_test.index)

# ------------------------------------------------
# Baseline model (majority class)
# ------------------------------------------------
dummy = DummyClassifier(strategy="most_frequent", random_state=42)
dummy.fit(X_train, y_train)
y_dummy_pred = dummy.predict(X_test)

print("\nBaseline (majority class) results:")
print("Baseline accuracy: {:.4f}".format(accuracy_score(y_test, y_dummy_pred)))

# ------------------------------------------------
# XGBoost (multiclass)
# ------------------------------------------------
n_classes = len(np.unique(y_train))

# Best hyperparameters from GridSearch
best_params = {
    "colsample_bytree": 1.0,
    "gamma": 0,
    "learning_rate": 0.1,
    "max_depth": 7,
    "min_child_weight": 1,
    "n_estimators": 600,
    "reg_alpha": 0.001,
    "reg_lambda": 1,
    "subsample": 0.8,
}

xgb_clf = xgb.XGBClassifier(
    objective="multi:softprob",
    eval_metric="mlogloss",
    num_class=n_classes,
    tree_method="hist",
    random_state=42,
    **best_params,  # Unpack the hyperparameters
)
xgb_clf.fit(X_train, y_train)

# ------------------------------------------------
# Calculate metrics FIRST (before saving metadata)
# ------------------------------------------------
# Predictions (encoded labels)
y_pred_enc = xgb_clf.predict(X_test)

# Probabilities for AUC (n_samples, n_classes)
y_prob = xgb_clf.predict_proba(X_test)
assert y_prob.shape == (X_test.shape[0], n_classes), "Shape mismatch for y_prob"

# Calculate all metrics
acc = accuracy_score(y_test, y_pred_enc)
prec = precision_score(y_test, y_pred_enc, average="weighted", zero_division=0)
rec = recall_score(y_test, y_pred_enc, average="weighted", zero_division=0)
f1 = f1_score(y_test, y_pred_enc, average="weighted", zero_division=0)

# Binarize for AUC
y_test_bin = label_binarize(y_test, classes=np.arange(n_classes))
auc_weighted = roc_auc_score(y_test_bin, y_prob, average="weighted", multi_class="ovr")
auc_macro = roc_auc_score(y_test_bin, y_prob, average="macro", multi_class="ovr")

print("\nXGBOOST model results:")
print("XGBOOST accuracy: {:.4f}".format(acc))
print("XGBOOST PRECISION (weighted): {:.4f}".format(prec))
print("XGBOOST RECALL    (weighted): {:.4f}".format(rec))
print("XGBOOST F1        (weighted): {:.4f}".format(f1))
print("XGBOOST AUC (weighted): {:.4f}".format(auc_weighted))
print("XGBOOST AUC (macro):    {:.4f}".format(auc_macro))

# ------------------------------------------------
# Save trained model and all artifacts
# ------------------------------------------------
MODEL_DIR = "C:/Users/AlexisPC/Desktop/saved_models/xgboost"
os.makedirs(MODEL_DIR, exist_ok=True)

print("\n" + "=" * 60)
print("SAVING MODEL AND ARTIFACTS")
print("=" * 60)

# 1. Save the trained XGBoost model
model_path = os.path.join(MODEL_DIR, "xgboost_fatigue_model.pkl")
joblib.dump(xgb_clf, model_path)
print(f"✅ Model saved: {model_path}")

# 2. Save the StandardScaler (critical for inference!)
scaler_path = os.path.join(MODEL_DIR, "xgboost_scaler.pkl")
joblib.dump(scaler, scaler_path)
print(f"✅ Scaler saved: {scaler_path}")

# 3. Save the LabelEncoder (to decode predictions back to original labels)
encoder_path = os.path.join(MODEL_DIR, "xgboost_label_encoder.pkl")
joblib.dump(le, encoder_path)
print(f"✅ Label encoder saved: {encoder_path}")

# 4. Save feature names (must match training order)
features_path = os.path.join(MODEL_DIR, "xgboost_features.pkl")
joblib.dump(X.columns.tolist(), features_path)
print(f"✅ Feature names saved: {features_path}")

# 5. Save columns to drop for inference (NEW data won't have ClusterRank!)
columns_to_drop_inference = [
    "selfReportTest",
    "cluster",
    "Test Name",
    "NaiveIndex",
    "NaiveIndex_z",
    # NOTE: ClusterRank NOT here - it won't exist in new data
    "subject_id",
    "dataset_id",
    "rank",
    "FatigueIndex",
    "FatigueIndex_z",
    "FatigueBin",
]
drop_cols_path = os.path.join(MODEL_DIR, "xgboost_drop_columns.pkl")
joblib.dump(columns_to_drop_inference, drop_cols_path)
print(f"✅ Drop columns list saved: {drop_cols_path}")

# 6. Save training metadata (NOW acc and f1 are defined!)
metadata = {
    "n_classes": n_classes,
    "class_labels": le.classes_.tolist(),
    "n_features": len(X.columns),
    "feature_names": X.columns.tolist(),
    "test_accuracy": acc,
    "test_f1_weighted": f1,
    "test_auc_weighted": auc_weighted,
    "test_auc_macro": auc_macro,
    "hyperparameters": best_params,  # Use the same dict
}
import json

metadata_path = os.path.join(MODEL_DIR, "xgboost_metadata.json")
with open(metadata_path, "w") as f:
    json.dump(metadata, f, indent=2)
print(f"✅ Training metadata saved: {metadata_path}")

print("=" * 60)
print("ALL ARTIFACTS SAVED SUCCESSFULLY")
print("=" * 60 + "\n")

# ------------------------------------------------
# Feature Importance
# ------------------------------------------------
# Get feature importance scores (using 'gain' for better interpretation)
feature_importance = xgb_clf.get_booster().get_score(importance_type="gain")

# Convert to DataFrame (get_score returns dict)
importance_df = pd.DataFrame(
    {
        "Feature": list(feature_importance.keys()),
        "Importance": list(feature_importance.values()),
    }
).sort_values(by="Importance", ascending=False)

print("\nTop 20 Most Important Features (by Gain):")
print(importance_df.head(20))

# Save to CSV
importance_df.to_csv(
    "C:/Users/AlexisPC/Desktop/pythonEYE/MachinLearning/feature_importance.csv",
    index=False,
)

# Plot feature importance
plt.figure(figsize=(10, 8))
top_n = 20  # Show top 20 features
top_features = importance_df.head(top_n)
plt.barh(range(len(top_features)), top_features["Importance"])
plt.yticks(range(len(top_features)), top_features["Feature"])
plt.xlabel("Importance Score")
plt.ylabel("Features")
plt.title(f"Top {top_n} Feature Importance (XGBoost)")
plt.gca().invert_yaxis()  # Highest importance at top
plt.tight_layout()
plt.show()

# Alternative: Built-in XGBoost plot with gain
fig, ax = plt.subplots(figsize=(10, 8))
xgb.plot_importance(xgb_clf, ax=ax, max_num_features=20, importance_type="gain")
plt.title("Feature Importance (by Gain)")
plt.tight_layout()
plt.show()

# ------------------------------------------------
# Comparison to baseline (accuracy)
# ------------------------------------------------
print("\nComparison of accuracies:")
print("Baseline: {:.4f}".format(accuracy_score(y_test, y_dummy_pred)))
print("XGBoost:  {:.4f}".format(acc))

# ------------------------------------------------
# Confusion Matrix (on original labels for readability)
# ------------------------------------------------
y_pred_orig = le.inverse_transform(y_pred_enc)
y_test_orig = le.inverse_transform(y_test)
labels_orig = le.classes_  # original label values (e.g., [1,2,3,4])

cm = confusion_matrix(y_test_orig, y_pred_orig, labels=labels_orig)
cm_df = pd.DataFrame(
    cm,
    columns=[f"Pred {c}" for c in labels_orig],
    index=[f"Actual {c}" for c in labels_orig],
)

plt.figure(figsize=(7, 6))
sns.heatmap(cm_df, annot=True, fmt="d", cmap="YlGnBu", cbar=False, linewidths=0.5)
plt.title("XGBoost Confusion Matrix (Original Labels)")
plt.ylabel("Actual")
plt.xlabel("Predicted")
plt.tight_layout()
plt.show()

# ------------------------------------------------
# Full classification report on original labels
# ------------------------------------------------
print("\nClassification report (original labels):\n")
print(classification_report(y_test_orig, y_pred_orig, digits=4))
# ------------------------------------------------
# Full classification report on original labels
# ------------------------------------------------
print("\nClassification report (original labels):\n")
print(classification_report(y_test_orig, y_pred_orig, digits=4))
# ------------------------------------------------
# Full classification report on original labels
# ------------------------------------------------
print("\nClassification report (original labels):\n")
print(classification_report(y_test_orig, y_pred_orig, digits=4))
