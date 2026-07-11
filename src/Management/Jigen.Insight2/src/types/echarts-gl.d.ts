// echarts-gl ships no TypeScript definitions (confirmed: no `.d.ts` in the
// package, no `@types/echarts-gl` on npm). These ambient declarations just
// let us import its tree-shakable entry points; the GL series/component
// option shapes themselves aren't type-checked (cast to `EChartOption` at
// the call site — see `graph-viewer-3d.tsx`).
declare module 'echarts-gl/charts' {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  export const Scatter3DChart: any
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  export const Line3DChart: any
}

declare module 'echarts-gl/components' {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  export const Grid3DComponent: any
}
