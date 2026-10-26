import type { DatabaseDetails, DatabaseUserInfo, SetDatabaseUsersData } from '../api-types';
import { request } from '../request';

/** List database names — `GET /api/database` */
export function fetchListDatabases() {
  return request<string[]>({ url: '/database' });
}

/**
 * Create a database — `POST /api/database?name=`
 *
 * @param name Database name
 */
export function fetchCreateDatabase(name: string) {
  return request<null>({
    url: '/database',
    method: 'post',
    params: { name }
  });
}

/**
 * Delete a database — `DELETE /api/database?name=&deletefiles=`
 *
 * @param name Database name
 * @param deletefiles Also delete the database files on disk (server default: true)
 */
export function fetchDeleteDatabase(name: string, deletefiles = true) {
  return request<null>({
    url: '/database',
    method: 'delete',
    params: { name, deletefiles }
  });
}

/**
 * Database details (storage breakdown, collections, users) — `GET /api/database/{name}/details`
 *
 * @param name Database name
 */
export function fetchDatabaseDetails(name: string) {
  return request<DatabaseDetails>({ url: `/database/${encodeURIComponent(name)}/details` });
}

/**
 * Compact a database: data files, vector index and sparse segments — `POST /api/database/{name}/shrink`
 *
 * Ingestion pauses while it runs; the server answers 409 when a shrink is already running.
 *
 * @param name Database name
 */
export function fetchShrinkDatabase(name: string) {
  return request<null>({
    url: `/database/${encodeURIComponent(name)}/shrink`,
    method: 'post',
    // a shrink rewrites every file of the database: it can take far longer than the default timeout
    timeout: 0
  });
}

/**
 * Users assigned to a database — `GET /api/database/{name}/users`
 *
 * @param name Database name
 */
export function fetchDatabaseUsers(name: string) {
  return request<DatabaseUserInfo[]>({ url: `/database/${encodeURIComponent(name)}/users` });
}

/**
 * Replace the users assigned to a database — `PUT /api/database/{name}/users`
 *
 * @param name Database name
 * @param data Users to assign
 */
export function fetchSetDatabaseUsers(name: string, data: SetDatabaseUsersData) {
  return request<DatabaseUserInfo[]>({
    url: `/database/${encodeURIComponent(name)}/users`,
    method: 'put',
    data
  });
}
