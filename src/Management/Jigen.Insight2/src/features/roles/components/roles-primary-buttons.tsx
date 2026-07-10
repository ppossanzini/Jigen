import { Plus } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { useIsSecurityAdmin } from '@/stores/auth-store'
import { useRolesContext } from './roles-provider'

export function RolesPrimaryButtons() {
  const isSecurityAdmin = useIsSecurityAdmin()
  const { setOpen } = useRolesContext()

  if (!isSecurityAdmin) {
    return null
  }

  return (
    <Button className='space-x-1' onClick={() => setOpen('add')}>
      <span>Add Role</span> <Plus size={18} />
    </Button>
  )
}
