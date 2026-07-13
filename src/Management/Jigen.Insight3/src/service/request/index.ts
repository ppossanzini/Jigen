import type { AxiosInstance, AxiosResponse, InternalAxiosRequestConfig } from 'axios';
import { createFlatRequest } from '@sa/axios';
import { useAuthStore } from '@/store/modules/auth';
import { getAppSettings } from '@/utils/app-settings';
import { $t } from '@/locales';
import { showErrorMsg } from './shared';
import type { RequestInstanceState } from './type';

/**
 * Requests whose non-401 errors are fully handled by the caller (no global error toast): the login
 * form renders its error inline, and the Overview dashboard owns its polling error state.
 */
const SELF_HANDLED_URLS = ['/identity/login', '/identity/logout', '/metric/server-status/'];

function isSelfHandled(url?: string) {
  return Boolean(url && SELF_HANDLED_URLS.some(item => url.includes(item)));
}

/**
 * `/identity/login` and `/identity/logout` are the only calls that use the ASP.NET Identity
 * session cookie instead of a bearer token (see `service/api/auth.ts`, which sets
 * `withCredentials: true` on just those two). They're excluded from the 401 refresh-and-retry
 * path below: a 401 there means "invalid credentials" / "already logged out", not "expired
 * token", and retrying with a refreshed bearer token would never help.
 */
function isCookieAuthEndpoint(url?: string) {
  return Boolean(url && (url.includes('/identity/login') || url.includes('/identity/logout')));
}

type RetriableConfig = InternalAxiosRequestConfig & { _retriedAfterRefresh?: boolean };

/**
 * `onError` needs the raw axios instance (to retry once after a refresh) and the request state
 * (to dedupe error toasts) — but referencing `request.instance`/`request.state` from inside
 * `request`'s own initializer is a type-inference cycle (TS can't know `request`'s type while
 * still evaluating the expression that produces it). These are populated right after `request` is
 * constructed below and are only ever read once real traffic starts flowing, well after that.
 */
let axiosInstance: AxiosInstance | null = null;
let requestState: RequestInstanceState | null = null;

/**
 * Jigen REST API request instance.
 *
 * - The base URL comes from the runtime settings (`public/settings.json`), resolved per request so
 *   the instance can be created before the settings are loaded.
 * - Auth is an OAuth2 bearer access token (OpenIddict authorization-code + PKCE — see
 *   `service/oauth.ts` and `store/modules/auth/`), attached below in `onRequest`. The ASP.NET
 *   Identity session cookie is only used by `/identity/login` and `/identity/logout`
 *   (`withCredentials: true` set per-call in `service/api/auth.ts`); every other call does not
 *   send credentials.
 * - Responses are plain REST payloads (no `{ code, msg, data }` envelope): any 2xx is a success and
 *   the body is returned as-is.
 */
export const request = createFlatRequest(
  {
    withCredentials: false,
    headers: {
      Accept: 'application/json',
      // forces axios to JSON.stringify even plain string bodies instead of sending raw text
      'Content-Type': 'application/json'
    }
  },
  {
    defaultState: {
      errMsgStack: []
    } as RequestInstanceState,
    transform(response: AxiosResponse) {
      return response.data;
    },
    async onRequest(config) {
      config.baseURL = getAppSettings().api.baseUrl;

      if (!isCookieAuthEndpoint(config.url)) {
        const authStore = useAuthStore();
        // proactively refreshes when the token is close to expiry, so most calls never hit the
        // reactive 401-then-refresh path below at all
        const token = await authStore.ensureFreshToken();

        if (token) {
          config.headers.set('Authorization', `Bearer ${token}`);
        }
      }

      return config;
    },
    isBackendSuccess() {
      // plain REST backend: axios only resolves 2xx responses, all of them are successes
      return true;
    },
    async onBackendFail() {
      return null;
    },
    async onError(error) {
      const config = error.config as RetriableConfig | undefined;
      const url = config?.url;
      const status = error.response?.status;

      // expired/invalid bearer token: refresh once and silently retry the original request: the
      // caller never sees this failure unless the refresh itself also fails
      if (status === 401 && config && axiosInstance && !isCookieAuthEndpoint(url) && !config._retriedAfterRefresh) {
        const authStore = useAuthStore();
        const newToken = await authStore.tryRefreshToken();

        if (newToken) {
          config._retriedAfterRefresh = true;
          config.headers = { ...config.headers, Authorization: `Bearer ${newToken}` } as typeof config.headers;

          try {
            return await axiosInstance.request(config);
          } catch {
            // retry failed too: fall through to the full reset below
          }
        }
      }

      // session expired or not authenticated (including a failed refresh above): reset auth
      // state, which redirects to login (a 401 from the login endpoint itself means invalid
      // credentials, handled inline by the form instead)
      if (status === 401 && !isCookieAuthEndpoint(url)) {
        const authStore = useAuthStore();
        authStore.resetStore();

        if (requestState) {
          showErrorMsg(requestState, $t('request.sessionExpired'));
        }

        return undefined;
      }

      // remaining errors of self-handled endpoints are rendered inline by their pages
      if (isSelfHandled(url)) {
        return undefined;
      }

      let message: string;

      if (!error.response) {
        // no HTTP response at all: server unreachable / network failure
        message = $t('request.serverUnreachable');
      } else if (typeof error.response.data === 'string' && error.response.data) {
        // Jigen controllers return plain-string reasons for 4xx (BadRequest/Conflict/NotFound)
        message = error.response.data;
      } else {
        message = error.message;
      }

      if (requestState) {
        showErrorMsg(requestState, message);
      }

      return undefined;
    }
  }
);

axiosInstance = request.instance;
requestState = request.state;
