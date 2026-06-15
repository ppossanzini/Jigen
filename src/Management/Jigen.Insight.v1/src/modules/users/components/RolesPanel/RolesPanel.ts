import { computed, defineComponent, reactive, ref } from 'vue'

import type { RoleItem } from '@/modules/users/types'

interface Props {
  title: string
  createLabel: string
  nameLabel: string
  actionsLabel: string
  editLabel: string
  deleteLabel: string
  cancelLabel: string
  saveLabel: string
  createDialogTitle: string
  editDialogTitle: string
  loading: boolean
  saving: boolean
  roles: RoleItem[]
}

interface RoleFormState {
  name: string
}

export default defineComponent({
  name: 'RolesPanel',
  props: {
    title: { type: String, required: true },
    createLabel: { type: String, required: true },
    nameLabel: { type: String, required: true },
    actionsLabel: { type: String, required: true },
    editLabel: { type: String, required: true },
    deleteLabel: { type: String, required: true },
    cancelLabel: { type: String, required: true },
    saveLabel: { type: String, required: true },
    createDialogTitle: { type: String, required: true },
    editDialogTitle: { type: String, required: true },
    loading: { type: Boolean, required: true },
    saving: { type: Boolean, required: true },
    roles: { type: Array as () => RoleItem[], required: true },
  },
  emits: {
    create: (_payload: { name: string }) => true,
    update: (_payload: { id: string; name: string }) => true,
    delete: (_payload: { id: string }) => true,
  },
  setup(props: Props, { emit }) {
    const dialogVisible = ref(false)
    const editingRoleId = ref('')

    const form = reactive<RoleFormState>({
      name: '',
    })

    const isCreateMode = computed(() => editingRoleId.value.length === 0)
    const dialogTitle = computed(() => (isCreateMode.value ? props.createDialogTitle : props.editDialogTitle))

    function resetForm() {
      form.name = ''
      editingRoleId.value = ''
    }

    function onCreateClick() {
      resetForm()
      dialogVisible.value = true
    }

    function onEditClick(role: RoleItem) {
      editingRoleId.value = role.id
      form.name = role.name || ''
      dialogVisible.value = true
    }

    function onDeleteClick(role: RoleItem) {
      if (!role.id) {
        return
      }

      emit('delete', { id: role.id })
    }

    function onDialogClose() {
      dialogVisible.value = false
      resetForm()
    }

    function onSaveClick() {
      if (isCreateMode.value) {
        emit('create', {
          name: form.name.trim(),
        })
        onDialogClose()
        return
      }

      emit('update', {
        id: editingRoleId.value,
        name: form.name.trim(),
      })
      onDialogClose()
    }

    return {
      form,
      dialogVisible,
      dialogTitle,
      onCreateClick,
      onEditClick,
      onDeleteClick,
      onDialogClose,
      onSaveClick,
    }
  },
})
