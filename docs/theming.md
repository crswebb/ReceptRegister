# Theming & Design Tokens

This document details the lightweight design token system and UI conventions.

## Contents
- [Token Categories](#token-categories)
- [Dark Mode](#dark-mode)
- [Components Using Tokens](#components-using-tokens)
- [Adding Tokens](#adding-tokens)
- [Accessibility](#accessibility)

## Token Categories
Defined in `wwwroot/css/variables.css`:
- Colors: `--color-*` (text, background, surface, primary, danger, success, warning, link, focus, shadow palette)
- Spacing: `--spacing-0..8` (2px scale)
- Typography: `--fs-*`, `--fw-*`, line heights
- Radii: `--radius-*`, including `--radius-pill`
- Elevation: `--elevation-*` shadow presets
- Motion: generic transition variables
- Z-indices: layer ordering tokens

## Dark Mode
Automatic via `prefers-color-scheme: dark`; user override persisted in `localStorage` key `rr_theme` and applied by toggling `data-theme="dark|light"` on `<html>`.

## Components Using Tokens
- Buttons (variants: primary, outline, subtle, danger, secondary; sizes sm, default, lg)
- Forms (consistent spacing, focus ring via token)
- Tables (responsive stacking, accessible caption)
- Pagination
- Recipe list (table vs card layout preference `rr_recipe_layout`)

## Adding Tokens
1. Add the new variable in `variables.css` with a meaningful semantic name.
2. Reference it in component styles; avoid raw hex or magic numbers.
3. Verify contrast if it affects text or interactive states.

## Accessibility
- Contrast meets WCAG AA (text 4.5:1 normal, 3:1 large)
- Focus indicators: 2px outline using `--color-focus-outline`
- Avoid conveying meaning solely with color; pair with icon/text where possible.

---
← Previous: [Data Migration](./data-migration.md) | Next: [Localization](./localization.md) →
