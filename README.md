# 3D Scaffold Nutrient Diffusion Simulation

## Overview

This project is a 3D computational simulation of a porous biological scaffold designed to study how scaffold structure influences nutrient diffusion over time. It models nutrient transport through complex pore networks using a voxel-based representation and visualizes both spatial and temporal concentration changes.

The simulation was developed in Unity (URP) and is intended as an educational and exploratory tool for understanding diffusion processes relevant to tissue engineering and cell viability.

## Motivation

In tissue engineering, scaffold geometry plays a critical role in determining whether nutrients can adequately reach cells embedded deep within the structure. Physical experiments can be costly and time-consuming, so computational models offer a powerful alternative for visualizing diffusion in 3D, testing how porosity and connectivity affect nutrient availability, and generating quantitative data for analysis.

## Features

- Procedural 3D scaffold generation using a voxel-based grid
- Adjustable porosity and structural parameters
- Time-stepped nutrient diffusion through pore regions
- Real-time visualization using color gradients
- Exportable histogram and time-series data in CSV format

## How the Simulation Works

1. A 3D voxel grid represents the scaffold volume.
2. Voxels are classified as solid scaffold or pore space.
3. Nutrient concentration values are updated over discrete time steps.
4. Diffusion occurs only through pore regions.
5. Data is visualized and logged during the simulation.

## Results & Observations

- Nutrient diffusion depends strongly on pore connectivity.
- Poorly connected regions exhibit slower diffusion.
- Time-series data shows different equilibrium rates for different scaffold designs.
- Results align with expected biological diffusion behavior.

## Applications

- Biology and bioengineering education
- Exploratory scaffold design analysis
- Introductory computational biology modeling
- Visualization of diffusion processes in 3D systems

## Tools & Technologies

- Unity (URP)
- C#
- Voxel-based spatial modeling
- CSV data export

## Disclaimer

This project is intended for educational and exploratory purposes only and is not a validated biomedical simulation.
