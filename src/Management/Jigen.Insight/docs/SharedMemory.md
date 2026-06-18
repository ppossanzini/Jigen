# Shared Memory

This file stores structural decisions for this repository and is intended to be versioned with the project so all contributors can read and update it.

## Frontend Structural Choices
- Keep shared primary input-control layout and theme in `src/assets/styles/global/base.less` using reusable `app-inputs-layout` classes.
- Do not use table-like detail widgets for non-tabular data (avoid `el-descriptions` and `el-table` in detail or analysis panels).
- In search views, render results as a list of cards instead of tables.
- Re-read this file at the start of each iteration before generating or refactoring frontend views.
