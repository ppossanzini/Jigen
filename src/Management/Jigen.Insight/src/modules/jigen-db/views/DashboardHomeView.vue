<template>
  <section class="dashboard-view">
    <div class="dashboard-grid">
      <el-card class="panel system-status">
        <template #header>
          <div class="panel-header">
            <h3>{{ t('dashboard.systemStatus') }}</h3>
            <div class="panel-actions">
              <el-select
                v-model="selectedWindow"
                size="small"
                class="window-select"
                :placeholder="t('dashboard.window')"
                @change="onWindowChange"
              >
                <el-option
                  v-for="option in windowOptions"
                  :key="option.value"
                  :label="option.label"
                  :value="option.value"
                />
              </el-select>
              <el-button type="success" size="small" :loading="loading" @click="onRefresh">
                {{ t('dashboard.refresh') }}
              </el-button>
            </div>
          </div>
        </template>

        <el-alert
          v-if="statusMetrics.length"
          :title="t('dashboard.lastUpdated', { value: lastUpdatedLabel })"
          type="info"
          :closable="false"
          show-icon
          class="status-alert"
        />

        <div v-if="statusMetrics.length" class="metrics-grid">
          <DashboardMetricCard
            v-for="metric in statusMetrics"
            :key="metric.title"
            :title="metric.title"
            :value="metric.value"
            :hint="metric.hint"
            :tone="metric.tone"
          />
        </div>

          <section v-if="trendSamples.length" class="global-chart-block">
            <div class="global-chart-header">
              <h4>{{ t('dashboard.globalTrendTitle') }}</h4>
              <el-tag effect="plain" type="info">{{ t('dashboard.samplesLabel', { count: trendSamples.length }) }}</el-tag>
            </div>

            <div
              ref="globalTrendChartRef"
              class="global-trend-chart"
              role="img"
              :aria-label="t('dashboard.globalTrendTitle')"
            />

            <div class="global-chart-legend">
              <span>{{ t('dashboard.cpuLegend', { max: globalTrendChart.cpuMax.toFixed(1) }) }}</span>
              <span>{{ t('dashboard.memoryLegend', { max: globalTrendChart.memoryMaxMb.toFixed(1) }) }}</span>
            </div>
          </section>

        <!-- <el-empty v-else :description="t('dashboard.noData')" /> -->
      </el-card>

      <el-card class="panel databases-status" v-loading="loading">
        <template #header>
          <div class="panel-header">
            <h3>{{ t('dashboard.databasesStatus') }}</h3>
            <el-tag effect="dark" type="info">{{ t('dashboard.latestSample') }}</el-tag>
          </div>
        </template>

        <div v-if="dbStatusChartRows.length" class="db-chart">
          <div v-for="row in dbStatusChartRows" :key="row.name" class="db-chart-row">
            <div class="db-chart-row-head">
              <h4>{{ row.name }}</h4>
              <span>{{ t('dashboard.queueLabel') }}: {{ row.queueValue }}</span>
            </div>

            <div class="db-chart-track">
              <div class="db-chart-fill" :style="{ width: `${row.queuePercent}%` }" />
            </div>

            <div class="database-metrics">
              <span>{{ t('dashboard.collectionsCount') }}: {{ row.collectionsCount }}</span>
              <span>{{ t('dashboard.elementsCount') }}: {{ row.totalElementsCount }}</span>
              <span>{{ t('dashboard.contentSize') }}: {{ formatBytes(row.contentSizeBytes) }}</span>
              <span>{{ t('dashboard.vectorSize') }}: {{ formatBytes(row.vectorSizeBytes) }}</span>
            </div>
          </div>
        </div>

        <!-- <el-empty v-else :description="t('dashboard.noData')" /> -->
      </el-card>

      <!-- <el-card class="panel samples-history" v-loading="loading">
        <template #header>
          <div class="panel-header">
            <h3>{{ t('dashboard.samplesHistory') }}</h3>
            <el-tag effect="plain" type="info">
              {{ t('dashboard.sampleInterval', { seconds: serverStatusHistory?.sampleIntervalSeconds ?? '-' }) }}
            </el-tag>
          </div>
        </template>

        <ul v-if="recentSamples.length" class="samples-list">
          <li v-for="sample in recentSamples" :key="sample.timestampUtc ?? ''">
            <div class="sample-time">{{ formatDateTime(sample.timestampUtc) }}</div>
            <div class="sample-metrics">
              <span>{{ t('dashboard.cpuUsage') }}: {{ toNumber(sample.cpuUsagePercent).toFixed(1) }}%</span>
              <span>{{ t('dashboard.memoryUsage') }}: {{ formatBytes(toNumber(sample.memoryUsageBytes)) }}</span>
              <span>{{ t('dashboard.monitoredDatabases') }}: {{ sample.databases.length }}</span>
            </div>
          </li>
        </ul>

        <el-empty v-else :description="t('dashboard.noData')" />
      </el-card> -->
    </div>
  </section>
</template>

<script lang="ts" src="./DashboardHomeView.ts"></script>
<style scoped lang="less" src="./DashboardHomeView.less"></style>
