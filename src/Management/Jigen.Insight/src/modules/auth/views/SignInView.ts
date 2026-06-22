import { defineComponent, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { useI18n } from 'vue-i18n'
import { authService } from '@/services/authService'
import SignInWorkspaceSelector from '@/modules/auth/components/SignInWorkspaceSelector/SignInWorkspaceSelector.vue'
import SignInHeroPanel from '@/modules/auth/components/SignInHeroPanel/SignInHeroPanel.vue'
import SignInCard from '@/modules/auth/components/SignInCard/SignInCard.vue'
import { defaultLastWorkspace, workspaceOptions, type SignInFormModel } from '@/modules/auth/types'
import { useAuthStore } from '@/stores/auth'

export default defineComponent({
  name: 'SignInView',
  components: {
    SignInWorkspaceSelector,
    SignInHeroPanel,
    SignInCard,
  },
  setup() {
    const router = useRouter()
    const { t } = useI18n()
    const authStore = useAuthStore()

    const loading = ref(false)
    const selectedWorkspace = ref(authStore.selectedWorkspace)
    const form = ref<SignInFormModel>({
      email: '',
      password: '',
      rememberMe: true,
    })

    const onWorkspaceChange = (workspace: string) => {
      selectedWorkspace.value = workspace
      authStore.setWorkspace(workspace)
    }

    const onForgotPassword = () => ElMessage.info(t('auth.forgotPasswordComingSoon'))
    const onOauthSignIn = async () => {
      loading.value = true

      try {
        await authService.startAuthorizationCodeFlow({
          userName: form.value.email,
          rememberMe: form.value.rememberMe,
          prompt: 'login',
        })
      } catch {
        ElMessage.error(t('auth.oauthStartError'))
      } finally {
        loading.value = false
      }
    }

    const onQuickDemo = async () => {
      await router.push({ name: 'dashboard-home' })
    }

    const onBrandAction = () => {
      ElMessage.success(t('auth.brandActionMessage'))
    }

    const onSubmit = async () => {
      loading.value = true

      try {
        const payload = {
          userName: form.value.email,
          password: form.value.password,
          workspace: selectedWorkspace.value,
        }

        const result = await authService.loginWithCredentials(payload)

        if (result?.token) {
          authStore.persistSession(
            result.token,
            form.value.email,
            form.value.rememberMe,
            result.roles ?? [],
          )

          ElMessage.success(t('auth.loginSuccess'))
          await router.push({ name: 'dashboard-home' })
          return
        }

        await authService.startAuthorizationCodeFlow({
          userName: form.value.email,
          rememberMe: form.value.rememberMe,
          prompt: 'none',
        })

      } catch {
        ElMessage.error(t('auth.loginError'))
      } finally {
        loading.value = false
      }
    }

    return {
      t,
      loading,
      form,
      selectedWorkspace,
      workspaceOptions,
      lastWorkspace: defaultLastWorkspace,
      onWorkspaceChange,
      onSubmit,
      onForgotPassword,
      onOauthSignIn,
      onQuickDemo,
      onBrandAction,
    }
  },
})
