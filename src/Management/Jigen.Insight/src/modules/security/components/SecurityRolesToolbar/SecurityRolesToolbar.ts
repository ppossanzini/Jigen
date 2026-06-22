import { defineComponent } from 'vue'

export default defineComponent({
  name: 'SecurityRolesToolbar',
  emits: ['create', 'refresh', 'delete'],
  props: {
    deleteDisabled: {
      type: Boolean,
      required: true,
    },
  },
})
