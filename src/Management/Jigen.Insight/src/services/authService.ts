import { BaseRestService } from '@/services/baseRestService'
import type { LoginData, LoginResult } from '~types/auth'

class AuthService extends BaseRestService {
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

  async login(payload: LoginData): Promise<LoginResult> {
    const data = await this.post<unknown, LoginData>('/identity/login', payload)

    if (typeof data === 'string' && data.length > 0) {
      return { token: data, roles: [] }
    }

    if (data && typeof data === 'object') {
      const objectData = data as Record<string, unknown>
      const token = objectData.token
      const roles = this.toRoles(objectData.roles)

      if (typeof token === 'string' && token.length > 0) {
        return { token, roles }
      }
    }

    return {
      token: `${payload.userName ?? 'user'}-session-token`,
      roles: [],
    }
  }
}

export const authService = new AuthService()
