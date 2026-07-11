import { ShieldAlert } from 'lucide-react'
import { toast } from 'sonner'
import { ConfirmDialog } from '@/components/confirm-dialog'
import type { DatabaseUserInfo } from '@/lib/api-types'
import { useDatabaseUsers, useSetDatabaseUsers } from '@/features/databases/hooks'

type DatabaseRevokeUserDialogProps = {
  open: boolean
  onOpenChange: (open: boolean) => void
  databaseName: string
  user: DatabaseUserInfo
}

export function DatabaseRevokeUserDialog({
  open,
  onOpenChange,
  databaseName,
  user,
}: DatabaseRevokeUserDialogProps) {
  const { data: users } = useDatabaseUsers(databaseName)
  const setDatabaseUsers = useSetDatabaseUsers()

  function handleRevoke() {
    const remaining = (users ?? []).filter((u) => u.userId !== user.userId)

    setDatabaseUsers.mutate(
      { name: databaseName, data: { users: remaining } },
      {
        onSuccess: () => {
          toast.success(`Access revoked for "${user.userName}".`)
          onOpenChange(false)
        },
        onError: () => {
          toast.error(`Failed to revoke access for "${user.userName}".`)
        },
      }
    )
  }

  return (
    <ConfirmDialog
      open={open}
      onOpenChange={onOpenChange}
      handleConfirm={handleRevoke}
      isLoading={setDatabaseUsers.isPending}
      destructive
      confirmText='Revoke access'
      title={
        <span className='text-destructive'>
          <ShieldAlert
            className='me-1 inline-block stroke-destructive'
            size={18}
          />{' '}
          Revoke Database Access
        </span>
      }
      desc={
        <div className='space-y-2'>
          <p>
            Remove <span className='font-bold'>{user.userName}</span>'s
            access to database <span className='font-bold'>{databaseName}</span>?
          </p>
          <p className='text-muted-foreground text-sm'>
            This only revokes access to this database. It does not delete the
            user account, their roles, or any data. The user keeps access to
            any other database they are assigned to.
          </p>
        </div>
      }
    />
  )
}
