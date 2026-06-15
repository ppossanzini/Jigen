export type AppTheme = 'dark' | 'light'

const THEME_KEY = 'jigen.theme'
const DEFAULT_THEME: AppTheme = 'dark'

function isTheme(value: string | null): value is AppTheme {
  return value === 'dark' || value === 'light'
}

export function applyTheme(theme: AppTheme) {
  document.documentElement.setAttribute('data-theme', theme)
  localStorage.setItem(THEME_KEY, theme)
}

export function initializeTheme() {
  const storedTheme = localStorage.getItem(THEME_KEY)
  const theme = isTheme(storedTheme) ? storedTheme : DEFAULT_THEME
  applyTheme(theme)
}

export function getCurrentTheme(): AppTheme {
  const current = document.documentElement.getAttribute('data-theme')
  return isTheme(current) ? current : DEFAULT_THEME
}

export function toggleTheme() {
  const nextTheme: AppTheme = getCurrentTheme() === 'dark' ? 'light' : 'dark'
  applyTheme(nextTheme)
}
