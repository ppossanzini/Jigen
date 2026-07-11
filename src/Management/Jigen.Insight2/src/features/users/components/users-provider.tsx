import React, { useState } from 'react'
import useDialogState from '@/hooks/use-dialog-state'
import type { UserSummary } from '@/lib/api-types'

type UsersDialogType = 'add' | 'edit' | 'delete'

type UsersContextType = {
  open: UsersDialogType | null
  setOpen: (str: UsersDialogType | null) => void
  currentRow: UserSummary | null
  setCurrentRow: React.Dispatch<React.SetStateAction<UserSummary | null>>
}

const UsersContext = React.createContext<UsersContextType | null>(null)

export function UsersProvider({ children }: { children: React.ReactNode }) {
  const [open, setOpen] = useDialogState<UsersDialogType>(null)
  const [currentRow, setCurrentRow] = useState<UserSummary | null>(null)

  return (
    <UsersContext value={{ open, setOpen, currentRow, setCurrentRow }}>
      {children}
    </UsersContext>
  )
}

// eslint-disable-next-line react-refresh/only-export-components
export const useUsersContext = () => {
  const usersContext = React.useContext(UsersContext)

  if (!usersContext) {
    throw new Error('useUsersContext has to be used within <UsersProvider>')
  }

  return usersContext
}
