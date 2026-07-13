import { computed, reactive, ref } from 'vue';
import { useRoute } from 'vue-router';
import { defineStore } from 'pinia';
import { useLoading } from '@sa/hooks';
import { fetchLogin, fetchLogout, fetchUserinfo } from '@/service/api';
import {
  buildAuthorizeUrl,
  exchangeCodeForToken,
  generateCodeChallenge,
  generateCodeVerifier,
  generateState,
  refreshAccessToken
} from '@/service/oauth';
import { useRouterPush } from '@/hooks/common/router';
import { localStg } from '@/utils/storage';
import { SetupStoreId } from '@/enum';
import { $t } from '@/locales';
import { useRouteStore } from '../route';
import { useTabStore } from '../tab';
import { useDatabaseStore } from '../database';
import {
  clearAuthStorage,
  consumeOAuthState,
  getRefreshToken,
  getToken,
  isTokenExpiringSoon,
  setTokens,
  stashOAuthState
} from './shared';

export const useAuthStore = defineStore(SetupStoreId.Auth, () => {
  const route = useRoute();
  const authStore = useAuthStore();
  const routeStore = useRouteStore();
  const tabStore = useTabStore();
  const databaseStore = useDatabaseStore();
  const { toLogin, routerPush, routerPushByKey } = useRouterPush(false);
  const { loading: loginLoading, startLoading, endLoading } = useLoading();
  const { loading: callbackLoading, startLoading: startCallbackLoading, endLoading: endCallbackLoading } = useLoading();

  /**
   * OAuth2 bearer access token (OpenIddict authorization-code + PKCE — see `service/oauth.ts`).
   * The ASP.NET Identity session cookie set by `login()`'s first step is only ever used to
   * complete `/connect/authorize`; every API call after that authenticates with this token
   * instead (`service/request/index.ts` attaches it as `Authorization: Bearer`).
   */
  const token = ref(getToken());

  /** Login error message key, rendered inline by the login form */
  const loginError = ref('');

  /** Callback-page error message key, rendered inline by `/auth/callback` */
  const callbackError = ref('');

  const userInfo: Api.Auth.UserInfo = reactive({
    userId: '',
    userName: '',
    roles: [],
    buttons: []
  });

  /** is super role in static route */
  const isStaticSuper = computed(() => {
    const { VITE_AUTH_ROUTE_MODE, VITE_STATIC_SUPER_ROLE } = import.meta.env;

    return VITE_AUTH_ROUTE_MODE === 'static' && userInfo.roles.includes(VITE_STATIC_SUPER_ROLE);
  });

  /** Is login */
  const isLogin = computed(() => Boolean(token.value));

  /** In-flight refresh, shared by any requests that race a proactive/reactive refresh */
  let refreshingPromise: Promise<string | null> | null = null;

  /** Reset auth store (also ends the server session, best effort) */
  async function resetStore() {
    recordUserId();

    if (token.value) {
      // fire and forget: drops the Identity cookie for hygiene; the token may already be invalid
      fetchLogout();
    }

    clearAuthStorage();

    authStore.$reset();
    databaseStore.reset();

    if (!route.meta.constant) {
      await toLogin();
    }

    tabStore.cacheTabs();
    routeStore.resetStore();
  }

  /** Record the user ID of the previous login session Used to compare with the current user ID on next login */
  function recordUserId() {
    if (!userInfo.userId) {
      return;
    }

    // Store current user ID locally for next login comparison
    localStg.set('lastLoginUserId', userInfo.userId);
  }

  /**
   * Check if current login user is different from previous login user If different, clear all tabs
   *
   * @returns {boolean} Whether to clear all tabs
   */
  function checkTabClear(): boolean {
    if (!userInfo.userId) {
      return false;
    }

    const lastLoginUserId = localStg.get('lastLoginUserId');

    // Clear all tabs if current user is different from previous user
    if (!lastLoginUserId || lastLoginUserId !== userInfo.userId) {
      localStg.remove('globalTabs');
      tabStore.clearTabs();

      localStg.remove('lastLoginUserId');
      return true;
    }

    localStg.remove('lastLoginUserId');
    return false;
  }

  /**
   * Login step 1 of 2 — establishes the ASP.NET Identity session cookie via `POST
   * /api/identity/login`, then hands off to a full browser navigation to `/connect/authorize`
   * (PKCE S256). This is a real page navigation, not a fetch/XHR: the cookie rides along
   * automatically and the server's redirect back to `/auth/callback` is followed natively,
   * which is also why this never has to deal with cross-origin CORS for the authorize step.
   *
   * `login()` does not itself mark the app as "logged in" — `completeOAuthCallback()` does that,
   * once `/auth/callback` has exchanged the returned code for a real access/refresh token pair.
   *
   * @param userName User name
   * @param password Password
   * @param [redirect=true] Whether to return to the originally intended page after login completes
   */
  async function login(userName: string, password: string, redirect = true) {
    startLoading();
    loginError.value = '';

    const { error } = await fetchLogin(userName, password);

    if (error) {
      loginError.value = getLoginErrorMessage(error.response?.status);
      endLoading();
      return;
    }

    const redirectTarget = redirect ? ((route.query?.redirect as string) ?? '') : '';

    const codeVerifier = generateCodeVerifier();
    const codeChallenge = await generateCodeChallenge(codeVerifier);
    const state = generateState();
    stashOAuthState(codeVerifier, state, redirectTarget);

    window.location.assign(buildAuthorizeUrl(codeChallenge, state));
    // no endLoading(): the page is navigating away to the server and back to /auth/callback
  }

  function getLoginErrorMessage(status?: number) {
    if (status === 401) {
      return $t('page.login.common.invalidCredentials');
    }

    if (status === 400) {
      return $t('form.required');
    }

    // no usable HTTP status: network failure / server down
    return $t('request.serverUnreachable');
  }

  /**
   * Login step 2 of 2 — called by the `/auth/callback` page once the browser lands back with
   * `?code=&state=`. Validates `state` against the value stashed before the redirect, exchanges
   * the code (+ stashed PKCE verifier) for a real token pair, then completes the same
   * post-login flow (tab-clear check, redirect, welcome toast) `login()` used to do directly
   * before the flow spanned a page navigation.
   *
   * @returns Whether the callback completed successfully
   */
  async function completeOAuthCallback(code: string, state: string): Promise<boolean> {
    startCallbackLoading();
    callbackError.value = '';

    const stash = consumeOAuthState();

    if (!code || !stash.codeVerifier || !state || state !== stash.state) {
      callbackError.value = $t('page.login.common.invalidCredentials');
      endCallbackLoading();
      return false;
    }

    try {
      const tokens = await exchangeCodeForToken(code, stash.codeVerifier);
      setTokens(tokens.access_token, tokens.refresh_token, tokens.expires_in);
      token.value = tokens.access_token;
    } catch {
      callbackError.value = $t('request.serverUnreachable');
      endCallbackLoading();
      return false;
    }

    await loadUserIdentity('');

    const isClear = checkTabClear();

    if (stash.redirect && !isClear) {
      await routerPush(stash.redirect);
    } else {
      await routerPushByKey('root');
    }

    // the transient callback page shouldn't linger as a tab once the redirect above lands
    tabStore.removeTabByRouteName('auth-callback');

    window.$notification?.success({
      title: $t('page.login.common.loginSuccess'),
      content: $t('page.login.common.welcomeBack', { userName: userInfo.userName }),
      duration: 4500
    });

    endCallbackLoading();
    return true;
  }

  /**
   * Resolve the current user identity from `GET /api/connect/userinfo` (Bearer-authenticated).
   * Falls back to the name the session was established with if the call fails for any reason —
   * real data either way, never a mock.
   *
   * @param fallbackUserName User name to use when the userinfo endpoint fails
   */
  async function loadUserIdentity(fallbackUserName: string) {
    const { data: info, error } = await fetchUserinfo();

    if (!error) {
      Object.assign(userInfo, {
        userId: info.sub,
        userName: info.preferred_username,
        roles: [],
        buttons: []
      } satisfies Api.Auth.UserInfo);
    } else {
      Object.assign(userInfo, {
        userId: '',
        userName: fallbackUserName,
        roles: [],
        buttons: []
      } satisfies Api.Auth.UserInfo);
    }

    // persisted so a page reload can restore the display name before userinfo resolves
    localStg.set('userName', userInfo.userName);
  }

  /**
   * Exchange the refresh token for a new access/refresh pair. Concurrent callers (e.g. several
   * requests firing around the same expiry) share the same in-flight exchange instead of each
   * firing their own `grant_type=refresh_token` request.
   *
   * @returns The new access token, or `null` if the refresh itself failed (session is over)
   */
  async function tryRefreshToken(): Promise<string | null> {
    if (refreshingPromise) {
      return refreshingPromise;
    }

    const refreshToken = getRefreshToken();

    if (!refreshToken) {
      return null;
    }

    refreshingPromise = (async () => {
      try {
        const tokens = await refreshAccessToken(refreshToken);
        setTokens(tokens.access_token, tokens.refresh_token, tokens.expires_in);
        token.value = tokens.access_token;
        return tokens.access_token;
      } catch {
        return null;
      } finally {
        refreshingPromise = null;
      }
    })();

    return refreshingPromise;
  }

  /**
   * Called from the request layer's `onRequest` hook before every API call: proactively refreshes
   * the access token when it's expired or close to it, so most requests never have to take the
   * reactive 401-then-refresh-then-retry path at all.
   *
   * @returns The token to send as the Bearer credential (possibly just-refreshed)
   */
  async function ensureFreshToken(): Promise<string> {
    if (token.value && isTokenExpiringSoon()) {
      await tryRefreshToken();
    }

    return token.value;
  }

  /**
   * Restore a previously established session on app boot. The access token is validated lazily —
   * the first 401 from the API goes through the refresh-then-retry path, falling back to a full
   * reset (and the login page) only if the refresh token is also gone/invalid.
   */
  async function initUserInfo() {
    const maybeToken = getToken();

    if (maybeToken) {
      token.value = maybeToken;
      await loadUserIdentity(localStg.get('userName') || '');
    }
  }

  return {
    token,
    userInfo,
    isStaticSuper,
    isLogin,
    loginLoading,
    loginError,
    callbackLoading,
    callbackError,
    resetStore,
    login,
    completeOAuthCallback,
    tryRefreshToken,
    ensureFreshToken,
    initUserInfo
  };
});
