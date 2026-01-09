import numpy as np  # linear algebra
import pandas as pd  # data processing, CSV file I/O (e.g. pd.read_csv)
import matplotlib.pyplot as plt  # for data visualization
import seaborn as sns  # for statistical data visualization
import os
import warnings
from sklearn.manifold import TSNE
from sklearn.model_selection import train_test_split
from sklearn.svm import SVC
from sklearn.metrics import (
    classification_report,
    confusion_matrix,
    accuracy_score,
    f1_score,
    precision_score,
    recall_score,
    roc_auc_score,
)
from sklearn.preprocessing import StandardScaler, LabelEncoder  # Add LabelEncoder
from sklearn.metrics import roc_curve
from sklearn.model_selection import GridSearchCV
from sklearn.dummy import DummyClassifier
import joblib
import json  # Add this import

warnings.filterwarnings("ignore")

# ------------------------------------------------
# Load data
# ------------------------------------------------
path = "C:/Users/AlexisPC/Desktop/allCSV/CLUSTERED/READY_FOR_TRINING.csv"  # ✅ Changed path
df = pd.read_csv(path)
df.columns = df.columns.str.strip()

print("Cluster distribution:")
if "cluster" in df.columns:
    print(df["cluster"].value_counts() / float(len(df)))

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

print("\nClass mapping (original_rank -> encoded):")
print(dict(zip(le.classes_, le.transform(le.classes_))))
print("Encoded class counts:\n", pd.Series(y).value_counts().sort_index())

print("\nFeatures used for training:")
print(X.columns.tolist())
print("Feature count:", len(X.columns))

X_train, X_test, y_train, y_test = train_test_split(
    X, y, test_size=0.3, random_state=42, stratify=y
)
cols = X_train.columns

scaler = StandardScaler()

X_train = scaler.fit_transform(X_train)
X_test = scaler.transform(X_test)
X_train = pd.DataFrame(X_train, columns=[cols])
X_test = pd.DataFrame(X_test, columns=[cols])

# ------------------------------------------------
# BASELINE MODEL (majority class predictor)
# ------------------------------------------------
dummy = DummyClassifier(strategy="most_frequent", random_state=42)
dummy.fit(X_train, y_train)
y_dummy_pred = dummy.predict(X_test)

print("\nBaseline (majority class) results:")
print("Baseline accuracy: {:.4f}".format(accuracy_score(y_test, y_dummy_pred)))
# print(classification_report(y_test, y_dummy_pred))


########################### UNCOMMENT FOR PLOTING DATA ###########################
tsne = TSNE(n_components=2, random_state=42)
X_embedded = tsne.fit_transform(X_train)

# Decode y_train to original labels for legend
y_train_orig = le.inverse_transform(y_train)

plt.figure(figsize=(8, 6))
sns.scatterplot(
    x=X_embedded[:, 0],
    y=X_embedded[:, 1],
    hue=y_train_orig,  # ✅ Use original labels (1,2,3,4) instead of encoded (0,1,2,3)
    palette="tab10",
    s=60,
)
# plt.title("t-SNE Projection of Features")
plt.xlabel("t-SNE Component 1")
plt.ylabel("t-SNE Component 2")
plt.legend(title="Fatigue Level", loc="best")
plt.tight_layout()

# Save plot instead of showing (to avoid blocking)
plt.savefig(
    os.path.join(
        "C:/Users/AlexisPC/Desktop/saved_models/svm", "tsne_visualization.png"
    ),
    dpi=150,
    bbox_inches="tight",
)
plt.close()
print("✅ t-SNE plot saved")
########################### UNCOMMENT FOR PLOTING DATA ###########################

# ------------------------------------------------
# SVM MODEL
# ------------------------------------------------
# Best hyperparameters from GridSearch
best_params = {"C": 1000, "kernel": "rbf", "gamma": 0.9}

svc = SVC(random_state=42, probability=True, **best_params)
svc.fit(X_train, y_train)
y_pred_enc = svc.predict(X_test)
y_prob = svc.predict_proba(X_test)

# Decode predictions back to original labels
y_pred_orig = le.inverse_transform(y_pred_enc)
y_test_orig = le.inverse_transform(y_test)

print("\nSVM model results:")
print("SVM accuracy: {:.4f}".format(accuracy_score(y_test, y_pred_enc)))
print(
    "SVM PRECISION score: {:.4f}".format(
        precision_score(y_test, y_pred_enc, average="weighted")
    )
)
print(
    "SVM RECALL score: {:.4f}".format(
        recall_score(y_test, y_pred_enc, average="weighted")
    )
)
print("SVM F1 score: {:.4f}".format(f1_score(y_test, y_pred_enc, average="weighted")))
auc = roc_auc_score(y_test, y_prob, multi_class="ovr", average="weighted")
print("SVM AUC (weighted): {:.4f}".format(auc))

# ------------------------------------------------
# Save model and all artifacts
# ------------------------------------------------
MODEL_DIR = "C:/Users/AlexisPC/Desktop/saved_models/svm"
os.makedirs(MODEL_DIR, exist_ok=True)

print("\n" + "=" * 60)
print("SAVING MODEL AND ARTIFACTS")
print("=" * 60)

# 1. Save the trained SVM model
model_path = os.path.join(MODEL_DIR, "svm_fatigue_model.pkl")
joblib.dump(svc, model_path)
print(f"✅ Model saved: {model_path}")

# 2. Save the StandardScaler
scaler_path = os.path.join(MODEL_DIR, "svm_scaler.pkl")
joblib.dump(scaler, scaler_path)
print(f"✅ Scaler saved: {scaler_path}")

# 3. Save the LabelEncoder
encoder_path = os.path.join(MODEL_DIR, "svm_label_encoder.pkl")
joblib.dump(le, encoder_path)
print(f"✅ Label encoder saved: {encoder_path}")

# 4. Save feature names
features_path = os.path.join(MODEL_DIR, "svm_features.pkl")
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
drop_cols_path = os.path.join(MODEL_DIR, "svm_drop_columns.pkl")
joblib.dump(columns_to_drop_inference, drop_cols_path)
print(f"✅ Drop columns list saved: {drop_cols_path}")

# 6. Save training metadata
metadata = {
    "n_classes": len(le.classes_),
    "class_labels": le.classes_.tolist(),
    "n_features": len(X.columns),
    "feature_names": X.columns.tolist(),
    "test_accuracy": accuracy_score(y_test, y_pred_enc),
    "test_f1_weighted": f1_score(y_test, y_pred_enc, average="weighted"),
    "test_auc_weighted": auc,  # ✅ Added AUC to metadata
    "hyperparameters": best_params,
}
metadata_path = os.path.join(MODEL_DIR, "svm_metadata.json")
with open(metadata_path, "w") as f:
    json.dump(metadata, f, indent=2)
print(f"✅ Training metadata saved: {metadata_path}")

print("=" * 60)
print("ALL ARTIFACTS SAVED SUCCESSFULLY")
print("=" * 60 + "\n")

# ------------------------------------------------
# Comparison
# ------------------------------------------------
print("\nComparison of accuracies:")
print("Baseline: {:.4f}".format(accuracy_score(y_test, y_dummy_pred)))
print("SVM:      {:.4f}".format(accuracy_score(y_test, y_pred_enc)))

# ------------------------------------------------
# Confusion Matrix (on original labels)
# ------------------------------------------------
labels_orig = le.classes_  # original label values (e.g., [1,2,3,4])
cm = confusion_matrix(y_test_orig, y_pred_orig, labels=labels_orig)

cm_matrix = pd.DataFrame(
    data=cm,
    columns=[f"Predicted {c}" for c in labels_orig],
    index=[f"Actual {c}" for c in labels_orig],
)

plt.figure(figsize=(6, 5))
sns.heatmap(cm_matrix, annot=True, fmt="d", cmap="YlGnBu", cbar=False)
plt.title("SVM Confusion Matrix (Original Labels)")
plt.ylabel("Actual")
plt.xlabel("Predicted")
plt.tight_layout()
plt.savefig(
    os.path.join(MODEL_DIR, "confusion_matrix.png"), dpi=150, bbox_inches="tight"
)
plt.close()  # Close instead of show
print("✅ Confusion matrix saved")

# ------------------------------------------------
# Full classification report on original labels
# ------------------------------------------------
print("\nClassification report (original labels):\n")
print(classification_report(y_test_orig, y_pred_orig, digits=4))
