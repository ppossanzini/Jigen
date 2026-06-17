import { defineComponent } from 'vue'

export default defineComponent({
  name: 'SecurityUsersToolbar',
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
    deleteDisabled: {
      type: Boolean,
      required: true,
    },
  },
})
