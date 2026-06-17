export interface DatabaseListItemApi {
  name?: string | null
  dbName?: string | null
  collectionsCount?: number | null
  collections?: number | null
}

export interface CollectionListItemApi {
  name?: string | null
  collectionName?: string | null
}

export interface DatabaseListResponse {
  items: DatabaseListItemApi[]
}

export interface CollectionListResponse {
  items: CollectionListItemApi[]
}
