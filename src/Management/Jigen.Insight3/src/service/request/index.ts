import type { AxiosResponse } from 'axios';
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
const SELF_HANDLED_URLS = ['/identity/login', '/metric/server-status/'];

function isSelfHandled(url?: string) {
  return Boolean(url && SELF_HANDLED_URLS.some(item => url.includes(item)));
}

/**
 * Jigen REST API request instance.
 *
 * - The base URL comes from the runtime settings (`public/settings.json`), resolved per request so
 *   the instance can be created before the settings are loaded.
 * - Auth is an ASP.NET Identity session cookie (`withCredentials`), not a bearer token.
 * - Responses are plain REST payloads (no `{ code, msg, data }` envelope): any 2xx is a success and
 *   the body is returned as-is.
 */
export const request = createFlatRequest(
  {
    withCredentials: true,
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

      return config;
    },
    isBackendSuccess() {
      // plain REST backend: axios only resolves 2xx responses, all of them are successes
      return true;
    },
    async onBackendFail() {
      return null;
    },
    onError(error) {
      const url = error.config?.url;
      const status = error.response?.status;

      // session expired or not authenticated: reset auth state, which redirects to login
      // (a 401 from the login endpoint itself means invalid credentials, handled by the form)
      if (status === 401 && !url?.includes('/identity/login')) {
        const authStore = useAuthStore();
        authStore.resetStore();

        showErrorMsg(request.state, $t('request.sessionExpired'));
        return;
      }

      // remaining errors of self-handled endpoints are rendered inline by their pages
      if (isSelfHandled(url)) {
        return;
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

      showErrorMsg(request.state, message);
    }
  }
);
