export interface SidebarItem {
  key: string
  label: string
  iconClass: string
  routeName?:
    | 'dashboard-home'
    | 'semantic-search'
    | 'graph-explorer'
    | 'database-management'
    | 'security-users'
    | 'security-roles'
    | 'coming-soon'
  children?: SidebarItem[]
}

export interface DatabaseRow {
  name: string
  collectionsCount: number
}

export interface DashboardMetric {
  title: string
  value: string
  hint: string
  tone: 'green' | 'cyan' | 'magenta' | 'neutral'
}
