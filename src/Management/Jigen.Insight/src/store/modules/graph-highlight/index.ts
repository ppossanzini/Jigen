import { ref } from 'vue';
import { defineStore } from 'pinia';
import { SetupStoreId } from '@/enum';

/** Search results to highlight on the Graph Explorer, staged by the Workbench for a single collection. */
export interface GraphHighlightPayload {
  database: string;
  collection: string;
  /** Result keys (base64, same format as `IndexGraphNode.key`) → score */
  matches: Map<string, number>;
  /** The resolved query vector (`SearchCollectionsResult.queryEmbedding`) */
  queryEmbedding: number[];
}

/**
 * One-shot handoff from Workbench to Graph Explorer: "show me this search result on the graph".
 *
 * A full result set plus a query embedding (a few hundred floats) doesn't fit in a URL query
 * string, so the Workbench stages it here right before navigating with `db`/`collection` query
 * params (see `graph-explorer/index.vue`'s deep-link handling), and Graph Explorer consumes it
 * once on mount. Consuming clears it, so a page refresh or a direct visit never replays stale
 * highlighting — and it's plain in-memory Pinia state (not persisted), so a reload clears it too.
 */
export const useGraphHighlightStore = defineStore(SetupStoreId.GraphHighlight, () => {
  const pending = ref<GraphHighlightPayload | null>(null);

  function stage(payload: GraphHighlightPayload) {
    pending.value = payload;
  }

  /** Returns the staged payload if it matches `database`/`collection`, clearing it either way. */
  function consume(database: string, collection: string): GraphHighlightPayload | null {
    const payload = pending.value;
    pending.value = null;

    if (!payload || payload.database !== database || payload.collection !== collection) return null;

    return payload;
  }

  function clear() {
    pending.value = null;
  }

  return {
    pending,
    stage,
    consume,
    clear
  };
});
