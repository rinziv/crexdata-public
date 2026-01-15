#!/bin/bash

#SBATCH --job-name=crexdata2
#SBATCH --output=alya.out
#SBATCH --error=alya.err
#SBATCH --ntasks=300
#SBATCH --time=2:00:00
#SBATCH --qos=gp_debug
#SBATCH --account=bsc08

module purge
module load cmake
module load oneapi
module load fftw/3.3.10
module load dlb
module load ucx

time srun ../alya fensap
