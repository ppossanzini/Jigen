# Stack Profile

Status: Locked

| Slot | Technology |
| --- | --- |
| Backend platform | .NET 10 / C# |
| Embedding runtime | ONNX Runtime 1.27 |
| Tokenization | Microsoft.ML.Tokenizers and ONNX Runtime Extensions |
| Tests | xUnit |

The embedding subsystem runs local ONNX models. Hardware execution providers are selected through `EmbeddingGeneratorOptions` and the corresponding build flavor.
