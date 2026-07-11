import axios from 'axios'
import { useAuthStore } from '@/stores/auth-store'
import { getSettings } from './settings'

/**
 * Shared axios instance. Kept intentionally "dumb": it only resolves the
 * base URL/token per request. Response error handling (401 → logout redirect,
 * 500 → error page, toasts) is centralized in TanStack Query's QueryCache /
 * mutation `onError` (see `main.tsx`), so callers don't have to handle it twice.
 */
export const apiClient = axios.create({
  withCredentials: true,
  headers: {
    Accept: 'application/json',
    // Forces axios to JSON.stringify even plain string bodies (e.g. the
    // embeddings endpoints, whose schema is `application/json` + `type:
    // string`) instead of sending them as raw unquoted text.
    'Content-Type': 'application/json',
  },
})

apiClient.interceptors.request.use((config) => {
  config.baseURL = getSettings().api.baseUrl

  const token = useAuthStore.getState().auth.token
  if (token) {
    config.headers.set('Authorization', `Bearer ${token}`)
  }

  return config
})
