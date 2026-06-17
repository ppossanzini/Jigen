import { BaseRestService } from '@/services/baseRestService'
import type {
  AuthorizationCodeResult,
  AuthorizationStartOptions,
  LoginData,
  LoginResult,
} from '~types/auth'

const OIDC_STATE_KEY = 'auth.oidc.state'
const OIDC_VERIFIER_KEY = 'auth.oidc.verifier'
const OIDC_REMEMBER_KEY = 'auth.oidc.rememberMe'
const OIDC_USERNAME_KEY = 'auth.oidc.userName'
const OIDC_CLIENT_ID_KEY = 'auth.oidc.clientId'
const OIDC_CLIENT_SECRET_KEY = 'auth.oidc.clientSecret'

const toBase64Url = (bytes: Uint8Array): string => {
  let binary = ''

  bytes.forEach((byte) => {
    binary += String.fromCharCode(byte)
  })

  return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/g, '')
}

const createRandomToken = (size: number): string => {
  const bytes = new Uint8Array(size)
  crypto.getRandomValues(bytes)
  return toBase64Url(bytes)
}

const createCodeChallenge = async (verifier: string): Promise<string> => {
  const encodedVerifier = new TextEncoder().encode(verifier)
  const digest = await crypto.subtle.digest('SHA-256', encodedVerifier)
  return toBase64Url(new Uint8Array(digest))
}

class AuthService extends BaseRestService {
  private getConfiguredClientId(): string {
    return import.meta.env.VITE_OIDC_CLIENT_ID?.trim() ?? ''
  }

  private getClientId(): string {
    const configured = this.getConfiguredClientId()

    if (configured) {
      return configured
    }

    return sessionStorage.getItem(OIDC_CLIENT_ID_KEY) ?? 'jigen-insight-spa'
  }

  private getClientSecret(): string {
    const configured = import.meta.env.VITE_OIDC_CLIENT_SECRET?.trim()

    if (configured) {
      return configured
    }

    return sessionStorage.getItem(OIDC_CLIENT_SECRET_KEY) ?? ''
  }

  private getRedirectUri(): string {
    const configured = import.meta.env.VITE_OIDC_REDIRECT_URI?.trim()

    if (configured) {
      return configured
    }

    return `${window.location.origin}/auth/callback`
  }

  private getScope(): string {
    const configured = import.meta.env.VITE_OIDC_SCOPE?.trim()

    if (configured) {
      return configured
    }

    return 'openid jigen_api'
  }

  private clearAuthorizationTransientData(): void {
    sessionStorage.removeItem(OIDC_STATE_KEY)
    sessionStorage.removeItem(OIDC_VERIFIER_KEY)
    sessionStorage.removeItem(OIDC_REMEMBER_KEY)
    sessionStorage.removeItem(OIDC_USERNAME_KEY)
  }

  private extractTokenPayload(payload: unknown): LoginResult | null {
    if (typeof payload === 'string' && payload.length > 0) {
      return { token: payload, roles: [] }
    }

    if (payload && typeof payload === 'object') {
      const raw = payload as Record<string, unknown>
      const token = raw.token ?? raw.access_token
      const roles = this.toRoles(raw.roles)

      if (typeof token === 'string' && token.length > 0) {
        return { token, roles }
      }
    }

    return null
  }

  private async ensureDefaultClient(redirectUri: string): Promise<void> {
    const upsertClient = async (clientId: string): Promise<boolean> => {
      const payload = {
        clientId,
        displayName: clientId,
        allowAuthorizationCode: true,
        allowClientCredentials: false,
        allowRefreshToken: true,
        redirectUris: [redirectUri],
        postLogoutRedirectUris: [`${window.location.origin}/sign-in`],
        scopes: this.getScope().split(/\s+/).filter((entry) => entry.length > 0),
      }

      const response = await this.post<unknown, typeof payload>('/identity/clients', payload)

      sessionStorage.setItem(OIDC_CLIENT_ID_KEY, clientId)

      if (response && typeof response === 'object') {
        const raw = response as Record<string, unknown>
        const secret = raw.clientSecret

        if (typeof secret === 'string' && secret.length > 0) {
          sessionStorage.setItem(OIDC_CLIENT_SECRET_KEY, secret)
          return true
        }
      }

      return false
    }

    const configuredClientId = this.getConfiguredClientId()
    const primaryClientId = this.getClientId()

    try {
      await upsertClient(primaryClientId)
    } catch {
      if (!configuredClientId) {
        try {
          const fallbackClientId = `jigen-insight-spa-${createRandomToken(8).toLowerCase()}`
          await upsertClient(fallbackClientId)
        } catch {
          // If the endpoint is protected or unavailable, continue with configured/default client.
        }
      }
    }
  }

  private toRoles(payload: unknown): string[] {
    if (!Array.isArray(payload)) {
      return []
    }

    return payload
      .map((entry) => {
        if (typeof entry === 'string') {
          return entry
        }

        if (typeof entry === 'object' && entry !== null) {
          const raw = entry as Record<string, unknown>

          if (typeof raw.name === 'string') {
            return raw.name
          }

          if (typeof raw.role === 'string') {
            return raw.role
          }
        }

        return null
      })
      .filter((entry): entry is string => typeof entry === 'string' && entry.length > 0)
  }

  async loginWithCredentials(payload: LoginData): Promise<LoginResult | null> {
    const response = await this.post<unknown, LoginData>('/identity/login', payload)
    return this.extractTokenPayload(response)
  }

  async startAuthorizationCodeFlow(options: AuthorizationStartOptions): Promise<void> {
    const redirectUri = this.getRedirectUri()
    await this.ensureDefaultClient(redirectUri)

    const state = createRandomToken(32)
    const verifier = createRandomToken(64)
    const challenge = await createCodeChallenge(verifier)

    sessionStorage.setItem(OIDC_STATE_KEY, state)
    sessionStorage.setItem(OIDC_VERIFIER_KEY, verifier)
    sessionStorage.setItem(OIDC_REMEMBER_KEY, options.rememberMe ? '1' : '0')
    sessionStorage.setItem(OIDC_USERNAME_KEY, options.userName)

    const params = new URLSearchParams({
      response_type: 'code',
      client_id: this.getClientId(),
      redirect_uri: redirectUri,
      scope: this.getScope(),
      state,
      code_challenge: challenge,
      code_challenge_method: 'S256',
    })

    if (options.prompt) {
      params.set('prompt', options.prompt)
    }

    window.location.assign(`/api/connect/authorize?${params.toString()}`)
  }

  async exchangeAuthorizationCode(code: string, state: string): Promise<AuthorizationCodeResult> {
    const expectedState = sessionStorage.getItem(OIDC_STATE_KEY)
    const verifier = sessionStorage.getItem(OIDC_VERIFIER_KEY)

    if (!expectedState || expectedState !== state || !verifier) {
      this.clearAuthorizationTransientData()
      throw new Error('Invalid authorization state')
    }

    const clientId = this.getClientId()
    const secret = this.getClientSecret()

    const sendTokenRequest = async (mode: 'basic' | 'body'): Promise<Response> => {
      const payload = new URLSearchParams({
        grant_type: 'authorization_code',
        code,
        redirect_uri: this.getRedirectUri(),
        code_verifier: verifier,
      })

      const headers: Record<string, string> = {
        Accept: 'application/json',
        'Content-Type': 'application/x-www-form-urlencoded;charset=UTF-8',
      }

      if (mode === 'basic' && secret.length > 0) {
        headers.Authorization = `Basic ${btoa(`${clientId}:${secret}`)}`
      } else {
        payload.set('client_id', clientId)

        if (secret.length > 0) {
          payload.set('client_secret', secret)
        }
      }

      return fetch('/api/connect/token', {
        method: 'POST',
        headers,
        body: payload.toString(),
      })
    }

    const firstMode: 'basic' | 'body' = secret.length > 0 ? 'basic' : 'body'
    let response = await sendTokenRequest(firstMode)

    let responseData: unknown = null

    try {
      responseData = (await response.json()) as unknown
    } catch {
      responseData = null
    }

    if (!response.ok) {
      const errorBody =
        responseData && typeof responseData === 'object'
          ? (responseData as Record<string, unknown>)
          : null

      const errorCode = typeof errorBody?.error === 'string' ? errorBody.error : ''
      const errorDescription =
        typeof errorBody?.error_description === 'string' ? errorBody.error_description : ''

      if (
        errorCode === 'invalid_request' &&
        errorDescription.includes('Multiple client credentials') &&
        secret.length > 0
      ) {
        const fallbackMode: 'basic' | 'body' = firstMode === 'basic' ? 'body' : 'basic'
        response = await sendTokenRequest(fallbackMode)

        try {
          responseData = (await response.json()) as unknown
        } catch {
          responseData = null
        }
      }
    }

    if (!response.ok) {
      throw new Error(`Token exchange failed with status ${response.status}`)
    }

    const parsed = this.extractTokenPayload(responseData)

    if (!parsed) {
      this.clearAuthorizationTransientData()
      throw new Error('Token exchange did not return an access token')
    }

    const rememberMe = sessionStorage.getItem(OIDC_REMEMBER_KEY) === '1'
    const userName = sessionStorage.getItem(OIDC_USERNAME_KEY) || 'guest'

    this.clearAuthorizationTransientData()

    return {
      token: parsed.token,
      roles: parsed.roles,
      rememberMe,
      userName,
    }
  }
}

export const authService = new AuthService()
