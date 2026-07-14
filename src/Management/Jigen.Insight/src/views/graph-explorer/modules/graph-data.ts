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
}

/**
 * Turns a raw `IndexGraphSnapshot` into chart-ready nodes/edges shared by the 2D and 3D views:
 * resolves the `number | string` schema numerics, colors nodes/edges by HNSW level using the
 * sequential shade ramp from the theme module (deleted nodes get the muted "deleted" tone
 * regardless of level), and drops any node without a usable position (and edges pointing at it) so
 * a partially-truncated snapshot never crashes the renderer.
 *
 * @param snapshot Server graph snapshot
 * @param levelShades Sequential color ramp, index 0 = level 0, length should cover `maxLevel + 1`
 * @param deletedColor Color override for deleted nodes
 */
export function prepareGraphData(snapshot: IndexGraphSnapshot, levelShades: string[], deletedColor: string): PreparedGraph {
  // callers derive `levelShades` from `useChartTheme().getSequentialShades()`, which always
  // returns at least one color, so falling back to the deleted (theme-derived) tone here just
  // keeps this function total without ever reaching for a literal hex color.
  const shades = levelShades.length ? levelShades : [deletedColor];
  const shadeFor = (level: number) => shades[Math.min(Math.max(level, 0), shades.length - 1)];

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

    const node: PreparedNode = {
      id,
      positionId,
      key: raw.key ?? null,
      maxLevel,
      isDeleted,
      degree,
      isEntrypoint,
      color: isDeleted ? deletedColor : shadeFor(maxLevel),
      symbolSize: (isEntrypoint ? Math.max(28, 12 + degree) : Math.min(40, 6 + degree * 1.5)) * 0.3,
      x: toNum(position[0]),
      y: toNum(position[1]),
      z: position.length > 2 ? toNum(position[2]) : undefined
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

  return { nodes, edges };
}
