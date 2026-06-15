import { computed, ref } from 'vue'
import { defineStore } from 'pinia'

import identityService from '@/services/identityService'
import type { LoginData, LoginResponse } from '@/@types/openapi'

const TOKEN_KEY = 'jigen.auth.token'
const USERNAME_KEY = 'jigen.auth.username'

function extractToken(payload: LoginResponse | null | undefined): string {
  if (!payload || typeof payload !== 'object') {
    return ''
  }

  const candidate = payload.access_token || payload.token || payload.id_token
  return typeof candidate === 'string' ? candidate : ''
}

function extractUsername(payload: LoginResponse | null | undefined, fallback: string): string {
  if (!payload || typeof payload !== 'object') {
    return fallback
  }

  const candidate = payload.userName || payload.username
  return typeof candidate === 'string' && candidate.length > 0 ? candidate : fallback
}

export const useAuthStore = defineStore('auth', () => {
  const token = ref('')
  const userName = ref('')
  const isLoading = ref(false)

  const isAuthenticated = computed(() => userName.value.length > 0 || token.value.length > 0)

  function restoreSession() {
    token.value = localStorage.getItem(TOKEN_KEY) || ''
    userName.value = localStorage.getItem(USERNAME_KEY) || ''
  }

  function persistSession() {
    if (token.value) {
      localStorage.setItem(TOKEN_KEY, token.value)
    } else {
      localStorage.removeItem(TOKEN_KEY)
    }

    if (userName.value) {
      localStorage.setItem(USERNAME_KEY, userName.value)
    } else {
      localStorage.removeItem(USERNAME_KEY)
    }
  }

  async function login(payload: LoginData) {
    isLoading.value = true
    try {
      const response = await identityService.login(payload)
      token.value = extractToken(response)
      userName.value = extractUsername(response, payload.userName || '')

      if (!token.value && !userName.value) {
        userName.value = payload.userName || ''
      }

      persistSession()
    } finally {
      isLoading.value = false
    }
  }

  async function logout() {
    isLoading.value = true
    try {
      await identityService.logout()
    } finally {
      token.value = ''
      userName.value = ''
      persistSession()
      isLoading.value = false
    }
  }

  return {
    token,
    userName,
    isLoading,
    isAuthenticated,
    restoreSession,
    login,
    logout,
  }
})
