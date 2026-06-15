import { defineComponent } from 'vue'

export default defineComponent({
  name: 'IndexToolbar',
  emits: ['create', 'import', 'tune', 'filter', 'compact', 'export'],
  props: {
    title: {
      type: String,
      required: true,
    },
    subtitle: {
      type: String,
      required: true,
    },
    createLabel: {
      type: String,
      required: true,
    },
    importLabel: {
      type: String,
      required: true,
    },
    tuneLabel: {
      type: String,
      required: true,
    },
    filterLabel: {
      type: String,
      required: true,
    },
    compactLabel: {
      type: String,
      required: true,
    },
    exportLabel: {
      type: String,
      required: true,
    },
  },
})
