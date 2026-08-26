from jigen import ConnectionOptions, Context, SearchOptions, equals

with Context(ConnectionOptions(database="demo")) as context:
    articles = context.collection("articles")

    # Let the Jigen server create the embedding with its ONNX model.
    articles.upsert(
        "article-1",
        {"title": "Jigen DB", "category": "database"},
        text="Jigen is a vector database written in C#.",
    )

    results = articles.search(
        "vector databases for .NET",
        top=5,
        filter=equals("category", "database"),
        options=SearchOptions(min_score=0.3),
    )
    for result in results:
        print(result.score, result.content)
