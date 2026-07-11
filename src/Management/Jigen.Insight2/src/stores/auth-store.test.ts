import { beforeEach, describe, expect, it } from 'vitest'
import { readInitialState, useAuthStore } from './auth-store'

const toBase64Url = (value: string): string =>
  btoa(value).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/g, '')

function makeJwt(payload: Record<string, unknown>): string {
  const header = toBase64Url(JSON.stringify({ alg: 'none', typ: 'JWT' }))
  const body = toBase64Url(JSON.stringify(payload))
  return `${header}.${body}.signature`
}

const futureExp = Math.floor(Date.now() / 1000) + 3600
const pastExp = Math.floor(Date.now() / 1000) - 3600

describe('useAuthStore', () => {
  beforeEach(() => {
    // logout() clears both storages; the explicit clear() calls also cover
    // storage entries this test file writes to directly (not via the store).
    useAuthStore.getState().auth.logout()
    localStorage.clear()
    sessionStorage.clear()
  })

  it('starts with no session when nothing is persisted', () => {
    expect(useAuthStore.getState().auth.token).toBeNull()
    expect(useAuthStore.getState().auth.userName).toBeNull()
    expect(useAuthStore.getState().auth.roles).toEqual([])
  })

  it('persists the session in localStorage when rememberMe is true, so a fresh hydration reads it back', () => {
    const token = makeJwt({ exp: futureExp })

    useAuthStore.getState().auth.persistSession(token, 'guest', true, ['DatabaseAdmin'])

    expect(localStorage.getItem('auth.token')).toBe(token)
    expect(sessionStorage.getItem('auth.token')).toBeNull()

    const hydrated = readInitialState()
    expect(hydrated.token).toBe(token)
    expect(hydrated.userName).toBe('guest')
    expect(hydrated.roles).toEqual(['DatabaseAdmin'])
  })

  it('persists the session in sessionStorage only when rememberMe is false', () => {
    const token = makeJwt({ exp: futureExp })

    useAuthStore.getState().auth.persistSession(token, 'guest', false, ['SecurityAdmin'])

    expect(sessionStorage.getItem('auth.token')).toBe(token)
    expect(localStorage.getItem('auth.token')).toBeNull()
  })

  it('derives roles from the JWT payload when none are passed explicitly', () => {
    const token = makeJwt({ exp: futureExp, roles: ['DatabaseAdmin', 'SecurityAdmin'] })

    useAuthStore.getState().auth.persistSession(token, 'guest', true)

    expect(useAuthStore.getState().auth.roles).toEqual(
      expect.arrayContaining(['DatabaseAdmin', 'SecurityAdmin'])
    )
  })

  it('does not hydrate an expired token from storage', () => {
    const expiredToken = makeJwt({ exp: pastExp })
    localStorage.setItem('auth.token', expiredToken)
    localStorage.setItem('auth.userName', 'guest')
    localStorage.setItem('auth.roles', JSON.stringify(['DatabaseAdmin']))

    const hydrated = readInitialState()

    expect(hydrated.token).toBeNull()
    expect(localStorage.getItem('auth.token')).toBeNull()
  })

  it('logout clears in-memory state and both storages', () => {
    const token = makeJwt({ exp: futureExp })
    useAuthStore.getState().auth.persistSession(token, 'guest', true, ['DatabaseAdmin'])

    useAuthStore.getState().auth.logout()

    expect(useAuthStore.getState().auth.token).toBeNull()
    expect(useAuthStore.getState().auth.userName).toBeNull()
    expect(useAuthStore.getState().auth.roles).toEqual([])
    expect(localStorage.getItem('auth.token')).toBeNull()
  })

  it('reset is an alias for logout', () => {
    const token = makeJwt({ exp: futureExp })
    useAuthStore.getState().auth.persistSession(token, 'guest', true)

    useAuthStore.getState().auth.reset()

    expect(useAuthStore.getState().auth.token).toBeNull()
  })
})
