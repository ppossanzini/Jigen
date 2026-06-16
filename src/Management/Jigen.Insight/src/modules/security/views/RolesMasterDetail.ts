import { computed, defineComponent, onBeforeUnmount, onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { useI18n } from 'vue-i18n'
import type { SecurityRoleApiModel } from '~types/security'
import { securityService } from '@/services/securityService'
import { useSecurityStore } from '@/stores/security'
import RolesMasterTable from '@/modules/security/components/RolesMasterTable/RolesMasterTable.vue'

interface RoleDialogForm {
  name: string
}

export default defineComponent({
  name: 'RolesMasterDetail',
  components: {
    RolesMasterTable,
  },
  setup() {
    const { t } = useI18n()
    const securityStore = useSecurityStore()

    const currentPage = ref(1)
    const pageSize = ref(6)
    const roleDialogVisible = ref(false)
    const roleDialogSaving = ref(false)
    const editRoleId = ref<string | null>(null)
    const previousRoleName = ref<string | null>(null)
    const roleDialogForm = reactive<RoleDialogForm>({
      name: '',
    })

    const visibleRoles = computed(() => {
      const start = (currentPage.value - 1) * pageSize.value
      return securityStore.roles.slice(start, start + pageSize.value)
    })

    const selectedRole = computed(() => securityStore.selectedRole)

    const usersForSelectedRole = computed(() => {
      if (!selectedRole.value?.name) {
        return []
      }

      const roleName = selectedRole.value.name

      return securityStore.users.filter((user) =>
        (securityStore.userRolesById[user.id] ?? []).includes(roleName),
      )
    })

    const roleDialogTitle = computed(() =>
      editRoleId.value ? t('security.roles.dialog.editTitle') : t('security.roles.dialog.createTitle'),
    )

    const ensureCurrentPage = () => {
      const maxPages = Math.max(1, Math.ceil(securityStore.roles.length / pageSize.value))

      if (currentPage.value > maxPages) {
        currentPage.value = maxPages
      }
    }

    const calculateDynamicPageSize = () => {
      const reserved = 500
      const rowHeight = 52
      const available = Math.max(window.innerHeight - reserved, rowHeight * 4)
      pageSize.value = Math.max(4, Math.floor(available / rowHeight))
      ensureCurrentPage()
    }

    const onSelectRole = (row: SecurityRoleApiModel) => {
      securityStore.setSelectedRole(row.id)
    }

    const onRolesPageChange = (page: number) => {
      currentPage.value = page
      const nextSelected = visibleRoles.value[0]

      if (nextSelected) {
        onSelectRole(nextSelected)
      }
    }

    const onOpenCreateRoleDialog = () => {
      editRoleId.value = null
      previousRoleName.value = null
      roleDialogForm.name = ''
      roleDialogVisible.value = true
    }

    const onOpenEditRoleDialog = () => {
      if (!selectedRole.value) {
        return
      }

      editRoleId.value = selectedRole.value.id
      previousRoleName.value = selectedRole.value.name
      roleDialogForm.name = selectedRole.value.name ?? ''
      roleDialogVisible.value = true
    }

    const onCloseRoleDialog = () => {
      roleDialogVisible.value = false
    }

    const saveRole = async () => {
      const normalizedName = roleDialogForm.name.trim()

      if (!normalizedName) {
        ElMessage.warning(t('security.roles.validation.nameRequired'))
        return
      }

      roleDialogSaving.value = true

      try {
        if (editRoleId.value) {
          await securityService.updateRole(editRoleId.value, { name: normalizedName })

          if (previousRoleName.value && previousRoleName.value !== normalizedName) {
            Object.keys(securityStore.userRolesById).forEach((userId) => {
              const assignedRoles = securityStore.userRolesById[userId] ?? []

              securityStore.userRolesById[userId] = assignedRoles.map((roleName) =>
                roleName === previousRoleName.value ? normalizedName : roleName,
              )
            })
          }

          ElMessage.success(t('security.roles.feedback.updated'))
        } else {
          await securityService.createRole({ name: normalizedName })
          ElMessage.success(t('security.roles.feedback.created'))
        }

        await securityStore.loadRoles()
        ensureCurrentPage()
        roleDialogVisible.value = false
      } catch {
        ElMessage.error(t('security.common.error'))
      } finally {
        roleDialogSaving.value = false
      }
    }

    const onDeleteRole = async () => {
      if (!selectedRole.value?.id || !selectedRole.value.name) {
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

        await securityService.deleteRole(selectedRole.value.id)
        securityStore.removeRoleFromAssignments(selectedRole.value.name)
        securityStore.setSelectedRole(null)
        await securityStore.loadRoles()
        ensureCurrentPage()

        const fallback = visibleRoles.value[0]
        if (fallback) {
          onSelectRole(fallback)
        }

        ElMessage.success(t('security.roles.feedback.deleted'))
      } catch {
        // Dialog cancellation is expected and should not show feedback.
      }
    }

    onMounted(async () => {
      try {
        if (!securityStore.users.length || !securityStore.roles.length) {
          await securityStore.loadAll()
        }

        calculateDynamicPageSize()

        const firstRole = visibleRoles.value[0]
        if (firstRole) {
          onSelectRole(firstRole)
        }
      } catch {
        ElMessage.error(t('security.common.error'))
      }

      window.addEventListener('resize', calculateDynamicPageSize)
    })

    onBeforeUnmount(() => {
      window.removeEventListener('resize', calculateDynamicPageSize)
    })

    return {
      t,
      securityStore,
      currentPage,
      pageSize,
      visibleRoles,
      selectedRole,
      usersForSelectedRole,
      roleDialogVisible,
      roleDialogSaving,
      roleDialogTitle,
      roleDialogForm,
      onSelectRole,
      onRolesPageChange,
      onOpenCreateRoleDialog,
      onOpenEditRoleDialog,
      onCloseRoleDialog,
      saveRole,
      onDeleteRole,
    }
  },
})
