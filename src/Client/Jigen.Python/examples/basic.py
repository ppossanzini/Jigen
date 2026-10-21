from jigen import ConnectionOptions, Context, SearchOptions, SparseVector, equals

with Context(ConnectionOptions(database="demo")) as context:
    articles = context.collection("articles")

    # Let the Jigen server create the embedding with its ONNX model.
    articles.upsert(
        "article-1",
        {"title": "Jigen DB", "category": "database"},
        text="Jigen is a vector database written in C#.",
        # Lexical slot, independent of the sentence: caller-supplied terms and
        # weights, never derived from the text by the server.
        sparse=SparseVector(terms=(12, 98), weights=(0.8, 1.2)),
    )

    results = articles.search(
        "vector databases for .NET",
        top=5,
        filter=equals("category", "database"),
        options=SearchOptions(min_score=0.3),
    )
    for result in results:
        print(result.score, result.content)

    # Exact lexical search and the fusion of the two rankings.
    lexical = articles.search_sparse(SparseVector(terms=(12, 98), weights=(0.8, 1.2)), top=5)
    print("lexical:", [(r.score, r.content) for r in lexical])
