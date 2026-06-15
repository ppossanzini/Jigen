import { defineComponent } from 'vue'
import type { PropType } from 'vue'
import type { IndexRow } from '@/modules/jigen-db/types'

export default defineComponent({
  name: 'IndexDetailPanel',
  props: {
    row: {
      type: Object as PropType<IndexRow | null>,
      required: true,
    },
    title: {
      type: String,
      required: true,
    },
    emptyLabel: {
      type: String,
      required: true,
    },
    statusLabel: {
      type: String,
      required: true,
    },
    sizeLabel: {
      type: String,
      required: true,
    },
    metricLabel: {
      type: String,
      required: true,
    },
    shardsLabel: {
      type: String,
      required: true,
    },
    updatedLabel: {
      type: String,
      required: true,
    },
    insightsTitle: {
      type: String,
      required: true,
    },
  },
})
