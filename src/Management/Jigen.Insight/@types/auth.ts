export interface LoginData {
  userName?: string | null
  password?: string | null
  workspace?: string | null
}

export interface LoginResult {
  token: string
  roles?: string[]
}

export interface WorkspaceOption {
  id: string
  label: string
}
