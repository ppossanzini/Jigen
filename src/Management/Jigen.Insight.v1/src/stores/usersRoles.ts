import { computed, ref } from 'vue'
import { defineStore } from 'pinia'

import usersRolesService from '@/services/usersRolesService'
import type {
  CreateRolePayload,
  CreateUserPayload,
  RoleItem,
  UpdateRolePayload,
  UpdateUserPayload,
  UserItem,
} from '@/modules/users/types'

export const useUsersRolesStore = defineStore('users-roles', () => {
  const users = ref<UserItem[]>([])
  const roles = ref<RoleItem[]>([])
  const isLoadingUsers = ref(false)
  const isLoadingRoles = ref(false)

  const roleMap = computed(() => {
    const map = new Map<string, RoleItem>()
    roles.value.forEach((role) => {
      map.set(role.id, role)
    })
    return map
  })

  async function loadUsers() {
    isLoadingUsers.value = true
    try {
      users.value = await usersRolesService.getUsers()
    } finally {
      isLoadingUsers.value = false
    }
  }

  async function loadRoles() {
    isLoadingRoles.value = true
    try {
      roles.value = await usersRolesService.getRoles()
    } finally {
      isLoadingRoles.value = false
    }
  }

  async function loadAll() {
    await Promise.all([loadUsers(), loadRoles()])
  }

  async function createUser(payload: CreateUserPayload) {
    await usersRolesService.createUser(payload)
    await loadUsers()
  }

  async function updateUser(userId: string, payload: UpdateUserPayload) {
    await usersRolesService.updateUser(userId, payload)
    await loadUsers()
  }

  async function deleteUser(userId: string) {
    await usersRolesService.deleteUser(userId)
    await loadUsers()
  }

  async function createRole(payload: CreateRolePayload) {
    await usersRolesService.createRole(payload)
    await loadRoles()
  }

  async function updateRole(roleId: string, payload: UpdateRolePayload) {
    await usersRolesService.updateRole(roleId, payload)
    await loadRoles()
    await loadUsers()
  }

  async function deleteRole(roleId: string) {
    await usersRolesService.deleteRole(roleId)
    await loadRoles()
    await loadUsers()
  }

  return {
    users,
    roles,
    roleMap,
    isLoadingUsers,
    isLoadingRoles,
    loadUsers,
    loadRoles,
    loadAll,
    createUser,
    updateUser,
    deleteUser,
    createRole,
    updateRole,
    deleteRole,
  }
})
