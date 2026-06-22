import { BaseRestService } from '@/services/baseRestService'

class DatabaseService extends BaseRestService {

  async listDatabases(): Promise<server.database.DatabaseName[]> {
    const response = await this.api.get<server.database.DatabaseName[]>('/database')
    return response.data
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
    const response = await this.api.get<string[]>(`/database/${encodeURIComponent(databaseName)}/collections`)
    return response.data
  }

  async getDatabaseDetails(databaseName: string): Promise<server.database.DatabaseDetails | null> {
    const response = await this.api.get<server.database.DatabaseDetails | null>(`/database/${encodeURIComponent(databaseName)}/details`)
    return response.data
  }

  async setDatabaseUsers(
    databaseName: string,
    users: server.database.DatabaseUserInfo[],
  ): Promise<server.database.DatabaseUserInfo[]> {
    const payload: server.database.SetDatabaseUsersData = {
      users: users.map((entry) => ({
        userId: entry.userId,
        userName: entry.userName,
      })),
    }

    const response = await this.api.put<server.database.DatabaseUserInfo[]>(
      `/database/${encodeURIComponent(databaseName)}/users`,
      payload,
    )

    return response.data
  }

  async calculateEmbeddings(sentence: string): Promise<number[]> {
    const response = await this.api.post<number[]>('/embeddings/calculate', sentence)
    return response.data
  }

  async searchCollections(
    databaseName: string,
    payload: server.database.SearchCollectionsData,
  ): Promise<server.database.SearchCollectionsResult> {
    const response = await this.api.post<server.database.SearchCollectionsResult>(
      `/database/${encodeURIComponent(databaseName)}/collections/search`,
      payload,
    )
    return response.data
  }
}

export const databaseService = new DatabaseService()
