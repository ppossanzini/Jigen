export const decodeBase64Utf8 = (value: string): string => {
  if (!value) return ''
  try {
    const binary = atob(value)
    const bytes = Uint8Array.from(binary, (char) => char.charCodeAt(0))
    return new TextDecoder().decode(bytes)
  } catch {
    return value
  }
}

export const toDisplayKey = (value: string | null | undefined): string =>
  value ? decodeBase64Utf8(value) || value : ''

export const LEVEL_COLORS = [
  '#4da5db', '#7fcf4b', '#e6a23c', '#d05ce3',
  '#f56c6c', '#3ed6c2', '#c8ba4b', '#8896e0',
]

export const levelColor = (level: number): string =>
  LEVEL_COLORS[Math.max(0, level) % LEVEL_COLORS.length] ?? LEVEL_COLORS[0]!

export const DELETED_COLOR = '#5c6672'
