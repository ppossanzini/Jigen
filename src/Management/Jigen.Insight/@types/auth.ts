export interface LoginData {
  userName?: string | null
  password?: string | null
  workspace?: string | null
}

export interface LoginResult {
  token: string
  roles?: string[]
}

export interface AuthorizationStartOptions {
  userName: string
  rememberMe: boolean
  prompt?: 'none' | 'login'
}

export interface AuthorizationCodeResult {
  token: string
  userName: string
  rememberMe: boolean
  roles?: string[]
}

export interface WorkspaceOption {
  id: string
  label: string
}
