import { createRouter, createWebHistory } from 'vue-router'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: '/',
      component: () => import('@/modules/jigen-db/views/AppShellView.vue'),
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

export default router
