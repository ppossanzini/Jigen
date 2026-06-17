<template>
  <section class="database-view">
    <DatabaseToolbar
      :title="t('databaseManagement.title')"
      :subtitle="t('databaseManagement.subtitle')"
      :create-label="t('databaseManagement.createDatabase')"
      :refresh-label="t('databaseManagement.refresh')"
      :delete-label="t('databaseManagement.deleteDatabase')"
      :create-disabled="!canManageDatabases"
      :delete-disabled="!canManageDatabases || !selectedRow"
      :admin-only-hint="t('databaseManagement.feedback.adminOnly')"
      @create="onOpenCreateDialog"
      @refresh="onRefresh"
      @delete="onDeleteDatabase()"
    />

    <div class="workspace-grid">
      <DatabaseTable
        :rows="visibleRows"
        :current-page="currentPage"
        :page-size="pageSize"
        :total="rows.length"
        :name-label="t('databaseManagement.columns.name')"
        :collections-label="t('databaseManagement.columns.collectionsCount')"
        :actions-label="t('databaseManagement.actions')"
        :read-action-label="t('databaseManagement.readCollections')"
        :delete-action-label="t('databaseManagement.deleteDatabase')"
        :delete-disabled="!canManageDatabases"
        :admin-only-hint="t('databaseManagement.feedback.adminOnly')"
        :per-page-label="t('databaseManagement.perPage')"
        @row-click="onRowClick"
        @page-change="onPageChange"
        @read-collections="onReadCollections"
        @delete="onDeleteDatabase"
      />

      <DatabaseDetailPanel
        :row="selectedRow"
        :title="t('databaseManagement.details')"
        :empty-label="t('databaseManagement.empty')"
        :collections-title="t('databaseManagement.collectionsTitle')"
        :collections="selectedCollections"
        :loading-collections="databaseStore.loadingCollections"
        :collections-label="t('databaseManagement.columns.collectionsCount')"
        :no-collections-label="t('databaseManagement.noCollections')"
        :choose-database-label="t('databaseManagement.chooseDatabase')"
        :loading-label="t('databaseManagement.loadingCollections')"
      />
    </div>

    <el-dialog
      :model-value="createDialogVisible"
      :title="t('databaseManagement.createDialog.title')"
      width="26rem"
      @close="onCloseCreateDialog"
    >
      <el-form @submit.prevent>
        <el-form-item :label="t('databaseManagement.createDialog.nameLabel')">
          <el-input
            v-model="createForm.name"
            :placeholder="t('databaseManagement.createDialog.namePlaceholder')"
            clearable
            maxlength="120"
            show-word-limit
          />
        </el-form-item>
      </el-form>

      <template #footer>
        <div class="dialog-actions">
          <el-button @click="onCloseCreateDialog">{{ t('databaseManagement.cancel') }}</el-button>
          <el-button type="primary" :loading="createSaving" @click="onCreateDatabase">
            {{ t('databaseManagement.createDatabase') }}
          </el-button>
        </div>
      </template>
    </el-dialog>
  </section>
</template>

<script lang="ts" src="./DatabaseManagementView.ts"></script>
<style scoped lang="less" src="./DatabaseManagementView.less"></style>
