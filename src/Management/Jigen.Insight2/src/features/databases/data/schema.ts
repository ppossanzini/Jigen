import { z } from 'zod'

export const createDatabaseSchema = z.object({
  name: z
    .string()
    .min(1, 'Database name is required.')
    .regex(
      /^[a-zA-Z0-9_-]+$/,
      'Only letters, numbers, hyphens and underscores are allowed.'
    ),
})
export type CreateDatabaseFormValues = z.infer<typeof createDatabaseSchema>
