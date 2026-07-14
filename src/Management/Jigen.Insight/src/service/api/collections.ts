import type {
  CollectionInfo,
  DocumentKeyType,
  DocumentPayload,
  GetCollectionGraphRequest,
  IndexGraphSnapshot,
  SearchCollectionsData,
  SearchCollectionsResult
} from '../api-types';
import { request } from '../request';

function collectionsBase(dbname: string) {
  return `/database/${encodeURIComponent(dbname)}/collections`;
}

function documentUrl(dbname: string, collection: string, key: string) {
  return `${collectionsBase(dbname)}/${encodeURIComponent(collection)}/documents/${encodeURIComponent(key)}`;
}

/** List collection names of a database — `GET /api/database/{dbname}/collections` */
export function fetchListCollections(dbname: string) {
  return request<string[]>({ url: collectionsBase(dbname) });
}

/**
 * Collection info (vectors, dimensions, sizes, index metrics) — `GET
 * /api/database/{dbname}/collections/{collection}/info`
 */
export function fetchCollectionInfo(dbname: string, collection: string) {
  return request<CollectionInfo>({
    url: `${collectionsBase(dbname)}/${encodeURIComponent(collection)}/info`
  });
}

/**
 * HNSW index graph snapshot — `GET /api/database/{dbname}/collections/{collection}/graph`
 *
 * @param dimensions Projection dimensions: 2 or 3 (server default: 2)
 * @param limit Max nodes returned (server default: 2000)
 * @param level Only nodes reaching this HNSW level (omit for all)
 */
export function fetchCollectionGraph(
  dbname: string,
  collection: string,
  options?: { dimensions?: number; limit?: number; level?: number }
) {
  return request<IndexGraphSnapshot>({
    url: `${collectionsBase(dbname)}/${encodeURIComponent(collection)}/graph`,
    params: options
  });
}

/**
 * HNSW index graph snapshot with a query vector projected into the same PCA basis —
 * `POST /api/database/{dbname}/collections/{collection}/graph`
 *
 * Use this instead of {@link fetchCollectionGraph} when highlighting a search result on
 * the graph: a query embedding is a few hundred floats and doesn't belong in a query string.
 */
export function fetchCollectionGraphWithQuery(
  dbname: string,
  collection: string,
  data: GetCollectionGraphRequest
) {
  return request<IndexGraphSnapshot>({
    url: `${collectionsBase(dbname)}/${encodeURIComponent(collection)}/graph`,
    method: 'post',
    data
  });
}

/**
 * Search one or more collections — `POST /api/database/{dbname}/collections/search`
 *
 * Provide either `sentence` (server-side embedding) or a raw `embeddings` array.
 */
export function fetchSearchCollections(dbname: string, data: SearchCollectionsData) {
  return request<SearchCollectionsResult>({
    url: `${collectionsBase(dbname)}/search`,
    method: 'post',
    data
  });
}

/**
 * Get a document's raw content — `GET .../documents/{key}`
 *
 * @param keyType Force the key interpretation; omit for server auto-detection
 */
export function fetchGetDocument(dbname: string, collection: string, key: string, keyType?: DocumentKeyType) {
  return request<string>({
    url: documentUrl(dbname, collection, key),
    params: { keyType }
  });
}

/** Get a document as JSON — `GET .../documents/{key}/json` */
export function fetchGetDocumentJson(dbname: string, collection: string, key: string, keyType?: DocumentKeyType) {
  return request<{ key: string; collection: string; content: unknown }>({
    url: `${documentUrl(dbname, collection, key)}/json`,
    params: { keyType }
  });
}

/**
 * Create or update a document — `PUT .../documents/{key}`
 *
 * The payload carries the JSON content and/or a sentence for server-side embedding.
 */
export function fetchSetDocument(
  dbname: string,
  collection: string,
  key: string,
  payload: DocumentPayload,
  keyType?: DocumentKeyType
) {
  return request<null>({
    url: documentUrl(dbname, collection, key),
    method: 'put',
    params: { keyType },
    data: payload
  });
}

/** Delete a document — `DELETE .../documents/{key}` */
export function fetchDeleteDocument(dbname: string, collection: string, key: string, keyType?: DocumentKeyType) {
  return request<null>({
    url: documentUrl(dbname, collection, key),
    method: 'delete',
    params: { keyType }
  });
}
