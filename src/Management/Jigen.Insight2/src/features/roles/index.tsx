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
import { useRoles } from './hooks'
import { RolesDialogs } from './components/roles-dialogs'
import { RolesPrimaryButtons } from './components/roles-primary-buttons'
import { RolesProvider } from './components/roles-provider'
import { RolesTable } from './components/roles-table'
import { rolesColumns as columns } from './components/roles-columns'

const route = getRouteApi('/_authenticated/roles/')

export function Roles() {
  const isSecurityAdmin = useIsSecurityAdmin()
  const search = route.useSearch()
  const navigate = route.useNavigate()
  const { data: roles = [] } = useRoles()

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
    columnFilters: [{ columnId: 'name', searchKey: 'name', type: 'string' }],
  })

  // eslint-disable-next-line react-hooks/incompatible-library
  const table = useReactTable({
    data: roles,
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
    <RolesProvider>
      <Header fixed>
        <Input
          placeholder='Filter roles...'
          value={(table.getColumn('name')?.getFilterValue() as string) ?? ''}
          onChange={(event) =>
            table.getColumn('name')?.setFilterValue(event.target.value)
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
          <RolesPrimaryButtons />
        </div>
        <RolesTable table={table} />
      </Main>

      <RolesDialogs />
    </RolesProvider>
  )
}
