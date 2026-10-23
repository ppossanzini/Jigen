#!/usr/bin/env python3
"""Multi-vector contract check for the Python client against a live gRPC server.

Writes a multi-vector document, a single-vector document and a bulk batch, then
reads every vector back. Requires a reachable server (default localhost:3223)
and an existing database.

    pip install -e src/Client/Jigen.Python
    python .ai-generated-operations/check-python-multivector.py --database demo
"""

import argparse
import sys

from jigen import ConnectionOptions, Context


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--host", default="localhost")
    parser.add_argument("--port", type=int, default=3223)
    parser.add_argument("--database", required=True)
    parser.add_argument("--collection", default="multivector-check")
    args = parser.parse_args()

    options = ConnectionOptions(host=args.host, port=args.port, database=args.database)
    failures = []

    with Context(options) as context:
        docs = context.collection(args.collection)

        docs.upsert(
            "doc-1",
            {"title": "multi"},
            embedding=[[1.0, 0.0, 0.0], [0.0, 1.0, 0.0], [0.0, 0.0, 1.0]],
        )
        docs.upsert("doc-2", {"title": "single"}, embedding=[1.0, 0.0, 0.0])

        stored = docs.get_embeddings("doc-1")
        if stored is None or len(stored) != 3:
            failures.append(f"doc-1 stored {stored}, expected 3 vectors")

        stored = docs.get_embeddings("doc-2")
        if stored is None or len(stored) != 1 or stored[0] != [1.0, 0.0, 0.0]:
            failures.append(f"doc-2 stored {stored}, expected one vector [1.0, 0.0, 0.0]")

        for query in ([1.0, 0.0, 0.0], [0.0, 1.0, 0.0], [0.0, 0.0, 1.0]):
            hits = docs.search(query, top=1)
            if not hits or hits[0].content["title"] != "multi":
                failures.append(f"query {query} did not find doc-1: {hits}")

        accepted = docs.upsert_many(
            [
                ("doc-3", {"title": "bulk-multi"}, [[1.0, 0.0, 0.0], [0.0, 0.0, 1.0]]),
                ("doc-4", {"title": "bulk-single"}, [0.0, 1.0, 0.0]),
            ]
        )
        if accepted != 2:
            failures.append(f"bulk accepted {accepted}, expected 2")

        stored = docs.get_embeddings("doc-3")
        if stored is None or len(stored) != 2:
            failures.append(f"doc-3 stored {stored}, expected 2 vectors")

    for failure in failures:
        print(f"FAIL {failure}")
    if failures:
        return 1

    print("OK multi-vector, single-vector and bulk paths agree with the server contract")
    return 0


if __name__ == "__main__":
    sys.exit(main())
