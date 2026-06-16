import { defineStore } from 'pinia'

interface AuthState {
  token: string | null
  selectedWorkspace: string
  userName: string | null
}

const AUTH_TOKEN_KEY = 'auth.token'
const AUTH_WORKSPACE_KEY = 'auth.workspace'
const AUTH_USERNAME_KEY = 'auth.userName'

export const useAuthStore = defineStore('auth', {
  state: (): AuthState => ({
    token: localStorage.getItem(AUTH_TOKEN_KEY) || sessionStorage.getItem(AUTH_TOKEN_KEY),
    selectedWorkspace: localStorage.getItem(AUTH_WORKSPACE_KEY) || 'project-orion',
    userName:
      localStorage.getItem(AUTH_USERNAME_KEY) || sessionStorage.getItem(AUTH_USERNAME_KEY),
  }),
  getters: {
    isAuthenticated: (state) => Boolean(state.token),
  },
  actions: {
    setWorkspace(workspace: string) {
      this.selectedWorkspace = workspace
      localStorage.setItem(AUTH_WORKSPACE_KEY, workspace)
    },
    persistSession(token: string, userName: string, rememberMe: boolean) {
      this.token = token
      this.userName = userName

      const targetStorage = rememberMe ? localStorage : sessionStorage
      const otherStorage = rememberMe ? sessionStorage : localStorage

      targetStorage.setItem(AUTH_TOKEN_KEY, token)
      targetStorage.setItem(AUTH_WORKSPACE_KEY, this.selectedWorkspace)
      targetStorage.setItem(AUTH_USERNAME_KEY, userName)

      otherStorage.removeItem(AUTH_TOKEN_KEY)
      otherStorage.removeItem(AUTH_USERNAME_KEY)
    },
    logout() {
      this.token = null
      this.userName = null

      localStorage.removeItem(AUTH_TOKEN_KEY)
      sessionStorage.removeItem(AUTH_TOKEN_KEY)
      localStorage.removeItem(AUTH_USERNAME_KEY)
      sessionStorage.removeItem(AUTH_USERNAME_KEY)
    },
  },
})
