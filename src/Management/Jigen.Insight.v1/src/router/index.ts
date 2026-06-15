import { createRouter, createWebHistory } from 'vue-router'

import LoginView from '@/modules/auth/views/LoginView/LoginView.vue'
import MainLayout from '@/layouts/MainLayout/MainLayout.vue'
import MainView from '@/modules/main/views/MainView/MainView.vue'
import SecurityLayout from '@/modules/security/views/SecurityLayout/SecurityLayout.vue'
import UsersMasterDetail from '@/modules/security/views/UsersMasterDetail/UsersMasterDetail.vue'
import RolesMasterDetail from '@/modules/security/views/RolesMasterDetail/RolesMasterDetail.vue'
import ComingSoon from '@/views/ComingSoon/ComingSoon.vue'
import pinia from '@/store'
import { useAuthStore } from '@/stores/auth'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: '/login',
      name: 'login',
      component: LoginView,
      meta: {
        guestOnly: true,
      },
    },
    {
      path: '/app',
      component: MainLayout,
      meta: {
        requiresAuth: true,
      },
      children: [
        {
          path: '',
          name: 'app-home',
          component: MainView,
        },
        {
          path: 'coming-soon',
          name: 'coming-soon',
          component: ComingSoon,
        },
        {
          path: 'security',
          component: SecurityLayout,
          children: [
            {
              path: '',
              redirect: 'utenti',
            },
            {
              path: 'utenti',
              name: 'app-security-users',
              component: UsersMasterDetail,
            },
            {
              path: 'ruoli',
              name: 'app-security-roles',
              component: RolesMasterDetail,
            },
          ],
        },
      ],
    },
    {
      path: '/',
      redirect: '/app',
    },
    {
      path: '/:pathMatch(.*)*',
      redirect: '/app/coming-soon?feature=unknown',
    },
  ],
})

router.beforeEach((to) => {
  const authStore = useAuthStore(pinia)
  authStore.restoreSession()

  if (to.meta.requiresAuth && !authStore.isAuthenticated) {
    return { name: 'login' }
  }

  if (to.meta.guestOnly && authStore.isAuthenticated) {
    return { name: 'app-home' }
  }

  return true
})

export default router
