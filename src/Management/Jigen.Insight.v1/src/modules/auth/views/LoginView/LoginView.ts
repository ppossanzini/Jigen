import { defineComponent } from 'vue'
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { ElMessage } from 'element-plus'
import { useRouter } from 'vue-router'

import LoginCard from '@/modules/auth/components/LoginCard/LoginCard.vue'
import { useAuthStore } from '@/stores/auth'
import logo from '@/assets/styles/global/jigen3.png'

interface LoginSubmitPayload {
  userName: string
  password: string
}

export default defineComponent({
  name: 'LoginView',
  components: {
    LoginCard,
  },
  setup() {
    const { t } = useI18n()
    const router = useRouter()
    const authStore = useAuthStore()

    const errorText = ref('')
    const logoSrc = logo

    async function onSubmit(payload: LoginSubmitPayload) {
      if (!payload.userName || !payload.password) {
        errorText.value = t('auth.required')
        return
      }

      errorText.value = ''
      try {
        await authStore.login(payload)
        ElMessage.success(t('app.brand'))
        await router.push({ name: 'app-home' })
      } catch {
        errorText.value = t('auth.loginError')
      }
    }

    return {
      t,
      authStore,
      errorText,
      logoSrc,
      onSubmit,
    }
  },
})
