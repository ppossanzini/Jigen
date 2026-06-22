import { defineComponent } from 'vue'
import type { PropType } from 'vue'
import type { SecurityRole, SecurityUser } from '@/stores/security'

export default defineComponent({
  name: 'SecurityRolesDetailPanel',
  emits: ['edit', 'delete'],
  props: {
    role: {
      type: Object as PropType<SecurityRole | null>,
      default: null,
    },
    users: {
      type: Array as PropType<SecurityUser[]>,
      required: true,
    },
    title: {
      type: String,
      required: true,
    },
    loadingUsers: {
      type: Boolean,
      required: true,
    },
  },
})
