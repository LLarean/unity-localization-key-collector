# ![unity](https://img.shields.io/badge/Unity-100000?style=for-the-badge&logo=unity&logoColor=white) Localization Key Collector

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE.md)
![stability-experimental](https://img.shields.io/badge/stability-experimental-orange.svg)

> [!WARNING]
> **Work in progress.** Use at your own risk.

Editor utility for collecting localization keys from prefabs, scenes, and code across a Unity project. Exports results to CSV for use in translation workflows.

## Installation

- *(via Package Manager)* Select **Add package from git URL** and enter:
  - `https://github.com/LLarean/unity-localization-key-collector.git`
- *(manually)* Clone or download and place into your project's *Assets* folder

## Quick Start

1. Open `Tools > Localization Key Collector`
2. Configure sources — prefabs, scenes, code
3. Expand **Project Settings** and set your localization component name and method
4. Set output paths for CSV files
5. Click **Collect & Export CSV**

Two files are produced: one for component-based keys, one for keys found in code.

## Requirements

- Unity 2021.3+

## Project Status

Experimental. Core functionality works but has not been tested across all project configurations.

---

<div align="center">

**Made with ❤️ for the Unity community**

⭐ If this project helped you, please consider giving it a star!

</div>
