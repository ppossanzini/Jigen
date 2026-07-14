/**
 * Typed aliases over the generated OpenAPI schema (`./api-schema.d.ts`).
 *
 * The schema file is generated with `pnpm gen:api-types` against a running Jigen server
 * (http://localhost:13223/openapi/v1.json). Do not edit the schema by hand; regenerate it.
 */
import type { components } from './api-schema';

export type AppSummary = components['schemas']['AppSummary'];
export type CollectionIndexInfo = components['schemas']['CollectionIndexInfo'];
export type CollectionInfo = components['schemas']['CollectionInfo'];
export type CollectionSearchResult = components['schemas']['CollectionSearchResult'];
export type CollectionSearchResultItem = components['schemas']['CollectionSearchResultItem'];
export type CollectionStatus = components['schemas']['CollectionStatus'];
export type CreateClientData = components['schemas']['CreateClientData'];
export type CreateClientResponse = components['schemas']['CreateClientResponse'];
export type CreateRoleData = components['schemas']['CreateRoleData'];
export type CreateUserData = components['schemas']['CreateUserData'];
export type DatabaseDetails = components['schemas']['DatabaseDetails'];
export type DatabaseStatus = components['schemas']['DatabaseStatus'];
export type DatabaseUserInfo = components['schemas']['DatabaseUserInfo'];
export type DocumentPayload = components['schemas']['DocumentPayload'];
export type GetCollectionGraphRequest = components['schemas']['GetCollectionGraphRequest'];
export type IndexGraphEdge = components['schemas']['IndexGraphEdge'];
export type IndexGraphNode = components['schemas']['IndexGraphNode'];
export type IndexGraphSnapshot = components['schemas']['IndexGraphSnapshot'];
export type LoginData = components['schemas']['LoginData'];
export type ProblemDetails = components['schemas']['ProblemDetails'];
export type RoleSummary = components['schemas']['RoleSummary'];
export type SearchCollectionsData = components['schemas']['SearchCollectionsData'];
export type SearchCollectionsResult = components['schemas']['SearchCollectionsResult'];
export type ServerStatusHistory = components['schemas']['ServerStatusHistory'];
export type ServerStatusSample = components['schemas']['ServerStatusSample'];
export type SetDatabaseUsersData = components['schemas']['SetDatabaseUsersData'];
export type UpdateRoleData = components['schemas']['UpdateRoleData'];
export type UpdateUserData = components['schemas']['UpdateUserData'];
export type UserDetail = components['schemas']['UserDetail'];
export type UserSummary = components['schemas']['UserSummary'];

/**
 * Key interpretation for document endpoints. Matches `CollectionsController.TryResolveKey`: guid =
 * 16 bytes, long = 8, int = 4, otherwise UTF-8 string; omitted = server auto-detection. The schema
 * types the `keyType` query param as a plain `string`; this union narrows it to the valid values.
 */
export type DocumentKeyType = 'string' | 'int' | 'long' | 'guid';

/** Fixed windows supported by `GET /api/metric/server-status/{window}` */
export type ServerStatusWindow = '1m' | '5m' | '10m' | '1h';

/** Response of `GET /api/connect/userinfo` (OpenID Connect standard claims subset) */
export interface UserinfoResponse {
  /** User id */
  sub: string;
  /** User name */
  preferred_username: string;
}
