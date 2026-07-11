import { ConfigDrawer } from '@/components/config-drawer'
import { Header } from '@/components/layout/header'
import { Main } from '@/components/layout/main'
import { ProfileDropdown } from '@/components/profile-dropdown'
import { ThemeSwitch } from '@/components/theme-switch'
import { SettingsAppearance } from './appearance'

export function Settings() {
  return (
    <>
      <Header>
        <div className='me-auto' />
        <ThemeSwitch />
        <ConfigDrawer />
        <ProfileDropdown />
      </Header>

      <Main fixed fluid>
        <div className='flex w-full overflow-y-hidden p-1'>
          <SettingsAppearance />
        </div>
      </Main>
    </>
  )
}
