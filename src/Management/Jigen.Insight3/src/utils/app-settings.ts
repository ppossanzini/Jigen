/**
 * Runtime app settings, loaded from `public/settings.json` at startup (same pattern as Insight
 * v1/v2): the API base URL is deployment configuration, not build configuration, so it must not be
 * baked into the bundle.
 */
export interface AppSettings {
  api: {
    /** Backend API base url, e.g. `http://localhost:13223/api` */
    baseUrl: string;
  };
}

const defaultSettings: AppSettings = {
  api: {
    baseUrl: 'http://localhost:13223/api'
  }
};

let runtimeSettings: AppSettings = {
  api: { ...defaultSettings.api }
};

function normalizeBaseUrl(value: string): string {
  return value.trim().replace(/\/+$/, '');
}

/** Fetch `settings.json` and merge it over the defaults. Failures fall back to the defaults. */
export async function loadAppSettings(): Promise<AppSettings> {
  runtimeSettings = {
    api: { baseUrl: normalizeBaseUrl(defaultSettings.api.baseUrl) }
  };

  try {
    const response = await fetch(`${import.meta.env.VITE_BASE_URL || '/'}settings.json`, { cache: 'no-store' });

    if (!response.ok) {
      return runtimeSettings;
    }

    const payload = (await response.json()) as Partial<AppSettings>;

    if (payload.api?.baseUrl) {
      runtimeSettings.api.baseUrl = normalizeBaseUrl(payload.api.baseUrl);
    }

    return runtimeSettings;
  } catch {
    return runtimeSettings;
  }
}

/** Get the current runtime settings (call `loadAppSettings` once at startup first) */
export function getAppSettings(): AppSettings {
  return runtimeSettings;
}
