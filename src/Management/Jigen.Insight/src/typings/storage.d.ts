/** The storage namespace */
declare namespace StorageType {
  interface Session {
    /** The theme color */
    themeColor: string;
    // /**
    //  * the theme settings
    //  */
    // themeSettings: App.Theme.ThemeSetting;
    /**
     * PKCE code verifier, stashed just before the browser navigates to `/connect/authorize` and
     * consumed by the `/auth/callback` page. Session-scoped: only needs to survive that one
     * redirect round trip within the same tab.
     */
    oauthCodeVerifier: string;
    /** Anti-CSRF `state` value paired with `oauthCodeVerifier`, checked against the callback's `state` */
    oauthState: string;
    /** Intended post-login destination, carried across the login -> authorize -> callback hop */
    oauthRedirect: string;
  }

  interface Local {
    /** The i18n language */
    lang: App.I18n.LangType;
    /**
     * OAuth2 bearer access token (OpenIddict authorization-code + PKCE). The ASP.NET Identity
     * session cookie is only used to complete `/connect/authorize`; every API call authenticates
     * with this token instead. See `store/modules/auth/shared.ts`.
     */
    token: string;
    /** Refresh token, exchanged for a new access/refresh pair when the access token is near expiry */
    refreshToken: string;
    /** Access token expiry, epoch ms (with a small safety skew subtracted) */
    tokenExpiresAt: number;
    /** Display name of the logged-in user (restore fallback while the userinfo call hasn't resolved yet) */
    userName: string;
    /** Fixed sider with mix-menu */
    mixSiderFixed: CommonType.YesOrNo;
    /** The theme color */
    themeColor: string;
    /** The dark mode */
    darkMode: boolean;
    /** The theme settings */
    themeSettings: App.Theme.ThemeSetting;
    /**
     * The override theme flags
     *
     * The value is the build time of the project
     */
    overrideThemeFlag: string;
    /** The global tabs */
    globalTabs: App.Global.Tab[];
    /** The backup theme setting before is mobile */
    backupThemeSettingBeforeIsMobile: {
      layout: UnionKey.ThemeLayoutMode;
      siderCollapse: boolean;
    };
    /** The last login user id */
    lastLoginUserId: string;
    /** Selected database name, shared across Databases/Collections/Workbench/Graph Explorer */
    currentDatabase: string;
  }
}
