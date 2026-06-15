import { defineComponent } from 'vue'
import { onMounted } from 'vue'
import { computed } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'

import { useNavigationStore } from '@/stores/navigation'
import { API_BASE_URL } from '@/services/baseRestService'

export default defineComponent({
  name: 'MainView',
  components: {
  },
  setup() {
    const { t } = useI18n()
    const router = useRouter()
    const navigationStore = useNavigationStore()

    const apiBaseUrl = API_BASE_URL

    const quickMetrics = computed(() => [
      { key: 'connections', label: t('main.metrics.connections'), value: '1' },
      { key: 'spaces', label: t('main.metrics.workspaces'), value: '3' },
      { key: 'alerts', label: t('main.metrics.alerts'), value: '0' },
      { key: 'latency', label: t('main.metrics.latency'), value: '< 10ms' },
    ])

    const timelineItems = computed(() => [
      { key: 'boot', event: t('main.timeline.boot'), time: 'now' },
      { key: 'auth', event: t('main.timeline.auth'), time: '2m' },
      { key: 'scan', event: t('main.timeline.scan'), time: '5m' },
      { key: 'ready', event: t('main.timeline.ready'), time: '8m' },
    ])

    onMounted(() => {
      navigationStore.setActiveMenu('app-home')
      navigationStore.setCurrentFeature('dashboard')
    })

    async function openCollections() {
      navigationStore.setCurrentFeature('collections')
      navigationStore.setActiveMenu('app-collections')
      await router.push({ name: 'coming-soon', query: { feature: 'collections' } })
    }

    async function openUsers() {
      navigationStore.setCurrentFeature('security')
      navigationStore.setActiveMenu('app-security-users')
      await router.push({ name: 'app-security-users' })
    }

    async function openDashboardDocs() {
      await router.push({ name: 'coming-soon', query: { feature: 'docs' } })
    }

    return {
      t,
      apiBaseUrl,
      quickMetrics,
      timelineItems,
      openCollections,
      openUsers,
      openDashboardDocs,
    }
  },
})
