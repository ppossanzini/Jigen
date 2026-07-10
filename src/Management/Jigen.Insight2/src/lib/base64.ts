/** Decodes a base64 string to UTF-8 text; falls back to the raw value if it isn't valid base64. */
export function decodeBase64Utf8(value: string): string {
  if (!value) return ''

  try {
    const binary = atob(value)
    const bytes = Uint8Array.from(binary, (char) => char.charCodeAt(0))
    return new TextDecoder().decode(bytes)
  } catch {
    return value
  }
}

/** Decodes a base64-encoded document key for display, falling back to the raw value. */
export function toDisplayKey(value: string | null | undefined): string {
  if (!value) return ''
  return decodeBase64Utf8(value) || value
}
