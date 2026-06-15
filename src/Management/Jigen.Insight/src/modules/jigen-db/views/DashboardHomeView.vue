<template>
  <section class="dashboard-view">
    <div class="dashboard-grid">
      <el-card class="panel system-status">
        <template #header>
          <div class="panel-header">
            <h3>{{ t('dashboard.systemStatus') }}</h3>
            <el-button type="success" size="small">Refresh</el-button>
          </div>
        </template>

        <div class="metrics-grid">
          <DashboardMetricCard
            v-for="metric in statusMetrics"
            :key="metric.title"
            :title="metric.title"
            :value="metric.value"
            :hint="metric.hint"
            :tone="metric.tone"
          />
        </div>
      </el-card>

      <el-card class="panel recent-searches">
        <template #header>
          <div class="panel-header">
            <h3>{{ t('dashboard.recentSearches') }}</h3>
            <span>24h</span>
          </div>
        </template>
        <img class="gemini-map" src="/assets/logo.svg" :alt="t('dashboard.heatmapAlt')" />
        <ul class="score-list">
          <li v-for="query in searchScores" :key="query.name">
            <span>{{ query.name }}</span>
            <el-tag effect="dark" :type="query.type">{{ query.level }}</el-tag>
          </li>
        </ul>
      </el-card>

      <el-card class="panel live-logs">
        <template #header>
          <div class="panel-header">
            <h3>{{ t('dashboard.liveLogs') }}</h3>
            <span>{{ t('dashboard.streaming') }}</span>
          </div>
        </template>
        <ul>
          <li v-for="log in logs" :key="log.time + log.message">
            <strong>{{ log.time }}</strong>
            <span>{{ log.message }}</span>
          </li>
        </ul>
      </el-card>

      <el-card class="panel top-queries">
        <template #header>
          <h3>{{ t('dashboard.topQueries') }}</h3>
        </template>
        <ul>
          <li v-for="entry in topQueries" :key="entry.query">
            <span>{{ entry.query }}</span>
            <strong>{{ entry.volume }}</strong>
          </li>
        </ul>
      </el-card>

      <el-card class="panel dataset-summary">
        <template #header>
          <h3>{{ t('dashboard.datasetSummaries') }}</h3>
        </template>
        <ul>
          <li v-for="dataset in datasets" :key="dataset.name">
            <div>
              <strong>{{ dataset.name }}</strong>
              <p>{{ dataset.size }}</p>
            </div>
            <span>{{ dataset.updated }}</span>
          </li>
        </ul>
      </el-card>

      <el-card class="panel alerts">
        <template #header>
          <h3>{{ t('dashboard.recentAlerts') }}</h3>
        </template>
        <ul>
          <li v-for="alert in alerts" :key="alert.title">
            <strong>{{ alert.title }}</strong>
            <p>{{ alert.detail }}</p>
          </li>
        </ul>
      </el-card>
    </div>
  </section>
</template>

<script lang="ts" src="./DashboardHomeView.ts"></script>
<style scoped lang="less" src="./DashboardHomeView.less"></style>
