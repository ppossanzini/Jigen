import { ref } from 'vue'
import { defineStore } from 'pinia'

export const useNavigationStore = defineStore('navigation', () => {
  const activeMenu = ref('app-home')
  const currentFeature = ref('dashboard')

  function setActiveMenu(value: string) {
    activeMenu.value = value
  }

  function setCurrentFeature(value: string) {
    currentFeature.value = value
  }

  return {
    activeMenu,
    currentFeature,
    setActiveMenu,
    setCurrentFeature,
  }
})
