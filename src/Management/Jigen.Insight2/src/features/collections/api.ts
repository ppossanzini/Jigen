import { apiClient } from '@/lib/api-client'
import type { CollectionInfo, IndexGraphSnapshot } from '@/lib/api-types'

export async function getCollections(dbname: string): Promise<string[]> {
  const response = await apiClient.get<string[]>(
    `/database/${encodeURIComponent(dbname)}/collections`,
  )
  return response.data
}

export async function getCollectionInfo(
  dbname: string,
  collection: string,
): Promise<CollectionInfo> {
  const response = await apiClient.get<CollectionInfo>(
    `/database/${encodeURIComponent(dbname)}/collections/${encodeURIComponent(
      collection,
    )}/info`,
  )
  return response.data
}

export interface CollectionGraphParams {
  dimensions?: number
  limit?: number
  level?: number
}

export async function getCollectionGraph(
  dbname: string,
  collection: string,
  params?: CollectionGraphParams,
): Promise<IndexGraphSnapshot> {
  const response = await apiClient.get<IndexGraphSnapshot>(
    `/database/${encodeURIComponent(dbname)}/collections/${encodeURIComponent(
      collection,
    )}/graph`,
    {
      params,
    },
  )
  return response.data
}
