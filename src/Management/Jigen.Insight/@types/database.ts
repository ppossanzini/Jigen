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

export interface CollectionInfoApi {
  name?: string | null
  vectors?: number | null
  dimensions?: number | null
  contentSize?: number | null
  vectorSize?: number | null
}

export interface DatabaseUserInfoApi {
  userId?: string | null
  userName?: string | null
}

export interface DatabaseDetailsApi {
  name?: string | null
  createdAtUtc?: string | null
  vectors?: number | null
  contentSize?: number | null
  vectorSize?: number | null
  allocatedContentSize?: number | null
  allocatedVectorSize?: number | null
  contentFreeSpace?: number | null
  vectorFreeSpace?: number | null
  collectionsCount?: number | null
  usersCount?: number | null
  collections?: CollectionInfoApi[] | null
  users?: DatabaseUserInfoApi[] | null
}

export interface DatabaseListResponse {
  items: DatabaseListItemApi[]
}

export interface CollectionListResponse {
  items: CollectionListItemApi[]
}

export interface DatabaseDetailsResponse {
  item: DatabaseDetailsApi
}
