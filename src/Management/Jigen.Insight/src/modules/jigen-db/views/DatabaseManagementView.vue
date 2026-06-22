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
        :collections-count-by-database="collectionsCountByDatabase"
        :selected-name="selectedRow ?? null"
        @row-click="onRowClick"
      />

      <DatabaseDetailPanel
        :row="selectedRow"
        :details="selectedDetails"
        :title="selectedRow ? `${selectedRow} Details` : t('databaseManagement.details')"
        :can-manage-users="canManageDatabaseUsers"
        :available-users="assignableUsers"
        :selected-user-id="selectedAssignableUserId"
        :assign-user-loading="assignUserSaving"
        @update:selected-user-id="selectedAssignableUserId = $event"
        @assign-user="onAssignUserToDatabase"
        @request-remove-user="onOpenRevokeAccessDialog"
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

    <el-dialog
      :model-value="revokeAccessDialogVisible"
      :title="t('databaseManagement.revokeAccessDialog.title')"
      width="34rem"
      @close="onCloseRevokeAccessDialog"
    >
      <div class="revoke-access-dialog">
        <p class="revoke-access-description">
          {{ t('databaseManagement.revokeAccessDialog.description') }}
        </p>

        <ul class="revoke-access-metadata">
          <li>
            <span>{{ t('databaseManagement.revokeAccessDialog.databaseLabel') }}</span>
            <strong>{{ selectedRow ?? t('databaseManagement.notAvailable') }}</strong>
          </li>
          <li>
            <span>{{ t('databaseManagement.revokeAccessDialog.userLabel') }}</span>
            <strong>{{ revokeAccessTargetUser?.userName || t('databaseManagement.notAvailable') }}</strong>
          </li>
          <li>
            <span>{{ t('databaseManagement.revokeAccessDialog.userIdLabel') }}</span>
            <strong>{{ revokeAccessTargetUser?.userId || t('databaseManagement.notAvailable') }}</strong>
          </li>
        </ul>

        <el-alert
          type="warning"
          :closable="false"
          :title="t('databaseManagement.revokeAccessDialog.nonCascadeNotice')"
          show-icon
        />

        <el-checkbox v-model="revokeAccessAcknowledge" class="revoke-access-check">
          {{ t('databaseManagement.revokeAccessDialog.acknowledge') }}
        </el-checkbox>
      </div>

      <template #footer>
        <div class="dialog-actions">
          <el-button @click="onCloseRevokeAccessDialog">{{ t('databaseManagement.cancel') }}</el-button>
          <el-button
            type="danger"
            :loading="revokeAccessSaving"
            :disabled="!revokeAccessAcknowledge || !revokeAccessTargetUser?.userId"
            @click="onConfirmRevokeAccess"
          >
            {{ t('databaseManagement.revokeAccessDialog.confirmAction') }}
          </el-button>
        </div>
      </template>
    </el-dialog>
  </section>
</template>

<script lang="ts" src="./DatabaseManagementView.ts"></script>
<style scoped lang="less" src="./DatabaseManagementView.less"></style>
