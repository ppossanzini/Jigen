import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: '/sign-in',
      name: 'sign-in',
      component: () => import('@/modules/auth/views/SignInView.vue'),
    },
    {
      path: '/',
      component: () => import('@/modules/jigen-db/views/AppShellView.vue'),
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
        },
        {
          path: 'index-management',
          name: 'index-management',
          component: () => import('@/modules/jigen-db/views/IndexManagementView.vue'),
        },
        {
          path: 'coming-soon',
          name: 'coming-soon',
          component: () => import('@/modules/jigen-db/views/ComingSoonView.vue'),
        },
      ],
    },
  ],
})

router.beforeEach((to) => {
  const authStore = useAuthStore()

  if (to.meta.requiresAuth && !authStore.isAuthenticated) {
    return { name: 'sign-in' }
  }

  if (to.name === 'sign-in' && authStore.isAuthenticated) {
    return { name: 'dashboard-home' }
  }

  return true
})

export default router
