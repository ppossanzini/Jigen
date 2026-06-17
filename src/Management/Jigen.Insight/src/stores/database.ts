import { defineStore } from 'pinia'
import { databaseService } from '@/services/databaseService'

export interface DatabaseSummary {
  name: string
  collectionsCount: number
}

interface DatabaseState {
  databases: DatabaseSummary[]
  selectedDatabaseName: string | null
  collectionsByDatabase: Record<string, string[]>
  loadingDatabases: boolean
  loadingCollections: boolean
}

export const useDatabaseStore = defineStore('database', {
  state: (): DatabaseState => ({
    databases: [],
    selectedDatabaseName: null,
    collectionsByDatabase: {},
    loadingDatabases: false,
    loadingCollections: false,
  }),
  getters: {
    selectedDatabase: (state) =>
      state.databases.find((entry) => entry.name === state.selectedDatabaseName) ?? null,
    selectedCollections: (state) => {
      if (!state.selectedDatabaseName) {
        return []
      }

      return state.collectionsByDatabase[state.selectedDatabaseName] ?? []
    },
  },
  actions: {
    async loadDatabases() {
      this.loadingDatabases = true

      try {
        this.databases = await databaseService.listDatabases()

        if (
          this.selectedDatabaseName &&
          !this.databases.some((entry) => entry.name === this.selectedDatabaseName)
        ) {
          this.selectedDatabaseName = null
        }
      } finally {
        this.loadingDatabases = false
      }
    },
    setSelectedDatabase(name: string | null) {
      this.selectedDatabaseName = name
    },
    async loadCollectionsFor(databaseName: string) {
      this.loadingCollections = true

      try {
        this.collectionsByDatabase[databaseName] = await databaseService.listCollections(databaseName)
      } finally {
        this.loadingCollections = false
      }
    },
    async createDatabase(name: string) {
      await databaseService.createDatabase(name)
      await this.loadDatabases()
    },
    async deleteDatabase(name: string) {
      await databaseService.deleteDatabase(name)
      delete this.collectionsByDatabase[name]

      if (this.selectedDatabaseName === name) {
        this.selectedDatabaseName = null
      }

      await this.loadDatabases()
    },
  },
})
