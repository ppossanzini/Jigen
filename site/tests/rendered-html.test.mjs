import assert from "node:assert/strict";
import test from "node:test";

async function render(path = "/") {
  const workerUrl = new URL("../dist/server/index.js", import.meta.url);
  workerUrl.searchParams.set("test", `${process.pid}-${Date.now()}`);
  const { default: worker } = await import(workerUrl.href);
  return worker.fetch(new Request(`http://localhost${path}`, { headers: { accept: "text/html" } }), {
    ASSETS: { fetch: async () => new Response("Not found", { status: 404 }) },
  }, { waitUntil() {}, passThroughOnException() {} });
}

test("renders the Jigen technical presentation", async () => {
  const response = await render();
  assert.equal(response.status, 200);
  const html = await response.text();
  assert.match(html, /<html lang="en">/);
  assert.match(html, /Jigen DB/);
  assert.match(html, /Vectors, in your process/);
  assert.match(html, /From zero to the first query/);
  assert.doesNotMatch(html, /codex-preview|SkeletonPreview/);
});

for (const [path, heading] of [
  ["/architecture", "A short path from write"],
  ["/embedded", "Vector search with no service boundary"],
  ["/server", "One vector service"],
  ["/clients", "One protocol. Native workflows"],
  ["/embeddings", "Different spaces. One ranked answer"],
  ["/benchmarks", "Measure the architecture"],
]) {
  test(`renders ${path}`, async () => {
    const response = await render(path);
    assert.equal(response.status, 200);
    assert.match(await response.text(), new RegExp(heading));
  });
}
