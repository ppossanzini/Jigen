import { apiClient } from '@/lib/api-client'
import type {
  DatabaseDetails,
  DatabaseUserInfo,
  SetDatabaseUsersData,
} from '@/lib/api-types'

export async function getDatabases(): Promise<string[]> {
  const response = await apiClient.get<string[]>('/database')
  return response.data
}

export async function createDatabase(name: string): Promise<void> {
  await apiClient.post('/database', undefined, {
    params: { name },
  })
}

export async function deleteDatabase(
  name: string,
  deletefiles: boolean,
): Promise<void> {
  await apiClient.delete('/database', {
    params: { name, deletefiles },
  })
}

export async function getDatabaseDetails(name: string): Promise<DatabaseDetails> {
  const response = await apiClient.get<DatabaseDetails>(
    `/database/${encodeURIComponent(name)}/details`,
  )
  return response.data
}

export async function getDatabaseUsers(
  name: string,
): Promise<DatabaseUserInfo[]> {
  const response = await apiClient.get<DatabaseUserInfo[]>(
    `/database/${encodeURIComponent(name)}/users`,
  )
  return response.data
}

export async function setDatabaseUsers(
  name: string,
  data: SetDatabaseUsersData,
): Promise<DatabaseUserInfo[]> {
  const response = await apiClient.put<DatabaseUserInfo[]>(
    `/database/${encodeURIComponent(name)}/users`,
    data,
  )
  return response.data
}
