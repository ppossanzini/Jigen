import { z } from 'zod'

export const userFormSchema = z
  .object({
    userName: z.string().min(1, 'Username is required.'),
    password: z.string().optional(),
    roles: z.array(z.string()),
    isEdit: z.boolean(),
  })
  .refine((data) => data.isEdit || (data.password?.length ?? 0) > 0, {
    message: 'Password is required.',
    path: ['password'],
  })

export type UserFormValues = z.infer<typeof userFormSchema>
