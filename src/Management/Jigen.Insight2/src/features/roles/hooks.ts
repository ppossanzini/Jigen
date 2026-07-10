import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import type { CreateRoleData, UpdateRoleData } from '@/lib/api-types'
import {
  createRole,
  deleteRole,
  getRoleUsers,
  getRoles,
  updateRole,
} from './api'

export function useRoles() {
  return useQuery({
    queryKey: ['roles'],
    queryFn: getRoles,
  })
}

export function useRoleUsers(id: string | null) {
  return useQuery({
    queryKey: ['roles', id, 'users'],
    queryFn: () => getRoleUsers(id!),
    enabled: !!id,
  })
}

export function useCreateRole() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (data: CreateRoleData) => createRole(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['roles'] })
    },
  })
}

export function useUpdateRole() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({
      id,
      data,
    }: {
      id: string
      data: UpdateRoleData
    }) => updateRole(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['roles'] })
    },
  })
}

export function useDeleteRole() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (id: string) => deleteRole(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['roles'] })
    },
  })
}
