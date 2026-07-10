import { apiClient } from '@/lib/api-client'
import type {
  SearchCollectionsData,
  SearchCollectionsResult,
} from '@/lib/api-types'

export async function searchCollections(
  dbname: string,
  data: SearchCollectionsData,
): Promise<SearchCollectionsResult> {
  const response = await apiClient.post<SearchCollectionsResult>(
    `/database/${encodeURIComponent(dbname)}/collections/search`,
    data,
  )
  return response.data
}

export async function getEmbeddingTasks(): Promise<string[]> {
  const response = await apiClient.get<string[]>('/embeddings/tasks')
  return response.data
}

export async function calculateEmbeddings(
  text: string,
): Promise<(number | string)[]> {
  const response = await apiClient.post<(number | string)[]>(
    '/embeddings/calculate',
    text,
  )
  return response.data
}

export async function calculateEmbeddingsWithTask(
  task: string,
  text: string,
): Promise<(number | string)[]> {
  const response = await apiClient.post<(number | string)[]>(
    `/embeddings/calculate/${encodeURIComponent(task)}`,
    text,
  )
  return response.data
}
