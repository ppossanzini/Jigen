<template>
  <div class="search-layout__details">
    <el-card class="panel">
      <h3>{{ $t('semanticSearch.labels.searchPath') }}</h3>
      <el-empty v-if="!searchPath.length" :description="$t('semanticSearch.labels.notCalculated')" />
      <el-timeline v-else>
        <el-timeline-item
          v-for="step in searchPath"
          :key="step.key"
          :timestamp="`${step.elapsedMs} ms`"
          placement="top"
        >
          <strong>{{ step.title }}</strong>
          <p>{{ step.detail }}</p>
        </el-timeline-item>
      </el-timeline>
    </el-card>

    <el-card class="panel">
      <h3>{{ $t('semanticSearch.labels.runtimeAnalysis') }}</h3>
      <ul class="analysis-metrics" role="list">
        <li>
          <span>{{ $t('semanticSearch.labels.embeddingTime') }}</span>
          <strong>{{ queryEmbeddingTimeLabel }}</strong>
        </li>
        <li>
          <span>{{ $t('semanticSearch.labels.fastestResponse') }}</span>
          <strong>{{ fastestResponseLabel }}</strong>
        </li>
        <li>
          <span>{{ $t('semanticSearch.labels.slowestResponse') }}</span>
          <strong>{{ slowestResponseLabel }}</strong>
        </li>
        <li>
          <span>{{ $t('semanticSearch.labels.highestScore') }}</span>
          <strong>{{ highestScoreLabel }}</strong>
        </li>
        <li>
          <span>{{ $t('semanticSearch.labels.lowestScore') }}</span>
          <strong>{{ lowestScoreLabel }}</strong>
        </li>
        <li>
          <span>{{ $t('semanticSearch.labels.closestCollection') }}</span>
          <strong>{{ closestCollectionLabel }}</strong>
        </li>
        <li>
          <span>{{ $t('semanticSearch.labels.globalTime') }}</span>
          <strong>{{ globalTimeLabel }}</strong>
        </li>
      </ul>

      <div class="collection-runtime-block">
        <h4>{{ $t('semanticSearch.labels.collectionRuntime') }}</h4>
        <el-empty v-if="!perCollectionMetrics.length" :description="$t('semanticSearch.labels.notCalculated')" />
        <ul v-else class="collection-runtime-list" role="list">
          <li v-for="metric in perCollectionMetrics" :key="metric.collection">
            <strong>{{ metric.collection || $t('semanticSearch.labels.notAvailable') }}</strong>
            <span>{{ metric.searchTimeMs.toFixed(1) }} ms • {{ metric.resultsCount }} {{ $t('semanticSearch.labels.resultCount').toLowerCase() }}</span>
          </li>
        </ul>
      </div>
    </el-card>
  </div>
</template>

<script lang="ts" src="./SemanticSearchDiagnosticsPanel.ts"></script>
<style scoped lang="less" src="./SemanticSearchDiagnosticsPanel.less"></style>
