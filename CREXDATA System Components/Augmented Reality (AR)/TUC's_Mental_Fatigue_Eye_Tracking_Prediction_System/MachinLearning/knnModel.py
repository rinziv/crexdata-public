# -*- coding: utf-8 -*-
import os
import warnings
import json  # Add this import

import numpy as np
import pandas as pd
import matplotlib.pyplot as plt
import seaborn as sns
from sklearn.metrics import roc_curve, auc

from sklearn.model_selection import train_test_split
from sklearn.preprocessing import StandardScaler, LabelEncoder, label_binarize
from sklearn.metrics import (
    accuracy_score,
    precision_score,
    recall_score,
    f1_score,
    classification_report,
    confusion_matrix,
    roc_auc_score,
)
from sklearn.neighbors import KNeighborsClassifier
import joblib

save_dir = "C:/Users/AlexisPC/Desktop/saved_models/knn"
os.makedirs(save_dir, exist_ok=True)

warnings.filterwarnings("ignore")

# ------------------------------------------------
# 1) Load data
# ------------------------------------------------
path = "C:/Users/AlexisPC/Desktop/allCSV/CLUSTERED/READY_FOR_TRINING.csv"  # ✅ Changed path
df = pd.read_csv(path)
df.columns = df.columns.str.strip()

print("Cluster distribution:")
if "cluster" in df.columns:
    print(df["cluster"].value_counts() / float(len(df)))

# ------------------------------------------------
# 2) Features / target
# ------------------------------------------------
# Drop columns that shouldn't be used for training
columns_to_drop = [
    "selfReportTest",
    "cluster",
    "Test Name",
    "NaiveIndex",
    "NaiveIndex_z",
    "ClusterRank",  # Target variable ✅
    "subject_id",
    "dataset_id",
    "rank",
    "FatigueIndex",
    "FatigueIndex_z",
    "FatigueBin",
]

# Also drop any "Unnamed" columns
columns_to_drop.extend([col for col in df.columns if "Unnamed" in col])
columns_to_drop = [col for col in columns_to_drop if col in df.columns]

print("\nDropping columns:", columns_to_drop)

X = df.drop(columns_to_drop, axis=1)

# Encode target labels
if "ClusterRank" not in df.columns:  # ✅ Changed from "rank" to "ClusterRank"
    raise ValueError(
        "Column 'ClusterRank' not found in the CSV. Please ensure it exists."
    )

le = LabelEncoder()
y = le.fit_transform(df["ClusterRank"])  # ✅ Changed from "rank" to "ClusterRank"
print(
    "Class mapping (original -> encoded):",
    dict(zip(le.classes_, le.transform(le.classes_))),
)
print("Encoded class counts:\n", pd.Series(y).value_counts().sort_index())

print("\nFeatures used for training:")
print(X.columns.tolist())
print("Feature count:", len(X.columns))

# ------------------------------------------------
# 3) Train/test split + scaling
# ------------------------------------------------
X_train, X_test, y_train, y_test = train_test_split(
    X, y, test_size=0.30, random_state=42, stratify=y
)

scaler = StandardScaler()
X_train = scaler.fit_transform(X_train)
X_test = scaler.transform(X_test)

# ------------------------------------------------
# 4) Train KNN
# ------------------------------------------------
# Best hyperparameters from GridSearch
best_params = {
    "metric": "manhattan",
    "n_neighbors": 3,
    "p": 1,
    "weights": "distance",
}

knn = KNeighborsClassifier(**best_params)
knn.fit(X_train, y_train)
print("KNN n_features_in_:", knn.n_features_in_)
# Predictions and probabilities
y_pred_enc = knn.predict(X_test)
y_prob = knn.predict_proba(X_test)

# ------------------------------------------------
# 5) Metrics
# ------------------------------------------------
acc = accuracy_score(y_test, y_pred_enc)
prec = precision_score(y_test, y_pred_enc, average="weighted", zero_division=0)
rec = recall_score(y_test, y_pred_enc, average="weighted", zero_division=0)
f1 = f1_score(y_test, y_pred_enc, average="weighted", zero_division=0)

print("\nKNN results:")
print(f"Accuracy: {acc:.4f}")
print(f"Precision (weighted): {prec:.4f}")
print(f"Recall    (weighted): {rec:.4f}")
print(f"F1        (weighted): {f1:.4f}")

# AUC (macro & weighted, multiclass)
n_classes = len(np.unique(y_train))
y_test_bin = label_binarize(y_test, classes=np.arange(n_classes))
auc_w = roc_auc_score(y_test_bin, y_prob, average="weighted", multi_class="ovr")
auc_m = roc_auc_score(y_test_bin, y_prob, average="macro", multi_class="ovr")
print(f"AUC (weighted): {auc_w:.4f}")
print(f"AUC (macro):    {auc_m:.4f}")

# ------------------------------------------------
# 6) Reports with original labels
# ------------------------------------------------
y_test_dec = le.inverse_transform(y_test)
y_pred_dec = le.inverse_transform(y_pred_enc)

print("\nClassification report (original labels):")
print(classification_report(y_test_dec, y_pred_dec))

cm = confusion_matrix(y_test_dec, y_pred_dec, labels=le.classes_)
cm_df = pd.DataFrame(
    cm,
    index=[f"Actual {c}" for c in le.classes_],
    columns=[f"Pred {c}" for c in le.classes_],
)

plt.figure(figsize=(6, 5))
sns.heatmap(cm_df, annot=True, fmt="d")
plt.title("KNN Confusion Matrix")
plt.ylabel("Actual")
plt.xlabel("Predicted")
plt.tight_layout()
plt.savefig(
    os.path.join(save_dir, "confusion_matrix.png"), dpi=150, bbox_inches="tight"
)
plt.close()  # Close instead of show
print("✅ Confusion matrix saved")

# ------------------------------------------------
# 7) ROC AUC CURVE
# ------------------------------------------------
y_test_bin = label_binarize(y_test, classes=np.arange(n_classes))

fpr_micro, tpr_micro, _ = roc_curve(y_test_bin.ravel(), y_prob.ravel())
auc_micro = auc(fpr_micro, tpr_micro)

plt.figure(figsize=(7, 5))
plt.plot(fpr_micro, tpr_micro, lw=2, label=f"ROC curve (AUC = {auc_micro:.3f})")
plt.plot([0, 1], [0, 1], "k--", lw=1)

plt.title("KNN Overall ROC Curve")
plt.xlabel("False Positive Rate")
plt.ylabel("True Positive Rate")
plt.legend(loc="lower right")
plt.tight_layout()
plt.savefig(os.path.join(save_dir, "roc_curve.png"), dpi=150, bbox_inches="tight")
plt.close()  # Close instead of show
print("✅ ROC curve saved")

# ------------------------------------------------
# 7) Save model & all artifacts
# ------------------------------------------------
print("\n" + "=" * 60)
print("SAVING MODEL AND ARTIFACTS")
print("=" * 60)

# 1. Save the trained KNN model
model_path = os.path.join(save_dir, "knn_fatigue_model.pkl")
joblib.dump(knn, model_path)
print(f"✅ Model saved: {model_path}")

# 2. Save the StandardScaler
scaler_path = os.path.join(save_dir, "knn_scaler.pkl")
joblib.dump(scaler, scaler_path)
print(f"✅ Scaler saved: {scaler_path}")

# 3. Save the LabelEncoder
encoder_path = os.path.join(save_dir, "knn_label_encoder.pkl")
joblib.dump(le, encoder_path)
print(f"✅ Label encoder saved: {encoder_path}")

# 4. Save feature names
features_path = os.path.join(save_dir, "knn_features.pkl")
joblib.dump(X.columns.tolist(), features_path)
print(f"✅ Feature names saved: {features_path}")

# 5. Save columns to drop for inference
columns_to_drop_inference = [
    "selfReportTest",
    "cluster",
    "Test Name",
    "NaiveIndex",
    "NaiveIndex_z",
    "subject_id",
    "dataset_id",
    "rank",
    "FatigueIndex",
    "FatigueIndex_z",
    "FatigueBin",
]
drop_cols_path = os.path.join(save_dir, "knn_drop_columns.pkl")
joblib.dump(columns_to_drop_inference, drop_cols_path)
print(f"✅ Drop columns list saved: {drop_cols_path}")

# 6. Save training metadata
metadata = {
    "n_classes": n_classes,
    "class_labels": le.classes_.tolist(),
    "n_features": len(X.columns),
    "feature_names": X.columns.tolist(),
    "test_accuracy": acc,
    "test_f1_weighted": f1,
    "test_auc_weighted": auc_w,  # ✅ Added AUC
    "test_auc_macro": auc_m,  # ✅ Added AUC macro
    "hyperparameters": best_params,  # ✅ Use the same dict
}
metadata_path = os.path.join(save_dir, "knn_metadata.json")
with open(metadata_path, "w") as f:
    json.dump(metadata, f, indent=2)
print(f"✅ Training metadata saved: {metadata_path}")

print("=" * 60)
print("ALL ARTIFACTS SAVED SUCCESSFULLY")
print("=" * 60 + "\n")
