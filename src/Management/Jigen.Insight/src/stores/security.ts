import { defineStore } from 'pinia'
import type { SecurityRoleApiModel, SecurityUserApiModel } from '~types/security'
import { securityService } from '@/services/securityService'

interface SecurityState {
  users: SecurityUserApiModel[]
  roles: SecurityRoleApiModel[]
  selectedUserId: string | null
  selectedRoleId: string | null
  userRolesById: Record<string, string[]>
  loadingUsers: boolean
  loadingRoles: boolean
}

export const useSecurityStore = defineStore('security', {
  state: (): SecurityState => ({
    users: [],
    roles: [],
    selectedUserId: null,
    selectedRoleId: null,
    userRolesById: {},
    loadingUsers: false,
    loadingRoles: false,
  }),
  getters: {
    selectedUser: (state) => state.users.find((user) => user.id === state.selectedUserId) ?? null,
    selectedRole: (state) => state.roles.find((role) => role.id === state.selectedRoleId) ?? null,
  },
  actions: {
    async loadUsers() {
      this.loadingUsers = true

      try {
        const users = await securityService.getUsers()
        this.users = users

        users.forEach((user) => {
          if (!this.userRolesById[user.id]) {
            this.userRolesById[user.id] = []
          }
        })

        const existingIds = new Set(users.map((user) => user.id))
        Object.keys(this.userRolesById).forEach((userId) => {
          if (!existingIds.has(userId)) {
            delete this.userRolesById[userId]
          }
        })

        if (this.selectedUserId && !existingIds.has(this.selectedUserId)) {
          this.selectedUserId = null
        }
      } finally {
        this.loadingUsers = false
      }
    },
    async loadRoles() {
      this.loadingRoles = true

      try {
        this.roles = await securityService.getRoles()

        if (this.selectedRoleId && !this.roles.some((role) => role.id === this.selectedRoleId)) {
          this.selectedRoleId = null
        }
      } finally {
        this.loadingRoles = false
      }
    },
    async loadAll() {
      await Promise.all([this.loadUsers(), this.loadRoles()])
    },
    setSelectedUser(id: string | null) {
      this.selectedUserId = id
    },
    setSelectedRole(id: string | null) {
      this.selectedRoleId = id
    },
    setUserRoles(userId: string, roles: string[]) {
      this.userRolesById[userId] = [...roles]
    },
    removeRoleFromAssignments(roleName: string) {
      Object.keys(this.userRolesById).forEach((userId) => {
        const assignedRoles = this.userRolesById[userId] ?? []
        this.userRolesById[userId] = assignedRoles.filter((entry) => entry !== roleName)
      })
    },
  },
})
