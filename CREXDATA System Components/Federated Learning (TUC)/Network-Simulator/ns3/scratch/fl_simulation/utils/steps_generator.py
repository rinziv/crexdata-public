import argparse
import csv
import random
import numpy as np

MIN_STEPS = 100
MAX_STEPS = 500
CLIENTS_PER_ROUND = 3
NUM_CLIENTS = 5
MODES = ["uniform", "gaussian", "linear"]


def sample_step(mode, min_value, max_value, round_no, num_rounds):
    if mode == "uniform":
        return np.random.randint(min_value, max_value)

    elif mode == "gaussian":
        mean = (min_value + max_value) / 2
        stddev = (max_value - min_value) / 6  # 99.7% within range
        value = int(np.random.normal(loc=mean, scale=stddev))
        return max(min_value, min(value, max_value))

    elif mode == "linear":
        progress_ratio = (round_no - 1) / max(1, num_rounds - 1)
        linear_value = min_value + progress_ratio * (max_value - min_value)
        noise = np.random.uniform(-10, 10)
        value = int(linear_value + noise)
        return max(min_value, min(value, max_value))

    else:
        raise ValueError(f"Unknown generation mode: {mode}")


def generate_step_data(
    num_rounds,
    num_clients,
    clients_per_round,
    min_value,
    max_value,
    mode,
):
    assert (
        0 < clients_per_round <= num_clients
    ), "clients_per_round must be > 0 and <= num_clients"
    assert mode in MODES, f"Invalid mode '{mode}'. Choose from {MODES}"

    data = []
    for round_no in range(1, num_rounds + 1):
        row = {"roundNo": round_no}
        active_clients = random.sample(range(num_clients), clients_per_round)

        shared_value = sample_step(mode, min_value, max_value, round_no, num_rounds)
        for c in range(num_clients):
            if c in active_clients:
                row[f"client_{c}"] = shared_value
            else:
                row[f"client_{c}"] = ""
        data.append(row)

    return data


def write_csv(data, output_path):
    if not data:
        return

    fieldnames = list(data[0].keys())
    with open(output_path, "w", newline="") as csvfile:
        writer = csv.DictWriter(csvfile, fieldnames=fieldnames)
        writer.writeheader()
        for row in data:
            writer.writerow(row)


def generate_and_save_steps(
    num_rounds,
    num_clients,
    clients_per_round,
    min_value,
    max_value,
    output_path,
    mode="linear",
):
    data = generate_step_data(
        num_rounds,
        num_clients,
        clients_per_round,
        min_value,
        max_value,
        mode,
    )
    write_csv(data, output_path)
    return data


def main():
    parser = argparse.ArgumentParser(description="Generate client step CSV file.")
    parser.add_argument("--rounds", type=int, default=30, help="Number of rounds")
    parser.add_argument(
        "--clients", type=int, default=NUM_CLIENTS, help="Total number of clients"
    )
    parser.add_argument(
        "--clients_per_round",
        type=int,
        default=CLIENTS_PER_ROUND,
        help="Clients per round",
    )
    parser.add_argument("--min", type=int, default=MIN_STEPS, help="Minimum step value")
    parser.add_argument("--max", type=int, default=MAX_STEPS, help="Maximum step value")
    parser.add_argument(
        "--mode",
        type=str,
        choices=MODES,
        default="linear",
        help="Step generation mode",
    )
    parser.add_argument(
        "--output_path", type=str, default="../steps.csv", help="Output CSV file"
    )

    args = parser.parse_args()

    _ = generate_and_save_steps(
        num_rounds=args.rounds,
        num_clients=args.clients,
        clients_per_round=args.clients_per_round,
        min_value=args.min,
        max_value=args.max,
        output_path=args.output_path,
        mode=args.mode,
    )


if __name__ == "__main__":
    main()
