declare namespace server {
  namespace database {
    type DatabaseName = string

    interface CollectionIndexInfo {
      indexSizeBytes?: number | null
      nodes?: number | null
      deletedNodes?: number | null
      maxLevel?: number | null
      nodesPerLevel?: number[] | null
      averageDegree?: number | null
      quantization?: string | null
    }

    interface CollectionInfo {
      name?: string | null
      vectors?: number | null
      dimensions?: number | null
      contentSize?: number | null
      vectorSize?: number | null
      index?: CollectionIndexInfo | null
    }

    interface GraphNode {
      positionId: number
      key?: string | null
      maxLevel?: number | null
      isDeleted?: boolean | null
      degree?: number | null
      position?: number[] | null
    }

    interface GraphEdge {
      source: number
      target: number
      level: number
    }

    interface CollectionGraph {
      collection?: string | null
      dimensions?: number | null
      totalNodes?: number | null
      liveNodes?: number | null
      deletedNodes?: number | null
      returnedNodes?: number | null
      maxLevel?: number | null
      entrypointPositionId?: number | null
      truncated?: boolean | null
      nodes: GraphNode[]
      edges: GraphEdge[]
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
      indexSize?: number | null
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
      embeddings?: number[] | null
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
      indexSizeBytes?: number | null
      deletedCount?: number | null
      maxLevel?: number | null
      averageDegree?: number | null
      quantization?: string | null
    }

    interface DatabaseStatus {
      name: string
      ingestionQueueLength?: number | null
      collectionsCount?: number | null
      totalElementsCount?: number | null
      contentSizeBytes?: number | null
      vectorSizeBytes?: number | null
      indexSizeBytes?: number | null
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