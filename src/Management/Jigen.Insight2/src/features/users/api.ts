import { apiClient } from '@/lib/api-client'
import type {
  CreateUserData,
  UpdateUserData,
  UserDetail,
  UserSummary,
} from '@/lib/api-types'

export async function getUsers(): Promise<UserSummary[]> {
  const response = await apiClient.get<UserSummary[]>('/users')
  return response.data
}

export async function createUser(data: CreateUserData): Promise<void> {
  await apiClient.post('/users', data)
}

export async function getUser(id: string): Promise<UserDetail> {
  const response = await apiClient.get<UserDetail>(
    `/users/${encodeURIComponent(id)}`,
  )
  return response.data
}

export async function updateUser(
  id: string,
  data: UpdateUserData,
): Promise<UserDetail> {
  const response = await apiClient.put<UserDetail>(
    `/users/${encodeURIComponent(id)}`,
    data,
  )
  return response.data
}

export async function deleteUser(id: string): Promise<void> {
  await apiClient.delete(`/users/${encodeURIComponent(id)}`)
}
