<template>
  <section class="main-view">

    <div class="dashboard-grid">
      <el-card class="panel-card">
        <template #header>
          <div class="panel-header">
            <span>{{ t('main.panels.overview') }}</span>
            <el-tag size="small" type="success" effect="dark">{{ t('main.panels.stable') }}</el-tag>
          </div>
        </template>

        <div class="metric-grid">
          <div v-for="item in quickMetrics" :key="item.key" class="metric-item">
            <small>{{ item.label }}</small>
            <strong>{{ item.value }}</strong>
          </div>
        </div>
      </el-card>

      <el-card class="panel-card">
        <template #header>
          <div class="panel-header">
            <span>{{ t('main.panels.quickActions') }}</span>
          </div>
        </template>

        <div class="action-list">
          <el-button class="action-btn" text type="primary" @click="openCollections">
            {{ t('main.actions.collections') }}
          </el-button>
          <el-button class="action-btn" text @click="openUsers">
            {{ t('main.actions.users') }}
          </el-button>
          <el-button class="action-btn" text @click="openDashboardDocs">
            {{ t('main.actions.docs') }}
          </el-button>
        </div>
      </el-card>

      <el-card class="panel-card wide">
        <template #header>
          <div class="panel-header">
            <span>{{ t('main.panels.activity') }}</span>
            <el-tag size="small" type="info" effect="plain">{{ t('main.panels.realtime') }}</el-tag>
          </div>
        </template>

        <ul class="timeline-list">
          <li v-for="item in timelineItems" :key="item.key">
            <span>{{ item.event }}</span>
            <small>{{ item.time }}</small>
          </li>
        </ul>
      </el-card>
    </div>
  </section>
</template>

<script lang="ts" src="./MainView.ts"></script>
<style scoped lang="less" src="./MainView.less"></style>
