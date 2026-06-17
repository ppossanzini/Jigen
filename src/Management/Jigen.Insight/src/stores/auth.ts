import { defineStore } from 'pinia'

interface AuthState {
  token: string | null
  selectedWorkspace: string
  userName: string | null
  roles: string[]
}

const AUTH_TOKEN_KEY = 'auth.token'
const AUTH_WORKSPACE_KEY = 'auth.workspace'
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

  const payloadPart = parts.at(1)

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

const resolveRoles = (token: string | null, roles: unknown): string[] => {
  const normalized = normalizeRoles(roles)

  if (normalized.length > 0) {
    return normalized
  }

  return extractRolesFromToken(token)
}

const parseStoredRoles = (): string[] => {
  const raw = localStorage.getItem(AUTH_ROLES_KEY) || sessionStorage.getItem(AUTH_ROLES_KEY)

  if (!raw) {
    return []
  }

  try {
    const parsed = JSON.parse(raw)

    if (!Array.isArray(parsed)) {
      return []
    }

    return normalizeRoles(parsed)
  } catch {
    return []
  }
}

const storedToken = localStorage.getItem(AUTH_TOKEN_KEY) || sessionStorage.getItem(AUTH_TOKEN_KEY)

export const useAuthStore = defineStore('auth', {
  state: (): AuthState => ({
    token: storedToken,
    selectedWorkspace: localStorage.getItem(AUTH_WORKSPACE_KEY) || 'project-orion',
    userName:
      localStorage.getItem(AUTH_USERNAME_KEY) || sessionStorage.getItem(AUTH_USERNAME_KEY),
    roles: resolveRoles(storedToken, parseStoredRoles()),
  }),
  getters: {
    isAuthenticated: (state) => Boolean(state.token),
    isDatabaseAdmin: (state) => state.roles.some(isDatabaseAdminRole),
  },
  actions: {
    setWorkspace(workspace: string) {
      this.selectedWorkspace = workspace
      localStorage.setItem(AUTH_WORKSPACE_KEY, workspace)
    },
    persistSession(token: string, userName: string, rememberMe: boolean, roles: string[] = []) {
      this.token = token
      this.userName = userName
      this.roles = resolveRoles(token, roles)

      const targetStorage = rememberMe ? localStorage : sessionStorage
      const otherStorage = rememberMe ? sessionStorage : localStorage

      targetStorage.setItem(AUTH_TOKEN_KEY, token)
      targetStorage.setItem(AUTH_WORKSPACE_KEY, this.selectedWorkspace)
      targetStorage.setItem(AUTH_USERNAME_KEY, userName)
      targetStorage.setItem(AUTH_ROLES_KEY, JSON.stringify(this.roles))

      otherStorage.removeItem(AUTH_TOKEN_KEY)
      otherStorage.removeItem(AUTH_USERNAME_KEY)
      otherStorage.removeItem(AUTH_ROLES_KEY)
    },
    logout() {
      this.token = null
      this.userName = null
      this.roles = []

      localStorage.removeItem(AUTH_TOKEN_KEY)
      sessionStorage.removeItem(AUTH_TOKEN_KEY)
      localStorage.removeItem(AUTH_USERNAME_KEY)
      sessionStorage.removeItem(AUTH_USERNAME_KEY)
      localStorage.removeItem(AUTH_ROLES_KEY)
      sessionStorage.removeItem(AUTH_ROLES_KEY)
    },
  },
})
