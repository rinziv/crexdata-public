import numpy as np
import pandas as pd
import matplotlib.pyplot as plt
import seaborn as sns
import warnings

from sklearn.model_selection import train_test_split, GridSearchCV
from sklearn.metrics import accuracy_score, classification_report, confusion_matrix
from sklearn.preprocessing import StandardScaler, LabelEncoder
from sklearn.neighbors import KNeighborsClassifier

warnings.filterwarnings("ignore")

# ------------------------------------------------
# Load dataset
# ------------------------------------------------
path = "C:/Users/AlexisPC/Desktop/allCSV/CLUSTERED/READY_FOR_TRINING.csv"
df = pd.read_csv(path)
df.columns = df.columns.str.strip()

# Print columns to see what we have
print("Available columns:")
print(df.columns.tolist())
print("\nDataset shape:", df.shape)

# Drop columns that shouldn't be used for training
columns_to_drop = [
    "selfReportTest",  # Self-reported fatigue (separate measure)
    "cluster",  # Original cluster label
    "Test Name",  # Test type (categorical identifier)
    "NaiveIndex",  # Naive fatigue score (derived feature)
    "NaiveIndex_z",  # Normalized naive score (derived feature)
    "ClusterRank",  # Target variable ✅
    "subject_id",  # Subject identifier
    "dataset_id",  # Dataset identifier
    "rank",  # Old rank column (if present)
    "FatigueIndex",  # Fatigue index from regression (if present)
    "FatigueIndex_z",  # Normalized fatigue index (if present)
    "FatigueBin",  # Fatigue bin/category (if present)
]

# Also drop any "Unnamed" columns and other non-feature columns
columns_to_drop.extend([col for col in df.columns if "Unnamed" in col])

# Filter to only drop columns that actually exist
columns_to_drop = [col for col in columns_to_drop if col in df.columns]

print("\nDropping columns:", columns_to_drop)

X = df.drop(columns_to_drop, axis=1)
print("\nFeatures used for training:")
print(X.columns.tolist())
print("Feature count:", len(X.columns))

# Encode labels
if "ClusterRank" not in df.columns:  # ✅ Changed from "rank" to "ClusterRank"
    raise ValueError(
        "Column 'ClusterRank' not found in the CSV. Please ensure it exists."
    )

le = LabelEncoder()
y = le.fit_transform(df["ClusterRank"])  # ✅ Changed from "rank" to "ClusterRank"
print("Class mapping:", dict(zip(le.classes_, le.transform(le.classes_))))

# Train-test split
X_train, X_test, y_train, y_test = train_test_split(
    X, y, test_size=0.3, random_state=42, stratify=y
)

# Scale features (KNN is distance-based → scaling is essential)
scaler = StandardScaler()
X_train = scaler.fit_transform(X_train)
X_test = scaler.transform(X_test)

# ------------------------------------------------
# KNN model + Grid Search
# ------------------------------------------------
knn = KNeighborsClassifier()

param_grid = {
    "n_neighbors": [3, 5, 7, 9, 11, 15, 21],
    "weights": ["uniform", "distance"],
    "metric": ["euclidean", "manhattan", "minkowski"],
    "p": [1, 2],  # only relevant for minkowski
}

grid_search = GridSearchCV(
    estimator=knn,
    param_grid=param_grid,
    scoring="accuracy",
    cv=5,
    n_jobs=-1,
    verbose=2,
)

grid_search.fit(X_train, y_train)

print("GridSearch CV best score : {:.4f}\n".format(grid_search.best_score_))
print("Best params:\n", grid_search.best_params_, "\n")
print("Best estimator:\n", grid_search.best_estimator_, "\n")

# ------------------------------------------------
# Evaluate on test set
# ------------------------------------------------
best_knn = grid_search.best_estimator_
y_pred_enc = best_knn.predict(X_test)
y_pred = le.inverse_transform(y_pred_enc)
y_test_decoded = le.inverse_transform(y_test)

print("Test Accuracy: {:.4f}".format(accuracy_score(y_test_decoded, y_pred)))
print("\nClassification report:\n", classification_report(y_test_decoded, y_pred))

# ------------------------------------------------
# Confusion matrix
# ------------------------------------------------
labels = le.classes_
cm = confusion_matrix(y_test_decoded, y_pred, labels=labels)
cm_df = pd.DataFrame(
    cm, index=[f"Actual {c}" for c in labels], columns=[f"Pred {c}" for c in labels]
)

plt.figure(figsize=(6, 5))
sns.heatmap(cm_df, annot=True, fmt="d", cbar=False)
plt.title("KNN Confusion Matrix")
plt.ylabel("Actual")
plt.xlabel("Predicted")
plt.tight_layout()
plt.show()
