import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: '/sign-in',
      name: 'sign-in',
      component: () => import('@/modules/auth/views/SignInView.vue'),
      meta: { title: 'Sign In | Jigen DB' },
    },
    {
      path: '/auth/callback',
      name: 'auth-callback',
      component: () => import('@/modules/auth/views/AuthCallbackView.vue'),
      meta: { title: 'Authorizing | Jigen DB' },
    },
    {
      path: '/',
      component: () => import('@/modules/main/views/AppShellView.vue'),
      meta: { requiresAuth: true },
      children: [
        {
          path: '',
          redirect: { name: 'dashboard-home' },
        },
        {
          path: 'dashboard',
          name: 'dashboard-home',
          component: () => import('@/modules/jigen-db/views/DashboardHomeView.vue'),
          meta: { title: 'Dashboard | Jigen DB' },
        },
        {
          path: 'search',
          name: 'semantic-search',
          component: () => import('@/modules/jigen-db/views/SemanticSearchView.vue'),
          meta: { title: 'Semantic Search | Jigen DB' },
        },
        {
          path: 'graph-explorer',
          name: 'graph-explorer',
          component: () => import('@/modules/jigen-db/views/GraphExplorerView.vue'),
          meta: { title: 'Graph Explorer | Jigen DB' },
        },
        {
          path: 'database-management',
          name: 'database-management',
          component: () => import('@/modules/jigen-db/views/DatabaseManagementView.vue'),
          meta: { title: 'Database Management | Jigen DB' },
        },
        {
          path: 'security/users',
          name: 'security-users',
          component: () => import('@/modules/security/views/SecurityUsersView.vue'),
          meta: { title: 'Security Users | Jigen DB' },
        },
        {
          path: 'security/roles',
          name: 'security-roles',
          component: () => import('@/modules/security/views/SecurityRolesView.vue'),
          meta: { title: 'Security Roles | Jigen DB' },
        },
        {
          path: 'coming-soon',
          name: 'coming-soon',
          component: () => import('@/modules/main/views/ComingSoonView.vue'),
          meta: { title: 'Coming Soon | Jigen DB' },
        },
      ],
    },
  ],
})

router.beforeEach((to) => {
  const authStore = useAuthStore()
  const routeTitle = typeof to.meta.title === 'string' ? to.meta.title : 'Jigen DB'
  document.title = routeTitle

  if (to.meta.requiresAuth && !authStore.isAuthenticated) {
    return { name: 'sign-in' }
  }

  if (to.name === 'sign-in' && authStore.isAuthenticated) {
    return { name: 'dashboard-home' }
  }

  return true
})

export default router
