import { BaseRestService } from '@/services/baseRestService'
import type {
  CollectionInfoApi,
  CollectionListItemApi,
  DatabaseDetailsApi,
  DatabaseListItemApi,
  DatabaseUserInfoApi,
} from '~types/database'

interface DatabaseItem {
  name: string
  collectionsCount: number
}

export interface DatabaseCollectionDetail {
  name: string
  vectors: number
  dimensions: number
  contentSize: number
  vectorSize: number
}

export interface DatabaseUserDetail {
  userId: string
  userName: string
}

export interface DatabaseDetailsItem {
  name: string
  createdAtUtc: string | null
  vectors: number
  contentSize: number
  vectorSize: number
  allocatedContentSize: number
  allocatedVectorSize: number
  contentFreeSpace: number
  vectorFreeSpace: number
  collectionsCount: number
  usersCount: number
  collections: DatabaseCollectionDetail[]
  users: DatabaseUserDetail[]
}

class DatabaseService extends BaseRestService {
  private toSafeNumber(value: unknown): number {
    if (typeof value === 'number' && Number.isFinite(value)) {
      return value
    }

    if (typeof value === 'string') {
      const parsed = Number(value)
      if (Number.isFinite(parsed)) {
        return parsed
      }
    }

    return 0
  }

  private toDatabaseItems(payload: unknown): DatabaseItem[] {
    if (!Array.isArray(payload)) {
      return []
    }

    return payload
      .map((entry) => {
        if (typeof entry === 'string' && entry.length > 0) {
          return {
            name: entry,
            collectionsCount: 0,
          }
        }

        if (typeof entry !== 'object' || entry === null) {
          return null
        }

        const item = entry as DatabaseListItemApi
        const name = item.name ?? item.dbName ?? null

        if (!name || name.length === 0) {
          return null
        }

        const collectionsCount = item.collectionsCount ?? item.collections ?? 0

        return {
          name,
          collectionsCount: Number.isFinite(collectionsCount) ? Number(collectionsCount) : 0,
        }
      })
      .filter((entry): entry is DatabaseItem => entry !== null)
  }

  private toCollectionNames(payload: unknown): string[] {
    if (!Array.isArray(payload)) {
      return []
    }

    return payload
      .map((entry) => {
        if (typeof entry === 'string') {
          return entry
        }

        if (typeof entry !== 'object' || entry === null) {
          return null
        }

        const item = entry as CollectionListItemApi
        return item.name ?? item.collectionName ?? null
      })
      .filter((entry): entry is string => typeof entry === 'string' && entry.length > 0)
  }

  private toCollectionDetails(payload: unknown): DatabaseCollectionDetail[] {
    if (!Array.isArray(payload)) {
      return []
    }

    return payload
      .map((entry) => {
        if (typeof entry !== 'object' || entry === null) {
          return null
        }

        const item = entry as CollectionInfoApi

        return {
          name: item.name ?? '',
          vectors: this.toSafeNumber(item.vectors),
          dimensions: this.toSafeNumber(item.dimensions),
          contentSize: this.toSafeNumber(item.contentSize),
          vectorSize: this.toSafeNumber(item.vectorSize),
        }
      })
      .filter((entry): entry is DatabaseCollectionDetail => entry !== null && entry.name.length > 0)
  }

  private toUsers(payload: unknown): DatabaseUserDetail[] {
    if (!Array.isArray(payload)) {
      return []
    }

    return payload
      .map((entry) => {
        if (typeof entry !== 'object' || entry === null) {
          return null
        }

        const item = entry as DatabaseUserInfoApi
        const userId = item.userId ?? ''
        const userName = item.userName ?? ''

        if (!userId && !userName) {
          return null
        }

        return { userId, userName }
      })
      .filter((entry): entry is DatabaseUserDetail => entry !== null)
  }

  private toDatabaseDetails(payload: unknown): DatabaseDetailsItem | null {
    if (typeof payload !== 'object' || payload === null) {
      return null
    }

    const item = payload as DatabaseDetailsApi
    const name = item.name ?? null

    if (!name || name.length === 0) {
      return null
    }

    return {
      name,
      createdAtUtc: item.createdAtUtc ?? null,
      vectors: this.toSafeNumber(item.vectors),
      contentSize: this.toSafeNumber(item.contentSize),
      vectorSize: this.toSafeNumber(item.vectorSize),
      allocatedContentSize: this.toSafeNumber(item.allocatedContentSize),
      allocatedVectorSize: this.toSafeNumber(item.allocatedVectorSize),
      contentFreeSpace: this.toSafeNumber(item.contentFreeSpace),
      vectorFreeSpace: this.toSafeNumber(item.vectorFreeSpace),
      collectionsCount: this.toSafeNumber(item.collectionsCount),
      usersCount: this.toSafeNumber(item.usersCount),
      collections: this.toCollectionDetails(item.collections),
      users: this.toUsers(item.users),
    }
  }

  async listDatabases(): Promise<DatabaseItem[]> {
    const response = await this.api.get('/database')
    return this.toDatabaseItems(response.data)
  }

  async createDatabase(name: string): Promise<void> {
    await this.api.post('/database', null, {
      params: { name },
    })
  }

  async deleteDatabase(name: string): Promise<void> {
    await this.api.delete('/database', {
      params: { name },
    })
  }

  async listCollections(databaseName: string): Promise<string[]> {
    const response = await this.api.get(`/database/${encodeURIComponent(databaseName)}/collections`)
    return this.toCollectionNames(response.data)
  }

  async getDatabaseDetails(databaseName: string): Promise<DatabaseDetailsItem | null> {
    const response = await this.api.get(`/database/${encodeURIComponent(databaseName)}/details`)
    return this.toDatabaseDetails(response.data)
  }

  async setDatabaseUsers(databaseName: string, users: DatabaseUserDetail[]): Promise<DatabaseUserDetail[]> {
    const response = await this.api.put(`/database/${encodeURIComponent(databaseName)}/users`, {
      users: users.map((entry) => ({
        userId: entry.userId,
        userName: entry.userName,
      })),
    })

    return this.toUsers(response.data)
  }
}

export const databaseService = new DatabaseService()
