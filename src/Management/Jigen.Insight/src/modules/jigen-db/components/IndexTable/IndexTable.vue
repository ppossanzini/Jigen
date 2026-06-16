<template>
  <section class="index-table-card">
    <el-table :data="rows" @row-click="onRowClick" height="100%">
      <el-table-column prop="name" label="Index" min-width="230">
        <template #default="scope">
          <div class="index-name-cell">
            <strong>{{ scope.row.name }}</strong>
            <small>{{ scope.row.description }}</small>
          </div>
        </template>
      </el-table-column>
      <el-table-column prop="dimension" :label="dimensionLabel" width="88" />
      <el-table-column prop="metric" :label="metricLabel" width="110" />
      <el-table-column prop="shardsReplicas" :label="shardsLabel" min-width="130" />
      <el-table-column prop="status" :label="statusLabel" width="120">
        <template #default="scope">
          <el-tag effect="dark" :type="toStatusType(scope.row.status)">{{ scope.row.status }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="size" :label="sizeLabel" width="115" />
      <el-table-column prop="updatedAt" :label="updatedLabel" min-width="170" />
      <el-table-column :label="actionsLabel" width="110">
        <template #default="scope">
          <div class="row-actions">
            <el-button
              class="icon-action"
              :aria-label="`${refreshActionLabel} ${scope.row.name}`"
              @click.stop="$emit('refresh')"
            >
              <i class="ti ti-refresh" />
            </el-button>
            <el-button
              class="icon-action"
              :aria-label="`${editActionLabel} ${scope.row.name}`"
              @click.stop="$emit('edit')"
            >
              <i class="ti ti-adjustments" />
            </el-button>
            <el-button
              class="icon-action danger"
              :aria-label="`${deleteActionLabel} ${scope.row.name}`"
              @click.stop="$emit('delete')"
            >
              <i class="ti ti-trash" />
            </el-button>
          </div>
        </template>
      </el-table-column>
    </el-table>

    <div class="pagination-row">
      <el-pagination
        layout="prev, pager, next"
        :current-page="currentPage"
        :page-size="pageSize"
        :total="total"
        @current-change="onPageChange"
      />
      <span class="rows-counter">{{ perPageLabel }}: {{ pageSize }}</span>
    </div>
  </section>
</template>

<script lang="ts" src="./IndexTable.ts"></script>
<style scoped lang="less" src="./IndexTable.less"></style>
