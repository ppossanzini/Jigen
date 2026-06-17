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
      const code = toQueryValue(route.query.code)
      const state = toQueryValue(route.query.state)
      const oauthError = toQueryValue(route.query.error)

      try {
        if (oauthError) {
          throw new Error(oauthError)
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
