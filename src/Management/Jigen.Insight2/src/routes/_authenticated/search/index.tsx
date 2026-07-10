import { z } from 'zod'
import { createFileRoute } from '@tanstack/react-router'
import { Search as SearchFeature } from '@/features/search'

const searchSchema = z.object({
  db: z.string().optional(),
})

export const Route = createFileRoute('/_authenticated/search/')({
  validateSearch: searchSchema,
  component: SearchFeature,
})
