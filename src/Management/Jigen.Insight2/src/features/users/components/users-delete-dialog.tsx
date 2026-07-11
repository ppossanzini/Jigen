import { useState } from 'react'
import { AlertTriangle } from 'lucide-react'
import { toast } from 'sonner'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { ConfirmDialog } from '@/components/confirm-dialog'
import { useDeleteUser } from '@/features/users/hooks'
import type { UserSummary } from '@/lib/api-types'

type UsersDeleteDialogProps = {
  open: boolean
  onOpenChange: (open: boolean) => void
  currentRow: UserSummary | null
}

export function UsersDeleteDialog({
  open,
  onOpenChange,
  currentRow,
}: UsersDeleteDialogProps) {
  const [confirmValue, setConfirmValue] = useState('')
  const deleteUser = useDeleteUser()

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
    const userName = currentRow.userName || ''
    if (confirmValue.trim() !== userName) return

    deleteUser.mutate(id, {
      onSuccess: () => {
        toast.success(`User "${userName}" deleted.`)
        handleOpenChange(false)
      },
      onError: () => {
        toast.error(`Failed to delete user "${userName}".`)
      },
    })
  }

  return (
    <ConfirmDialog
      open={open}
      onOpenChange={handleOpenChange}
      handleConfirm={handleDelete}
      disabled={
        confirmValue.trim() !== currentRow.userName || deleteUser.isPending
      }
      isLoading={deleteUser.isPending}
      destructive
      confirmText='Delete'
      title={
        <span className='text-destructive'>
          <AlertTriangle
            className='me-1 inline-block stroke-destructive'
            size={18}
          />{' '}
          Delete User
        </span>
      }
      desc={
        <div className='space-y-4'>
          <p>
            Are you sure you want to delete{' '}
            <span className='font-bold'>{currentRow.userName}</span>? This
            action will permanently remove the user from the system. This
            cannot be undone.
          </p>

          <Label className='my-2'>
            Username:
            <Input
              value={confirmValue}
              onChange={(event) => setConfirmValue(event.target.value)}
              placeholder='Type the username to confirm.'
              autoFocus
            />
          </Label>
        </div>
      }
    />
  )
}
