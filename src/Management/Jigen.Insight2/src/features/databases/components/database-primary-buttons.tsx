import { DatabasePlus } from 'lucide-react'
import { useIsDatabaseAdmin } from '@/stores/auth-store'
import { Button } from '@/components/ui/button'
import { useDatabaseContext } from './database-provider'

export function DatabasePrimaryButtons() {
  const { setOpen } = useDatabaseContext()
  const isDatabaseAdmin = useIsDatabaseAdmin()

  if (!isDatabaseAdmin) return null

  return (
    <Button className='space-x-1' onClick={() => setOpen('create')}>
      <span>New Database</span> <DatabasePlus size={18} />
    </Button>
  )
}
