import { getRouteApi } from '@tanstack/react-router'
import { ConfigDrawer } from '@/components/config-drawer'
import { Header } from '@/components/layout/header'
import { Main } from '@/components/layout/main'
import { ProfileDropdown } from '@/components/profile-dropdown'
import { ThemeSwitch } from '@/components/theme-switch'
import { useDatabases } from './hooks'
import { DatabaseDetailPanel } from './components/database-detail-panel'
import { DatabaseDialogs } from './components/database-dialogs'
import { DatabasePrimaryButtons } from './components/database-primary-buttons'
import { DatabaseProvider } from './components/database-provider'
import { DatabaseTable } from './components/database-table'

const route = getRouteApi('/_authenticated/databases/')

export function Databases() {
  const { selected } = route.useSearch()
  const navigate = route.useNavigate()
  const { data: databases, isLoading } = useDatabases()

  function handleSelect(name: string) {
    navigate({ search: (prev) => ({ ...prev, selected: name }), replace: true })
  }

  function handlePanelOpenChange(open: boolean) {
    if (!open) {
      navigate({ search: (prev) => ({ ...prev, selected: undefined }), replace: true })
    }
  }

  return (
    <DatabaseProvider>
      <Header fixed>
        <div className='me-auto' />
        <ThemeSwitch />
        <ConfigDrawer />
        <ProfileDropdown />
      </Header>

      <Main fluid className='flex flex-1 flex-col gap-4 sm:gap-6'>
        <div className='flex justify-end'>
          <DatabasePrimaryButtons />
        </div>
        <DatabaseTable
          databases={databases ?? []}
          isLoading={isLoading}
          selected={selected ?? null}
          onSelect={handleSelect}
        />
      </Main>

      <DatabaseDetailPanel
        databaseName={selected ?? null}
        onOpenChange={handlePanelOpenChange}
      />
      <DatabaseDialogs />
    </DatabaseProvider>
  )
}
