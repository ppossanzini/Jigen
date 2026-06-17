import { BaseRestService } from '@/services/baseRestService'
import type { CollectionListItemApi, DatabaseListItemApi } from '~types/database'

interface DatabaseItem {
  name: string
  collectionsCount: number
}

class DatabaseService extends BaseRestService {
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
}

export const databaseService = new DatabaseService()
