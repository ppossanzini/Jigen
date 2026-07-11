import { z } from 'zod'
import { createFileRoute } from '@tanstack/react-router'
import { AuthCallback } from '@/features/auth/callback'

const searchSchema = z.object({
  code: z.string().optional(),
  state: z.string().optional(),
  error: z.string().optional(),
  error_description: z.string().optional(),
  redirect: z.string().optional(),
})

export const Route = createFileRoute('/auth/callback')({
  component: AuthCallback,
  validateSearch: searchSchema,
})
