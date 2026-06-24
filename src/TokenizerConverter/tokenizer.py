from transformers import AutoTokenizer
from onnxruntime_extensions import gen_processing_models

model_id = "intfloat/multilingual-e5-small"

tokenizer = AutoTokenizer.from_pretrained(model_id)

# genera il modello ONNX del tokenizer/preprocessing
onnx_model = gen_processing_models(
    tokenizer,
    pre_kwargs={}
)

# alcune versioni ritornano una tupla/lista, altre un singolo modello
if isinstance(onnx_model, (list, tuple)):
    tokenizer_onnx = onnx_model[0]
else:
    tokenizer_onnx = onnx_model

with open("tokenizer.onnx", "wb") as f:
    f.write(tokenizer_onnx.SerializeToString())

print("Salvato tokenizer.onnx")