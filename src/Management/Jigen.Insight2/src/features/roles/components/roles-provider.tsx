import React, { useState } from 'react'
import useDialogState from '@/hooks/use-dialog-state'
import type { RoleSummary } from '@/lib/api-types'

type RolesDialogType = 'add' | 'edit' | 'delete' | 'view-users'

type RolesContextType = {
  open: RolesDialogType | null
  setOpen: (str: RolesDialogType | null) => void
  currentRow: RoleSummary | null
  setCurrentRow: React.Dispatch<React.SetStateAction<RoleSummary | null>>
}

const RolesContext = React.createContext<RolesContextType | null>(null)

export function RolesProvider({ children }: { children: React.ReactNode }) {
  const [open, setOpen] = useDialogState<RolesDialogType>(null)
  const [currentRow, setCurrentRow] = useState<RoleSummary | null>(null)

  return (
    <RolesContext value={{ open, setOpen, currentRow, setCurrentRow }}>
      {children}
    </RolesContext>
  )
}

// eslint-disable-next-line react-refresh/only-export-components
export const useRolesContext = () => {
  const rolesContext = React.useContext(RolesContext)

  if (!rolesContext) {
    throw new Error('useRolesContext has to be used within <RolesProvider>')
  }

  return rolesContext
}
