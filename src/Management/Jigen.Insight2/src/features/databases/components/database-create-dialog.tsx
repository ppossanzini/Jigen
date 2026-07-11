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
import { useCreateDatabase } from '@/features/databases/hooks'
import {
  createDatabaseSchema,
  type CreateDatabaseFormValues,
} from '../data/schema'

type DatabaseCreateDialogProps = {
  open: boolean
  onOpenChange: (open: boolean) => void
}

export function DatabaseCreateDialog({
  open,
  onOpenChange,
}: DatabaseCreateDialogProps) {
  const createDatabase = useCreateDatabase()

  const form = useForm<CreateDatabaseFormValues>({
    resolver: zodResolver(createDatabaseSchema),
    defaultValues: { name: '' },
  })

  function handleOpenChange(next: boolean) {
    if (!next) form.reset()
    onOpenChange(next)
  }

  function onSubmit(values: CreateDatabaseFormValues) {
    createDatabase.mutate(values.name, {
      onSuccess: () => {
        toast.success(`Database "${values.name}" created.`)
        handleOpenChange(false)
      },
      onError: () => {
        toast.error(`Failed to create database "${values.name}".`)
      },
    })
  }

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>New Database</DialogTitle>
          <DialogDescription>
            Create a new Jigen database to hold collections.
          </DialogDescription>
        </DialogHeader>
        <Form {...form}>
          <form
            id='database-create-form'
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
                    <Input placeholder='my-database' autoFocus {...field} />
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
            form='database-create-form'
            disabled={createDatabase.isPending}
          >
            {createDatabase.isPending && (
              <Loader2 className='animate-spin' />
            )}
            Create
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
