import numpy as np
import pandas as pd
import matplotlib.pyplot as plt
import seaborn as sns
import warnings

from sklearn.model_selection import train_test_split, GridSearchCV
from sklearn.metrics import accuracy_score, classification_report, confusion_matrix
from sklearn.preprocessing import StandardScaler, LabelEncoder

import xgboost as xgb

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
print("Cluster distribution:")
print(df["cluster"].value_counts() / float(len(df)))

# ------------------------------------------------
# Features and target
# ------------------------------------------------
# Drop columns that shouldn't be used for training
columns_to_drop = [
    "selfReportTest",  # Self-reported fatigue (separate measure)
    "cluster",  # Original cluster label
    "Test Name",  # Test type (categorical identifier)
    "NaiveIndex",  # Naive fatigue score (derived feature)
    "NaiveIndex_z",  # Normalized naive score (derived feature)
    "ClusterRank",  # Target variable
    "subject_id",  # Subject identifier
    "dataset_id",  # Dataset identifier
    "rank",  # Old rank column (if present)
    "FatigueIndex",  # Fatigue index from regression (if present)
    "FatigueIndex_z",  # Normalized fatigue index (if present)
    "FatigueBin",  # Fatigue bin/category (if present)
]

# Also drop any "Unnamed" columns
columns_to_drop.extend([col for col in df.columns if "Unnamed" in col])

# Filter to only drop columns that actually exist
columns_to_drop = [col for col in columns_to_drop if col in df.columns]

print("\nDropping columns:", columns_to_drop)

X = df.drop(columns_to_drop, axis=1)

print("\nFeatures used for training:")
print(X.columns.tolist())
print("Feature count:", len(X.columns))

# Encode labels so they start at 0
le = LabelEncoder()
y = le.fit_transform(df["ClusterRank"])  # Changed from "rank" to "ClusterRank"
print("\nClass mapping:", dict(zip(le.classes_, le.transform(le.classes_))))
print("Encoded class distribution:\n", pd.Series(y).value_counts())

# ------------------------------------------------
# Train-test split
# ------------------------------------------------
X_train, X_test, y_train, y_test = train_test_split(
    X, y, test_size=0.3, random_state=42, stratify=y
)
cols = X_train.columns

# Standardize features
scaler = StandardScaler()
X_train = scaler.fit_transform(X_train)
X_test = scaler.transform(X_test)
X_train = pd.DataFrame(X_train, columns=cols)
X_test = pd.DataFrame(X_test, columns=cols)

# ------------------------------------------------
# XGBoost model setup
# ------------------------------------------------
classes = np.unique(y_train)
n_classes = len(classes)
is_binary = n_classes == 2

if is_binary:
    objective = "binary:logistic"
    eval_metric = "logloss"
    pos_label = classes[1]
    n_pos = np.sum(y_train == pos_label)
    n_neg = len(y_train) - n_pos
    scale_pos_weight = (n_neg / n_pos) if n_pos > 0 else 1.0
else:
    objective = "multi:softprob"
    eval_metric = "mlogloss"
    scale_pos_weight = None

xgb_clf = xgb.XGBClassifier(
    objective=objective,
    eval_metric=eval_metric,
    n_estimators=400,
    tree_method="hist",
    random_state=42,
    num_class=n_classes if not is_binary else None,
    scale_pos_weight=scale_pos_weight if is_binary else None,
)

# ------------------------------------------------
# Hyperparameter grid
# ------------------------------------------------
param_grid = {
    "max_depth": [3, 5, 7],
    "min_child_weight": [1, 3, 5],
    "subsample": [0.8, 1.0],
    "colsample_bytree": [0.8, 1.0],
    "gamma": [0, 0.1, 0.2],
    "reg_alpha": [0, 0.001, 0.01],
    "reg_lambda": [1, 5, 10],
    "learning_rate": [0.05, 0.1],
    "n_estimators": [200, 400, 600],
}

# ------------------------------------------------
# Grid search
# ------------------------------------------------
grid_search = GridSearchCV(
    estimator=xgb_clf,
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
best_xgb = grid_search.best_estimator_
y_pred_enc = best_xgb.predict(X_test)
y_pred = le.inverse_transform(y_pred_enc)  # decode back to original labels
y_test_decoded = le.inverse_transform(y_test)

print("Test Accuracy: {:.4f}".format(accuracy_score(y_test_decoded, y_pred)))
print("\nClassification report:\n", classification_report(y_test_decoded, y_pred))

# ------------------------------------------------
# Confusion matrix
# ------------------------------------------------
labels = le.classes_  # original labels [1,2,3,4]
cm = confusion_matrix(y_test_decoded, y_pred, labels=labels)
cm_df = pd.DataFrame(
    cm, index=[f"Actual {c}" for c in labels], columns=[f"Pred {c}" for c in labels]
)

plt.figure(figsize=(6, 5))
sns.heatmap(cm_df, annot=True, fmt="d", cbar=False)
plt.title("XGBoost Confusion Matrix")
plt.ylabel("Actual")
plt.xlabel("Predicted")
plt.tight_layout()
plt.show()
