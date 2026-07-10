import { useEffect, useState } from 'react'
import { getRouteApi } from '@tanstack/react-router'
import {
  type SortingState,
  type VisibilityState,
  getCoreRowModel,
  getFilteredRowModel,
  getPaginationRowModel,
  getSortedRowModel,
  useReactTable,
} from '@tanstack/react-table'
import { ConfigDrawer } from '@/components/config-drawer'
import { DataTableViewOptions } from '@/components/data-table'
import { Header } from '@/components/layout/header'
import { Main } from '@/components/layout/main'
import { ProfileDropdown } from '@/components/profile-dropdown'
import { ThemeSwitch } from '@/components/theme-switch'
import { Input } from '@/components/ui/input'
import { useTableUrlState } from '@/hooks/use-table-url-state'
import { ForbiddenError } from '@/features/errors/forbidden'
import { useIsSecurityAdmin } from '@/stores/auth-store'
import { useUsers } from './hooks'
import { UsersDialogs } from './components/users-dialogs'
import { UsersPrimaryButtons } from './components/users-primary-buttons'
import { UsersProvider } from './components/users-provider'
import { UsersTable } from './components/users-table'
import { usersColumns as columns } from './components/users-columns'

const route = getRouteApi('/_authenticated/users/')

export function Users() {
  const isSecurityAdmin = useIsSecurityAdmin()
  const search = route.useSearch()
  const navigate = route.useNavigate()
  const { data: users = [] } = useUsers()

  const [columnVisibility, setColumnVisibility] = useState<VisibilityState>({})
  const [sorting, setSorting] = useState<SortingState>([])

  const {
    columnFilters,
    onColumnFiltersChange,
    pagination,
    onPaginationChange,
    ensurePageInRange,
  } = useTableUrlState({
    search,
    navigate,
    pagination: { defaultPage: 1, defaultPageSize: 10 },
    globalFilter: { enabled: false },
    columnFilters: [
      { columnId: 'userName', searchKey: 'userName', type: 'string' },
    ],
  })

  // eslint-disable-next-line react-hooks/incompatible-library
  const table = useReactTable({
    data: users,
    columns,
    state: {
      sorting,
      pagination,
      columnFilters,
      columnVisibility,
    },
    onPaginationChange,
    onColumnFiltersChange,
    onSortingChange: setSorting,
    onColumnVisibilityChange: setColumnVisibility,
    getPaginationRowModel: getPaginationRowModel(),
    getCoreRowModel: getCoreRowModel(),
    getFilteredRowModel: getFilteredRowModel(),
    getSortedRowModel: getSortedRowModel(),
  })

  useEffect(() => {
    ensurePageInRange(table.getPageCount())
  }, [table, ensurePageInRange])

  if (!isSecurityAdmin) {
    return <ForbiddenError />
  }

  return (
    <UsersProvider>
      <Header fixed>
        <Input
          placeholder='Filter users...'
          value={
            (table.getColumn('userName')?.getFilterValue() as string) ?? ''
          }
          onChange={(event) =>
            table.getColumn('userName')?.setFilterValue(event.target.value)
          }
          className='h-8 min-w-0 flex-1'
        />
        <DataTableViewOptions table={table} />
        <ThemeSwitch />
        <ConfigDrawer />
        <ProfileDropdown />
      </Header>

      <Main fluid className='flex flex-1 flex-col gap-4 sm:gap-6'>
        <div className='flex justify-end'>
          <UsersPrimaryButtons />
        </div>
        <UsersTable table={table} />
      </Main>

      <UsersDialogs />
    </UsersProvider>
  )
}
