import { defineComponent } from 'vue'
import type { PropType } from 'vue'

export default defineComponent({
  name: 'DatabaseCollectionsPanel',
  props: {
    collections: {
      type: Array as PropType<server.database.CollectionInfo[]>,
      required: true,
    },
    selectedCollectionName: {
      type: String,
      default: null,
    },
  },
  emits: ['select'],
})
