import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { Loader2 } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
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
import {
  useCreateRole,
  useUpdateRole,
} from '@/features/roles/hooks'
import type { RoleSummary } from '@/lib/api-types'
import { roleFormSchema, type RoleFormValues } from '../data/schema'

type RolesActionDialogProps = {
  open: boolean
  onOpenChange: (open: boolean) => void
  currentRow?: RoleSummary
}

export function RolesActionDialog({
  open,
  onOpenChange,
  currentRow,
}: RolesActionDialogProps) {
  const isEdit = !!currentRow
  const createRole = useCreateRole()
  const updateRole = useUpdateRole()

  const form = useForm<RoleFormValues>({
    resolver: zodResolver(roleFormSchema),
    defaultValues: {
      name: (currentRow?.name as string) || '',
    },
  })

  function handleOpenChange(next: boolean) {
    if (!next) form.reset()
    onOpenChange(next)
  }

  function onSubmit(values: RoleFormValues) {
    if (isEdit && currentRow) {
      const id = currentRow.id || ''
      updateRole.mutate(
        {
          id,
          data: {
            name: values.name,
          },
        },
        {
          onSuccess: () => {
            toast.success(`Role "${values.name}" updated.`)
            handleOpenChange(false)
          },
          onError: () => {
            toast.error(`Failed to update role "${values.name}".`)
          },
        }
      )
    } else {
      createRole.mutate(
        {
          name: values.name,
        },
        {
          onSuccess: () => {
            toast.success(`Role "${values.name}" created.`)
            handleOpenChange(false)
          },
          onError: () => {
            toast.error(`Failed to create role "${values.name}".`)
          },
        }
      )
    }
  }

  const isPending = createRole.isPending || updateRole.isPending

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{isEdit ? 'Edit Role' : 'New Role'}</DialogTitle>
          <DialogDescription>
            {isEdit ? 'Update the role name here.' : 'Create a new role here.'}
          </DialogDescription>
        </DialogHeader>
        <Form {...form}>
          <form
            id='role-action-form'
            onSubmit={form.handleSubmit(onSubmit)}
            className='space-y-4'
          >
            <FormField
              control={form.control}
              name='name'
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Name</FormLabel>
                  <FormControl>
                    <Input placeholder='Administrator' autoFocus {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
          </form>
        </Form>
        <DialogFooter>
          <Button
            type='submit'
            form='role-action-form'
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
