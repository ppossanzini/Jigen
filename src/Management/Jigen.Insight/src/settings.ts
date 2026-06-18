export interface AppSettings {
  api: {
    baseUrl: string
  }
  oidc: {
    clientId: string
    redirectUri: string
    scope: string
  }
}

const defaultSettings: AppSettings = {
  api: {
    baseUrl: 'http://localhost:13223',
  },
  oidc: {
    clientId: 'jigen-insight-spa',
    redirectUri: '',
    scope: 'openid jigen_api',
  },
}

let runtimeSettings: AppSettings = {
  api: {
    baseUrl: defaultSettings.api.baseUrl,
  },
  oidc: {
    clientId: defaultSettings.oidc.clientId,
    redirectUri: defaultSettings.oidc.redirectUri,
    scope: defaultSettings.oidc.scope,
  },
}

const normalizeBaseUrl = (value: string): string => {
  const trimmed = value.trim()
  return trimmed.replace(/\/+$/, '')
}

const mergeSettings = (incoming: Partial<AppSettings>): AppSettings => {
  runtimeSettings = {
    ...runtimeSettings,
    ...incoming,
    api: Object.assign({}, runtimeSettings.api, incoming.api),
    oidc: Object.assign({}, runtimeSettings.oidc, incoming.oidc),
  }

  runtimeSettings.api.baseUrl = normalizeBaseUrl(runtimeSettings.api.baseUrl)

  return runtimeSettings
}

export const loadSettings = async (): Promise<AppSettings> => {
  runtimeSettings = {
    api: {
      baseUrl: normalizeBaseUrl(defaultSettings.api.baseUrl),
    },
    oidc: {
      clientId: defaultSettings.oidc.clientId,
      redirectUri: defaultSettings.oidc.redirectUri,
      scope: defaultSettings.oidc.scope,
    },
  }

  try {
    const response = await fetch('/settings.json', {
      cache: 'no-store',
    })

    if (!response.ok) {
      return runtimeSettings
    }

    const payload = (await response.json()) as Partial<AppSettings>
    return mergeSettings(payload)
  } catch {
    return runtimeSettings
  }
}

export const getSettings = (): AppSettings => runtimeSettings

export const buildApiUrl = (path: string): string => {
  const sanitizedPath = path.startsWith('/') ? path : `/${path}`
  return `${runtimeSettings.api.baseUrl}${sanitizedPath}`
}
