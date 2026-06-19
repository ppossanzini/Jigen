import { computed, defineComponent } from 'vue'
import type { PropType } from 'vue'
import type { SecurityUserDetail } from '@/stores/security'

export default defineComponent({
  name: 'SecurityUsersDetailPanel',
  emits: ['update:selectedRoles', 'save-roles', 'edit', 'delete'],
  props: {
    user: {
      type: Object as PropType<SecurityUserDetail | null>,
      default: null,
    },
    selectedRoles: {
      type: Array as PropType<string[]>,
      required: true,
    },
    roleOptions: {
      type: Array as PropType<string[]>,
      required: true,
    },
    title: {
      type: String,
      required: true,
    },
    loading: {
      type: Boolean,
      required: true,
    },
    saving: {
      type: Boolean,
      required: true,
    },
  },
  setup(props, { emit }) {
    const editableRoles = computed({
      get: () => props.selectedRoles,
      set: (value: string[]) => emit('update:selectedRoles', value),
    })

    return {
      editableRoles,
    }
  },
})
