/**
 * Default theme settings — Jigen brand palette
 *
 * This is the single central place brand colors are wired into the app. Never hardcode these hex
 * values anywhere else; components/charts must read colors from the theme store instead.
 *
 * - primary (lime): #B5E61D
 * - info (violet): #7C5CFF
 * - success (teal): #00C9A7
 * - warning (orange): #FF5C39
 * - dark surface: #15170E, light surface: #F8FAF1 / #EFF3E2 (see `tokens` below)
 */
export const themeSettings: App.Theme.ThemeSetting = {
  themeScheme: 'dark',
  grayscale: false,
  colourWeakness: false,
  recommendColor: false,
  themeColor: '#B5E61D',
  themeRadius: 6,
  otherColor: {
    info: '#7C5CFF',
    success: '#00C9A7',
    warning: '#FF5C39',
    error: '#F5222D'
  },
  isInfoFollowPrimary: false,
  layout: {
    mode: 'vertical',
    scrollMode: 'content'
  },
  page: {
    animate: true,
    animateMode: 'fade-slide'
  },
  header: {
    height: 56,
    breadcrumb: {
      visible: true,
      showIcon: true
    },
    multilingual: {
      visible: true
    },
    globalSearch: {
      visible: true
    }
  },
  tab: {
    visible: true,
    cache: true,
    height: 44,
    mode: 'chrome',
    closeTabByMiddleClick: false
  },
  fixedHeaderAndTab: true,
  sider: {
    inverted: false,
    width: 220,
    collapsedWidth: 64,
    mixWidth: 90,
    mixCollapsedWidth: 64,
    mixChildMenuWidth: 200,
    autoSelectFirstMenu: false
  },
  footer: {
    visible: true,
    fixed: false,
    height: 48,
    right: true
  },
  watermark: {
    visible: false,
    text: 'Jigen Insight',
    enableUserName: false,
    enableTime: false,
    timeFormat: 'YYYY-MM-DD HH:mm'
  },
  tokens: {
    light: {
      // lime is too pale for text/icons on a light background; use a darker shade of the same hue
      // (~5.8:1 contrast on white) instead of the raw brand lime used on dark surfaces
      themeColors: {
        primary: '#546c0b'
      },
      colors: {
        container: 'rgb(248, 250, 241)',
        layout: 'rgb(239, 243, 226)',
        inverted: 'rgb(21, 23, 14)',
        'base-text': 'rgb(21, 23, 14)'
      },
      boxShadow: {
        header: '0 1px 2px rgb(21, 23, 14, 0.08)',
        sider: '2px 0 8px 0 rgb(21, 23, 14, 0.05)',
        tab: '0 1px 2px rgb(21, 23, 14, 0.08)'
      }
    },
    dark: {
      colors: {
        container: 'rgb(27, 29, 20)',
        layout: 'rgb(21, 23, 14)',
        'base-text': 'rgb(239, 243, 226)'
      }
    }
  }
};

/**
 * Override theme settings
 *
 * If publish new version, use `overrideThemeSettings` to override certain theme settings
 */
export const overrideThemeSettings: Partial<App.Theme.ThemeSetting> = {};
