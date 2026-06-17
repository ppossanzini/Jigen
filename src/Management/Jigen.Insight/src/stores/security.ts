import { defineStore } from 'pinia'
import { securityService } from '@/services/securityService'

export interface SecurityUser {
  id: string
  userName: string
}

export interface SecurityRole {
  id: string
  name: string
}

export interface SecurityUserDetail {
  id: string
  userName: string
  roles: string[]
  permissions: string[]
}

interface SecurityState {
  users: SecurityUser[]
  roles: SecurityRole[]
  selectedUserId: string | null
  selectedRoleId: string | null
  userDetailsById: Record<string, SecurityUserDetail>
  usersByRoleId: Record<string, SecurityUser[]>
  loadingUsers: boolean
  loadingRoles: boolean
  loadingUserDetail: boolean
  loadingRoleUsers: boolean
}

const toRoleNames = (roles: unknown): string[] => {
  if (!Array.isArray(roles)) {
    return []
  }

  const names = new Set<string>()

  roles.forEach((entry) => {
    if (typeof entry === 'string' && entry.trim().length > 0) {
      names.add(entry.trim())
    }
  })

  return [...names]
}

const toSecurityUser = (entry: server.security.UserSummary): SecurityUser | null => {
  const id = typeof entry.id === 'string' ? entry.id.trim() : ''

  if (id.length === 0) {
    return null
  }

  const userName = typeof entry.userName === 'string' && entry.userName.trim().length > 0
    ? entry.userName.trim()
    : id

  return {
    id,
    userName,
  }
}

const toSecurityRole = (entry: server.security.RoleSummary): SecurityRole | null => {
  const id = typeof entry.id === 'string' ? entry.id.trim() : ''

  if (id.length === 0) {
    return null
  }

  const name = typeof entry.name === 'string' && entry.name.trim().length > 0
    ? entry.name.trim()
    : id

  return {
    id,
    name,
  }
}

const toUserDetail = (entry: server.security.UserDetail, fallbackUserName: string): SecurityUserDetail => {
  const id = typeof entry.id === 'string' ? entry.id.trim() : ''
  const userName = typeof entry.userName === 'string' && entry.userName.trim().length > 0
    ? entry.userName.trim()
    : fallbackUserName

  return {
    id,
    userName,
    roles: toRoleNames(entry.roles),
    permissions: toRoleNames(entry.permissions),
  }
}

export const useSecurityStore = defineStore('security', {
  state: (): SecurityState => ({
    users: [],
    roles: [],
    selectedUserId: null,
    selectedRoleId: null,
    userDetailsById: {},
    usersByRoleId: {},
    loadingUsers: false,
    loadingRoles: false,
    loadingUserDetail: false,
    loadingRoleUsers: false,
  }),
  getters: {
    selectedUser: (state) => state.users.find((entry) => entry.id === state.selectedUserId) ?? null,
    selectedRole: (state) => state.roles.find((entry) => entry.id === state.selectedRoleId) ?? null,
    selectedUserDetail: (state) => {
      if (!state.selectedUserId) {
        return null
      }

      return state.userDetailsById[state.selectedUserId] ?? null
    },
    selectedRoleUsers: (state) => {
      if (!state.selectedRoleId) {
        return []
      }

      return state.usersByRoleId[state.selectedRoleId] ?? []
    },
  },
  actions: {
    async loadUsers() {
      this.loadingUsers = true

      try {
        const payload = await securityService.listUsers()
        this.users = payload
          .map(toSecurityUser)
          .filter((entry): entry is SecurityUser => entry !== null)

        if (this.selectedUserId && !this.users.some((entry) => entry.id === this.selectedUserId)) {
          this.selectedUserId = null
        }
      } finally {
        this.loadingUsers = false
      }
    },
    async loadRoles() {
      this.loadingRoles = true

      try {
        const payload = await securityService.listRoles()
        this.roles = payload
          .map(toSecurityRole)
          .filter((entry): entry is SecurityRole => entry !== null)

        if (this.selectedRoleId && !this.roles.some((entry) => entry.id === this.selectedRoleId)) {
          this.selectedRoleId = null
        }
      } finally {
        this.loadingRoles = false
      }
    },
    setSelectedUser(id: string | null) {
      this.selectedUserId = id
    },
    setSelectedRole(id: string | null) {
      this.selectedRoleId = id
    },
    async loadUserDetail(userId: string) {
      this.loadingUserDetail = true

      try {
        const user = this.users.find((entry) => entry.id === userId)
        const payload = await securityService.getUser(userId)
        this.userDetailsById[userId] = toUserDetail(payload, user?.userName ?? userId)
      } finally {
        this.loadingUserDetail = false
      }
    },
    async loadUsersForRole(roleId: string) {
      this.loadingRoleUsers = true

      try {
        const payload = await securityService.listUsersForRole(roleId)
        this.usersByRoleId[roleId] = payload
          .map(toSecurityUser)
          .filter((entry): entry is SecurityUser => entry !== null)
      } finally {
        this.loadingRoleUsers = false
      }
    },
    async createUser(payload: server.security.CreateUserData) {
      await securityService.createUser(payload)
      await this.loadUsers()
    },
    async updateUser(userId: string, payload: server.security.UpdateUserData) {
      await securityService.updateUser(userId, payload)
      await this.loadUsers()
      await this.loadUserDetail(userId)
    },
    async saveUserRoles(userId: string, roles: string[]) {
      const user = this.users.find((entry) => entry.id === userId)
      await securityService.updateUser(userId, {
        userName: user?.userName ?? userId,
        roles,
      })
      await this.loadUserDetail(userId)
      await this.loadRoles()

      if (this.selectedRoleId) {
        await this.loadUsersForRole(this.selectedRoleId)
      }
    },
    async deleteUser(userId: string) {
      await securityService.deleteUser(userId)
      delete this.userDetailsById[userId]

      if (this.selectedUserId === userId) {
        this.selectedUserId = null
      }

      await this.loadUsers()

      if (this.selectedRoleId) {
        await this.loadUsersForRole(this.selectedRoleId)
      }
    },
    async createRole(payload: server.security.CreateRoleData) {
      await securityService.createRole(payload)
      await this.loadRoles()
    },
    async updateRole(roleId: string, payload: server.security.UpdateRoleData) {
      await securityService.updateRole(roleId, payload)
      await this.loadRoles()

      if (this.selectedUserId) {
        await this.loadUserDetail(this.selectedUserId)
      }
    },
    async deleteRole(roleId: string) {
      await securityService.deleteRole(roleId)
      delete this.usersByRoleId[roleId]

      if (this.selectedRoleId === roleId) {
        this.selectedRoleId = null
      }

      await this.loadRoles()

      if (this.selectedUserId) {
        await this.loadUserDetail(this.selectedUserId)
      }
    },
  },
})
