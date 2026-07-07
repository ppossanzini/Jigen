<template>
  <section class="graph-explorer-view">
    <header class="view-header">
      <div>
        <h2>{{ t('graphExplorer.title') }}</h2>
        <p>{{ t('graphExplorer.subtitle') }}</p>
      </div>
    </header>

    <el-card class="panel controls-panel">
      <el-form label-position="top">
        <div class="graph-form-grid">
          <div class="graph-form-grid__field">
            <el-form-item :label="t('graphExplorer.database')">
              <el-select
                v-model="selectedDatabaseName"
                :loading="databaseStore.loadingDatabases"
                filterable
                clearable
              >
                <el-option v-for="name in databaseNames" :key="name" :label="name" :value="name" />
              </el-select>
            </el-form-item>
          </div>

          <div class="graph-form-grid__field">
            <el-form-item :label="t('graphExplorer.collection')">
              <el-select
                v-model="selectedCollection"
                :disabled="!selectedDatabaseName"
                :loading="collectionsLoading"
                filterable
                clearable
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

          <div class="graph-form-grid__field">
            <el-form-item :label="t('graphExplorer.layer')">
              <el-select v-model="selectedLevel" :disabled="!hasGraph">
                <el-option
                  v-for="option in levelOptions"
                  :key="option.value"
                  :label="option.label"
                  :value="option.value"
                />
              </el-select>
            </el-form-item>
          </div>

          <div class="graph-form-grid__field">
            <el-form-item :label="t('graphExplorer.nodeLimit')">
              <el-input-number
                v-model="nodeLimit"
                :min="nodeLimitMin"
                :max="nodeLimitMax"
                :step="nodeLimitStep"
                controls-position="right"
              />
            </el-form-item>
          </div>

          <div class="graph-form-grid__field">
            <el-form-item :label="t('graphExplorer.view')">
              <el-radio-group v-model="viewMode">
                <el-radio-button value="2d">{{ t('graphExplorer.mode2d') }}</el-radio-button>
                <el-radio-button value="3d">{{ t('graphExplorer.mode3d') }}</el-radio-button>
              </el-radio-group>
            </el-form-item>
          </div>

          <div class="graph-form-grid__field">
            <el-form-item :label="t('graphExplorer.forceLayout')">
              <el-switch v-model="forceLayout" :disabled="viewMode === '3d'" />
            </el-form-item>
          </div>

          <div class="graph-form-grid__field graph-form-grid__field--actions">
            <el-form-item label=" ">
              <el-button
                type="primary"
                :loading="loading"
                :disabled="!selectedDatabaseName || !selectedCollection"
                @click="loadGraph"
              >
                {{ t('graphExplorer.load') }}
              </el-button>
            </el-form-item>
          </div>
        </div>
      </el-form>

      <div v-if="hasGraph" class="graph-stats">
        <span>{{ t('graphExplorer.stats.shownNodes') }}: {{ graph?.returnedNodes ?? 0 }}</span>
        <span>{{ t('graphExplorer.stats.totalNodes') }}: {{ graph?.totalNodes ?? 0 }}</span>
        <span>{{ t('graphExplorer.stats.liveNodes') }}: {{ graph?.liveNodes ?? 0 }}</span>
        <span>{{ t('graphExplorer.stats.deletedNodes') }}: {{ graph?.deletedNodes ?? 0 }}</span>
        <span>{{ t('graphExplorer.stats.edges') }}: {{ graph?.edges?.length ?? 0 }}</span>
        <span>{{ t('graphExplorer.stats.maxLevel') }}: {{ graph?.maxLevel ?? 0 }}</span>
        <el-tag v-if="graph?.truncated" type="warning" effect="dark">
          {{ t('graphExplorer.stats.truncated') }}
        </el-tag>
      </div>
    </el-card>

    <div class="graph-body">
      <el-empty v-if="!hasGraph" :description="t('graphExplorer.empty')" />
      <el-empty v-else-if="!hasNodes" :description="t('graphExplorer.noGraph')" />
      <template v-else>
        <GraphViewer2D v-if="viewMode === '2d'" :graph="graph" :force-layout="forceLayout" />
        <GraphViewer3D v-else :graph="graph" />
      </template>
    </div>
  </section>
</template>

<script lang="ts" src="./GraphExplorerView.ts"></script>
<style scoped lang="less" src="./GraphExplorerView.less"></style>
