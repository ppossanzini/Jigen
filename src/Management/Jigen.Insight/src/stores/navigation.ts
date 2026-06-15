import { defineStore } from 'pinia'

interface NavigationState {
  activeNavKey: string
  featureContext: string
}

export const useNavigationStore = defineStore('navigation', {
  state: (): NavigationState => ({
    activeNavKey: 'home',
    featureContext: 'home',
  }),
  actions: {
    setActiveNav(key: string) {
      this.activeNavKey = key
    },
    setFeatureContext(feature: string) {
      this.featureContext = feature
    },
  },
})
