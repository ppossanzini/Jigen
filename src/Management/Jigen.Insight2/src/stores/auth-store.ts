import { create } from 'zustand'

const AUTH_TOKEN_KEY = 'auth.token'
const AUTH_USERNAME_KEY = 'auth.userName'
const AUTH_ROLES_KEY = 'auth.roles'

const normalizeRoles = (roles: unknown): string[] => {
  if (!Array.isArray(roles)) {
    return []
  }

  const unique = new Set<string>()

  roles.forEach((entry) => {
    if (typeof entry === 'string') {
      const value = entry.trim()
      if (value.length > 0) {
        unique.add(value)
      }
    }
  })

  return [...unique]
}

const decodeJwtPayload = (token: string): Record<string, unknown> | null => {
  const parts = token.split('.')
  const payloadPart = parts[1]

  if (!payloadPart) {
    return null
  }

  try {
    const base64 = payloadPart.replace(/-/g, '+').replace(/_/g, '/')
    const padded = base64.padEnd(Math.ceil(base64.length / 4) * 4, '=')
    const json = atob(padded)
    const payload = JSON.parse(json)

    if (typeof payload !== 'object' || payload === null) {
      return null
    }

    return payload as Record<string, unknown>
  } catch {
    return null
  }
}

const toNumericDate = (value: unknown): number | null => {
  if (typeof value === 'number' && Number.isFinite(value)) {
    return value
  }

  if (typeof value === 'string') {
    const parsed = Number(value)
    if (Number.isFinite(parsed)) {
      return parsed
    }
  }

  return null
}

export const isJwtValid = (token: string | null): boolean => {
  if (!token) {
    return false
  }

  const payload = decodeJwtPayload(token)
  if (!payload) {
    return false
  }

  const exp = toNumericDate(payload.exp)
  if (!exp) {
    return true
  }

  const nowInSeconds = Date.now() / 1000
  return exp > nowInSeconds
}

const extractRolesFromToken = (token: string | null): string[] => {
  if (!token) {
    return []
  }

  const payload = decodeJwtPayload(token)
  if (!payload) {
    return []
  }

  const roleClaims = [
    payload.roles,
    payload.role,
    payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'],
  ]

  const roles: string[] = []

  roleClaims.forEach((claim) => {
    if (Array.isArray(claim)) {
      roles.push(...claim.filter((entry): entry is string => typeof entry === 'string'))
      return
    }
    if (typeof claim === 'string') {
      roles.push(claim)
    }
  })

  return normalizeRoles(roles)
}

const isDatabaseAdminRole = (role: string): boolean => {
  const normalized = role.replace(/[^a-z]/gi, '').toLowerCase()
  return normalized.includes('databaseadmin')
}

const isSecurityAdminRole = (role: string): boolean => {
  const normalized = role.replace(/[^a-z]/gi, '').toLowerCase()
  return normalized.includes('securityadmin')
}

const resolveRoles = (token: string | null, roles: unknown): string[] => {
  const normalized = normalizeRoles(roles)
  if (normalized.length > 0) {
    return normalized
  }
  return extractRolesFromToken(token)
}

const parseStoredRoles = (raw: string | null): string[] => {
  if (!raw) {
    return []
  }

  try {
    const parsed = JSON.parse(raw)
    return Array.isArray(parsed) ? normalizeRoles(parsed) : []
  } catch {
    return []
  }
}

/** Exported for tests: reads what the store would hydrate to right now, without needing a module reload to observe storage changes. */
export const readInitialState = () => {
  const rawToken =
    localStorage.getItem(AUTH_TOKEN_KEY) || sessionStorage.getItem(AUTH_TOKEN_KEY)
  const token = isJwtValid(rawToken) ? rawToken : null

  if (!token) {
    localStorage.removeItem(AUTH_TOKEN_KEY)
    sessionStorage.removeItem(AUTH_TOKEN_KEY)
    localStorage.removeItem(AUTH_ROLES_KEY)
    sessionStorage.removeItem(AUTH_ROLES_KEY)
    localStorage.removeItem(AUTH_USERNAME_KEY)
    sessionStorage.removeItem(AUTH_USERNAME_KEY)
  }

  const rawRoles =
    localStorage.getItem(AUTH_ROLES_KEY) || sessionStorage.getItem(AUTH_ROLES_KEY)

  return {
    token,
    userName:
      localStorage.getItem(AUTH_USERNAME_KEY) ||
      sessionStorage.getItem(AUTH_USERNAME_KEY) ||
      null,
    roles: resolveRoles(token, parseStoredRoles(rawRoles)),
  }
}

interface AuthState {
  auth: {
    token: string | null
    userName: string | null
    roles: string[]
    persistSession: (
      token: string,
      userName: string,
      rememberMe: boolean,
      roles?: string[]
    ) => void
    logout: () => void
    /** Alias of `logout`, kept for call sites that reset auth state on a 401. */
    reset: () => void
  }
}

export const useAuthStore = create<AuthState>()((set, get) => {
  const clearStorage = () => {
    localStorage.removeItem(AUTH_TOKEN_KEY)
    sessionStorage.removeItem(AUTH_TOKEN_KEY)
    localStorage.removeItem(AUTH_USERNAME_KEY)
    sessionStorage.removeItem(AUTH_USERNAME_KEY)
    localStorage.removeItem(AUTH_ROLES_KEY)
    sessionStorage.removeItem(AUTH_ROLES_KEY)
  }

  const logout = () => {
    clearStorage()
    set((state) => ({
      auth: { ...state.auth, token: null, userName: null, roles: [] },
    }))
  }

  return {
    auth: {
      ...readInitialState(),
      persistSession: (token, userName, rememberMe, roles = []) => {
        const resolved = resolveRoles(token, roles)
        const targetStorage = rememberMe ? localStorage : sessionStorage
        const otherStorage = rememberMe ? sessionStorage : localStorage

        targetStorage.setItem(AUTH_TOKEN_KEY, token)
        targetStorage.setItem(AUTH_USERNAME_KEY, userName)
        targetStorage.setItem(AUTH_ROLES_KEY, JSON.stringify(resolved))
        otherStorage.removeItem(AUTH_TOKEN_KEY)
        otherStorage.removeItem(AUTH_USERNAME_KEY)
        otherStorage.removeItem(AUTH_ROLES_KEY)

        set((state) => ({
          auth: { ...state.auth, token, userName, roles: resolved },
        }))
      },
      logout,
      reset: () => get().auth.logout(),
    },
  }
})

export const useIsAuthenticated = (): boolean =>
  useAuthStore((state) => isJwtValid(state.auth.token))

export const useIsDatabaseAdmin = (): boolean =>
  useAuthStore((state) => state.auth.roles.some(isDatabaseAdminRole))

export const useIsSecurityAdmin = (): boolean =>
  useAuthStore((state) => state.auth.roles.some(isSecurityAdminRole))

/** Non-reactive check for use outside React (e.g. router `beforeLoad` guards). */
export const getIsAuthenticated = (): boolean =>
  isJwtValid(useAuthStore.getState().auth.token)
