export interface SearchResultRow {
  id: string
  collection: string
  key: string
  content: string
  score: number
  searchTimeMs: number
}

export interface SearchDiagnostics {
  embeddingsCalculationTimeMs: number
  searchTimeMs: number
  mergeTimeMs: number
  sortingTimeMs: number
  totalTimeMs: number
}
