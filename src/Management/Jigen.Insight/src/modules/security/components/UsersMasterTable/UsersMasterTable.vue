<template>
  <section class="master-table">
    <div class="panel-header master-table__header">
      <h3>{{ title }}</h3>
    </div>

    <el-table
      :data="rows"
      stripe
      highlight-current-row
      :empty-text="emptyLabel"
      class="master-table__body"
      @row-click="onRowClick"
      v-loading="loading"
    >
      <el-table-column prop="userName" :label="userNameLabel" min-width="180" />
    </el-table>

    <div class="master-table__footer">
      <el-pagination
        :current-page="currentPage"
        :page-size="pageSize"
        :total="total"
        layout="prev, pager, next"
        @current-change="onPageChange"
      />
      <span class="master-table__rows-label">{{ rowsLabel }}: {{ pageSize }}</span>
    </div>
  </section>
</template>

<script lang="ts" src="./UsersMasterTable.ts"></script>
<style scoped lang="less" src="./UsersMasterTable.less"></style>
