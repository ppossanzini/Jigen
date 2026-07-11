import { useMutation, useQuery } from '@tanstack/react-query'
import type { SearchCollectionsData } from '@/lib/api-types'
import {
  calculateEmbeddings,
  calculateEmbeddingsWithTask,
  getEmbeddingTasks,
  searchCollections,
} from './api'

export function useEmbeddingTasks() {
  return useQuery({
    queryKey: ['embeddings', 'tasks'],
    queryFn: getEmbeddingTasks,
  })
}

export function useSearchCollections() {
  return useMutation({
    mutationFn: ({
      dbname,
      data,
    }: {
      dbname: string
      data: SearchCollectionsData
    }) => searchCollections(dbname, data),
  })
}

export function useCalculateEmbeddings() {
  return useMutation({
    mutationFn: (text: string) => calculateEmbeddings(text),
  })
}

export function useCalculateEmbeddingsWithTask() {
  return useMutation({
    mutationFn: ({ task, text }: { task: string; text: string }) =>
      calculateEmbeddingsWithTask(task, text),
  })
}
