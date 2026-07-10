import { useMemo } from 'react'
import { UserMinus } from 'lucide-react'
import { toast } from 'sonner'
import { formatBytes, formatNumber } from '@/lib/utils'
import { useIsSecurityAdmin } from '@/stores/auth-store'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { SelectDropdown } from '@/components/select-dropdown'
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
} from '@/components/ui/sheet'
import { Skeleton } from '@/components/ui/skeleton'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { useUsers } from '@/features/users/hooks'
import { useDatabaseDetails, useSetDatabaseUsers } from '../hooks'
import { useDatabaseContext } from './database-provider'

type DatabaseDetailPanelProps = {
  databaseName: string | null
  onOpenChange: (open: boolean) => void
}

export function DatabaseDetailPanel({
  databaseName,
  onOpenChange,
}: DatabaseDetailPanelProps) {
  const isSecurityAdmin = useIsSecurityAdmin()
  const { data: details, isLoading } = useDatabaseDetails(databaseName)
  const { data: allUsers } = useUsers()
  const setDatabaseUsers = useSetDatabaseUsers()
  const { setOpen, setCurrentDatabase, setCurrentUser } = useDatabaseContext()

  const assignableUsers = useMemo(() => {
    const assignedIds = new Set((details?.users ?? []).map((u) => u.userId))
    return (allUsers ?? []).filter((u) => !assignedIds.has(u.id))
  }, [allUsers, details?.users])

  function handleAssignUser(userId: string) {
    if (!databaseName) return
    const user = assignableUsers.find((u) => u.id === userId)
    if (!user) return

    const nextUsers = [
      ...(details?.users ?? []),
      { userId: user.id, userName: user.userName },
    ]

    setDatabaseUsers.mutate(
      { name: databaseName, data: { users: nextUsers } },
      {
        onSuccess: () =>
          toast.success(`"${user.userName}" now has access to ${databaseName}.`),
        onError: () =>
          toast.error(`Failed to grant access to "${user.userName}".`),
      }
    )
  }

  return (
    <Sheet open={!!databaseName} onOpenChange={onOpenChange}>
      <SheetContent className='w-full max-w-full gap-4 sm:max-w-none'>
        <SheetHeader>
          <SheetTitle>{databaseName}</SheetTitle>
          <SheetDescription>
            Database details, collections and access.
          </SheetDescription>
        </SheetHeader>

        <div className='flex-1 space-y-6 overflow-y-auto px-4 pb-6 lg:px-6'>
          {isLoading || !details ? (
            <div className='space-y-3'>
              <Skeleton className='h-24 w-full' />
              <Skeleton className='h-40 w-full' />
            </div>
          ) : (
            <>
              <div className='grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-6'>
                <StatCard label='Vectors' value={formatNumber(details.vectors)} />
                <StatCard
                  label='Collections'
                  value={formatNumber(details.collectionsCount)}
                />
                <StatCard
                  label='Content size'
                  value={formatBytes(details.contentSize)}
                />
                <StatCard
                  label='Vector size'
                  value={formatBytes(details.vectorSize)}
                />
                <StatCard
                  label='Index size'
                  value={formatBytes(details.indexSize)}
                />
                <StatCard label='Users' value={formatNumber(details.usersCount)} />
              </div>

              <div className='grid gap-6 lg:grid-cols-2'>
                <section className='space-y-2'>
                  <h3 className='text-sm font-semibold'>Collections</h3>
                  <div className='overflow-hidden rounded-md border'>
                    <Table>
                      <TableHeader>
                        <TableRow>
                          <TableHead>Name</TableHead>
                          <TableHead>Vectors</TableHead>
                          <TableHead>Dims</TableHead>
                          <TableHead>Index size</TableHead>
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {(details.collections ?? []).length === 0 ? (
                          <TableRow>
                            <TableCell
                              colSpan={4}
                              className='text-muted-foreground h-16 text-center'
                            >
                              No collections yet.
                            </TableCell>
                          </TableRow>
                        ) : (
                          (details.collections ?? []).map((collection) => (
                            <TableRow key={collection.name}>
                              <TableCell className='font-medium'>
                                {collection.name}
                              </TableCell>
                              <TableCell>
                                {formatNumber(collection.vectors)}
                              </TableCell>
                              <TableCell>
                                {formatNumber(collection.dimensions)}
                              </TableCell>
                              <TableCell>
                                {formatBytes(collection.index?.indexSizeBytes)}
                              </TableCell>
                            </TableRow>
                          ))
                        )}
                      </TableBody>
                    </Table>
                  </div>
                </section>

                <section className='space-y-2'>
                  <div className='flex items-center justify-between'>
                    <h3 className='text-sm font-semibold'>Users with access</h3>
                    {isSecurityAdmin && assignableUsers.length > 0 && (
                      <SelectDropdown
                        isControlled
                        defaultValue={undefined}
                        onValueChange={handleAssignUser}
                        placeholder='Grant access...'
                        className='w-44'
                        items={assignableUsers.map((u) => ({
                          label: u.userName ?? u.id ?? '',
                          value: u.id ?? '',
                        }))}
                      />
                    )}
                  </div>
                  <div className='space-y-1'>
                    {(details.users ?? []).length === 0 ? (
                      <p className='text-muted-foreground text-sm'>
                        No users have explicit access.
                      </p>
                    ) : (
                      (details.users ?? []).map((user) => (
                        <div
                          key={user.userId}
                          className='flex items-center justify-between rounded-md border px-3 py-2'
                        >
                          <span className='text-sm'>{user.userName}</span>
                          {isSecurityAdmin && (
                            <Button
                              variant='ghost'
                              size='icon'
                              className='text-destructive hover:text-destructive size-7'
                              onClick={() => {
                                setCurrentDatabase(databaseName)
                                setCurrentUser(user)
                                setOpen('revoke-user')
                              }}
                            >
                              <UserMinus className='size-4' />
                              <span className='sr-only'>
                                Revoke access for {user.userName}
                              </span>
                            </Button>
                          )}
                        </div>
                      ))
                    )}
                  </div>
                </section>
              </div>
            </>
          )}
        </div>
      </SheetContent>
    </Sheet>
  )
}

function StatCard({ label, value }: { label: string; value: string }) {
  return (
    <Card className='gap-1 py-3'>
      <CardHeader className='px-3'>
        <CardTitle className='text-muted-foreground text-xs font-normal'>
          {label}
        </CardTitle>
      </CardHeader>
      <CardContent className='px-3 text-lg font-semibold'>{value}</CardContent>
    </Card>
  )
}
