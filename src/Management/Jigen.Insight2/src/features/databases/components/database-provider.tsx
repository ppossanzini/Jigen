import React, { useState } from 'react'
import useDialogState from '@/hooks/use-dialog-state'
import type { DatabaseUserInfo } from '@/lib/api-types'

type DatabaseDialogType = 'create' | 'delete' | 'revoke-user'

type DatabaseContextType = {
  open: DatabaseDialogType | null
  setOpen: (str: DatabaseDialogType | null) => void
  /** Name of the database the current dialog operates on. */
  currentDatabase: string | null
  setCurrentDatabase: React.Dispatch<React.SetStateAction<string | null>>
  /** User targeted by the "revoke access" dialog. */
  currentUser: DatabaseUserInfo | null
  setCurrentUser: React.Dispatch<React.SetStateAction<DatabaseUserInfo | null>>
}

const DatabaseContext = React.createContext<DatabaseContextType | null>(null)

export function DatabaseProvider({ children }: { children: React.ReactNode }) {
  const [open, setOpen] = useDialogState<DatabaseDialogType>(null)
  const [currentDatabase, setCurrentDatabase] = useState<string | null>(null)
  const [currentUser, setCurrentUser] = useState<DatabaseUserInfo | null>(null)

  return (
    <DatabaseContext
      value={{
        open,
        setOpen,
        currentDatabase,
        setCurrentDatabase,
        currentUser,
        setCurrentUser,
      }}
    >
      {children}
    </DatabaseContext>
  )
}

// eslint-disable-next-line react-refresh/only-export-components
export const useDatabaseContext = () => {
  const context = React.useContext(DatabaseContext)

  if (!context) {
    throw new Error('useDatabaseContext has to be used within <DatabaseProvider>')
  }

  return context
}
