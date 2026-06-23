<template>
  <div class="search-layout__results">
    <el-card class="panel embedding-panel">
      <div class="result-card__block-header">
        <h3>{{ $t('semanticSearch.labels.queryEmbedding') }}</h3>
      </div>
      <el-empty v-if="!queryEmbeddingText" :description="$t('semanticSearch.labels.notCalculated')" />
      <code v-else class="embedding-code embedding-code--muted">{{ queryEmbeddingText }}</code>
    </el-card>

    <el-card class="panel results-panel">
      <template #header>
        <div class="panel-header">
          <h3>{{ $t('semanticSearch.table.title') }}</h3>
          <el-tag class="result-count-tag" effect="dark" type="success">{{ resultRows.length }}</el-tag>
        </div>
      </template>

      <el-empty v-if="!resultRows.length" :description="$t('semanticSearch.feedback.noResults')" />
      <el-scrollbar v-else class="result-cards-scroll">
        <div class="result-cards-list" role="list">
          <article v-for="row in resultRows" :key="row.id" class="result-card" role="listitem">
            <header class="result-card__header">
              <div class="result-card__identity">
                <strong>{{ row.id }}</strong>
                <span>{{ $t('semanticSearch.labels.collection') }}: {{ row.collection }}</span>
              </div>
              <div class="result-card__metrics">
                <el-button
                  class="result-json-copy-button"
                  text
                  circle
                  size="small"
                  :title="$t('semanticSearch.buttons.copyContentJson')"
                  :aria-label="$t('semanticSearch.buttons.copyContentJson')"
                  @click="onCopyResultContentJson(row)"
                >
                  <i class="ti ti-copy"></i>
                </el-button>
                <el-tag class="result-score-tag" effect="dark" type="warning">
                  {{ $t('semanticSearch.labels.score') }}: {{ row.score.toFixed(4) }}
                </el-tag>
                <el-tag class="result-latency-tag" effect="dark" type="info">{{ row.latencyMs }} ms</el-tag>
              </div>
            </header>

            <section class="result-card__block">
              <h4>{{ $t('semanticSearch.labels.attributes') }}</h4>
              <p v-if="!hasAttributes(row.attributes)" class="content-cell">{{ $t('semanticSearch.labels.notAvailable') }}</p>
              <div v-else class="attribute-tags">
                <el-tag
                  class="result-attribute-tag"
                  v-for="attribute in toAttributeEntries(row.attributes)"
                  :key="`${row.id}-${attribute.key}`"
                  size="small"
                  effect="plain"
                >
                  {{ attribute.key }}: {{ attribute.value }}
                </el-tag>
              </div>
            </section>

            <section class="result-card__block">
              <h4>{{ $t('semanticSearch.labels.content') }}</h4>
              <p class="content-cell">{{ row.content || $t('semanticSearch.labels.noContent') }}</p>
            </section>
          </article>
        </div>
      </el-scrollbar>
    </el-card>
  </div>
</template>

<script lang="ts" src="./SemanticSearchResultsPanel.ts"></script>
<style scoped lang="less" src="./SemanticSearchResultsPanel.less"></style>
