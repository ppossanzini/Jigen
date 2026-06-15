import { computed, defineComponent, reactive, ref } from 'vue'

import type { RoleItem, UserItem } from '@/modules/users/types'

interface Props {
  title: string
  createLabel: string
  usernameLabel: string
  passwordLabel: string
  rolesLabel: string
  actionsLabel: string
  editLabel: string
  deleteLabel: string
  cancelLabel: string
  saveLabel: string
  createDialogTitle: string
  editDialogTitle: string
  loading: boolean
  saving: boolean
  users: UserItem[]
  roles: RoleItem[]
}

interface UserFormState {
  userName: string
  password: string
  roles: string[]
}

export default defineComponent({
  name: 'UsersPanel',
  props: {
    title: { type: String, required: true },
    createLabel: { type: String, required: true },
    usernameLabel: { type: String, required: true },
    passwordLabel: { type: String, required: true },
    rolesLabel: { type: String, required: true },
    actionsLabel: { type: String, required: true },
    editLabel: { type: String, required: true },
    deleteLabel: { type: String, required: true },
    cancelLabel: { type: String, required: true },
    saveLabel: { type: String, required: true },
    createDialogTitle: { type: String, required: true },
    editDialogTitle: { type: String, required: true },
    loading: { type: Boolean, required: true },
    saving: { type: Boolean, required: true },
    users: { type: Array as () => UserItem[], required: true },
    roles: { type: Array as () => RoleItem[], required: true },
  },
  emits: {
    create: (_payload: { userName: string; password: string; roles: string[] }) => true,
    update: (_payload: { id: string; userName: string; roles: string[] }) => true,
    delete: (_payload: { id: string }) => true,
  },
  setup(props: Props, { emit }) {
    const dialogVisible = ref(false)
    const editingUserId = ref('')

    const form = reactive<UserFormState>({
      userName: '',
      password: '',
      roles: [],
    })

    const isCreateMode = computed(() => editingUserId.value.length === 0)
    const dialogTitle = computed(() => (isCreateMode.value ? props.createDialogTitle : props.editDialogTitle))

    function resetForm() {
      form.userName = ''
      form.password = ''
      form.roles = []
      editingUserId.value = ''
    }

    function onCreateClick() {
      resetForm()
      dialogVisible.value = true
    }

    function onEditClick(user: UserItem) {
      editingUserId.value = user.id
      form.userName = user.userName || ''
      form.password = ''
      form.roles = Array.isArray(user.roles) ? [...user.roles] : []
      dialogVisible.value = true
    }

    function onDeleteClick(user: UserItem) {
      if (!user.id) {
        return
      }

      emit('delete', { id: user.id })
    }

    function onDialogClose() {
      dialogVisible.value = false
      resetForm()
    }

    function onSaveClick() {
      if (isCreateMode.value) {
        emit('create', {
          userName: form.userName.trim(),
          password: form.password,
          roles: [...form.roles],
        })
        onDialogClose()
        return
      }

      emit('update', {
        id: editingUserId.value,
        userName: form.userName.trim(),
        roles: [...form.roles],
      })
      onDialogClose()
    }

    function getRoleNames(userRoles: string[]) {
      if (!Array.isArray(userRoles) || userRoles.length === 0) {
        return ['-']
      }

      return userRoles.map((userRole) => props.roles.find((role) => role.id === userRole || role.name === userRole)?.name || userRole)
    }

    function canMutate(user: UserItem) {
      return Boolean(user.id && user.id.length > 0)
    }

    return {
      form,
      isCreateMode,
      dialogVisible,
      dialogTitle,
      onCreateClick,
      onEditClick,
      onDeleteClick,
      onDialogClose,
      onSaveClick,
      getRoleNames,
      canMutate,
    }
  },
})
