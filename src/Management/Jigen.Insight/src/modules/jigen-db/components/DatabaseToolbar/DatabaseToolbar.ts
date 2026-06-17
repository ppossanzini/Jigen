import { defineComponent } from 'vue'

export default defineComponent({
  name: 'DatabaseToolbar',
  emits: ['create', 'refresh', 'delete'],
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
    refreshLabel: {
      type: String,
      required: true,
    },
    deleteLabel: {
      type: String,
      required: true,
    },
    createDisabled: {
      type: Boolean,
      required: true,
    },
    deleteDisabled: {
      type: Boolean,
      required: true,
    },
    adminOnlyHint: {
      type: String,
      required: true,
    },
  },
})
