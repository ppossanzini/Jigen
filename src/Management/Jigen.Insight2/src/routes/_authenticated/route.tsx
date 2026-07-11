import { createFileRoute, redirect } from '@tanstack/react-router'
import { getIsAuthenticated } from '@/stores/auth-store'
import { AuthenticatedLayout } from '@/components/layout/authenticated-layout'

export const Route = createFileRoute('/_authenticated')({
  beforeLoad: ({ location }) => {
    if (!getIsAuthenticated()) {
      throw redirect({
        to: '/sign-in',
        search: { redirect: location.href },
      })
    }
  },
  component: AuthenticatedLayout,
})
