import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { useRoleUsers } from '@/features/roles/hooks'
import type { RoleSummary } from '@/lib/api-types'

type RolesUsersPanelProps = {
  open: boolean
  onOpenChange: (open: boolean) => void
  currentRow: RoleSummary | null
}

export function RolesUsersPanel({
  open,
  onOpenChange,
  currentRow,
}: RolesUsersPanelProps) {
  const { data: users = [] } = useRoleUsers((open && currentRow?.id) || null)

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Users with role: {currentRow?.name}</DialogTitle>
          <DialogDescription>
            List of users who have this role assigned.
          </DialogDescription>
        </DialogHeader>
        <div className='space-y-2 max-h-96 overflow-y-auto'>
          {users.length === 0 ? (
            <p className='text-sm text-muted-foreground'>
              No users have this role.
            </p>
          ) : (
            <ul className='space-y-1'>
              {users.map((user) => (
                <li key={user.id} className='text-sm py-1 px-2 rounded hover:bg-muted'>
                  {user.userName}
                </li>
              ))}
            </ul>
          )}
        </div>
      </DialogContent>
    </Dialog>
  )
}
