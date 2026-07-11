import { useEffect } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { Loader2 } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import {
  Form,
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '@/components/ui/form'
import { Input } from '@/components/ui/input'
import { PasswordInput } from '@/components/password-input'
import { useCreateUser, useUpdateUser, useUser } from '@/features/users/hooks'
import { useRoles } from '@/features/roles/hooks'
import type { UserSummary } from '@/lib/api-types'
import { userFormSchema, type UserFormValues } from '../data/schema'

type UsersActionDialogProps = {
  open: boolean
  onOpenChange: (open: boolean) => void
  currentRow?: UserSummary
}

export function UsersActionDialog({
  open,
  onOpenChange,
  currentRow,
}: UsersActionDialogProps) {
  const isEdit = !!currentRow
  const { data: userDetail } = useUser(isEdit ? currentRow?.id ?? null : null)
  const { data: rolesData = [] } = useRoles()
  const createUser = useCreateUser()
  const updateUser = useUpdateUser()

  const form = useForm<UserFormValues>({
    resolver: zodResolver(userFormSchema),
    defaultValues: {
      userName: currentRow?.userName || '',
      password: '',
      roles: (userDetail?.roles as string[]) || [],
      isEdit,
    },
  })

  // `useUser` resolves after the initial render, so react-hook-form's
  // `defaultValues` (captured once at mount) would otherwise miss the
  // user's current roles — reset the form once the detail query lands.
  useEffect(() => {
    if (isEdit && userDetail) {
      form.reset({
        userName: userDetail.userName || currentRow?.userName || '',
        password: '',
        roles: (userDetail.roles as string[]) || [],
        isEdit,
      })
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [userDetail])

  function handleOpenChange(next: boolean) {
    if (!next) form.reset()
    onOpenChange(next)
  }

  function onSubmit(values: UserFormValues) {
    if (isEdit && currentRow) {
      const id = currentRow.id || ''
      updateUser.mutate(
        {
          id,
          data: {
            userName: values.userName,
            password: values.password || undefined,
            roles: values.roles,
          },
        },
        {
          onSuccess: () => {
            toast.success(`User "${values.userName}" updated.`)
            handleOpenChange(false)
          },
          onError: () => {
            toast.error(`Failed to update user "${values.userName}".`)
          },
        }
      )
    } else {
      createUser.mutate(
        {
          userName: values.userName,
          password: values.password!,
          roles: values.roles,
        },
        {
          onSuccess: () => {
            toast.success(`User "${values.userName}" created.`)
            handleOpenChange(false)
          },
          onError: () => {
            toast.error(`Failed to create user "${values.userName}".`)
          },
        }
      )
    }
  }

  const isPending = createUser.isPending || updateUser.isPending

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{isEdit ? 'Edit User' : 'New User'}</DialogTitle>
          <DialogDescription>
            {isEdit
              ? 'Update the user information here.'
              : 'Create a new user here.'}
          </DialogDescription>
        </DialogHeader>
        <Form {...form}>
          <form
            id='user-action-form'
            onSubmit={form.handleSubmit(onSubmit)}
            className='space-y-4'
          >
            <FormField
              control={form.control}
              name='userName'
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Username</FormLabel>
                  <FormControl>
                    <Input placeholder='john_doe' autoFocus {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name='password'
              render={({ field }) => (
                <FormItem>
                  <FormLabel>
                    {isEdit ? 'New password (leave blank to keep current)' : 'Password'}
                  </FormLabel>
                  <FormControl>
                    <PasswordInput placeholder='••••••••' {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name='roles'
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Roles</FormLabel>
                  <div className='space-y-2'>
                    {rolesData.map((role) => {
                      const roleName = role.name || ''
                      return (
                        <FormItem key={role.id} className='flex items-center space-x-2'>
                          <FormControl>
                            <Checkbox
                              checked={field.value.includes(roleName)}
                              onCheckedChange={(checked) => {
                                if (checked) {
                                  field.onChange([...field.value, roleName])
                                } else {
                                  field.onChange(
                                    field.value.filter((r) => r !== roleName)
                                  )
                                }
                              }}
                            />
                          </FormControl>
                          <FormLabel className='font-normal'>{roleName}</FormLabel>
                        </FormItem>
                      )
                    })}
                  </div>
                  <FormMessage />
                </FormItem>
              )}
            />
          </form>
        </Form>
        <DialogFooter>
          <Button
            type='submit'
            form='user-action-form'
            disabled={isPending}
          >
            {isPending && <Loader2 className='animate-spin' />}
            {isEdit ? 'Update' : 'Create'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
