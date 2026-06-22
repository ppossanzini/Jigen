<template>
  <aside class="detail-panel">
    <h3>{{ title }}</h3>

    <template v-if="row">
      <ul v-if="details" class="summary-list">
        <li><span>{{ $t('databaseManagement.columns.collectionsCount') }}</span><strong>{{ details.collectionsCount ?? 0 }}</strong></li>
        <li><span>{{ $t('databaseManagement.detailsLabels.usersCount') }}</span><strong>{{ details.usersCount ?? 0 }}</strong></li>
        <li><span>{{ $t('databaseManagement.detailsLabels.vectors') }}</span><strong>{{ details.vectors ?? 0 }}</strong></li>
        <li>
          <span>{{ $t('databaseManagement.detailsLabels.createdAtUtc') }}</span>
          <strong>{{ formatDate(details.createdAtUtc ?? null) || $t('databaseManagement.notAvailable') }}</strong>
        </li>
        <li><span>{{ $t('databaseManagement.detailsLabels.contentSize') }}</span><strong>{{ formatBytes(details.contentSize ?? 0) }}</strong></li>
        <li><span>{{ $t('databaseManagement.detailsLabels.vectorSize') }}</span><strong>{{ formatBytes(details.vectorSize ?? 0) }}</strong></li>
        <li><span>{{ $t('databaseManagement.detailsLabels.allocatedContentSize') }}</span><strong>{{ formatBytes(details.allocatedContentSize ?? 0) }}</strong></li>
        <li><span>{{ $t('databaseManagement.detailsLabels.allocatedVectorSize') }}</span><strong>{{ formatBytes(details.allocatedVectorSize ?? 0) }}</strong></li>
        <li><span>{{ $t('databaseManagement.detailsLabels.contentFreeSpace') }}</span><strong>{{ formatBytes(details.contentFreeSpace ?? 0) }}</strong></li>
        <li><span>{{ $t('databaseManagement.detailsLabels.vectorFreeSpace') }}</span><strong>{{ formatBytes(details.vectorFreeSpace ?? 0) }}</strong></li>
      </ul>

      <div v-if="details" class="insight-box">
        <h4>{{ $t('databaseManagement.collectionsTitle') }}</h4>
        <p v-if="!(details.collections?.length ?? 0)">{{ $t('databaseManagement.noCollections') }}</p>
        <el-scrollbar v-else max-height="18rem">
          <ul class="collection-list">
            <li v-for="(collection, index) in details.collections" :key="collection.name ?? `detail-collection-${index}`">
              <div class="collection-name">{{ collection.name || $t('databaseManagement.notAvailable') }}</div>
              <small>
                {{ $t('databaseManagement.detailsLabels.vectors') }}: {{ collection.vectors ?? 0 }} |
                {{ $t('databaseManagement.detailsLabels.dimensions') }}: {{ collection.dimensions ?? 0 }}
              </small>
            </li>
          </ul>
        </el-scrollbar>
      </div>

      <div v-if="details" class="insight-box">
        <h4>{{ $t('databaseManagement.usersTitle') }}</h4>
        <p v-if="!(details.users?.length ?? 0)">{{ $t('databaseManagement.noUsers') }}</p>
        <el-scrollbar v-else max-height="14rem">
          <ul class="collection-list">
            <li v-for="(user, index) in details.users" :key="`${user.userId ?? 'nouser'}-${user.userName ?? index}`" class="user-item">
              <div class="user-item-main">
                <div class="collection-name">{{ user.userName || $t('databaseManagement.notAvailable') }}</div>
                <small>{{ user.userId || $t('databaseManagement.notAvailable') }}</small>
              </div>
              <el-button
                v-if="canManageUsers"
                type="danger"
                link
                :disabled="!user.userId"
                class="remove-user-button"
                @click="onRequestRemoveUser(user.userId, user.userName)"
              >
                {{ $t('databaseManagement.assignUser.removeAction') }}
              </el-button>
            </li>
          </ul>
        </el-scrollbar>
        <p v-if="!canManageUsers" class="access-guard-note">
          {{ $t('databaseManagement.assignUser.securityAdminOnly') }}
        </p>
      </div>

      <div v-if="details && canManageUsers" class="insight-box">
        <h4>{{ $t('databaseManagement.assignUser.title') }}</h4>
        <p v-if="!availableUsers.length">{{ $t('databaseManagement.assignUser.noUsersAvailable') }}</p>
        <el-form v-else @submit.prevent>
          <el-form-item>
            <el-select
              :model-value="selectedUserId"
              :placeholder="$t('databaseManagement.assignUser.placeholder')"
              filterable
              clearable
              @update:model-value="onSelectUser"
            >
              <el-option
                v-for="user in availableUsers"
                :key="user.userId"
                :label="user.userName || user.userId"
                :value="user.userId"
              />
            </el-select>
          </el-form-item>
          <el-button
            type="primary"
            :loading="assignUserLoading"
            :disabled="!selectedUserId"
            @click="onAssignUser"
          >
            {{ $t('databaseManagement.assignUser.action') }}
          </el-button>
        </el-form>
      </div>

      <p v-if="!details" class="empty">{{ $t('databaseManagement.empty') }}</p>
    </template>

    <p v-else class="empty">{{ $t('databaseManagement.chooseDatabase') }}</p>
  </aside>
</template>

<script lang="ts" src="./DatabaseDetailPanel.ts"></script>
<style scoped lang="less" src="./DatabaseDetailPanel.less"></style>
