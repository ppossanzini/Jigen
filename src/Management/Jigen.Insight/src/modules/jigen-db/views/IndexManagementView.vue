<template>
  <section class="index-view">
    <IndexToolbar
      :title="t('indexManagement.title')"
      :subtitle="t('indexManagement.subtitle')"
      :create-label="t('indexManagement.createIndex')"
      :import-label="t('indexManagement.importVectors')"
      :tune-label="t('indexManagement.tuneIndex')"
      :filter-label="t('indexManagement.filters')"
      :compact-label="t('app.compactView')"
      :export-label="t('app.export')"
      @create="onPlaceholderAction"
      @import="onPlaceholderAction"
      @tune="onPlaceholderAction"
      @filter="onPlaceholderAction"
      @compact="onPlaceholderAction"
      @export="onPlaceholderAction"
    />

    <div class="workspace-grid">
      <IndexTable
        :rows="visibleRows"
        :current-page="currentPage"
        :page-size="pageSize"
        :total="rows.length"
        :dimension-label="t('indexManagement.dimension')"
        :metric-label="t('indexManagement.metric')"
        :shards-label="t('indexManagement.shardsReplicas')"
        :status-label="t('indexManagement.status')"
        :size-label="t('indexManagement.size')"
        :updated-label="t('indexManagement.updatedAt')"
        :actions-label="t('indexManagement.actions')"
        :refresh-action-label="t('indexManagement.refreshAction')"
        :edit-action-label="t('indexManagement.editAction')"
        :delete-action-label="t('indexManagement.deleteAction')"
        :per-page-label="t('indexManagement.perPage')"
        @row-click="onRowClick"
        @page-change="onPageChange"
        @refresh="onPlaceholderAction"
        @edit="onPlaceholderAction"
        @delete="onPlaceholderAction"
      />

      <IndexDetailPanel
        :row="selectedRow"
        :title="t('indexManagement.details')"
        :empty-label="t('comingSoon.subtitle')"
        :status-label="t('indexManagement.status')"
        :size-label="t('indexManagement.size')"
        :metric-label="t('indexManagement.metric')"
        :shards-label="t('indexManagement.shardsReplicas')"
        :updated-label="t('indexManagement.updatedAt')"
        :insights-title="t('indexManagement.healthInsights')"
      />
    </div>

    <div class="bottom-cards">
      <el-card>
        <h3>{{ t('indexManagement.healthInsights') }}</h3>
        <p>{{ t('indexManagement.healthCardText') }}</p>
      </el-card>
      <el-card>
        <h3>{{ t('indexManagement.storageOptimization') }}</h3>
        <p>{{ t('indexManagement.storageCardText') }}</p>
      </el-card>
      <el-card>
        <h3>{{ t('indexManagement.recentTasks') }}</h3>
        <p>{{ t('indexManagement.tasksCardText') }}</p>
      </el-card>
    </div>
  </section>
</template>

<script lang="ts" src="./IndexManagementView.ts"></script>
<style scoped lang="less" src="./IndexManagementView.less"></style>
