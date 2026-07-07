<template>
  <aside class="collection-detail-panel">
    <header class="panel-header">
      <h3>{{ title }}</h3>
    </header>

    <p v-if="!collection" class="empty">{{ $t('databaseManagement.chooseCollection') }}</p>

    <template v-else>
      <ul class="summary-list">
        <li><span>{{ $t('databaseManagement.detailsLabels.collectionName') }}</span><strong>{{ collection.name }}</strong></li>
        <li><span>{{ $t('databaseManagement.detailsLabels.vectors') }}</span><strong>{{ collection.vectors }}</strong></li>
        <li><span>{{ $t('databaseManagement.detailsLabels.dimensions') }}</span><strong>{{ collection.dimensions }}</strong></li>
        <li><span>{{ $t('databaseManagement.detailsLabels.contentSize') }}</span><strong>{{ formatBytes(collection.contentSize) }}</strong></li>
        <li><span>{{ $t('databaseManagement.detailsLabels.vectorSize') }}</span><strong>{{ formatBytes(collection.vectorSize) }}</strong></li>
        <li><span>{{ $t('databaseManagement.detailsLabels.indexSize') }}</span><strong>{{ formatBytes(collection.index?.indexSizeBytes ?? 0) }}</strong></li>
        <li><span>{{ $t('databaseManagement.detailsLabels.liveNodes') }}</span><strong>{{ collection.index?.nodes ?? 0 }}</strong></li>
        <li><span>{{ $t('databaseManagement.detailsLabels.deletedNodes') }}</span><strong>{{ collection.index?.deletedNodes ?? 0 }}</strong></li>
        <li><span>{{ $t('databaseManagement.detailsLabels.maxLevel') }}</span><strong>{{ collection.index?.maxLevel ?? 0 }}</strong></li>
        <li><span>{{ $t('databaseManagement.detailsLabels.avgDegree') }}</span><strong>{{ collection.index?.averageDegree ?? 0 }}</strong></li>
        <li><span>{{ $t('databaseManagement.detailsLabels.quantization') }}</span><strong>{{ collection.index?.quantization ?? '-' }}</strong></li>
      </ul>

      <div class="explorer-box">
        <h4>{{ $t('databaseManagement.collectionExplorerTitle') }}</h4>
        <p>{{ $t('databaseManagement.collectionExplorerEmpty') }}</p>
      </div>
    </template>
  </aside>
</template>

<script lang="ts">
import CollectionExplorerPanel from './CollectionExplorerPanel'

export default CollectionExplorerPanel
</script>
<style scoped lang="less" src="./CollectionExplorerPanel.less"></style>
