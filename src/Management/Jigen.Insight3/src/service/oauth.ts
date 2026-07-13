import axios from 'axios';
import { getAppSettings } from '@/utils/app-settings';

/**
 * OpenIddict authorization-code + PKCE flow against the Jigen server (`src/Server/Modules/Identity`).
 *
 * These calls deliberately bypass the shared `request` instance (`service/request/index.ts`): the
 * token endpoint speaks `application/x-www-form-urlencoded`, not JSON, and neither call should go
 * through the shared 401/refresh/error-toast interceptor chain (that chain is for authenticated
 * business calls; a failed token exchange is handled inline by the caller instead).
 */

const CLIENT_ID = 'jigen-insight-spa';
const SCOPE = 'openid offline_access';

export interface TokenResponse {
  access_token: string;
  token_type: string;
  expires_in: number;
  refresh_token?: string;
  scope?: string;
}

function connectUrl(path: string): string {
  return `${getAppSettings().api.baseUrl}/connect/${path}`;
}

function redirectUri(): string {
  return `${window.location.origin}/auth/callback`;
}

function base64UrlEncode(bytes: Uint8Array): string {
  let binary = '';
  bytes.forEach(byte => {
    binary += String.fromCharCode(byte);
  });

  return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

/** Cryptographically random PKCE code verifier (43-128 chars per RFC 7636; this yields 86) */
export function generateCodeVerifier(): string {
  const bytes = new Uint8Array(64);
  crypto.getRandomValues(bytes);

  return base64UrlEncode(bytes);
}

/** S256 code challenge for a verifier — `base64url(sha256(verifier))`, as the server requires */
export async function generateCodeChallenge(verifier: string): Promise<string> {
  const digest = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(verifier));

  return base64UrlEncode(new Uint8Array(digest));
}

/** Random anti-CSRF `state` value, checked against the value echoed back to `/auth/callback` */
export function generateState(): string {
  const bytes = new Uint8Array(16);
  crypto.getRandomValues(bytes);

  return base64UrlEncode(bytes);
}

/**
 * Full `/connect/authorize` URL for a top-level browser navigation (not a fetch/XHR — the
 * authorize step relies on the ASP.NET Identity session cookie and ends in a redirect, both of
 * which a plain navigation handles natively without any CORS involvement).
 */
export function buildAuthorizeUrl(codeChallenge: string, state: string): string {
  const params = new URLSearchParams({
    client_id: CLIENT_ID,
    response_type: 'code',
    scope: SCOPE,
    redirect_uri: redirectUri(),
    code_challenge: codeChallenge,
    code_challenge_method: 'S256',
    state
  });

  return `${connectUrl('authorize')}?${params.toString()}`;
}

/** `POST /connect/token` with `grant_type=authorization_code` — exchanges the code for tokens */
export async function exchangeCodeForToken(code: string, codeVerifier: string): Promise<TokenResponse> {
  const body = new URLSearchParams({
    grant_type: 'authorization_code',
    code,
    redirect_uri: redirectUri(),
    client_id: CLIENT_ID,
    code_verifier: codeVerifier
  });

  const response = await axios.post<TokenResponse>(connectUrl('token'), body, {
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' }
  });

  return response.data;
}

/** `POST /connect/token` with `grant_type=refresh_token` — exchanges a refresh token for a new pair */
export async function refreshAccessToken(refreshToken: string): Promise<TokenResponse> {
  const body = new URLSearchParams({
    grant_type: 'refresh_token',
    refresh_token: refreshToken,
    client_id: CLIENT_ID
  });

  const response = await axios.post<TokenResponse>(connectUrl('token'), body, {
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' }
  });

  return response.data;
}
