import type { UserinfoResponse } from '../api-types';
import { request } from '../request';

/**
 * Login — `POST /api/identity/login`
 *
 * Establishes an ASP.NET Identity session cookie (`withCredentials: true`, the one call in this
 * service module that isn't bearer-authenticated — see `service/request/index.ts`). That cookie
 * is only used to complete the `/connect/authorize` step right after this call succeeds; it is
 * not the app's ongoing auth mechanism. Returns 204 No Content on success, 400 for missing
 * fields, 401 for invalid credentials.
 *
 * @param userName User name
 * @param password Password
 */
export function fetchLogin(userName: string, password: string) {
  return request<null>({
    url: '/identity/login',
    method: 'post',
    withCredentials: true,
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
    method: 'post',
    withCredentials: true
  });
}

/** Current user — `GET /api/connect/userinfo` (bearer-authenticated, like every other API call) */
export function fetchUserinfo() {
  return request<UserinfoResponse>({ url: '/connect/userinfo' });
}
