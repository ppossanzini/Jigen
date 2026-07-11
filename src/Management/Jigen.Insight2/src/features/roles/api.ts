import { apiClient } from '@/lib/api-client'
import type {
  CreateRoleData,
  RoleSummary,
  UpdateRoleData,
  UserSummary,
} from '@/lib/api-types'

export async function getRoles(): Promise<RoleSummary[]> {
  const response = await apiClient.get<RoleSummary[]>('/roles')
  return response.data
}

export async function createRole(data: CreateRoleData): Promise<void> {
  await apiClient.post('/roles', data)
}

export async function updateRole(
  id: string,
  data: UpdateRoleData,
): Promise<void> {
  await apiClient.put(`/roles/${encodeURIComponent(id)}`, data)
}

export async function deleteRole(id: string): Promise<void> {
  await apiClient.delete(`/roles/${encodeURIComponent(id)}`)
}

export async function getRoleUsers(id: string): Promise<UserSummary[]> {
  const response = await apiClient.get<UserSummary[]>(
    `/roles/${encodeURIComponent(id)}/users`,
  )
  return response.data
}
