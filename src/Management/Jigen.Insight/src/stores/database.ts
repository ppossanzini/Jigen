import { defineStore } from 'pinia'
import { databaseService } from '@/services/databaseService'

export interface DatabaseDetails extends server.database.DatabaseDetails {}

interface DatabaseState {
  databases: server.database.DatabaseName[]
  selectedDatabaseName: string | null
  selectedCollectionName: string | null
  collectionsByDatabase: Record<string, string[]>
  detailsByDatabase: Record<string, DatabaseDetails | null>
  loadingDatabases: boolean
  loadingCollections: boolean
  loadingDetails: boolean
}

export const useDatabaseStore = defineStore('database', {
  state: (): DatabaseState => ({
    databases: [],
    selectedDatabaseName: null,
    selectedCollectionName: null,
    collectionsByDatabase: {},
    detailsByDatabase: {},
    loadingDatabases: false,
    loadingCollections: false,
    loadingDetails: false,
  }),
  getters: {
    selectedDatabase: (state) =>
      state.databases.find((entry) => entry === state.selectedDatabaseName) ?? null,
    selectedCollections: (state) => {
      if (!state.selectedDatabaseName) {
        return []
      }

      return state.collectionsByDatabase[state.selectedDatabaseName] ?? []
    },
    selectedDatabaseCollections: (state) => {
      if (!state.selectedDatabaseName) {
        return []
      }

      const details = state.detailsByDatabase[state.selectedDatabaseName]
      return Array.isArray(details?.collections) ? details.collections : []
    },
    selectedCollection: (state) => {
      if (!state.selectedDatabaseName || !state.selectedCollectionName) {
        return null
      }

      const details = state.detailsByDatabase[state.selectedDatabaseName]
      if (!details) {
        return null
      }

      const collections = Array.isArray(details.collections) ? details.collections : []
      return collections.find((entry) => entry?.name === state.selectedCollectionName) ?? null
    },
    selectedDatabaseDetails: (state): DatabaseDetails | null => {
      if (!state.selectedDatabaseName) {
        return null
      }

      return state.detailsByDatabase[state.selectedDatabaseName] ?? null
    },
  },
  actions: {
    async loadDatabases() {
      this.loadingDatabases = true

      try {
        const payload = await databaseService.listDatabases()
        this.databases = payload

        if (
          this.selectedDatabaseName &&
          !this.databases.includes(this.selectedDatabaseName)
        ) {
          this.selectedDatabaseName = null
        }
      } finally {
        this.loadingDatabases = false
      }
    },
    setSelectedDatabase(name: string | null) {
      if (this.selectedDatabaseName !== name) {
        this.selectedCollectionName = null
      }

      this.selectedDatabaseName = name
    },
    setSelectedCollection(name: string | null) {
      this.selectedCollectionName = name
    },
    async loadCollectionsFor(databaseName: string) {
      this.loadingCollections = true

      try {
        this.collectionsByDatabase[databaseName] = await databaseService.listCollections(databaseName)
      } finally {
        this.loadingCollections = false
      }
    },
    async loadDetailsFor(databaseName: string) {
      this.loadingDetails = true

      try {
        this.detailsByDatabase[databaseName] = await databaseService.getDatabaseDetails(databaseName)

        if (this.selectedDatabaseName !== databaseName) {
          return
        }

        if (!this.selectedCollectionName) {
          return
        }

        const collections = this.detailsByDatabase[databaseName]?.collections ?? []
        if (!collections.some((entry) => entry.name === this.selectedCollectionName)) {
          this.selectedCollectionName = null
        }
      } finally {
        this.loadingDetails = false
      }
    },
    async setDatabaseUsers(databaseName: string, users: server.database.DatabaseUserInfo[]) {
      const updatedUsers = await databaseService.setDatabaseUsers(databaseName, users)
      const currentDetails = this.detailsByDatabase[databaseName]

      if (!currentDetails) {
        await this.loadDetailsFor(databaseName)
        return
      }

      this.detailsByDatabase[databaseName] = {
        ...currentDetails,
        users: updatedUsers,
        usersCount: updatedUsers.length,
      }
    },
    async createDatabase(name: string) {
      await databaseService.createDatabase(name)
      await this.loadDatabases()
    },
    async deleteDatabase(name: string) {
      await databaseService.deleteDatabase(name)
      delete this.collectionsByDatabase[name]
      delete this.detailsByDatabase[name]

      if (this.selectedDatabaseName === name) {
        this.selectedDatabaseName = null
        this.selectedCollectionName = null
      }

      await this.loadDatabases()
    },
  },
})
