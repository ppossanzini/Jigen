import { ref } from 'vue';
import { defineStore } from 'pinia';
import { fetchListDatabases } from '@/service/api';
import { localStg } from '@/utils/storage';
import { SetupStoreId } from '@/enum';

/**
 * Shared "current database" selection.
 *
 * The Databases page sets it on create/row-click; Collections, Workbench and Graph Explorer all
 * read it (and offer a selector bound to it) so the choice carries across those pages. Persisted
 * to localStorage so it survives navigation and reloads; re-validated against the live list on
 * each load in case the database was deleted meanwhile.
 */
export const useDatabaseStore = defineStore(SetupStoreId.Database, () => {
  /** All database names the current user can see (`GET /api/database`, permission-filtered) */
  const databases = ref<string[]>([]);
  /** Currently selected database name, or '' if none */
  const current = ref(localStg.get('currentDatabase') || '');
  const loading = ref(false);
  const loaded = ref(false);

  function setCurrent(name: string) {
    current.value = name;
    localStg.set('currentDatabase', name);
  }

  /** Load the database list; keeps the current selection if it's still present, else picks none/first */
  async function loadDatabases() {
    loading.value = true;

    const { data, error } = await fetchListDatabases();

    if (!error) {
      databases.value = data ?? [];

      if (current.value && !databases.value.includes(current.value)) {
        setCurrent('');
      }
    }

    loading.value = false;
    loaded.value = true;

    return !error;
  }

  function reset() {
    databases.value = [];
    setCurrent('');
    loaded.value = false;
  }

  return {
    databases,
    current,
    loading,
    loaded,
    setCurrent,
    loadDatabases,
    reset
  };
});
