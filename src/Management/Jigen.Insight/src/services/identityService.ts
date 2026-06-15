import BaseRestService from '@/services/baseRestService'
import type { LoginData, LoginResponse } from '@/@types/openapi'

class IdentityService extends BaseRestService {
  async login(payload: LoginData): Promise<LoginResponse> {
    try {
      const response = await this.client.post<LoginResponse>('/identity/login', payload)
      return response.data
    } catch (error) {
      throw this.normalizeError(error)
    }
  }

  async logout(): Promise<void> {
    try {
      await this.client.post('/identity/logout')
    } catch (error) {
      throw this.normalizeError(error)
    }
  }
}

const identityService = new IdentityService()

export default identityService
