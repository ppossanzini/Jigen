import {
  Database,
  HelpCircle,
  LayoutDashboard,
  Network,
  SearchIcon,
  Settings,
  Shield,
  Users,
} from 'lucide-react'
import { type SidebarData } from '../types'

export const sidebarData: SidebarData = {
  navGroups: [
    {
      title: 'General',
      items: [
        {
          title: 'Dashboard',
          url: '/',
          icon: LayoutDashboard,
        },
        {
          title: 'Databases',
          url: '/databases',
          icon: Database,
        },
        {
          title: 'Search',
          url: '/search',
          icon: SearchIcon,
        },
        {
          title: 'Graph Explorer',
          url: '/graph-explorer',
          icon: Network,
        },
        {
          title: 'Users',
          url: '/users',
          icon: Users,
        },
        {
          title: 'Roles',
          url: '/roles',
          icon: Shield,
        },
      ],
    },
    {
      title: 'Other',
      items: [
        {
          title: 'Settings',
          url: '/settings',
          icon: Settings,
        },
        {
          title: 'Help Center',
          url: '/help-center',
          icon: HelpCircle,
        },
      ],
    },
  ],
}
