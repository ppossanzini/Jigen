<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { fetchListUsers, fetchSetDatabaseUsers } from '@/service/api';
import type { DatabaseUserInfo, UserSummary } from '@/service/api-types';
import { $t } from '@/locales';

defineOptions({
  name: 'DatabaseUsersEditor'
});

interface Props {
  database: string;
  users: DatabaseUserInfo[];
}

const props = defineProps<Props>();

interface Emits {
  (e: 'updated', users: DatabaseUserInfo[]): void;
}

const emit = defineEmits<Emits>();

const allUsers = ref<UserSummary[]>([]);
const selectedIds = ref<string[]>([]);
const saving = ref(false);
const loadingUsers = ref(false);

watch(
  () => props.users,
  value => {
    selectedIds.value = value.map(u => u.userId ?? '').filter(Boolean);
  },
  { immediate: true }
);

async function loadUsers() {
  loadingUsers.value = true;
  const { data, error } = await fetchListUsers();
  if (!error) {
    allUsers.value = data ?? [];
  }
  loadingUsers.value = false;
}

loadUsers();

const options = computed(() =>
  allUsers.value.map(u => ({ label: u.userName ?? u.id ?? '', value: u.id ?? '' }))
);

const hasChanges = computed(() => {
  const current = new Set(props.users.map(u => u.userId ?? ''));
  const next = new Set(selectedIds.value);

  return current.size !== next.size || [...current].some(id => !next.has(id));
});

async function handleSave() {
  const users: DatabaseUserInfo[] = selectedIds.value.map(userId => ({
    userId,
    userName: allUsers.value.find(u => u.id === userId)?.userName ?? ''
  }));

  saving.value = true;
  const { data, error } = await fetchSetDatabaseUsers(props.database, { users });
  saving.value = false;

  if (!error) {
    window.$message?.success($t('page.databases.detail.usersSaveSuccess'));
    emit('updated', data ?? users);
  }
}
</script>

<template>
  <div class="flex-col gap-12px">
    <NSelect
      v-model:value="selectedIds"
      multiple
      filterable
      :loading="loadingUsers"
      :options="options"
      :placeholder="$t('page.databases.detail.usersPlaceholder')"
    />
    <div class="flex justify-end">
      <NButton size="small" type="primary" :disabled="!hasChanges" :loading="saving" @click="handleSave">
        {{ $t('page.databases.detail.usersSave') }}
      </NButton>
    </div>
  </div>
</template>
