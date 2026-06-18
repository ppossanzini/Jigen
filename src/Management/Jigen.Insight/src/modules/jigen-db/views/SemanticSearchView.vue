<template>
  <section class="semantic-search-view">
    <header class="view-header">
      <div>
        <h2>{{ t('semanticSearch.title') }}</h2>
        <p>{{ t('semanticSearch.subtitle') }}</p>
      </div>
      <el-tag type="info" effect="dark">
        {{ t('semanticSearch.labels.resultCount') }}: {{ resultRows.length }}
      </el-tag>
    </header>

    <el-row :gutter="14" class="workspace-grid">
      <el-col :xs="24" :lg="16" class="left-column">
        <el-card class="panel controls-panel">
          <el-form label-position="top">
            <el-row :gutter="12" class="app-inputs-layout">
              <el-col :xs="24" :md="12" class="app-inputs-layout__field">
                <el-form-item :label="t('semanticSearch.labels.database')">
                  <el-select
                    v-model="selectedDatabaseName"
                    :placeholder="t('semanticSearch.placeholders.database')"
                    :loading="databaseStore.loadingDatabases"
                    filterable
                    clearable
                  >
                    <el-option
                      v-for="databaseName in databaseNames"
                      :key="databaseName"
                      :label="databaseName"
                      :value="databaseName"
                    />
                  </el-select>
                </el-form-item>
              </el-col>

              <el-col :xs="24" :md="12" class="app-inputs-layout__field app-inputs-layout__field--wide">
                <el-form-item :label="t('semanticSearch.labels.collections')">
                  <el-select
                    v-model="selectedCollections"
                    multiple
                    collapse-tags
                    collapse-tags-tooltip
                    :placeholder="t('semanticSearch.placeholders.collections')"
                    :disabled="!selectedDatabaseName"
                    :loading="collectionsLoading"
                  >
                    <el-option
                      v-for="collection in availableCollections"
                      :key="collection"
                      :label="collection"
                      :value="collection"
                    />
                  </el-select>
                </el-form-item>
              </el-col>

              <el-col :span="24" class="app-inputs-layout__field app-inputs-layout__field--full">
                <el-form-item :label="t('semanticSearch.labels.query')">
                  <el-input
                    v-model="searchText"
                    type="textarea"
                    :rows="3"
                    maxlength="2000"
                    show-word-limit
                    :placeholder="t('semanticSearch.placeholders.query')"
                  />
                </el-form-item>
              </el-col>
            </el-row>
          </el-form>

          <div class="actions-row">
            <el-button type="primary" :loading="searching" @click="onRunSearch">
              {{ t('semanticSearch.buttons.runSearch') }}
            </el-button>
            <el-button class="secondary-action" :disabled="!availableCollections.length" @click="onSelectAllCollections">
              {{ t('semanticSearch.buttons.selectAllCollections') }}
            </el-button>
            <el-button class="secondary-action" @click="onClear">
              {{ t('semanticSearch.buttons.clear') }}
            </el-button>
          </div>
        </el-card>

        <el-card class="panel embedding-panel">
          <h3>{{ t('semanticSearch.labels.queryEmbedding') }}</h3>
          <el-empty v-if="!queryEmbeddingText" :description="t('semanticSearch.labels.notCalculated')" />
          <code v-else class="embedding-code">{{ queryEmbeddingText }}</code>
        </el-card>

        <el-card class="panel results-panel">
          <template #header>
            <div class="panel-header">
              <h3>{{ t('semanticSearch.table.title') }}</h3>
              <el-tag effect="dark" type="success">{{ resultRows.length }}</el-tag>
            </div>
          </template>

          <el-empty v-if="!resultRows.length" :description="t('semanticSearch.feedback.noResults')" />
          <el-scrollbar v-else class="result-cards-scroll">
            <div class="result-cards-list" role="list">
              <article v-for="row in resultRows" :key="row.id" class="result-card" role="listitem">
                <header class="result-card__header">
                  <div class="result-card__identity">
                    <strong>{{ row.id }}</strong>
                    <span>{{ t('semanticSearch.labels.collection') }}: {{ row.collection }}</span>
                  </div>
                  <div class="result-card__metrics">
                    <el-tag effect="dark" type="warning">{{ row.score.toFixed(4) }}</el-tag>
                    <el-tag effect="dark" type="info">{{ row.latencyMs }} ms</el-tag>
                  </div>
                </header>

                <section class="result-card__block">
                  <h4>{{ t('semanticSearch.labels.attributes') }}</h4>
                  <div class="attribute-tags">
                    <el-tag
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
                  <h4>{{ t('semanticSearch.labels.content') }}</h4>
                  <p class="content-cell">{{ row.content || t('semanticSearch.labels.noContent') }}</p>
                </section>

                <section class="result-card__block">
                  <h4>{{ t('semanticSearch.labels.responseEmbedding') }}</h4>
                  <code class="embedding-inline">{{ formatEmbedding(row.responseEmbedding) }}</code>
                </section>
              </article>
            </div>
          </el-scrollbar>
        </el-card>
      </el-col>

      <el-col :xs="24" :lg="8" class="right-column">
        <el-card class="panel">
          <h3>{{ t('semanticSearch.labels.searchPath') }}</h3>
          <el-empty v-if="!searchPath.length" :description="t('semanticSearch.labels.notCalculated')" />
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
          <h3>{{ t('semanticSearch.labels.runtimeAnalysis') }}</h3>
          <ul class="analysis-metrics" role="list">
            <li>
              <span>{{ t('semanticSearch.labels.embeddingTime') }}</span>
              <strong>{{ formatMs(queryEmbeddingTimeMs) }}</strong>
            </li>
            <li>
              <span>{{ t('semanticSearch.labels.fastestResponse') }}</span>
              <strong>{{ fastestResponseLabel }}</strong>
            </li>
            <li>
              <span>{{ t('semanticSearch.labels.slowestResponse') }}</span>
              <strong>{{ slowestResponseLabel }}</strong>
            </li>
            <li>
              <span>{{ t('semanticSearch.labels.highestScore') }}</span>
              <strong>{{ highestScoreLabel }}</strong>
            </li>
            <li>
              <span>{{ t('semanticSearch.labels.lowestScore') }}</span>
              <strong>{{ lowestScoreLabel }}</strong>
            </li>
            <li>
              <span>{{ t('semanticSearch.labels.closestCollection') }}</span>
              <strong>{{ closestCollectionLabel }}</strong>
            </li>
            <li>
              <span>{{ t('semanticSearch.labels.globalTime') }}</span>
              <strong>{{ formatMs(globalOperationTimeMs) }}</strong>
            </li>
          </ul>
        </el-card>
      </el-col>
    </el-row>
  </section>
</template>

<script lang="ts" src="./SemanticSearchView.ts"></script>
<style scoped lang="less" src="./SemanticSearchView.less"></style>
