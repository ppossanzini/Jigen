import type {
  CreateRoleData,
  CreateUserData,
  UpdateRoleData,
  UpdateUserData,
} from '@/@types/openapi'

export interface RoleItem {
  id: string
  name: string
}

export interface UserItem {
  id: string
  userName: string
  roles: string[]
}

export type CreateUserPayload = CreateUserData
export type UpdateUserPayload = UpdateUserData
export type CreateRolePayload = CreateRoleData
export type UpdateRolePayload = UpdateRoleData
