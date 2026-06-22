declare namespace server {
  namespace database {
    type DatabaseName = string

    interface CollectionInfo {
      name?: string | null
      vectors?: number | null
      dimensions?: number | null
      contentSize?: number | null
      vectorSize?: number | null
    }

    interface DatabaseUserInfo {
      userId?: string | null
      userName?: string | null
    }

    interface DatabaseDetails {
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
      collections?: CollectionInfo[] | null
      users?: DatabaseUserInfo[] | null
    }

    interface SetDatabaseUsersData {
      users?: DatabaseUserInfo[] | null
    }

    interface SearchCollectionsData {
      collections?: string[] | null
      sentence?: string | null
      top?: number | null
    }

    interface CollectionSearchResultItem {
      collection?: string | null
      key?: string | null
      content?: unknown
      score?: number | null
    }

    interface CollectionSearchResult {
      collection?: string | null
      searchTime?: number | null
      results?: CollectionSearchResultItem[] | null
    }

    interface SearchCollectionsResult {
      embeddingsCalculationTime?: number | null
      searchTime?: number | null
      mergeTime?: number | null
      sortingTime?: number | null
      collectionsResults?: CollectionSearchResult[] | null
      mergedResults?: CollectionSearchResultItem[] | null
    }

  }

  namespace metrics {
    interface CollectionStatus {
      name: string
      elementsCount?: number | null
      dimensions?: number | null
      contentSizeBytes?: number | null
      vectorSizeBytes?: number | null
    }

    interface DatabaseStatus {
      name: string
      ingestionQueueLength?: number | null
      collectionsCount?: number | null
      totalElementsCount?: number | null
      contentSizeBytes?: number | null
      vectorSizeBytes?: number | null
      collections: CollectionStatus[]
    }

    interface ServerStatusSample {
      timestampUtc?: string | null
      cpuUsagePercent?: number | null
      memoryUsageBytes?: number | null
      databases: DatabaseStatus[]
    }

    interface ServerStatusHistory {
      fromUtc?: string | null
      toUtc?: string | null
      sampleIntervalSeconds?: number | null
      samples: ServerStatusSample[]
    }
  }
}