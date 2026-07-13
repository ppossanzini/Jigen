import { localStg, sessionStg } from '@/utils/storage';

/** How far ahead of the real `expires_in` to treat a token as "expiring soon" (proactive refresh) */
const EXPIRY_SKEW_MS = 30_000;

/** Get the current OAuth2 bearer access token (empty string if not logged in) */
export function getToken() {
  return localStg.get('token') || '';
}

/** Get the current refresh token (empty string if none stored) */
export function getRefreshToken() {
  return localStg.get('refreshToken') || '';
}

/** Persist a fresh access/refresh token pair from a token-endpoint response */
export function setTokens(accessToken: string, refreshToken: string | undefined, expiresInSeconds: number) {
  localStg.set('token', accessToken);

  if (refreshToken) {
    localStg.set('refreshToken', refreshToken);
  }

  localStg.set('tokenExpiresAt', Date.now() + expiresInSeconds * 1000 - EXPIRY_SKEW_MS);
}

/** Whether the stored access token is expired or close enough to warrant a proactive refresh */
export function isTokenExpiringSoon() {
  const expiresAt = localStg.get('tokenExpiresAt');

  if (!expiresAt) {
    return false;
  }

  return Date.now() >= expiresAt;
}

/** Clear all auth storage (tokens + cached display name) */
export function clearAuthStorage() {
  localStg.remove('token');
  localStg.remove('refreshToken');
  localStg.remove('tokenExpiresAt');
  localStg.remove('userName');
}

/**
 * Stash PKCE/anti-CSRF state just before navigating the browser away to `/connect/authorize`.
 * `sessionStorage` survives the redirect round trip (same tab) and self-cleans on tab close.
 */
export function stashOAuthState(codeVerifier: string, state: string, redirect: string) {
  sessionStg.set('oauthCodeVerifier', codeVerifier);
  sessionStg.set('oauthState', state);
  sessionStg.set('oauthRedirect', redirect);
}

/** Read back and clear the stashed OAuth state once `/auth/callback` has consumed it */
export function consumeOAuthState() {
  const codeVerifier = sessionStg.get('oauthCodeVerifier') || '';
  const state = sessionStg.get('oauthState') || '';
  const redirect = sessionStg.get('oauthRedirect') || '';

  sessionStg.remove('oauthCodeVerifier');
  sessionStg.remove('oauthState');
  sessionStg.remove('oauthRedirect');

  return { codeVerifier, state, redirect };
}
