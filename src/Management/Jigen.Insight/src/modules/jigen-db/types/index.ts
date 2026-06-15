export interface SidebarItem {
  key: string
  label: string
  iconClass: string
  routeName: 'dashboard-home' | 'index-management' | 'coming-soon'
}

export interface IndexRow {
  id: string
  name: string
  description: string
  dimension: number
  metric: string
  shardsReplicas: string
  status: 'Healthy' | 'Warning' | 'Degraded'
  size: string
  updatedAt: string
  namespace: string
  owner: string
}

export interface DashboardMetric {
  title: string
  value: string
  hint: string
  tone: 'green' | 'cyan' | 'magenta' | 'neutral'
}
