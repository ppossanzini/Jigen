export const LEVEL_COLORS = [
  '#4da5db',
  '#7fcf4b',
  '#e6a23c',
  '#d05ce3',
  '#f56c6c',
  '#3ed6c2',
  '#c8ba4b',
  '#8896e0',
]

export function levelColor(level: number): string {
  return LEVEL_COLORS[Math.max(0, level) % LEVEL_COLORS.length] ?? LEVEL_COLORS[0]
}

export const DELETED_COLOR = '#5c6672'
