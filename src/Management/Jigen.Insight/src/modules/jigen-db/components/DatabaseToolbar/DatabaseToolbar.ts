import { defineComponent } from 'vue'

export default defineComponent({
  name: 'DatabaseToolbar',
  emits: ['create', 'refresh', 'delete'],
  props: {
    createDisabled: {
      type: Boolean,
      required: true,
    },
    deleteDisabled: {
      type: Boolean,
      required: true,
    },
  },
})
