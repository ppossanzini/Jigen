import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import type { SetDatabaseUsersData } from '@/lib/api-types'
import {
  createDatabase,
  deleteDatabase,
  getDatabaseDetails,
  getDatabaseUsers,
  getDatabases,
  setDatabaseUsers,
} from './api'

export function useDatabases() {
  return useQuery({
    queryKey: ['databases'],
    queryFn: getDatabases,
  })
}

export function useDatabaseDetails(name: string | null) {
  return useQuery({
    queryKey: ['databases', name, 'details'],
    queryFn: () => getDatabaseDetails(name!),
    enabled: !!name,
  })
}

export function useDatabaseUsers(name: string | null) {
  return useQuery({
    queryKey: ['databases', name, 'users'],
    queryFn: () => getDatabaseUsers(name!),
    enabled: !!name,
  })
}

export function useCreateDatabase() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (name: string) => createDatabase(name),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['databases'] })
    },
  })
}

export function useDeleteDatabase() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({
      name,
      deletefiles,
    }: {
      name: string
      deletefiles: boolean
    }) => deleteDatabase(name, deletefiles),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['databases'] })
    },
  })
}

export function useSetDatabaseUsers() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({
      name,
      data,
    }: {
      name: string
      data: SetDatabaseUsersData
    }) => setDatabaseUsers(name, data),
    onSuccess: (_data, variables) => {
      const { name } = variables
      queryClient.invalidateQueries({
        queryKey: ['databases', name, 'details'],
      })
      queryClient.invalidateQueries({
        queryKey: ['databases', name, 'users'],
      })
    },
  })
}
