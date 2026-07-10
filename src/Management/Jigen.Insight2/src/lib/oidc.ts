import { buildApiUrl, getSettings } from './settings'
import { createCodeChallenge, createRandomToken } from './pkce'

const OIDC_STATE_KEY = 'auth.oidc.state'
const OIDC_VERIFIER_KEY = 'auth.oidc.verifier'
const OIDC_REMEMBER_KEY = 'auth.oidc.rememberMe'
const OIDC_USERNAME_KEY = 'auth.oidc.userName'
const OIDC_REDIRECT_KEY = 'auth.oidc.redirectTo'

export interface AuthorizationStartOptions {
  userName: string
  rememberMe: boolean
  /** Where to send the user after a successful sign-in; the OIDC redirect_uri
   * is fixed and can't carry this, so it's stashed in sessionStorage instead. */
  redirectTo?: string
}

export interface AuthorizationCodeResult {
  token: string
  roles: string[]
  rememberMe: boolean
  userName: string
  redirectTo?: string
}

const getRedirectUri = (): string => {
  const configured = getSettings().oidc.redirectUri
  return configured || `${window.location.origin}/auth/callback`
}

const findTokenInPayload = (payload: Record<string, unknown>): string | null => {
  const directTokenKeys = ['token', 'access_token', 'accessToken', 'jwt', 'jwtToken']

  for (const key of directTokenKeys) {
    const value = payload[key]
    if (typeof value === 'string' && value.length > 0) {
      return value
    }
  }

  return null
}

const toRoles = (payload: unknown): string[] => {
  if (!Array.isArray(payload)) {
    return []
  }

  return payload
    .map((entry) => {
      if (typeof entry === 'string') return entry
      if (typeof entry === 'object' && entry !== null) {
        const raw = entry as Record<string, unknown>
        if (typeof raw.name === 'string') return raw.name
        if (typeof raw.role === 'string') return raw.role
      }
      return null
    })
    .filter((entry): entry is string => typeof entry === 'string' && entry.length > 0)
}

const extractTokenPayload = (
  payload: unknown
): { token: string; roles: string[] } | null => {
  if (payload && typeof payload === 'object') {
    const raw = payload as Record<string, unknown>
    const token = findTokenInPayload(raw)
    if (token) {
      return { token, roles: toRoles(raw.roles ?? raw.authorities) }
    }
  }
  return null
}

const clearTransientData = (): void => {
  sessionStorage.removeItem(OIDC_STATE_KEY)
  sessionStorage.removeItem(OIDC_VERIFIER_KEY)
  sessionStorage.removeItem(OIDC_REMEMBER_KEY)
  sessionStorage.removeItem(OIDC_USERNAME_KEY)
  sessionStorage.removeItem(OIDC_REDIRECT_KEY)
}

/** Redirects the browser to the OIDC authorize endpoint (Authorization Code + PKCE). */
export const startAuthorizationCodeFlow = async (
  options: AuthorizationStartOptions
): Promise<void> => {
  const redirectUri = getRedirectUri()
  const state = createRandomToken(32)
  const verifier = createRandomToken(64)
  const challenge = await createCodeChallenge(verifier)

  sessionStorage.setItem(OIDC_STATE_KEY, state)
  sessionStorage.setItem(OIDC_VERIFIER_KEY, verifier)
  sessionStorage.setItem(OIDC_REMEMBER_KEY, options.rememberMe ? '1' : '0')
  sessionStorage.setItem(OIDC_USERNAME_KEY, options.userName)
  if (options.redirectTo) {
    sessionStorage.setItem(OIDC_REDIRECT_KEY, options.redirectTo)
  }

  const params = new URLSearchParams({
    response_type: 'code',
    client_id: getSettings().oidc.clientId,
    redirect_uri: redirectUri,
    scope: getSettings().oidc.scope,
    state,
    code_challenge: challenge,
    code_challenge_method: 'S256',
  })

  window.location.assign(`${buildApiUrl('/connect/authorize')}?${params.toString()}`)
}

/** Exchanges the authorization code returned on `/auth/callback` for an access token. */
export const exchangeAuthorizationCode = async (
  code: string,
  state: string
): Promise<AuthorizationCodeResult> => {
  const expectedState = sessionStorage.getItem(OIDC_STATE_KEY)
  const verifier = sessionStorage.getItem(OIDC_VERIFIER_KEY)

  if (!expectedState || expectedState !== state || !verifier) {
    clearTransientData()
    throw new Error('Invalid authorization state')
  }

  const payload = new URLSearchParams({
    grant_type: 'authorization_code',
    code,
    redirect_uri: getRedirectUri(),
    code_verifier: verifier,
    client_id: getSettings().oidc.clientId,
  })

  const response = await fetch(buildApiUrl('/connect/token'), {
    method: 'POST',
    credentials: 'include',
    headers: {
      Accept: 'application/json',
      'Content-Type': 'application/x-www-form-urlencoded;charset=UTF-8',
    },
    body: payload.toString(),
  })

  const responseData = (await response.json()) as unknown

  if (!response.ok) {
    clearTransientData()
    throw new Error(`Token exchange failed with status ${response.status}`)
  }

  const parsed = extractTokenPayload(responseData)
  if (!parsed) {
    clearTransientData()
    throw new Error('Token exchange did not return an access token')
  }

  const rememberMe = sessionStorage.getItem(OIDC_REMEMBER_KEY) === '1'
  const userName = sessionStorage.getItem(OIDC_USERNAME_KEY) || 'guest'
  const redirectTo = sessionStorage.getItem(OIDC_REDIRECT_KEY) || undefined
  clearTransientData()

  return { token: parsed.token, roles: parsed.roles, rememberMe, userName, redirectTo }
}
