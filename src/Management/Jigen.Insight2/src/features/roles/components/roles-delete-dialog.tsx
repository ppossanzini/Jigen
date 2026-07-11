import { useState } from 'react'
import { AlertTriangle } from 'lucide-react'
import { toast } from 'sonner'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { ConfirmDialog } from '@/components/confirm-dialog'
import { useDeleteRole } from '@/features/roles/hooks'
import type { RoleSummary } from '@/lib/api-types'

type RolesDeleteDialogProps = {
  open: boolean
  onOpenChange: (open: boolean) => void
  currentRow: RoleSummary | null
}

export function RolesDeleteDialog({
  open,
  onOpenChange,
  currentRow,
}: RolesDeleteDialogProps) {
  const [confirmValue, setConfirmValue] = useState('')
  const deleteRole = useDeleteRole()

  if (!currentRow) return null

  function handleOpenChange(next: boolean) {
    if (!next) {
      setConfirmValue('')
    }
    onOpenChange(next)
  }

  function handleDelete() {
    if (!currentRow) return
    const id = currentRow.id || ''
    const name = currentRow.name || ''
    if (confirmValue.trim() !== name) return

    deleteRole.mutate(id, {
      onSuccess: () => {
        toast.success(`Role "${name}" deleted.`)
        handleOpenChange(false)
      },
      onError: () => {
        toast.error(`Failed to delete role "${name}".`)
      },
    })
  }

  return (
    <ConfirmDialog
      open={open}
      onOpenChange={handleOpenChange}
      handleConfirm={handleDelete}
      disabled={
        confirmValue.trim() !== currentRow.name || deleteRole.isPending
      }
      isLoading={deleteRole.isPending}
      destructive
      confirmText='Delete'
      title={
        <span className='text-destructive'>
          <AlertTriangle
            className='me-1 inline-block stroke-destructive'
            size={18}
          />{' '}
          Delete Role
        </span>
      }
      desc={
        <div className='space-y-4'>
          <p>
            Are you sure you want to delete{' '}
            <span className='font-bold'>{currentRow.name}</span>? This action
            will permanently remove the role from the system. This cannot be
            undone.
          </p>

          <Label className='my-2'>
            Role name:
            <Input
              value={confirmValue}
              onChange={(event) => setConfirmValue(event.target.value)}
              placeholder='Type the role name to confirm.'
              autoFocus
            />
          </Label>
        </div>
      }
    />
  )
}
