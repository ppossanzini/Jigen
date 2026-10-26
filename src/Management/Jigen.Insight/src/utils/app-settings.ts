/**
 * Runtime app settings, loaded from `public/settings.json` at startup (same pattern as Insight
 * v1/v2): the API base URL is deployment configuration, not build configuration, so it must not be
 * baked into the bundle.
 */
export interface AppSettings {
  api: {
    /** Backend API base url, e.g. `http://localhost:13223/api` */
    baseUrl: string;
    /**
     * Request timeout in milliseconds; `0` disables it. Kept long because a busy server (e.g.
     * during a massive ingest) can take well over the axios default to answer.
     */
    timeoutMs: number;
  };
}

const defaultSettings: AppSettings = {
  api: {
    baseUrl: 'http://localhost:13223/api',
    timeoutMs: 5 * 60 * 1000
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
    api: { ...defaultSettings.api, baseUrl: normalizeBaseUrl(defaultSettings.api.baseUrl) }
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

    const timeoutMs = payload.api?.timeoutMs;
    if (typeof timeoutMs === 'number' && Number.isFinite(timeoutMs) && timeoutMs >= 0) {
      runtimeSettings.api.timeoutMs = timeoutMs;
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
