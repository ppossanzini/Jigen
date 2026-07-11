import { z } from 'zod'
import { createFileRoute } from '@tanstack/react-router'
import { GraphExplorer } from '@/features/graph-explorer'

const searchSchema = z.object({
  db: z.string().optional(),
  collection: z.string().optional(),
})

export const Route = createFileRoute('/_authenticated/graph-explorer/')({
  validateSearch: searchSchema,
  component: GraphExplorer,
})
