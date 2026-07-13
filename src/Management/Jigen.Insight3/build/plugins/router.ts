import type { RouteMeta } from 'vue-router';
import ElegantVueRouter from '@elegant-router/vue/vite';
import type { RouteKey } from '@elegant-router/types';

export function setupElegantRouter() {
  return ElegantVueRouter({
    layouts: {
      base: 'src/layouts/base-layout/index.vue',
      blank: 'src/layouts/blank-layout/index.vue'
    },
    routePathTransformer(routeName, routePath) {
      const key = routeName as RouteKey;

      if (key === 'login') {
        const modules: UnionKey.LoginModule[] = ['pwd-login'];

        const moduleReg = modules.join('|');

        return `/login/:module(${moduleReg})?`;
      }

      // must match the redirect URI registered for the OpenIddict client exactly
      // (`JigenIdentity:DefaultClient:RedirectUris` in the server's appsettings.json)
      if (key === 'auth-callback') {
        return '/auth/callback';
      }

      return routePath;
    },
    onRouteMetaGen(routeName) {
      const key = routeName as RouteKey;

      const constantRoutes: RouteKey[] = ['login', 'auth-callback', '403', '404', '500'];
      // transient/exception pages that must never show up as a sidebar entry or menu item
      const hiddenRoutes: RouteKey[] = ['login', 'auth-callback', 'iframe-page'];

      const meta: Partial<RouteMeta> = {
        title: key,
        i18nKey: `route.${key}` as App.I18n.I18nKey
      };

      if (constantRoutes.includes(key)) {
        meta.constant = true;
      }

      if (hiddenRoutes.includes(key)) {
        meta.hideInMenu = true;
      }

      return meta;
    }
  });
}
