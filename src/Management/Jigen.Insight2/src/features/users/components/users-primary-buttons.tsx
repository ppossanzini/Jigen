import { UserPlus } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { useIsSecurityAdmin } from '@/stores/auth-store'
import { useUsersContext } from './users-provider'

export function UsersPrimaryButtons() {
  const isSecurityAdmin = useIsSecurityAdmin()
  const { setOpen } = useUsersContext()

  if (!isSecurityAdmin) {
    return null
  }

  return (
    <Button className='space-x-1' onClick={() => setOpen('add')}>
      <span>Add User</span> <UserPlus size={18} />
    </Button>
  )
}
