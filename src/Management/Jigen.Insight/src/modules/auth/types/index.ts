import type { WorkspaceOption } from '~types/auth'

export interface SignInFormModel {
  email: string
  password: string
  rememberMe: boolean
}

export interface LastWorkspaceInfo {
  name: string
}

export const workspaceOptions: WorkspaceOption[] = [
  { id: 'project-orion', label: 'Project Orion' },
  { id: 'vector-lab', label: 'Vector Lab' },
  { id: 'research-prod', label: 'Research Prod' },
]

export const defaultLastWorkspace: LastWorkspaceInfo = {
  name: 'Project Orion',
}
