<template>
  <section class="security-view">
    <SecurityRolesToolbar
      :title="t('security.roles.title')"
      :subtitle="t('security.roles.subtitle')"
      :create-label="t('security.roles.actions.create')"
      :refresh-label="t('security.roles.actions.refresh')"
      :delete-label="t('security.roles.actions.delete')"
      :delete-disabled="!selectedRole"
      @create="onOpenCreateRoleDialog"
      @refresh="onRefresh"
      @delete="onDeleteRole"
    />

    <div class="workspace-grid">
      <SecurityRolesTable
        :rows="visibleRoles"
        :current-page="currentPage"
        :page-size="pageSize"
        :total="securityStore.roles.length"
        :loading="securityStore.loadingRoles"
        :name-label="t('security.roles.columns.name')"
        :id-label="t('security.roles.columns.id')"
        :actions-label="t('security.roles.actions.actionsLabel')"
        :open-label="t('security.roles.actions.openDetail')"
        :per-page-label="t('security.common.rows')"
        :empty-label="t('security.common.empty')"
        @row-click="onSelectRole"
        @open="onSelectRole"
        @page-change="onPageChange"
      />

      <SecurityRolesDetailPanel
        :role="selectedRole"
        :users="securityStore.selectedRoleUsers"
        :title="t('security.roles.detailTitle')"
        :id-label="t('security.roles.columns.id')"
        :name-label="t('security.roles.columns.name')"
        :users-title="t('security.roles.usersTitle')"
        :no-users-label="t('security.roles.noUsers')"
        :choose-label="t('security.roles.emptySelection')"
        :edit-label="t('security.common.edit')"
        :delete-label="t('security.common.delete')"
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
