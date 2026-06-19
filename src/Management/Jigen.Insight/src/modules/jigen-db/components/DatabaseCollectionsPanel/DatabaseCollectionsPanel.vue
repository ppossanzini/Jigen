<template>
  <aside class="collections-panel">
    <header class="panel-header">
      <h3>{{ $t('databaseManagement.collectionsPanelTitle') }}</h3>
    </header>

    <p v-if="!collections.length" class="empty">{{ $t('databaseManagement.noCollections') }}</p>

    <el-scrollbar v-else class="collections-scroll" max-height="100%">
      <div class="collections-list" role="listbox" :aria-label="$t('databaseManagement.collectionsPanelTitle')">
        <div
          v-for="collection in collections"
          :key="collection.name"
          text
          class="collection-option"
          :class="{ 'is-active': collection.name === selectedCollectionName }"
          :aria-pressed="collection.name === selectedCollectionName"
          @click="$emit('select', collection.name)"
        >
          <div class="collection-title">{{ collection.name }}</div>
          <small>
            {{ $t('databaseManagement.detailsLabels.vectors') }}: {{ collection.vectors }} |
            {{ $t('databaseManagement.detailsLabels.dimensions') }}: {{ collection.dimensions }}
          </small>
        </div>
      </div>
    </el-scrollbar>
  </aside>
</template>

<script lang="ts">
import DatabaseCollectionsPanel from './DatabaseCollectionsPanel'

export default DatabaseCollectionsPanel
</script>
<style scoped lang="less" src="./DatabaseCollectionsPanel.less"></style>
