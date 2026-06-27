<template>
  <el-card class="panel controls-panel">
    <el-form label-position="top">
      <div class="search-form-grid app-inputs-layout">
        <div class="search-form-grid__field">
          <el-form-item :label="$t('semanticSearch.labels.database')">
            <el-select
              :model-value="selectedDatabaseName"
              :placeholder="$t('semanticSearch.placeholders.database')"
              :loading="loadingDatabases"
              filterable
              clearable
              @update:model-value="onUpdateDatabase"
            >
              <el-option
                v-for="databaseName in databaseNames"
                :key="databaseName"
                :label="databaseName"
                :value="databaseName"
              />
            </el-select>
          </el-form-item>
        </div>

        <div class="search-form-grid__field">
          <el-form-item :label="$t('semanticSearch.labels.collections')">
            <el-select
              :model-value="selectedCollections"
              multiple
              collapse-tags
              collapse-tags-tooltip
              :placeholder="$t('semanticSearch.placeholders.collections')"
              :disabled="!selectedDatabaseName"
              :loading="collectionsLoading"
              @update:model-value="onUpdateCollections"
            >
              <el-option
                v-for="collection in availableCollections"
                :key="collection"
                :label="collection"
                :value="collection"
              />
            </el-select>
          </el-form-item>
        </div>

        <div v-if="embeddingTasksLoading || embeddingTasks.length > 0" class="search-form-grid__field">
          <el-form-item :label="$t('semanticSearch.labels.embeddingTask')">
            <el-select
              :model-value="selectedEmbeddingTask"
              :placeholder="$t('semanticSearch.placeholders.embeddingTask')"
              :loading="embeddingTasksLoading"
              :disabled="embeddingTasksLoading || !embeddingTasks.length"
              @update:model-value="onUpdateEmbeddingTask"
            >
              <el-option v-for="task in embeddingTasks" :key="task" :label="task" :value="task" />
            </el-select>
          </el-form-item>
        </div>

        <div class="search-form-grid__field search-form-grid__field--top-results">
          <el-form-item :label="$t('semanticSearch.labels.topResults')">
            <el-input-number
              :model-value="topResults"
              :min="topResultsMin"
              :max="topResultsMax"
              :step="1"
              controls-position="right"
              @update:model-value="onUpdateTopResults"
            />
          </el-form-item>
        </div>

        <div class="search-form-grid__field search-form-grid__field--query">
          <div class="search-query-grid">
            <div>
              <el-form-item :label="$t('semanticSearch.labels.query')">
                <el-input
                  :model-value="searchText"
                  type="textarea"
                  :rows="3"
                  maxlength="2000"
                  show-word-limit
                  :placeholder="$t('semanticSearch.placeholders.query')"
                  @update:model-value="onUpdateSearchText"
                  @keydown.enter.exact.prevent="onSearchTextEnter"
                />
              </el-form-item>
            </div>

            <div class="search-actions-column">
              <el-form-item :label="$t('semanticSearch.labels.actions')">
                <div class="actions-column-stack">
                  <el-button type="primary" :loading="searching" :disabled="!canRunSearch" @click="onRunSearch">
                    {{ $t('semanticSearch.buttons.runSearch') }}
                  </el-button>
                  <el-button class="secondary-action" @click="onClear">
                    {{ $t('semanticSearch.buttons.clear') }}
                  </el-button>
                </div>
              </el-form-item>
            </div>
          </div>
        </div>
      </div>
    </el-form>
  </el-card>
</template>

<script lang="ts" src="./SemanticSearchControlsPanel.ts"></script>
<style scoped lang="less" src="./SemanticSearchControlsPanel.less"></style>
