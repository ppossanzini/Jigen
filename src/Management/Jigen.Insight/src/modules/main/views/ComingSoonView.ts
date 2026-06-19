import { computed, defineComponent } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useNavigationStore } from '@/stores/navigation'

export default defineComponent({
  name: 'ComingSoonView',
  setup() {
    const route = useRoute()
    const router = useRouter()
    const { t } = useI18n()
    const navigationStore = useNavigationStore()

    const featureName = computed(() => {
      const fromQuery = typeof route.query.feature === 'string' ? route.query.feature : ''
      return fromQuery || navigationStore.featureContext
    })

    const goBack = async () => {
      navigationStore.setActiveNav('home')
      await router.push({ name: 'dashboard-home' })
    }

    return {
      t,
      featureName,
      goBack,
    }
  },
})
