import { computed, reactive, ref } from 'vue';
import { useRoute } from 'vue-router';
import { defineStore } from 'pinia';
import { useLoading } from '@sa/hooks';
import { fetchLogin, fetchLogout, fetchUserinfo } from '@/service/api';
import { useRouterPush } from '@/hooks/common/router';
import { localStg } from '@/utils/storage';
import { SetupStoreId } from '@/enum';
import { $t } from '@/locales';
import { useRouteStore } from '../route';
import { useTabStore } from '../tab';
import { SESSION_MARKER, clearAuthStorage, getToken } from './shared';

export const useAuthStore = defineStore(SetupStoreId.Auth, () => {
  const route = useRoute();
  const authStore = useAuthStore();
  const routeStore = useRouteStore();
  const tabStore = useTabStore();
  const { toLogin, redirectFromLogin } = useRouterPush(false);
  const { loading: loginLoading, startLoading, endLoading } = useLoading();

  /**
   * Auth is an ASP.NET Identity session cookie (httpOnly, not readable from JS). This is a local
   * marker that a session was established; the server remains the source of truth — any 401 resets
   * the store and returns to the login page.
   */
  const token = ref(getToken());

  /** Login error message key, rendered inline by the login form */
  const loginError = ref('');

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

  /** Reset auth store (also ends the server session, best effort) */
  async function resetStore() {
    recordUserId();

    if (token.value) {
      // fire and forget: the cookie may already be invalid
      fetchLogout();
    }

    clearAuthStorage();

    authStore.$reset();

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
   * Login — establishes a session cookie via `POST /api/identity/login`, then loads the current
   * user from `GET /api/connect/userinfo`.
   *
   * @param userName User name
   * @param password Password
   * @param [redirect=true] Whether to redirect after login. Default is `true`
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

    // session cookie established: mark it locally so the route guard lets us in
    localStg.set('token', SESSION_MARKER);
    token.value = SESSION_MARKER;

    await loadUserIdentity(userName);

    // Check if the tab needs to be cleared
    const isClear = checkTabClear();
    let needRedirect = redirect;

    if (isClear) {
      // If the tab needs to be cleared,it means we don't need to redirect.
      needRedirect = false;
    }
    await redirectFromLogin(needRedirect);

    window.$notification?.success({
      title: $t('page.login.common.loginSuccess'),
      content: $t('page.login.common.welcomeBack', { userName: userInfo.userName }),
      duration: 4500
    });

    endLoading();
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
   * Resolve the current user identity.
   *
   * Prefers `GET /api/connect/userinfo` (OIDC claims). The endpoint currently returns 500 even
   * with a valid session cookie (server-side bug, reported), so on failure it falls back to the
   * user name the session was established with — real data, no mock. Roles/buttons are not
   * exposed by the API and stay empty.
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

    // persisted so a page reload can restore the display name without the userinfo endpoint
    localStg.set('userName', userInfo.userName);
  }

  /**
   * Restore a previously established session on app boot.
   *
   * The session cookie is httpOnly, so it cannot be validated eagerly here; it is validated
   * lazily instead — the first 401 from the API resets the store and returns to login.
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
    resetStore,
    login,
    initUserInfo
  };
});
