import { defineComponent } from 'vue'
import type { PropType } from 'vue'
import type { DatabaseRow } from '@/modules/jigen-db/types'

export default defineComponent({
  name: 'DatabaseDetailPanel',
  props: {
    row: {
      type: Object as PropType<DatabaseRow | null>,
      default: null,
    },
    title: {
      type: String,
      required: true,
    },
    emptyLabel: {
      type: String,
      required: true,
    },
    collectionsTitle: {
      type: String,
      required: true,
    },
    collections: {
      type: Array as PropType<string[]>,
      required: true,
    },
    loadingCollections: {
      type: Boolean,
      required: true,
    },
    collectionsLabel: {
      type: String,
      required: true,
    },
    noCollectionsLabel: {
      type: String,
      required: true,
    },
    chooseDatabaseLabel: {
      type: String,
      required: true,
    },
    loadingLabel: {
      type: String,
      required: true,
    },
  },
})
