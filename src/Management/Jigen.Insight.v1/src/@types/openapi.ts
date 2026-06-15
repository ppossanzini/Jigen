export interface LoginData {
  userName?: string | null
  password?: string | null
}

export interface CreateUserData {
  userName?: string | null
  password?: string | null
  roles?: string[] | null
}

export interface UpdateUserData {
  userName?: string | null
  password?: string | null
  roles?: string[] | null
}

export interface CreateRoleData {
  name?: string | null
}

export interface UpdateRoleData {
  name?: string | null
}

export interface UserApiItem {
  id?: string | null
  userId?: string | null
  userName?: string | null
  username?: string | null
  roles?: string[] | null
  roleIds?: string[] | null
  [key: string]: unknown
}

export interface RoleApiItem {
  id?: string | null
  roleId?: string | null
  name?: string | null
  [key: string]: unknown
}

export interface LoginResponse {
  access_token?: string
  token?: string
  id_token?: string
  userName?: string
  username?: string
  [key: string]: unknown
}

export interface FeatureMenuItem {
  key: string
  routeName: string
  query?: Record<string, string>
}
