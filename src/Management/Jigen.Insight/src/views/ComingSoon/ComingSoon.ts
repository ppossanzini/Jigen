import { defineComponent } from 'vue'
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'

export default defineComponent({
  name: 'ComingSoon',
  setup() {
    const route = useRoute()
    const router = useRouter()
    const { t } = useI18n()

    const featureLabel = computed(() => {
      const feature = route.query.feature
      return typeof feature === 'string' && feature.length > 0 ? feature : 'feature'
    })

    async function goBackHome() {
      await router.push({ name: 'app-home' })
    }

    return {
      t,
      featureLabel,
      goBackHome,
    }
  },
})
