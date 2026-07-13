import type { ServerStatusHistory, ServerStatusWindow } from '../api-types';
import { request } from '../request';

/**
 * Server status history — `GET /api/metric/server-status/{window}`
 *
 * Samples include CPU %, memory, and per-database/per-collection sizes and HNSW stats.
 *
 * @param window One of the fixed windows: 1m | 5m | 10m | 1h
 */
export function fetchServerStatus(window: ServerStatusWindow) {
  return request<ServerStatusHistory>({ url: `/metric/server-status/${window}` });
}
