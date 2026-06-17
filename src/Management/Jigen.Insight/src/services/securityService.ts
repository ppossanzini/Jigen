import { BaseRestService } from '@/services/baseRestService'

class SecurityService extends BaseRestService {
  private async getWithFallback<T>(primaryPath: string, fallbackPath: string): Promise<T> {
    try {
      const response = await this.api.get<T>(primaryPath)
      return response.data
    } catch {
      const response = await this.api.get<T>(fallbackPath)
      return response.data
    }
  }

  async listUsers(): Promise<server.security.UserSummary[]> {
    return this.getWithFallback<server.security.UserSummary[]>('/users', '/identity/users')
  }

  async getUser(id: string): Promise<server.security.UserDetail> {
    return this.getWithFallback<server.security.UserDetail>(`/users/${encodeURIComponent(id)}`, `/identity/users/${encodeURIComponent(id)}`)
  }

  async createUser(payload: server.security.CreateUserData): Promise<void> {
    await this.api.post('/users', payload)
  }

  async updateUser(id: string, payload: server.security.UpdateUserData): Promise<server.security.UserDetail> {
    const response = await this.api.put<server.security.UserDetail>(`/users/${encodeURIComponent(id)}`, payload)
    return response.data
  }

  async deleteUser(id: string): Promise<void> {
    await this.api.delete(`/users/${encodeURIComponent(id)}`)
  }

  async listRoles(): Promise<server.security.RoleSummary[]> {
    return this.getWithFallback<server.security.RoleSummary[]>('/roles', '/identity/roles')
  }

  async createRole(payload: server.security.CreateRoleData): Promise<void> {
    await this.api.post('/roles', payload)
  }

  async updateRole(id: string, payload: server.security.UpdateRoleData): Promise<void> {
    await this.api.put(`/roles/${encodeURIComponent(id)}`, payload)
  }

  async deleteRole(id: string): Promise<void> {
    await this.api.delete(`/roles/${encodeURIComponent(id)}`)
  }

  async listUsersForRole(roleId: string): Promise<server.security.UserSummary[]> {
    return this.getWithFallback<server.security.UserSummary[]>(
      `/roles/${encodeURIComponent(roleId)}/users`,
      `/identity/roles/${encodeURIComponent(roleId)}/users`,
    )
  }
}

export const securityService = new SecurityService()
