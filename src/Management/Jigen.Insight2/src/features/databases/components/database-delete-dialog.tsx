import { useState } from 'react'
import { AlertTriangle } from 'lucide-react'
import { toast } from 'sonner'
import { Checkbox } from '@/components/ui/checkbox'
import { ConfirmDialog } from '@/components/confirm-dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { useDeleteDatabase } from '@/features/databases/hooks'

type DatabaseDeleteDialogProps = {
  open: boolean
  onOpenChange: (open: boolean) => void
  databaseName: string
}

export function DatabaseDeleteDialog({
  open,
  onOpenChange,
  databaseName,
}: DatabaseDeleteDialogProps) {
  const [confirmValue, setConfirmValue] = useState('')
  const [deleteFiles, setDeleteFiles] = useState(true)
  const deleteDatabase = useDeleteDatabase()

  function handleOpenChange(next: boolean) {
    if (!next) {
      setConfirmValue('')
      setDeleteFiles(true)
    }
    onOpenChange(next)
  }

  function handleDelete() {
    if (confirmValue.trim() !== databaseName) return

    deleteDatabase.mutate(
      { name: databaseName, deletefiles: deleteFiles },
      {
        onSuccess: () => {
          toast.success(`Database "${databaseName}" deleted.`)
          handleOpenChange(false)
        },
        onError: () => {
          toast.error(`Failed to delete database "${databaseName}".`)
        },
      }
    )
  }

  return (
    <ConfirmDialog
      open={open}
      onOpenChange={handleOpenChange}
      handleConfirm={handleDelete}
      disabled={confirmValue.trim() !== databaseName || deleteDatabase.isPending}
      isLoading={deleteDatabase.isPending}
      destructive
      confirmText='Delete'
      title={
        <span className='text-destructive'>
          <AlertTriangle
            className='me-1 inline-block stroke-destructive'
            size={18}
          />{' '}
          Delete Database
        </span>
      }
      desc={
        <div className='space-y-4'>
          <p>
            Are you sure you want to delete{' '}
            <span className='font-bold'>{databaseName}</span>? This removes
            the database and all its collections. This cannot be undone.
          </p>

          <Label className='flex items-center gap-2 font-normal'>
            <Checkbox
              checked={deleteFiles}
              onCheckedChange={(value) => setDeleteFiles(!!value)}
            />
            Also delete files on disk
          </Label>

          <Label className='my-2'>
            Database name:
            <Input
              value={confirmValue}
              onChange={(event) => setConfirmValue(event.target.value)}
              placeholder='Type the database name to confirm.'
              autoFocus
            />
          </Label>
        </div>
      }
    />
  )
}
