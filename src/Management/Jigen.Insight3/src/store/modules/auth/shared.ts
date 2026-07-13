import { localStg } from '@/utils/storage';

/**
 * Marker stored in place of a bearer token: the real credential is an httpOnly session cookie the
 * client cannot read. Presence of the marker means "a session was established"; the server is the
 * source of truth (401 responses reset the auth store).
 */
export const SESSION_MARKER = 'cookie-session';

/** Get token */
export function getToken() {
  return localStg.get('token') || '';
}

/** Clear auth storage */
export function clearAuthStorage() {
  localStg.remove('token');
  localStg.remove('userName');
}
