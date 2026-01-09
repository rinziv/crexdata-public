import numpy as np  # linear algebra
import pandas as pd  # data processing, CSV file I/O (e.g. pd.read_csv)
import matplotlib.pyplot as plt  # for data visualization
import seaborn as sns  # for statistical data visualization
import os
import warnings
from sklearn.manifold import TSNE
from sklearn.model_selection import train_test_split
from sklearn.svm import SVC
from sklearn.metrics import classification_report, confusion_matrix, accuracy_score
from sklearn.preprocessing import StandardScaler
from sklearn.metrics import roc_curve
from sklearn.model_selection import GridSearchCV
from sklearn.model_selection import RandomizedSearchCV
from sklearn.model_selection import ShuffleSplit

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
print(df["cluster"].value_counts() / float(len(df)))

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
    "ClusterRank",  # Old rank column (if present)
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
y = df["ClusterRank"]  # ✅ Changed from "rank" to "ClusterRank"

print("\nFeatures used for training:")
print(X.columns.tolist())
print("Feature count:", len(X.columns))
print("\nTarget distribution:")
print(y.value_counts())

X_train, X_test, y_train, y_test = train_test_split(
    X, y, test_size=0.3, random_state=42, stratify=y
)
cols = X_train.columns

scaler = StandardScaler()

X_train = scaler.fit_transform(X_train)

X_test = scaler.transform(X_test)
X_train = pd.DataFrame(X_train, columns=[cols])
X_test = pd.DataFrame(X_test, columns=[cols])


########################### UNCOMMENT FOR PLOTING DATA ###########################
# tsne = TSNE(n_components=2, random_state=42)
# X_embedded = tsne.fit_transform(X_train)

# plt.figure(figsize=(8, 6))
# sns.scatterplot(
#     x=X_embedded[:, 0], y=X_embedded[:, 1], hue=y_train, palette="tab10", s=60
# )
# plt.title("t-SNE Projection of Features")
# plt.show()
########################### UNCOMMENT FOR PLOTING DATA ###########################


svc = SVC()

# ------------------------------------------------
# OPTION 1: Reduce Grid Size (Fastest - ~10x speedup)
# ------------------------------------------------
parameters = [
    {
        "C": [10, 1000],  # Focus on extremes
        "kernel": ["rbf"],
        "gamma": [0.1, 0.5, 0.9],  # Low, mid, high
    },
]

grid_search = GridSearchCV(
    estimator=svc,
    param_grid=parameters,
    scoring="accuracy",
    cv=3,  # 3-fold instead of 5
    n_jobs=-1,
    verbose=2,
)

# ------------------------------------------------
# OPTION 2: Use RandomizedSearchCV (Good balance)
# ------------------------------------------------

param_distributions = {
    "C": [1, 10, 100, 1000],
    "kernel": ["rbf"],
    "gamma": [0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9],
}

random_search = RandomizedSearchCV(
    estimator=svc,
    param_distributions=param_distributions,
    n_iter=20,  # Test only 20 random combinations (instead of all 36)
    scoring="accuracy",
    cv=5,
    n_jobs=-1,
    verbose=2,
    random_state=42,
)

random_search.fit(X_train, y_train)

print("RandomizedSearch CV best score : {:.4f}\n".format(random_search.best_score_))
print("Best params:\n", random_search.best_params_)

# ------------------------------------------------
# OPTION 3: Reduce CV Folds
# ------------------------------------------------
grid_search = GridSearchCV(
    estimator=svc,
    param_grid=parameters,
    scoring="accuracy",
    cv=3,  # ✅ Changed from 5 to 3 folds (40% faster)
    n_jobs=-1,
    verbose=2,
)

# ------------------------------------------------
# OPTION 4: Sample Your Data (if dataset is very large)
# ------------------------------------------------
# Use only 50% of training data for grid search
cv_splitter = ShuffleSplit(n_splits=3, test_size=0.5, random_state=42)

grid_search = GridSearchCV(
    estimator=svc,
    param_grid=parameters,
    scoring="accuracy",
    cv=cv_splitter,  # Custom CV with 50% samples
    n_jobs=-1,
    verbose=2,
)

grid_search.fit(X_train, y_train)


print(
    "Parameters that give the best results :", "\n\n", (grid_search.best_params_)
)  # print parameters that give the best resultsprint("GridSearch CV best score : {:.4f}\n\n".format(grid_search.best_score_))# best score achieved during the GridSearchCV
# print estimator that was chosen by the GridSearch
print(
    "\n\nEstimator that was chosen by the search :",
    "\n\n",
    (grid_search.best_estimator_),
)


# C = [1.0, 10, 100, 1000]
# # kernel = ["linear", "rbf", "poly"]
# kernel = ["rbf"]

# for k in kernel:
#     for c in C:
#         svc = SVC(
#             kernel=k,
#             C=c,
#             random_state=42,
#             class_weight="balanced",
#             decision_function_shape="ovo",
#         )
#         svc.fit(X_train, y_train)

#         y_pred = svc.predict(X_test)

#         print(
#             "Model accuracy score with default hyperparameters: {0:0.4f}".format(
#                 accuracy_score(y_test, y_pred)
#             )
#         )

#         cm = confusion_matrix(y_test, y_pred)

#         cm_matrix = pd.DataFrame(data=cm)
#         plt.figure(figsize=(10, 7))
#         plt.title("Confusion Matrix")
#         sns.heatmap(cm_matrix, annot=True, fmt="d", cmap="YlGnBu")
#         plt.show()
#         plt.show()
