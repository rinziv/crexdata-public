#!/usr/bin/env python3
"""
Visualize the time-based weight progression for fake data labels
Run this to see how probabilities shift from label 1 (normal) to label 4 (fatigue)
"""

import numpy as np
import matplotlib.pyplot as plt

# Configuration (must match eyeRealTimeSlidingWindow.py)
MINUTES_FOR_FULL_FATIGUE = 30

# Time range (0 to MINUTES_FOR_FULL_FATIGUE + 5 minutes)
time_minutes = np.linspace(0, MINUTES_FOR_FULL_FATIGUE + 5, 200)

# Calculate weights for each time point
label1_weights = []
label2_weights = []
label3_weights = []
label4_weights = []

for elapsed_minutes in time_minutes:
    # Calculate fatigue progression (0.0 at start -> 1.0 at MINUTES_FOR_FULL_FATIGUE)
    fatigue_progress = min(elapsed_minutes / MINUTES_FOR_FULL_FATIGUE, 1.0)

    # Same formula as in eyeRealTimeSlidingWindow.py
    weights = [
        0.70 - (0.68 * fatigue_progress),  # Label 1: 70% -> 2%
        0.20 + (0.05 * fatigue_progress) - (0.17 * (fatigue_progress**2)),  # Label 2
        0.08 + (0.27 * fatigue_progress) - (0.15 * (fatigue_progress**2)),  # Label 3
        0.02 + (0.68 * fatigue_progress),  # Label 4: 2% -> 70%
    ]

    # Normalize
    total = sum(weights)
    weights = [w / total for w in weights]

    label1_weights.append(weights[0])
    label2_weights.append(weights[1])
    label3_weights.append(weights[2])
    label4_weights.append(weights[3])

# Create visualization
plt.figure(figsize=(12, 7))

# Plot each label's probability over time
plt.plot(
    time_minutes,
    label1_weights,
    "g-",
    linewidth=2.5,
    label="Label 1 (Normal)",
    marker="o",
    markevery=20,
)
plt.plot(
    time_minutes,
    label2_weights,
    "b-",
    linewidth=2.5,
    label="Label 2 (Mild)",
    marker="s",
    markevery=20,
)
plt.plot(
    time_minutes,
    label3_weights,
    "orange",
    linewidth=2.5,
    label="Label 3 (Moderate)",
    marker="^",
    markevery=20,
)
plt.plot(
    time_minutes,
    label4_weights,
    "r-",
    linewidth=2.5,
    label="Label 4 (Fatigue)",
    marker="d",
    markevery=20,
)

# Add vertical line at target time
plt.axvline(
    x=MINUTES_FOR_FULL_FATIGUE,
    color="gray",
    linestyle="--",
    linewidth=2,
    alpha=0.7,
    label=f"Target: {MINUTES_FOR_FULL_FATIGUE} min",
)

# Formatting
plt.xlabel("Time (minutes)", fontsize=12, fontweight="bold")
plt.ylabel("Probability", fontsize=12, fontweight="bold")
plt.title(
    f"Fake Data Label Probability Progression Over Time\n(Target: {MINUTES_FOR_FULL_FATIGUE} minutes)",
    fontsize=14,
    fontweight="bold",
)
plt.legend(loc="center left", fontsize=11, framealpha=0.95)
plt.grid(True, alpha=0.3, linestyle="--")
plt.xlim(0, max(time_minutes))
plt.ylim(0, 0.8)

# Add annotations at key points
key_times = [0, MINUTES_FOR_FULL_FATIGUE / 2, MINUTES_FOR_FULL_FATIGUE]
for t in key_times:
    idx = np.argmin(np.abs(time_minutes - t))
    plt.axvline(x=t, color="lightgray", linestyle=":", alpha=0.5)

    # Add text box showing probabilities at this time
    text = f"t={t:.0f}min\n"
    text += f"L1: {label1_weights[idx]:.0%}\n"
    text += f"L2: {label2_weights[idx]:.0%}\n"
    text += f"L3: {label3_weights[idx]:.0%}\n"
    text += f"L4: {label4_weights[idx]:.0%}"

    plt.text(
        t,
        0.73,
        text,
        fontsize=9,
        ha="center",
        va="top",
        bbox=dict(boxstyle="round,pad=0.5", facecolor="lightyellow", alpha=0.8),
    )

plt.tight_layout()
plt.savefig("fake_data_weight_progression.png", dpi=150, bbox_inches="tight")
print("✅ Visualization saved as: fake_data_weight_progression.png")
plt.show()

# Print sample values
print("\n📊 Sample Weight Values:")
print("=" * 70)
sample_times = [0, 5, 10, 15, 20, 25, 30]
for t in sample_times:
    if t > max(time_minutes):
        continue
    idx = np.argmin(np.abs(time_minutes - t))
    progress = min(t / MINUTES_FOR_FULL_FATIGUE, 1.0) * 100
    print(
        f"Time: {t:3.0f} min (Progress: {progress:3.0f}%) | "
        f"L1: {label1_weights[idx]:.2f}, L2: {label2_weights[idx]:.2f}, "
        f"L3: {label3_weights[idx]:.2f}, L4: {label4_weights[idx]:.2f}"
    )
