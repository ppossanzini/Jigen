import { DotsHorizontalIcon } from '@radix-ui/react-icons'
import { type Row } from '@tanstack/react-table'
import { Edit, Trash2, Users } from 'lucide-react'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuShortcut,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { useIsSecurityAdmin } from '@/stores/auth-store'
import type { RoleSummary } from '@/lib/api-types'
import { useRolesContext } from './roles-provider'

type DataTableRowActionsProps = {
  row: Row<RoleSummary>
}

export function DataTableRowActions({ row }: DataTableRowActionsProps) {
  const isSecurityAdmin = useIsSecurityAdmin()
  const { setOpen, setCurrentRow } = useRolesContext()

  return (
    <DropdownMenu modal={false}>
      <DropdownMenuTrigger asChild>
        <Button
          variant='ghost'
          className='flex h-8 w-8 p-0 data-[state=open]:bg-muted'
        >
          <DotsHorizontalIcon className='h-4 w-4' />
          <span className='sr-only'>Open menu</span>
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align='end' className='w-40'>
        <DropdownMenuItem
          onClick={() => {
            setCurrentRow(row.original)
            setOpen('view-users')
          }}
        >
          View users
          <DropdownMenuShortcut>
            <Users size={16} />
          </DropdownMenuShortcut>
        </DropdownMenuItem>

        {isSecurityAdmin && (
          <>
            <DropdownMenuSeparator />
            <DropdownMenuItem
              onClick={() => {
                setCurrentRow(row.original)
                setOpen('edit')
              }}
            >
              Edit
              <DropdownMenuShortcut>
                <Edit size={16} />
              </DropdownMenuShortcut>
            </DropdownMenuItem>
            <DropdownMenuSeparator />
            <DropdownMenuItem
              onClick={() => {
                setCurrentRow(row.original)
                setOpen('delete')
              }}
              className='text-red-500!'
            >
              Delete
              <DropdownMenuShortcut>
                <Trash2 size={16} />
              </DropdownMenuShortcut>
            </DropdownMenuItem>
          </>
        )}
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
