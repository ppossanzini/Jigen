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
          meta: { title: 'Dashboard | Jigen DB' },
        },
        {
          path: 'index-management',
          name: 'index-management',
          component: () => import('@/modules/jigen-db/views/IndexManagementView.vue'),
          meta: { title: 'Index Management | Jigen DB' },
        },
        {
          path: 'security',
          component: () => import('@/modules/security/views/SecurityLayout.vue'),
          children: [
            {
              path: '',
              redirect: { name: 'security-users' },
            },
            {
              path: 'users',
              name: 'security-users',
              component: () => import('@/modules/security/views/UsersMasterDetail.vue'),
              meta: { title: 'Security Users | Jigen DB' },
            },
            {
              path: 'roles',
              name: 'security-roles',
              component: () => import('@/modules/security/views/RolesMasterDetail.vue'),
              meta: { title: 'Security Roles | Jigen DB' },
            },
          ],
        },
        {
          path: 'coming-soon',
          name: 'coming-soon',
          component: () => import('@/modules/jigen-db/views/ComingSoonView.vue'),
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
