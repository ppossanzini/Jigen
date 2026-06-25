import { defineComponent, ref } from 'vue'

export default defineComponent({
  name: 'ShellHeader',
  props: {
    appName: {
      type: String,
      required: true,
    },
    workspace: {
      type: String,
      required: true,
    },
    userName: {
      type: String,
      required: true,
    },
    searchPlaceholder: {
      type: String,
      required: true,
    },
    searchLabel: {
      type: String,
      required: true,
    },
    notificationLabel: {
      type: String,
      required: true,
    },
    logoutLabel: {
      type: String,
      required: true,
    },
  },
  emits: ['search', 'switch-workspace', 'notifications', 'logout'],
  setup(_, { emit }) {
    const searchText = ref('')

    const onSearch = () => emit('search', searchText.value)
    const onSwitchWorkspace = () => emit('switch-workspace')
    const onNotifications = () => emit('notifications')
    const onUserMenuCommand = (command: string) => {
      if (command === 'logout') {
        emit('logout')
      }
    }

    return {
      searchText,
      onSearch,
      onSwitchWorkspace,
      onNotifications,
      onUserMenuCommand,
    }
  },
})
