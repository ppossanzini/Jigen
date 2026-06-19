<template>
  <section class="database-view">
    <DatabaseToolbar
      :create-disabled="!canManageDatabases"
      :delete-disabled="!canManageDatabases || !selectedRow"
      @create="onOpenCreateDialog"
      @refresh="onRefresh"
      @delete="onDeleteDatabase()"
    />

    <div class="workspace-grid" :class="workspaceGridClass">
      <DatabaseTable
        :rows="visibleRows"
        :selected-name="selectedRow?.name ?? null"
        @row-click="onRowClick"
      />

      <DatabaseDetailPanel
        :row="selectedRow"
        :details="selectedDetails"
        :title="selectedRow ? `${selectedRow.name} Details` : t('databaseManagement.details')"
        :can-assign-users="canManageDatabases"
        :available-users="assignableUsers"
        :selected-user-id="selectedAssignableUserId"
        :assign-user-loading="assignUserSaving"
        @update:selected-user-id="selectedAssignableUserId = $event"
        @assign-user="onAssignUserToDatabase"
      />

      <DatabaseCollectionsPanel
        v-if="showCollectionsPanel"
        :collections="selectedDatabaseCollections"
        :selected-collection-name="selectedCollectionName"
        @select="onSelectCollection"
      />

      <CollectionExplorerPanel
        v-if="showCollectionDetailsPanel"
        :collection="selectedCollection"
        :title="selectedCollection ? `${selectedCollection.name} Details` : t('databaseManagement.collectionDetailsTitle')"
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
