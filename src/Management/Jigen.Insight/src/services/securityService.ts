import { BaseRestService } from '@/services/baseRestService'
import type {
  CreateRoleData,
  CreateUserData,
  SecurityRoleApiModel,
  SecurityUserApiModel,
  UpdateRoleData,
  UpdateUserData,
} from '~types/security'

class SecurityService extends BaseRestService {
  private toRoleNames(payload: unknown): string[] {
    if (!Array.isArray(payload)) {
      return []
    }

    return payload.filter((entry): entry is string => typeof entry === 'string' && entry.length > 0)
  }

  private toUser(payload: unknown): SecurityUserApiModel | null {
    if (typeof payload !== 'object' || payload === null) {
      return null
    }

    const raw = payload as Record<string, unknown>
    const id = String(raw.id ?? '')

    if (!id) {
      return null
    }

    return {
      id,
      userName: typeof raw.userName === 'string' ? raw.userName : null,
      roles: this.toRoleNames(raw.roles),
    }
  }

  private toUsers(payload: unknown): SecurityUserApiModel[] {
    if (!Array.isArray(payload)) {
      return []
    }

    return payload
      .map((entry) => this.toUser(entry))
      .filter((entry): entry is SecurityUserApiModel => entry !== null)
  }

  private toRoles(payload: unknown): SecurityRoleApiModel[] {
    if (!Array.isArray(payload)) {
      return []
    }

    return payload
      .filter((entry) => typeof entry === 'object' && entry !== null)
      .map((entry) => {
        const raw = entry as Record<string, unknown>
        return {
          id: String(raw.id ?? ''),
          name: typeof raw.name === 'string' ? raw.name : null,
        }
      })
      .filter((entry) => entry.id.length > 0)
  }

  async getUsers(): Promise<SecurityUserApiModel[]> {
    const primary = await this.api.get('/users')

    if (primary.status === 200) {
      return this.toUsers(primary.data)
    }

    const fallback = await this.api.get('/identity/users')
    return this.toUsers(fallback.data)
  }

  async getUserById(id: string): Promise<SecurityUserApiModel> {
    const primary = await this.api.get(`/users/${id}`)
    const primaryUser = this.toUser(primary.data)

    if (primaryUser) {
      return primaryUser
    }

    const fallback = await this.api.get(`/identity/users/${id}`)
    const fallbackUser = this.toUser(fallback.data)

    if (fallbackUser) {
      return fallbackUser
    }

    throw new Error('Invalid user detail payload')
  }

  async createUser(payload: CreateUserData): Promise<void> {
    await this.api.post('/users', payload)
  }

  async updateUser(id: string, payload: UpdateUserData): Promise<void> {
    await this.api.put(`/users/${id}`, payload)
  }

  async deleteUser(id: string): Promise<void> {
    await this.api.delete(`/users/${id}`)
  }

  async getRoles(): Promise<SecurityRoleApiModel[]> {
    const primary = await this.api.get('/roles')

    if (primary.status === 200) {
      return this.toRoles(primary.data)
    }

    const fallback = await this.api.get('/identity/roles')
    return this.toRoles(fallback.data)
  }

  async createRole(payload: CreateRoleData): Promise<void> {
    await this.api.post('/roles', payload)
  }

  async updateRole(id: string, payload: UpdateRoleData): Promise<void> {
    await this.api.put(`/roles/${id}`, payload)
  }

  async deleteRole(id: string): Promise<void> {
    await this.api.delete(`/roles/${id}`)
  }
}

export const securityService = new SecurityService()
