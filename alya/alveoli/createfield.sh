#!/bin/bash

#SBATCH --job-name=field
#SBATCH -t 2:00:00
#SBATCH --account=bsc21
#SBATCH --qos=gp_debug
#SBATCH -n 1
#SBATCH --error=field-%J.err
#SBATCH --output=field_%J.out

ulimit -Ss unlimited

module load ucx
module load oneapi/2023.2.0
module load intel/2023.2.0
module load impi/2021.10.0
module load mkl/2023.2.0
module load hdf5
module load python
module load ALYA-MPIO-TOOLS

date
python /gpfs/scratch/bsc21/bsc021315/TOOLS/fields/rename.py fensap
date
