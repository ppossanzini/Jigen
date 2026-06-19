import { defineComponent } from 'vue'
import type { PropType } from 'vue'
import type { DatabaseCollectionDetail } from '@/services/databaseService'

export default defineComponent({
  name: 'DatabaseCollectionsPanel',
  props: {
    collections: {
      type: Array as PropType<DatabaseCollectionDetail[]>,
      required: true,
    },
    selectedCollectionName: {
      type: String,
      default: null,
    },
  },
  emits: ['select'],
})
