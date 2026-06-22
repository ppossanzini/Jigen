import { defineComponent } from 'vue'

export default defineComponent({
  name: 'SecurityUsersToolbar',
  emits: ['create', 'refresh', 'delete'],
  props: {
    deleteDisabled: {
      type: Boolean,
      required: true,
    },
  },
})
