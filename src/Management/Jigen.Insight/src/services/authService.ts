import { BaseRestService } from '@/services/baseRestService'
import type { LoginData, LoginResult } from '~types/auth'

class AuthService extends BaseRestService {
  async login(payload: LoginData): Promise<LoginResult> {
    const data = await this.post<unknown, LoginData>('/identity/login', payload)

    if (typeof data === 'string' && data.length > 0) {
      return { token: data }
    }

    if (data && typeof data === 'object') {
      const objectData = data as Record<string, unknown>
      const token = objectData.token

      if (typeof token === 'string' && token.length > 0) {
        return { token }
      }
    }

    return {
      token: `${payload.userName ?? 'user'}-session-token`,
    }
  }
}

export const authService = new AuthService()
