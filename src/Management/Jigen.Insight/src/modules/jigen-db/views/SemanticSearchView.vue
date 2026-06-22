<template>
  <section class="semantic-search-view">
    <header class="view-header">
      <div>
        <h2>{{ t('semanticSearch.title') }}</h2>
        <p>{{ t('semanticSearch.subtitle') }}</p>
      </div>
      <el-tag v-if="hasSearchResults" type="info" effect="dark">
        {{ t('semanticSearch.labels.resultCount') }}: {{ resultRows.length }}
      </el-tag>
    </header>

    <div class="search-layout">
      <div class="search-layout__top">
        <SemanticSearchControlsPanel
          :database-names="databaseNames"
          :selected-database-name="selectedDatabaseName"
          :selected-collections="selectedCollections"
          :search-text="searchText"
          :top-results="topResults"
          :top-results-min="topResultsMin"
          :top-results-max="topResultsMax"
          :loading-databases="databaseStore.loadingDatabases"
          :collections-loading="collectionsLoading"
          :available-collections="availableCollections"
          :searching="searching"
          :can-run-search="canRunSearch"
          @update:selected-database-name="onUpdateSelectedDatabaseName"
          @update:selected-collections="onUpdateSelectedCollections"
          @update:search-text="onUpdateSearchText"
          @update:top-results="onUpdateTopResults"
          @run-search="onRunSearch"
          @search-enter="onSearchTextEnter"
          @clear="onClear"
        />
      </div>

      <div v-if="hasSearchResults" class="search-layout__main">
        <SemanticSearchResultsPanel
          :query-embedding-text="queryEmbeddingText"
          :result-rows="resultRows"
          @copy-query-embedding="copyQueryEmbeddingToClipboard"
          @copy-embedding="copyEmbeddingToClipboard"
          @copy-result-json="copyResultJsonToClipboard"
        />

        <SemanticSearchDiagnosticsPanel
          :search-path="searchPath"
          :query-embedding-time-ms="queryEmbeddingTimeMs"
          :global-operation-time-ms="globalOperationTimeMs"
          :result-rows="resultRows"
          :per-collection-metrics="perCollectionMetrics"
        />
      </div>
    </div>
  </section>
</template>

<script lang="ts" src="./SemanticSearchView.ts"></script>
<style scoped lang="less" src="./SemanticSearchView.less"></style>
