<template>
  <section class="security-view">
    <SecurityRolesToolbar
      :delete-disabled="!selectedRole"
      @create="onOpenCreateRoleDialog"
      @refresh="onRefresh"
      @delete="onDeleteRole"
    />

    <div class="workspace-grid">
      <SecurityRolesTable
        :rows="visibleRoles"
        :loading="securityStore.loadingRoles"
        @row-click="onSelectRole"
        @open="onSelectRole"
      />

      <SecurityRolesDetailPanel
        :role="selectedRole"
        :users="securityStore.selectedRoleUsers"
        :title="selectedRole ? `${selectedRole.name} Details` : t('security.roles.detailTitle')"
        :loading-users="securityStore.loadingRoleUsers"
        @edit="onOpenEditRoleDialog"
        @delete="onDeleteRole"
      />
    </div>

    <el-dialog
      :model-value="roleDialogVisible"
      :title="roleDialogTitle"
      width="520px"
      @close="onCloseRoleDialog"
    >
      <el-form label-position="top">
        <el-form-item :label="t('security.roles.columns.name')" required :error="roleDialogErrors.name">
          <el-input v-model="roleDialogForm.name" />
        </el-form-item>
      </el-form>

      <template #footer>
        <div class="dialog-actions">
          <el-button @click="onCloseRoleDialog">{{ t('security.common.cancel') }}</el-button>
          <el-button type="primary" :loading="roleDialogSaving" @click="saveDialogRole">
            {{ t('security.common.save') }}
          </el-button>
        </div>
      </template>
    </el-dialog>
  </section>
</template>

<script lang="ts" src="./SecurityRolesView.ts"></script>
<style scoped lang="less" src="./SecurityRolesView.less"></style>
