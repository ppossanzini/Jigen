import { computed, defineComponent } from 'vue'

export default defineComponent({
  name: 'DashboardMetricCard',
  props: {
    title: {
      type: String,
      required: true,
    },
    value: {
      type: String,
      required: true,
    },
    hint: {
      type: String,
      required: true,
    },
    tone: {
      type: String as () => 'green' | 'cyan' | 'magenta' | 'neutral',
      default: 'neutral',
    },
  },
  setup(props) {
    const tagType = computed(() => {
      if (props.tone === 'green') return 'success'
      if (props.tone === 'magenta') return 'danger'
      if (props.tone === 'cyan') return 'info'
      return 'warning'
    })

    const tagClass = computed(() => `metric-tag metric-tag--${props.tone}`)

    return {
      tagType,
      tagClass,
    }
  },
})
