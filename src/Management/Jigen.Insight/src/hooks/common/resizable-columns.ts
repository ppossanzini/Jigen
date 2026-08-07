import { computed, h, onBeforeUnmount, reactive } from 'vue';
import type { ComputedRef } from 'vue';
import type { DataTableColumns } from 'naive-ui';

/**
 * Persisted column-width map keyed by column key.
 */
interface WidthMap {
  [key: string]: number;
}

/**
 * Options for useResizableColumns.
 */
export interface ResizableColumnsOptions {
  /** Unique key used to persist widths in localStorage. */
  storageKey: string;
  /** Fallback width (px) when no persisted or initial width is available. */
  defaultWidth?: number;
  /** Minimum allowed column width (px). */
  minWidth?: number;
}

// ── global document-level listeners (only one set ever attached) ──────────

let activeInstance: ReturnType<typeof createResizeController> | null = null;

function createResizeController(widthMap: WidthMap, save: () => void) {
  let resizing: { key: string; startX: number; startWidth: number; minWidth: number } | null = null;

  function start(e: MouseEvent, key: string, minWidth: number) {
    e.preventDefault();
    e.stopPropagation();
    const currentWidth = widthMap[key] ?? 150;
    resizing = { key, startX: e.clientX, startWidth: currentWidth, minWidth };
    document.body.style.cursor = 'col-resize';
    document.body.style.userSelect = 'none';
  }

  function move(e: MouseEvent) {
    if (!resizing) return;
    const diff = e.clientX - resizing.startX;
    const newWidth = Math.max(resizing.minWidth, resizing.startWidth + diff);
    widthMap[resizing.key] = newWidth;
  }

  function stop() {
    if (resizing) {
      resizing = null;
      save();
    }
    document.body.style.cursor = '';
    document.body.style.userSelect = '';
  }

  return { start, move, stop };
}

let docMoveHandler: ((e: MouseEvent) => void) | null = null;
let docUpHandler: (() => void) | null = null;

function ensureGlobalListeners() {
  if (docMoveHandler) return;
  docMoveHandler = (e: MouseEvent) => activeInstance?.move(e);
  docUpHandler = () => activeInstance?.stop();
  document.addEventListener('mousemove', docMoveHandler);
  document.addEventListener('mouseup', docUpHandler);
}

// ── helpers ───────────────────────────────────────────────────────────────

function buildTitleVNode(
  content: unknown,
  key: string,
  onHandleMouseDown: (e: MouseEvent, key: string) => void
) {
  return h('div', { class: 'resizable-col-header' }, [
    h('span', { class: 'resizable-col-title' }, content as string),
    h('div', {
      class: 'resizable-col-handle',
      onMousedown: (e: MouseEvent) => onHandleMouseDown(e, key)
    })
  ]);
}

// ── composable ────────────────────────────────────────────────────────────

/**
 * Makes NDataTable columns user-resizable by dragging the right edge of each
 * column header.  Widths are persisted in `localStorage` keyed by `storageKey`.
 *
 * @example
 * ```ts
 * const { columns } = useResizableColumns(rawColumns, { storageKey: 'results-table' });
 * // use `columns` in NDataTable :columns
 * ```
 */
export function useResizableColumns<T>(
  rawColumns: DataTableColumns<T>,
  options: ResizableColumnsOptions
): { columns: ComputedRef<DataTableColumns<T>> } {
  const { storageKey, defaultWidth = 150, minWidth = 60 } = options;

  // --- persisted widths ---------------------------------------------------
  const widthMap = reactive<WidthMap>({});

  function load() {
    try {
      const raw = localStorage.getItem(`jigen:col-widths:${storageKey}`);
      if (raw) Object.assign(widthMap, JSON.parse(raw));
    } catch {
      /* ignore corrupt data */
    }
  }

  function save() {
    localStorage.setItem(`jigen:col-widths:${storageKey}`, JSON.stringify({ ...widthMap }));
  }

  load();

  // --- resize controller --------------------------------------------------
  const controller = createResizeController(widthMap as WidthMap, save);

  function onHandleMouseDown(e: MouseEvent, key: string) {
    // Prevent row-click / sort when grabbing the handle
    activeInstance = controller;
    ensureGlobalListeners();
    controller.start(e, key, minWidth);
  }

  onBeforeUnmount(() => {
    if (activeInstance === controller) {
      activeInstance = null;
      controller.stop();
    }
  });

  // --- wrap columns (reactive to widthMap changes) -----------------------
  const columns = computed<DataTableColumns<T>>(() =>
    rawColumns.map(col => {
      // Skip selection / expand columns — they have no key
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      const colKey = (col as any).key as string | number | undefined;
      if (colKey == null) return col;

      const key = String(colKey);

      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      const baseWidth = Number((col as any).width) || defaultWidth;
      const width = widthMap[key] ?? baseWidth;

      // keep widthMap in sync in case a new column appears
      if (!(key in widthMap)) widthMap[key] = baseWidth;

      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      const originalTitle = (col as any).title as NaiveUI.TableColumnWithKey<T>['title'];

      return {
        ...col,
        width,
        title:
          typeof originalTitle === 'function'
            ? () => {
                const content = (originalTitle as () => unknown)();
                return buildTitleVNode(content, key, onHandleMouseDown);
              }
            : originalTitle != null
              ? () => buildTitleVNode(originalTitle, key, onHandleMouseDown)
              : originalTitle
      };
    })
  );

  return { columns };
}
