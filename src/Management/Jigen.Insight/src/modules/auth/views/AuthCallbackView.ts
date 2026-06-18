import { defineComponent, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { useI18n } from 'vue-i18n'
import { authService } from '@/services/authService'
import { useAuthStore } from '@/stores/auth'

const toQueryValue = (value: unknown): string | null => {
  if (typeof value === 'string') {
    return value
  }

  if (Array.isArray(value) && typeof value[0] === 'string') {
    return value[0]
  }

  return null
}

const parseHashParams = (): URLSearchParams => {
  const rawHash = window.location.hash

  if (!rawHash.startsWith('#')) {
    return new URLSearchParams()
  }

  const fragment = rawHash.slice(1)

  if (fragment.startsWith('/')) {
    const queryIndex = fragment.indexOf('?')

    if (queryIndex >= 0) {
      return new URLSearchParams(fragment.slice(queryIndex + 1))
    }
  }

  return new URLSearchParams(fragment)
}

export default defineComponent({
  name: 'AuthCallbackView',
  setup() {
    const route = useRoute()
    const router = useRouter()
    const { t } = useI18n()
    const authStore = useAuthStore()
    const loading = ref(true)

    const goToSignIn = async () => {
      await router.replace({ name: 'sign-in' })
    }

    onMounted(async () => {
      const hashParams = parseHashParams()
      const code = toQueryValue(route.query.code) || hashParams.get('code')
      const state = toQueryValue(route.query.state) || hashParams.get('state')
      const oauthError = toQueryValue(route.query.error) || hashParams.get('error')
      const directAccessToken = hashParams.get('access_token')

      try {
        if (oauthError) {
          throw new Error(oauthError)
        }

        if (directAccessToken) {
          const context = authService.consumeAuthorizationTransientContext()

          authStore.persistSession(directAccessToken, context.userName, context.rememberMe)
          ElMessage.success(t('auth.loginSuccess'))
          await router.replace({ name: 'dashboard-home' })
          return
        }

        if (!code || !state) {
          ElMessage.error(t('auth.oauthCallbackMissingCode'))
          await goToSignIn()
          return
        }

        const result = await authService.exchangeAuthorizationCode(code, state)

        authStore.persistSession(
          result.token,
          result.userName,
          result.rememberMe,
          result.roles ?? [],
        )

        ElMessage.success(t('auth.loginSuccess'))
        await router.replace({ name: 'dashboard-home' })
      } catch {
        ElMessage.error(t('auth.oauthCallbackError'))
        await goToSignIn()
      } finally {
        loading.value = false
      }
    })

    return {
      loading,
      t,
      goToSignIn,
    }
  },
})
