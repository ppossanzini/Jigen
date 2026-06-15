import BaseRestService from '@/services/baseRestService'
import type { RoleApiItem, UserApiItem } from '@/@types/openapi'
import type {
  CreateRolePayload,
  CreateUserPayload,
  RoleItem,
  UpdateRolePayload,
  UpdateUserPayload,
  UserItem,
} from '@/modules/users/types'

function extractArray(payload: unknown): unknown[] {
  if (Array.isArray(payload)) {
    return payload
  }

  if (!payload || typeof payload !== 'object') {
    return []
  }

  const candidate = payload as { items?: unknown[]; data?: unknown[]; value?: unknown[] }
  if (Array.isArray(candidate.items)) {
    return candidate.items
  }

  if (Array.isArray(candidate.data)) {
    return candidate.data
  }

  if (Array.isArray(candidate.value)) {
    return candidate.value
  }

  return []
}

function normalizeRole(item: unknown): RoleItem | null {
  if (typeof item === 'string') {
    return {
      id: item,
      name: item,
    }
  }

  if (!item || typeof item !== 'object') {
    return null
  }

  const role = item as RoleApiItem
  const roleName = role.name || role.id || role.roleId || ''
  if (!roleName) {
    return null
  }

  return {
    id: role.id || role.roleId || roleName,
    name: roleName,
  }
}

function normalizeUser(item: unknown): UserItem | null {
  if (typeof item === 'string') {
    return {
      id: item,
      userName: item,
      roles: [],
    }
  }

  if (!item || typeof item !== 'object') {
    return null
  }

  const user = item as UserApiItem
  const userName = user.userName || user.username || user.id || user.userId || ''
  if (!userName) {
    return null
  }

  const roles = Array.isArray(user.roles)
    ? user.roles.filter((role): role is string => typeof role === 'string')
    : Array.isArray(user.roleIds)
      ? user.roleIds.filter((role): role is string => typeof role === 'string')
      : []

  return {
    id: user.id || user.userId || userName,
    userName,
    roles,
  }
}

class UsersRolesService extends BaseRestService {
  async getUsers(): Promise<UserItem[]> {
    try {
      const response = await this.client.get<unknown>('/users')
      return extractArray(response.data).map(normalizeUser).filter((user): user is UserItem => user !== null)
    } catch (error) {
      throw this.normalizeError(error)
    }
  }

  async createUser(payload: CreateUserPayload): Promise<void> {
    try {
      await this.client.post('/users', payload)
    } catch (error) {
      throw this.normalizeError(error)
    }
  }

  async updateUser(userId: string, payload: UpdateUserPayload): Promise<void> {
    try {
      await this.client.put(`/users/${userId}`, payload)
    } catch (error) {
      throw this.normalizeError(error)
    }
  }

  async deleteUser(userId: string): Promise<void> {
    try {
      await this.client.delete(`/users/${userId}`)
    } catch (error) {
      throw this.normalizeError(error)
    }
  }

  async getRoles(): Promise<RoleItem[]> {
    try {
      const response = await this.client.get<unknown>('/roles')
      return extractArray(response.data).map(normalizeRole).filter((role): role is RoleItem => role !== null)
    } catch (error) {
      throw this.normalizeError(error)
    }
  }

  async createRole(payload: CreateRolePayload): Promise<void> {
    try {
      await this.client.post('/roles', payload)
    } catch (error) {
      throw this.normalizeError(error)
    }
  }

  async updateRole(roleId: string, payload: UpdateRolePayload): Promise<void> {
    try {
      await this.client.put(`/roles/${roleId}`, payload)
    } catch (error) {
      throw this.normalizeError(error)
    }
  }

  async deleteRole(roleId: string): Promise<void> {
    try {
      await this.client.delete(`/roles/${roleId}`)
    } catch (error) {
      throw this.normalizeError(error)
    }
  }
}

const usersRolesService = new UsersRolesService()

export default usersRolesService
