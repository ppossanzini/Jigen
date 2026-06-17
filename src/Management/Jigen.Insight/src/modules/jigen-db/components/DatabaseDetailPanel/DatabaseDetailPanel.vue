<template>
  <aside class="detail-panel">
    <h3>{{ title }}</h3>

    <template v-if="row">
      <ul>
        <li><span>{{ collectionsLabel }}</span><strong>{{ row.collectionsCount }}</strong></li>
      </ul>

      <div class="insight-box">
        <h4>{{ collectionsTitle }}</h4>
        <p v-if="loadingCollections">{{ loadingLabel }}</p>
        <p v-else-if="!collections.length">{{ noCollectionsLabel }}</p>
        <el-scrollbar v-else max-height="18rem">
          <ul class="collection-list">
            <li v-for="collection in collections" :key="collection">{{ collection }}</li>
          </ul>
        </el-scrollbar>
      </div>
    </template>

    <p v-else class="empty">{{ chooseDatabaseLabel }}</p>
  </aside>
</template>

<script lang="ts" src="./DatabaseDetailPanel.ts"></script>
<style scoped lang="less" src="./DatabaseDetailPanel.less"></style>
