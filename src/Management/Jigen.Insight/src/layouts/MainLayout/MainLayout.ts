import { defineComponent } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import { ref } from 'vue'

import { useAuthStore } from '@/stores/auth'
import { useNavigationStore } from '@/stores/navigation'
import { applyTheme, getCurrentTheme, type AppTheme } from '@/stores/theme'

export default defineComponent({
  name: 'MainLayout',
  setup() {
    const { t } = useI18n()
    const router = useRouter()
    const authStore = useAuthStore()
    const navigationStore = useNavigationStore()

    const isDarkMode = ref<string>(getCurrentTheme())

    async function onMenuSelect(index: string) {
      navigationStore.setActiveMenu(index)

      if (index === 'app-home') {
        navigationStore.setCurrentFeature('dashboard')
        await router.push({ name: 'app-home' })
        return
      }

      if (index === 'app-collections') {
        navigationStore.setCurrentFeature('collections')
        await router.push({ name: 'coming-soon', query: { feature: 'collections' } })
        return
      }

      if (index === 'app-security-users') {
        navigationStore.setCurrentFeature('security')
        await router.push({ name: 'app-security-users' })
        return
      }

      if (index === 'app-security-roles') {
        navigationStore.setCurrentFeature('security')
        await router.push({ name: 'app-security-roles' })
        return
      }
    }

    async function onLogout() {
      await authStore.logout()
      await router.push({ name: 'login' })
    }

    async function onQuickOpenCollections() {
      navigationStore.setCurrentFeature('collections')
      navigationStore.setActiveMenu('app-collections')
      await router.push({ name: 'coming-soon', query: { feature: 'collections' } })
    }

    function onSetTheme(theme: AppTheme) {
      applyTheme(theme)
      isDarkMode.value = theme
    }

    return {
      t,
      authStore,
      navigationStore,
      isDarkMode,
      onMenuSelect,
      onLogout,
      onQuickOpenCollections,
      onSetTheme,
    }
  },
})
