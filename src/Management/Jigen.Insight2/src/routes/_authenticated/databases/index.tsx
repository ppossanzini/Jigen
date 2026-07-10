import { z } from 'zod'
import { createFileRoute } from '@tanstack/react-router'
import { Databases } from '@/features/databases'

const searchSchema = z.object({
  selected: z.string().optional(),
})

export const Route = createFileRoute('/_authenticated/databases/')({
  validateSearch: searchSchema,
  component: Databases,
})
