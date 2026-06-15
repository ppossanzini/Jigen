import { defineComponent, ref, computed, onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import { ElMessage } from 'element-plus'

import { useUsersRolesStore } from '@/stores/usersRoles'
import type { RoleItem } from '@/modules/users/types'

interface RoleFormState {
  name: string
}

export default defineComponent({
  name: 'RolesMasterDetail',
  setup() {
    const { t } = useI18n()
    const store = useUsersRolesStore()

    const selectedRole = ref<RoleItem | null>(null)
    const roleDialogVisible = ref(false)
    const deleteDialogVisible = ref(false)
    const savingRole = ref(false)
    const deletingRole = ref(false)

    const roleForm = ref<RoleFormState>({
      name: '',
    })

    const isRoleCreateMode = computed(() => !selectedRole.value)
    const roleDialogTitle = computed(() =>
      isRoleCreateMode.value
        ? t('security.roles.dialog.createTitle')
        : t('security.roles.dialog.editTitle')
    )

    const roleUsersData = computed(() => {
      if (!selectedRole.value) return []
      return store.users.filter((user) => user.roles.includes(selectedRole.value!.name))
    })

    function onSelectRole(row: RoleItem) {
      selectedRole.value = row
    }

    function onCreateRoleClick() {
      roleForm.value = { name: '' }
      roleDialogVisible.value = true
    }

    function onEditRoleClick() {
      if (!selectedRole.value) return
      roleForm.value = {
        name: selectedRole.value.name,
      }
      roleDialogVisible.value = true
    }

    function onRoleDialogClose() {
      roleDialogVisible.value = false
      roleForm.value = { name: '' }
    }

    async function onSaveRoleClick() {
      if (!roleForm.value.name.trim()) {
        ElMessage.error(t('security.roles.validation.nameRequired'))
        return
      }

      savingRole.value = true
      try {
        if (isRoleCreateMode.value) {
          await store.createRole({
            name: roleForm.value.name.trim(),
          })
          ElMessage.success(t('security.roles.feedback.created'))
        } else if (selectedRole.value) {
          await store.updateRole(selectedRole.value.id, {
            name: roleForm.value.name.trim(),
          })
          ElMessage.success(t('security.roles.feedback.updated'))
          await store.loadRoles()
          selectedRole.value = store.roles.find((r) => r.id === selectedRole.value?.id) || null
        }
        roleDialogVisible.value = false
      } catch (error) {
        const msg = error instanceof Error ? error.message : t('security.common.error')
        ElMessage.error(msg)
      } finally {
        savingRole.value = false
      }
    }

    function onDeleteRoleClick() {
      deleteDialogVisible.value = true
    }

    function onDeleteDialogClose() {
      deleteDialogVisible.value = false
    }

    async function onConfirmDeleteRoleClick() {
      if (!selectedRole.value?.id) return
      deletingRole.value = true
      try {
        await store.deleteRole(selectedRole.value.id)
        ElMessage.success(t('security.roles.feedback.deleted'))
        selectedRole.value = null
        deleteDialogVisible.value = false
      } catch (error) {
        const msg = error instanceof Error ? error.message : t('security.common.error')
        ElMessage.error(msg)
      } finally {
        deletingRole.value = false
      }
    }

    onMounted(async () => {
      try {
        await store.loadAll()
      } catch (error) {
        const msg = error instanceof Error ? error.message : t('security.common.error')
        ElMessage.error(msg)
      }
    })

    return {
      t,
      store,
      selectedRole,
      roleDialogVisible,
      deleteDialogVisible,
      savingRole,
      deletingRole,
      roleForm,
      isRoleCreateMode,
      roleDialogTitle,
      roleUsersData,
      onSelectRole,
      onCreateRoleClick,
      onEditRoleClick,
      onRoleDialogClose,
      onSaveRoleClick,
      onDeleteRoleClick,
      onDeleteDialogClose,
      onConfirmDeleteRoleClick,
    }
  },
})
