import type {
  AppSummary,
  CreateRoleData,
  CreateUserData,
  RoleSummary,
  UpdateRoleData,
  UpdateUserData,
  UserDetail,
  UserSummary
} from '../api-types';
import { request } from '../request';

// Users

/** List users — `GET /api/users` (requires the security admin role) */
export function fetchListUsers() {
  return request<UserSummary[]>({ url: '/users' });
}

/** User detail (roles, permissions) — `GET /api/users/{id}` */
export function fetchUserDetail(id: string) {
  return request<UserDetail>({ url: `/users/${encodeURIComponent(id)}` });
}

/** Create a user — `POST /api/users` (204) */
export function fetchCreateUser(data: CreateUserData) {
  return request<null>({
    url: '/users',
    method: 'post',
    data
  });
}

/** Update a user — `PUT /api/users/{id}` (returns the updated detail) */
export function fetchUpdateUser(id: string, data: UpdateUserData) {
  return request<UserDetail>({
    url: `/users/${encodeURIComponent(id)}`,
    method: 'put',
    data
  });
}

/** Delete a user — `DELETE /api/users/{id}` (204) */
export function fetchDeleteUser(id: string) {
  return request<null>({
    url: `/users/${encodeURIComponent(id)}`,
    method: 'delete'
  });
}

// Roles

/** List roles — `GET /api/roles` */
export function fetchListRoles() {
  return request<RoleSummary[]>({ url: '/roles' });
}

/** Users in a role — `GET /api/roles/{id}/users` */
export function fetchUsersInRole(id: string) {
  return request<UserSummary[]>({ url: `/roles/${encodeURIComponent(id)}/users` });
}

/** Create a role — `POST /api/roles` (204) */
export function fetchCreateRole(data: CreateRoleData) {
  return request<null>({
    url: '/roles',
    method: 'post',
    data
  });
}

/** Update a role — `PUT /api/roles/{id}` (204) */
export function fetchUpdateRole(id: string, data: UpdateRoleData) {
  return request<null>({
    url: `/roles/${encodeURIComponent(id)}`,
    method: 'put',
    data
  });
}

/** Delete a role — `DELETE /api/roles/{id}` (204) */
export function fetchDeleteRole(id: string) {
  return request<null>({
    url: `/roles/${encodeURIComponent(id)}`,
    method: 'delete'
  });
}

// Apps

/** List OpenIddict client applications — `GET /api/identity/apps` */
export function fetchListApps() {
  return request<AppSummary[]>({ url: '/identity/apps' });
}
