import { useQuery } from '@tanstack/react-query'
import {
  getCollectionGraph,
  getCollectionInfo,
  getCollections,
  type CollectionGraphParams,
} from './api'

export function useCollections(dbname: string | null) {
  return useQuery({
    queryKey: ['databases', dbname, 'collections'],
    queryFn: () => getCollections(dbname!),
    enabled: !!dbname,
  })
}

export function useCollectionInfo(
  dbname: string | null,
  collection: string | null,
) {
  return useQuery({
    queryKey: ['databases', dbname, 'collections', collection, 'info'],
    queryFn: () => getCollectionInfo(dbname!, collection!),
    enabled: !!dbname && !!collection,
  })
}

export function useCollectionGraph(
  dbname: string | null,
  collection: string | null,
  params?: CollectionGraphParams,
) {
  return useQuery({
    queryKey: [
      'databases',
      dbname,
      'collections',
      collection,
      'graph',
      params || {},
    ],
    queryFn: () => getCollectionGraph(dbname!, collection!, params),
    enabled: !!dbname && !!collection,
  })
}
