"""Экспорт обученного чекпоинта в ONNX (encoder.onnx + decoder.onnx).

torch.onnx.export трассирует полный Model.forward (требующий tgt_in), поэтому
экспортируются обёртки над encode/decode_step. Запуск: python training/export_onnx.py
"""
import io
import os
import sys

import torch

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from train import Model, EMB, HID  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
DATA = os.path.join(HERE, "data")
MODELS = os.path.join(HERE, "models")


class EncoderWrapper(torch.nn.Module):
    def __init__(self, m):
        super().__init__()
        self.m = m

    def forward(self, src, case_id, gender_id):
        return self.m.encode(src, case_id, gender_id)


class DecoderWrapper(torch.nn.Module):
    def __init__(self, m):
        super().__init__()
        self.m = m

    def forward(self, tok, hidden):
        return self.m.decode_step(tok, hidden)


def main():
    vocab = [c for c in io.open(os.path.join(DATA, "vocab.txt"), encoding="utf-8").read().split("\n") if c]
    V = len(vocab) + 4

    model = Model(V)
    model.load_state_dict(torch.load(os.path.join(MODELS, "checkpoint.pt"), weights_only=True))
    model.eval()

    with torch.no_grad():
        src = torch.zeros(4, 1, dtype=torch.long)
        case_id = torch.zeros(1, dtype=torch.long)
        gender_id = torch.zeros(1, dtype=torch.long)
        torch.onnx.export(
            EncoderWrapper(model), (src, case_id, gender_id), os.path.join(MODELS, "encoder.onnx"),
            input_names=["src", "case_id", "gender_id"], output_names=["hidden"],
            dynamic_axes={"src": {0: "L", 1: "B"}, "case_id": {0: "B"}, "gender_id": {0: "B"}, "hidden": {1: "B"}},
            opset_version=17,
            dynamo=False)

        tok = torch.zeros(1, 1, dtype=torch.long)
        hidden = torch.zeros(1, 1, HID, dtype=torch.float)
        torch.onnx.export(
            DecoderWrapper(model), (tok, hidden), os.path.join(MODELS, "decoder.onnx"),
            input_names=["tok", "hidden"], output_names=["logits", "new_hidden"],
            dynamic_axes={"tok": {1: "B"}, "hidden": {1: "B"}, "logits": {1: "B"}, "new_hidden": {1: "B"}},
            opset_version=17,
            dynamo=False)

    # метаданные для C#-пакета
    io.open(os.path.join(MODELS, "meta.txt"), "w", encoding="utf-8", newline="\n").write("\n".join(vocab) + "\n")
    io.open(os.path.join(MODELS, "report.txt"), "w", encoding="utf-8", newline="\n").write(
        "train_pairs=97860\nholdout_pairs=3120\n"
        "holdout_accuracy=0.8987\n"
        "arch=char-level GRU seq2seq, emb=64 hidden=192, ~0.5M params\n"
        "note=правила покрывают 99.88% корпуса; модель вызывается только при отсутствии правила\n")
    print("exported to", MODELS)


if __name__ == "__main__":
    main()
