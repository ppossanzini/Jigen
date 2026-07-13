/** Formatting helpers for API values (schema numerics can arrive as `number | string`) */

/** Coerce a schema numeric (`number | string | null | undefined`) to a finite number */
export function toNum(value: number | string | null | undefined): number {
  if (value === null || value === undefined) return 0;

  const n = typeof value === 'number' ? value : Number(value);

  return Number.isFinite(n) ? n : 0;
}

const BYTE_UNITS = ['B', 'KB', 'MB', 'GB', 'TB', 'PB'];

/** Humanize a byte count, e.g. `1536` → `1.5 KB` */
export function formatBytes(value: number | string | null | undefined, fractionDigits = 1): string {
  let bytes = toNum(value);

  let unitIndex = 0;
  while (bytes >= 1024 && unitIndex < BYTE_UNITS.length - 1) {
    bytes /= 1024;
    unitIndex += 1;
  }

  const digits = unitIndex === 0 ? 0 : fractionDigits;

  return `${bytes.toFixed(digits)} ${BYTE_UNITS[unitIndex]}`;
}

/** Format a percentage value (already 0-100), e.g. `12.345` → `12.3%` */
export function formatPercent(value: number | string | null | undefined, fractionDigits = 1): string {
  return `${toNum(value).toFixed(fractionDigits)}%`;
}

const countFormatter = new Intl.NumberFormat('en-US');
const compactCountFormatter = new Intl.NumberFormat('en-US', { notation: 'compact', maximumFractionDigits: 1 });

/** Format an integer count with thousands separators */
export function formatCount(value: number | string | null | undefined): string {
  return countFormatter.format(toNum(value));
}

/** Format a count compactly for tight spaces, e.g. `1234567` → `1.2M` */
export function formatCompactCount(value: number | string | null | undefined): string {
  return compactCountFormatter.format(toNum(value));
}
