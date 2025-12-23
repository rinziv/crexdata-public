import random
import argparse
import logging

# Setup logging
logging.basicConfig(
    level=logging.INFO, format="%(asctime)s [%(levelname)s] %(message)s"
)

# Unified default values for parser args
NUM_NODES = 21
MIN_X = 40
MIN_Y = 40
MAX_X = 80
MAX_Y = 80
OUTPUT_PTH = "../conf/coordinates.txt"


def generate_coordinates(num_nodes, minX, maxX, minY, maxY, output_path):
    """Generate a file with random (x, y) coordinates in the specified range."""
    try:
        with open(output_path, "w") as file:
            for _ in range(num_nodes):
                x = random.uniform(minX, maxX)
                y = random.uniform(minY, maxY)
                file.write(f"{x:.2f}\t{y:.2f}\n")
        logging.info(
            f"[✓] Generated file '{output_path}' with {num_nodes} coordinates."
        )
    except Exception as e:
        logging.error(f"Failed to generate coordinates: {e}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Generate a coordinates file.")
    parser.add_argument(
        "--num_nodes",
        type=int,
        default=NUM_NODES,
        help="Number of coordinates to generate.",
    )
    parser.add_argument("--minX", type=float, default=MIN_X, help="Minimum X value.")
    parser.add_argument("--minY", type=float, default=MIN_Y, help="Minimum Y value.")
    parser.add_argument("--maxX", type=float, default=MAX_X, help="Maximum X value.")
    parser.add_argument("--maxY", type=float, default=MAX_Y, help="Maximum Y value.")
    parser.add_argument(
        "--output_path",
        type=str,
        default=OUTPUT_PTH,
        help="Path to output the coordinates file.",
    )

    args = parser.parse_args()
    generate_coordinates(**vars(args))
