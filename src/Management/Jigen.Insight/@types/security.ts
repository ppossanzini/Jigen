export interface SecurityUserApiModel {
  id: string
  userName: string | null
  roles?: string[] | null
}

export interface SecurityRoleApiModel {
  id: string
  name: string | null
}

export interface CreateUserData {
  userName: string | null
  password: string | null
  roles: string[] | null
}

export interface UpdateUserData {
  userName: string | null
  password: string | null
  roles: string[] | null
}

export interface CreateRoleData {
  name: string | null
}

export interface UpdateRoleData {
  name: string | null
}
