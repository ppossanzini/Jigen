import type { UserinfoResponse } from '../api-types';
import { request } from '../request';

/**
 * Login — `POST /api/identity/login`
 *
 * Establishes an ASP.NET Identity session cookie. Returns 204 No Content on success, 400 for
 * missing fields, 401 for invalid credentials.
 *
 * @param userName User name
 * @param password Password
 */
export function fetchLogin(userName: string, password: string) {
  return request<null>({
    url: '/identity/login',
    method: 'post',
    data: {
      userName,
      password
    }
  });
}

/** Logout — `POST /api/identity/logout` (clears the session cookie, 204) */
export function fetchLogout() {
  return request<null>({
    url: '/identity/logout',
    method: 'post'
  });
}

/** Current user — `GET /api/connect/userinfo` (requires an authenticated session) */
export function fetchUserinfo() {
  return request<UserinfoResponse>({ url: '/connect/userinfo' });
}
