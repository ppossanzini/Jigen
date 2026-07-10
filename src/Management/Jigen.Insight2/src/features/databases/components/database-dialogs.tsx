import { DatabaseCreateDialog } from './database-create-dialog'
import { DatabaseDeleteDialog } from './database-delete-dialog'
import { useDatabaseContext } from './database-provider'
import { DatabaseRevokeUserDialog } from './database-revoke-user-dialog'

export function DatabaseDialogs() {
  const { open, setOpen, currentDatabase, currentUser } = useDatabaseContext()

  return (
    <>
      <DatabaseCreateDialog
        open={open === 'create'}
        onOpenChange={() => setOpen(open === 'create' ? null : 'create')}
      />

      {currentDatabase && (
        <DatabaseDeleteDialog
          open={open === 'delete'}
          onOpenChange={() => setOpen(open === 'delete' ? null : 'delete')}
          databaseName={currentDatabase}
        />
      )}

      {currentDatabase && currentUser && (
        <DatabaseRevokeUserDialog
          open={open === 'revoke-user'}
          onOpenChange={() => setOpen(open === 'revoke-user' ? null : 'revoke-user')}
          databaseName={currentDatabase}
          user={currentUser}
        />
      )}
    </>
  )
}
