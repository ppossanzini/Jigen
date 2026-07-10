import { DatabaseX, Loader2 } from 'lucide-react'
import { cn } from '@/lib/utils'
import { useIsDatabaseAdmin } from '@/stores/auth-store'
import { Button } from '@/components/ui/button'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { useDatabaseContext } from './database-provider'

type DatabaseTableProps = {
  databases: string[]
  isLoading: boolean
  selected: string | null
  onSelect: (name: string) => void
}

export function DatabaseTable({
  databases,
  isLoading,
  selected,
  onSelect,
}: DatabaseTableProps) {
  const { setOpen, setCurrentDatabase } = useDatabaseContext()
  const isDatabaseAdmin = useIsDatabaseAdmin()

  return (
    <div className='overflow-hidden rounded-md border'>
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Name</TableHead>
            {isDatabaseAdmin && (
              <TableHead className='w-12 text-end'>Actions</TableHead>
            )}
          </TableRow>
        </TableHeader>
        <TableBody>
          {isLoading ? (
            <TableRow>
              <TableCell
                colSpan={isDatabaseAdmin ? 2 : 1}
                className='h-24 text-center'
              >
                <Loader2 className='mx-auto size-5 animate-spin' />
              </TableCell>
            </TableRow>
          ) : databases.length === 0 ? (
            <TableRow>
              <TableCell
                colSpan={isDatabaseAdmin ? 2 : 1}
                className='h-24 text-center'
              >
                No databases found.
              </TableCell>
            </TableRow>
          ) : (
            databases.map((name) => (
              <TableRow
                key={name}
                data-state={selected === name && 'selected'}
                className={cn(
                  'group/row cursor-pointer',
                  selected === name && 'bg-muted'
                )}
                onClick={() => onSelect(name)}
              >
                <TableCell className='font-medium'>{name}</TableCell>
                {isDatabaseAdmin && (
                  <TableCell className='text-end'>
                    <Button
                      variant='ghost'
                      size='icon'
                      className='text-destructive hover:text-destructive'
                      onClick={(event) => {
                        event.stopPropagation()
                        setCurrentDatabase(name)
                        setOpen('delete')
                      }}
                    >
                      <DatabaseX className='size-4' />
                      <span className='sr-only'>Delete {name}</span>
                    </Button>
                  </TableCell>
                )}
              </TableRow>
            ))
          )}
        </TableBody>
      </Table>
    </div>
  )
}
