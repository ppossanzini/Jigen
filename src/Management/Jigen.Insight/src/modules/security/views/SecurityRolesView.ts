import { computed, defineComponent, onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { useI18n } from 'vue-i18n'
import SecurityRolesToolbar from '@/modules/security/components/SecurityRolesToolbar/SecurityRolesToolbar.vue'
import SecurityRolesTable from '@/modules/security/components/SecurityRolesTable/SecurityRolesTable.vue'
import SecurityRolesDetailPanel from '@/modules/security/components/SecurityRolesDetailPanel/SecurityRolesDetailPanel.vue'
import { useSecurityStore, type SecurityRole } from '@/stores/security'

type RoleDialogMode = 'create' | 'edit'

interface RoleDialogForm {
  name: string
}

interface RoleDialogErrors {
  name: string
}

export default defineComponent({
  name: 'SecurityRolesView',
  components: {
    SecurityRolesToolbar,
    SecurityRolesTable,
    SecurityRolesDetailPanel,
  },
  setup() {
    const { t } = useI18n()
    const securityStore = useSecurityStore()

    const roleDialogVisible = ref(false)
    const roleDialogMode = ref<RoleDialogMode>('create')
    const roleDialogSaving = ref(false)

    const roleDialogForm = reactive<RoleDialogForm>({
      name: '',
    })

    const roleDialogErrors = reactive<RoleDialogErrors>({
      name: '',
    })

    const selectedRole = computed(() => securityStore.selectedRole)
    const roles = computed(() => securityStore.roles)
    const visibleRoles = roles

    const roleDialogTitle = computed(() =>
      roleDialogMode.value === 'create' ? t('security.roles.dialog.createTitle') : t('security.roles.dialog.editTitle'),
    )

    const onSelectRole = async (role: SecurityRole) => {
      securityStore.setSelectedRole(role.id)
      await securityStore.loadUsersForRole(role.id)
    }

    const refreshData = async () => {
      await securityStore.loadRoles()

      if (!securityStore.selectedRoleId) {
        const first = visibleRoles.value[0] ?? null

        if (first) {
          await onSelectRole(first)
        }

        return
      }

      await securityStore.loadUsersForRole(securityStore.selectedRoleId)
    }

    const onOpenCreateRoleDialog = () => {
      roleDialogMode.value = 'create'
      roleDialogForm.name = ''
      roleDialogErrors.name = ''
      roleDialogVisible.value = true
    }

    const onOpenEditRoleDialog = () => {
      if (!selectedRole.value) {
        ElMessage.warning(t('security.roles.feedback.selectRole'))
        return
      }

      roleDialogMode.value = 'edit'
      roleDialogForm.name = selectedRole.value.name
      roleDialogErrors.name = ''
      roleDialogVisible.value = true
    }

    const onCloseRoleDialog = () => {
      roleDialogVisible.value = false
    }

    const saveDialogRole = async () => {
      roleDialogErrors.name = ''

      const normalizedName = roleDialogForm.name.trim()

      if (!normalizedName) {
        roleDialogErrors.name = t('security.roles.validation.nameRequired')
        return
      }

      roleDialogSaving.value = true

      try {
        if (roleDialogMode.value === 'create') {
          await securityStore.createRole({ name: normalizedName })
          ElMessage.success(t('security.roles.feedback.created'))
        } else if (selectedRole.value) {
          await securityStore.updateRole(selectedRole.value.id, { name: normalizedName })
          ElMessage.success(t('security.roles.feedback.updated'))
        }

        roleDialogVisible.value = false
        await refreshData()
      } catch {
        ElMessage.error(t('security.common.error'))
      } finally {
        roleDialogSaving.value = false
      }
    }

    const onDeleteRole = async () => {
      if (!selectedRole.value) {
        ElMessage.warning(t('security.roles.feedback.selectRole'))
        return
      }

      try {
        await ElMessageBox.confirm(
          t('security.roles.feedback.deleteConfirm'),
          t('security.common.warning'),
          {
            type: 'warning',
            confirmButtonText: t('security.common.delete'),
            cancelButtonText: t('security.common.cancel'),
          },
        )

        await securityStore.deleteRole(selectedRole.value.id)
        const nextSelected = roles.value[0] ?? null

        if (nextSelected) {
          await onSelectRole(nextSelected)
        }

        ElMessage.success(t('security.roles.feedback.deleted'))
      } catch {
        // Dialog cancellation is expected and should not show feedback.
      }
    }

    const onRefresh = async () => {
      try {
        await refreshData()
        ElMessage.success(t('security.roles.feedback.refreshed'))
      } catch {
        ElMessage.error(t('security.common.error'))
      }
    }

    onMounted(() => {
      void refreshData()
    })

    return {
      t,
      securityStore,
      selectedRole,
      visibleRoles,
      roleDialogVisible,
      roleDialogTitle,
      roleDialogForm,
      roleDialogErrors,
      roleDialogSaving,
      onSelectRole,
      onOpenCreateRoleDialog,
      onOpenEditRoleDialog,
      onCloseRoleDialog,
      saveDialogRole,
      onDeleteRole,
      onRefresh,
    }
  },
})
