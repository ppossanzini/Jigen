declare namespace server.security {
  interface UserSummary {
    id?: string | null
    userName?: string | null
  }

  interface UserDetail {
    id?: string | null
    userName?: string | null
    roles?: string[] | null
    permissions?: string[] | null
  }

  interface RoleSummary {
    id?: string | null
    name?: string | null
  }

  interface CreateUserData {
    userName?: string | null
    password?: string | null
    roles?: string[] | null
  }

  interface UpdateUserData {
    userName?: string | null
    password?: string | null
    roles?: string[] | null
  }

  interface CreateRoleData {
    name?: string | null
  }

  interface UpdateRoleData {
    name?: string | null
  }
}
