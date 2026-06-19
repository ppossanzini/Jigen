import { defineComponent } from 'vue'
import type { PropType } from 'vue'
import type { DatabaseRow } from '@/modules/jigen-db/types'
import type { DatabaseDetails } from '@/stores/database'

export default defineComponent({
  name: 'DatabaseDetailPanel',
  emits: ['update:selectedUserId', 'assign-user', 'request-remove-user'],
  props: {
    row: {
      type: Object as PropType<DatabaseRow | null>,
      default: null,
    },
    details: {
      type: Object as PropType<DatabaseDetails | null>,
      default: null,
    },
    title: {
      type: String,
      required: true,
    },
    canManageUsers: {
      type: Boolean,
      default: false,
    },
    availableUsers: {
      type: Array as PropType<Array<{ userId: string; userName: string }>>,
      default: () => [],
    },
    selectedUserId: {
      type: String,
      default: '',
    },
    assignUserLoading: {
      type: Boolean,
      default: false,
    },
  },
  setup(_, { emit }) {
    const formatBytes = (value: number): string => {
      if (value <= 0) {
        return '0 B'
      }

      const units = ['B', 'KB', 'MB', 'GB', 'TB']
      const exponent = Math.min(Math.floor(Math.log(value) / Math.log(1024)), units.length - 1)
      const scaled = value / 1024 ** exponent
      return `${scaled.toFixed(scaled < 10 && exponent > 0 ? 1 : 0)} ${units[exponent]}`
    }

    const formatDate = (value: string | null): string => {
      if (!value) {
        return ''
      }

      const parsed = new Date(value)
      if (Number.isNaN(parsed.getTime())) {
        return ''
      }

      return parsed.toLocaleString()
    }

    const onSelectUser = (userId: string) => {
      emit('update:selectedUserId', userId)
    }

    const onAssignUser = () => {
      emit('assign-user')
    }

    const onRequestRemoveUser = (userId: string, userName: string) => {
      emit('request-remove-user', { userId, userName })
    }

    return {
      formatBytes,
      formatDate,
      onSelectUser,
      onAssignUser,
      onRequestRemoveUser,
    }
  },
})
