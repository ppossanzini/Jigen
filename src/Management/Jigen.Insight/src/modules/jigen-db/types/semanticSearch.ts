export interface SearchResultRow {
  id: string
  collection: string
  attributes: Record<string, string>
  content: string
  score: number
  responseEmbedding: number[]
  latencyMs: number
}

export interface SearchPathStep {
  key: string
  title: string
  detail: string
  elapsedMs: number
}

export interface PerCollectionMetric {
  collection: string
  searchTimeMs: number
  resultsCount: number
}
