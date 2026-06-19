<template>
  <section class="database-table-card">
    <header class="panel-header">
      <h3>{{ $t('databaseManagement.columns.name') }}</h3>
    </header>

    <el-scrollbar class="database-scroll" max-height="100%">
      <div class="database-list" role="listbox" :aria-label="$t('databaseManagement.columns.name')">
        <button
          v-for="row in rows"
          :key="row.name"
          type="button"
          class="database-option"
          :class="{ 'is-active': row.name === selectedName }"
          :aria-pressed="row.name === selectedName"
          @click="onRowClick(row)"
        >
          <div class="database-title">{{ row.name }}</div>
          <small>{{ $t('databaseManagement.columns.collectionsCount') }}: {{ row.collectionsCount }}</small>
        </button>
      </div>
    </el-scrollbar>
  </section>
</template>

<script lang="ts" src="./DatabaseTable.ts"></script>
<style scoped lang="less" src="./DatabaseTable.less"></style>
