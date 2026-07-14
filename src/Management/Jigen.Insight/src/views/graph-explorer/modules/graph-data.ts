import type { IndexGraphSnapshot } from '@/service/api-types';
import { toNum } from '@/utils/format';

/** A graph node normalized for chart consumption: numeric fields resolved, color/size precomputed. */
export interface PreparedNode {
  id: string;
  positionId: number;
  key: string | null;
  maxLevel: number;
  isDeleted: boolean;
  degree: number;
  isEntrypoint: boolean;
  color: string;
  symbolSize: number;
  x: number;
  y: number;
  /** Present only for 3D snapshots (`dimensions=3`) */
  z?: number;
  /** True when `key` is present in the `matches` map passed to {@link prepareGraphData}. */
  isMatch: boolean;
  /** Rank among matched nodes, 0 = highest score (closest). Only set when `isMatch`. */
  matchRank?: number;
  /** Raw search score for this node. Only set when `isMatch`. */
  matchScore?: number;
}

/** A graph edge normalized for chart consumption, resolved against the node id set. */
export interface PreparedEdge {
  sourceId: string;
  targetId: string;
  level: number;
  color: string;
  width: number;
}

export interface PreparedGraph {
  nodes: PreparedNode[];
  edges: PreparedEdge[];
  /** True when a non-empty `matches` map was supplied, i.e. highlighting is active. */
  hasMatches: boolean;
}

/**
 * Turns a raw `IndexGraphSnapshot` into chart-ready nodes/edges shared by the 2D and 3D views:
 * resolves the `number | string` schema numerics, colors nodes/edges by HNSW level using the
 * sequential shade ramp from the theme module (deleted nodes get the muted "deleted" tone
 * regardless of level), and drops any node without a usable position (and edges pointing at it) so
 * a partially-truncated snapshot never crashes the renderer.
 *
 * When `matches` is supplied (search-result keys → score, same base64 key format as
 * `IndexGraphNode.key` — both travel the wire as raw key bytes, no decoding needed to compare),
 * matched nodes are colored from `matchShades` instead of the level ramp, ranked by score
 * descending (index 0 of `matchShades` = highest score / closest). `matchShades` should come from
 * a color channel distinct from `levelShades` (see `useChartTheme().getSequentialShades`) so the
 * two scales stay visually separable on the same chart.
 *
 * @param snapshot Server graph snapshot
 * @param levelShades Sequential color ramp, index 0 = level 0, length should cover `maxLevel + 1`
 * @param deletedColor Color override for deleted nodes
 * @param matches Search-result keys (base64) → score, for highlighting; omit/empty for no highlighting
 * @param matchShades Sequential color ramp for matches, index 0 = best score; length should cover `matches.size`
 */
export function prepareGraphData(
  snapshot: IndexGraphSnapshot,
  levelShades: string[],
  deletedColor: string,
  matches?: Map<string, number>,
  matchShades?: string[]
): PreparedGraph {
  // callers derive `levelShades` from `useChartTheme().getSequentialShades()`, which always
  // returns at least one color, so falling back to the deleted (theme-derived) tone here just
  // keeps this function total without ever reaching for a literal hex color.
  const shades = levelShades.length ? levelShades : [deletedColor];
  const shadeFor = (level: number) => shades[Math.min(Math.max(level, 0), shades.length - 1)];

  const hasMatches = Boolean(matches && matches.size > 0);
  const rankByKey = new Map<string, number>();
  if (hasMatches) {
    const ranked = [...(matches as Map<string, number>).entries()].sort((a, b) => b[1] - a[1]);
    ranked.forEach(([key], index) => rankByKey.set(key, index));
  }
  const matchColorFor = (rank: number) => {
    const palette = matchShades && matchShades.length ? matchShades : shades;

    return palette[Math.min(rank, palette.length - 1)];
  };

  const entrypointId = snapshot.entrypointPositionId != null ? toNum(snapshot.entrypointPositionId) : null;

  const nodeMap = new Map<string, PreparedNode>();
  const nodes: PreparedNode[] = [];

  for (const raw of snapshot.nodes ?? []) {
    const position = raw.position ?? [];
    if (position.length < 2) continue;

    const positionId = toNum(raw.positionId);
    const id = String(positionId);
    const maxLevel = toNum(raw.maxLevel);
    const isDeleted = Boolean(raw.isDeleted);
    const degree = toNum(raw.degree);
    const isEntrypoint = entrypointId !== null && entrypointId === positionId;

    const rank = raw.key && hasMatches ? rankByKey.get(raw.key) : undefined;
    const isMatch = rank !== undefined;

    const node: PreparedNode = {
      id,
      positionId,
      key: raw.key ?? null,
      maxLevel,
      isDeleted,
      degree,
      isEntrypoint,
      color: isDeleted ? deletedColor : isMatch ? matchColorFor(rank) : shadeFor(maxLevel),
      symbolSize: (isEntrypoint ? Math.max(28, 12 + degree) : Math.min(40, 6 + degree * 1.5)) * 0.3 * (isMatch ? 1.5 : 1),
      x: toNum(position[0]),
      y: toNum(position[1]),
      z: position.length > 2 ? toNum(position[2]) : undefined,
      isMatch,
      matchRank: rank,
      matchScore: isMatch ? matches?.get(raw.key as string) : undefined
    };

    nodeMap.set(id, node);
    nodes.push(node);
  }

  const edges: PreparedEdge[] = [];

  for (const raw of snapshot.edges ?? []) {
    const sourceId = String(toNum(raw.source));
    const targetId = String(toNum(raw.target));

    if (!nodeMap.has(sourceId) || !nodeMap.has(targetId)) continue;

    const level = toNum(raw.level);

    edges.push({
      sourceId,
      targetId,
      level,
      color: shadeFor(level),
      width: Math.min(4, 1 + level * 0.6)
    });
  }

  return { nodes, edges, hasMatches };
}
