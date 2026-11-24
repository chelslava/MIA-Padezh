"""Пошаговая трассировка: pytorch greedy vs ONNX greedy на одной фамилии."""
import io
import os
import sys

import numpy as np
import torch
import onnxruntime as ort

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from train import Model, EMB, HID  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
DATA = os.path.join(HERE, "data")
MODELS = os.path.join(HERE, "models")

PAD, BOS, EOS = 0, 1, 2
vocab = [c for c in io.open(os.path.join(DATA, "vocab.txt"), encoding="utf-8").read().split("\n") if c]
stoi = {c: i + 4 for i, c in enumerate(vocab)}
V = len(vocab) + 4

model = Model(V)
model.load_state_dict(torch.load(os.path.join(MODELS, "checkpoint.pt"), weights_only=True))
model.eval()

lemma = "авксентюк"
ids = [stoi[ch] for ch in lemma] + [EOS]
src = torch.tensor(ids, dtype=torch.long)[:, None]
case_id = torch.tensor([1], dtype=torch.long)
gender_id = torch.tensor([0], dtype=torch.long)

enc = ort.InferenceSession(os.path.join(MODELS, "encoder.onnx"))
dec = ort.InferenceSession(os.path.join(MODELS, "decoder.onnx"))

with torch.no_grad():
    hidden_pt = model.encode(src, case_id, gender_id)
hidden_ox = enc.run(["hidden"], {"src": src.numpy(), "case_id": case_id.numpy(), "gender_id": gender_id.numpy()})[0]

h_pt, h_ox = hidden_pt, hidden_ox
t_pt, t_ox = torch.tensor([[BOS]], dtype=torch.long), np.array([[BOS]], dtype=np.int64)
for step in range(12):
    with torch.no_grad():
        lg_pt, h_pt = model.decode_step(t_pt, h_pt)
    lg_ox, h_ox = dec.run(["logits", "new_hidden"], {"tok": t_ox, "hidden": h_ox})
    a_pt = int(np.argmax(lg_pt.numpy()[0, 0]))
    a_ox = int(np.argmax(lg_ox[0, 0]))
    ch_pt = vocab[a_pt - 4] if 4 <= a_pt - 4 < len(vocab) else f"<{a_pt}>"
    ch_ox = vocab[a_ox - 4] if 4 <= a_ox - 4 < len(vocab) else f"<{a_ox}>"
    diff = float(np.abs(lg_pt.numpy()[0, 0] - lg_ox[0, 0]).max())
    print(f"step {step}: pt={ch_pt}({a_pt}) onnx={ch_ox}({a_ox}) logit_diff={diff:.2e} hidden_diff={float(np.abs(h_pt.numpy() - h_ox).max()):.2e}")
    t_pt = torch.tensor([[a_pt]], dtype=torch.long)
    t_ox = np.array([[a_ox]], dtype=np.int64)
