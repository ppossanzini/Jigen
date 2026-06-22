import { defineComponent } from 'vue'
import type { PropType } from 'vue'

export default defineComponent({
  name: 'CollectionExplorerPanel',
  props: {
    collection: {
      type: Object as PropType<server.database.CollectionInfo | null>,
      default: null,
    },
    title: {
      type: String,
      required: true,
    },
  },
  setup() {
    const formatBytes = (value: number | null | undefined): string => {
      if (typeof value !== 'number' || !Number.isFinite(value)) {
        return '0 B'
      }

      if (value <= 0) {
        return '0 B'
      }

      const units = ['B', 'KB', 'MB', 'GB', 'TB']
      const exponent = Math.min(Math.floor(Math.log(value) / Math.log(1024)), units.length - 1)
      const scaled = value / 1024 ** exponent
      return `${scaled.toFixed(scaled < 10 && exponent > 0 ? 1 : 0)} ${units[exponent]}`
    }

    return {
      formatBytes,
    }
  },
})
