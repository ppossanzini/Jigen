import { computed, defineComponent, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { ElMessage } from 'element-plus'
import ShellHeader from '@/modules/jigen-db/components/ShellHeader/ShellHeader.vue'
import ShellSidebar from '@/modules/jigen-db/components/ShellSidebar/ShellSidebar.vue'
import type { SidebarItem } from '@/modules/jigen-db/types'
import { useNavigationStore } from '@/stores/navigation'
import { useAuthStore } from '@/stores/auth'

export default defineComponent({
  name: 'AppShellView',
  components: {
    ShellHeader,
    ShellSidebar
  },
  setup() {
    const route = useRoute()
    const router = useRouter()
    const { t } = useI18n()
    const navigationStore = useNavigationStore()
    const authStore = useAuthStore()

    const workspaceName = computed(() => {
      const map: Record<string, string> = {
        'project-orion': 'Project Orion',
        'vector-lab': 'Vector Lab',
        'research-prod': 'Research Prod',
      }

      return map[authStore.selectedWorkspace] || 'Project Orion'
    })

    const userName = computed(() => authStore.userName || 'Guest User')

    const navItems = computed<SidebarItem[]>(() => [
      { key: 'home', label: t('nav.home'), iconClass: 'ti ti-home', routeName: 'dashboard-home' },
      { key: 'search', label: t('nav.search'), iconClass: 'ti ti-search', routeName: 'coming-soon' },
      { key: 'indexes', label: t('nav.indexes'), iconClass: 'ti ti-database', routeName: 'database-management' },
      {
        key: 'security',
        label: t('nav.security'),
        iconClass: 'ti ti-shield-lock',
        children: [
          {
            key: 'security-users',
            label: t('nav.securityUsers'),
            iconClass: 'ti ti-users',
            routeName: 'security-users',
          },
          {
            key: 'security-roles',
            label: t('nav.securityRoles'),
            iconClass: 'ti ti-shield-check',
            routeName: 'security-roles',
          },
        ],
      },
      { key: 'pipelines', label: t('nav.pipelines'), iconClass: 'ti ti-adjustments-horizontal', routeName: 'coming-soon' },
      { key: 'datasets', label: t('nav.datasets'), iconClass: 'ti ti-folder', routeName: 'coming-soon' },
      {
        key: 'settings', label: t('nav.settings'), iconClass: 'ti ti-settings', routeName: 'coming-soon',

      },
    ])


    watch(
      () => route.name,
      (routeName) => {
        if (routeName === 'dashboard-home') navigationStore.setActiveNav('home')
        if (routeName === 'database-management') navigationStore.setActiveNav('indexes')
        if (routeName === 'security-users') navigationStore.setActiveNav('security-users')
        if (routeName === 'security-roles') navigationStore.setActiveNav('security-roles')
      },
      { immediate: true },
    )

    const onNavigate = async (key: string) => {
      const findByKey = (items: SidebarItem[], lookupKey: string): SidebarItem | null => {
        for (const item of items) {
          if (item.key === lookupKey) {
            return item
          }

          if (item.children?.length) {
            const nested = findByKey(item.children, lookupKey)
            if (nested) {
              return nested
            }
          }
        }

        return null
      }

      const target = findByKey(navItems.value, key)

      if (!target?.routeName) return

      navigationStore.setActiveNav(target.key)

      if (target.routeName === 'coming-soon') {
        navigationStore.setFeatureContext(target.label)
        await router.push({ name: target.routeName, query: { feature: target.label } })
        return
      }

      await router.push({ name: target.routeName })
    }

    const onSearch = (term: string) => {
      if (!term.trim()) return
      ElMessage.info(t('app.searchNotice', { term }))
    }

    const onSwitchWorkspace = () => ElMessage.success(t('app.switch'))
    const onNotifications = () => ElMessage.info(t('app.notifications'))

    return {
      t,
      navigationStore,
      workspaceName,
      userName,
      navItems,
      onNavigate,
      onSearch,
      onSwitchWorkspace,
      onNotifications,
    }
  },
})
