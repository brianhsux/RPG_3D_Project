# 仙俠 Q 版透明素材（ComfyUI）

這組工作流用 SDXL 批次產出透明背景的 2D 仙俠／修仙 Q 版遊戲素材：單一角色或單一道具，粗描邊、隔離、全身。預設主題是一隻帶光暈的白色靈雀。同一套圖也可以改成八卦陣、紫金葫蘆、青龍、劍俠。

模型權重放在你本機的 ComfyUI `models/`，此資料夾只有工作流與檔名佔位。

| 檔案 | 用途 |
| --- | --- |
| `xianxia-sprite.json` | 拖進 ComfyUI 畫布的 UI 工作流 |
| `xianxia-sprite-api.json` | `POST /prompt` 的 `prompt` 物件（節點 id → `class_type`、`inputs`、`_meta`） |

畫布上的輸出選擇預設關閉，只存一張帶 alpha 的 PNG。

## 安裝缺少的節點

用 ComfyUI Manager 安裝，或在 `ComfyUI/custom_nodes` 執行：

```bash
git clone https://github.com/huchenlei/ComfyUI-layerdiffuse.git
git clone https://github.com/Jcd1230/rembg-comfyui-node.git
git clone https://github.com/Fannovel16/comfyui_controlnet_aux.git
```

接著在 **ComfyUI 正在使用的那個 Python** 裡安裝依賴（M4 Pro 用 CPU 版 rembg；`rembg[gpu]` 是給 NVIDIA CUDA 的）：

```bash
pip install -r ComfyUI-layerdiffuse/requirements.txt
pip install -r comfyui_controlnet_aux/requirements.txt
pip install rembg
```

裝完後重開 ComfyUI，再重新載入工作流。

| 套件 | 圖上會用到的 class_type |
| --- | --- |
| [huchenlei/ComfyUI-layerdiffuse](https://github.com/huchenlei/ComfyUI-layerdiffuse) | `LayeredDiffusionApply`、`LayeredDiffusionDecodeRGBA` |
| [Jcd1230/rembg-comfyui-node](https://github.com/Jcd1230/rembg-comfyui-node) | `Image Remove Background (rembg)` |
| [Fannovel16/comfyui_controlnet_aux](https://github.com/Fannovel16/comfyui_controlnet_aux) | `OpenposePreprocessor`、`CannyEdgePreprocessor` |

`ComfySwitchNode`（If/Else Switch）、`CLIPTextEncodeSDXL`、`PrimitiveStringMultiline` 隨目前的 ComfyUI 本體提供，在 `comfy_extras` 裡。

## 放進 ComfyUI 的檔名

把檔案放進下列路徑，或把節點上的檔名改成你實際的檔名。名稱必須與節點字串一致，ComfyUI 才過得了清單檢查。

| 路徑 | 佔位檔名 | 建議內容 |
| --- | --- | --- |
| `models/checkpoints/` | `animagine-xl.safetensors` | 動漫／遊戲素材向 SDXL，例如 Animagine XL 3.1 或 4.0 |
| `models/loras/` | `xianxia-guofeng.safetensors` | 國風／仙俠 LoRA。強度 0 時不套用 |
| `models/loras/` | `game-icon-chibi.safetensors` | 粗描邊遊戲圖示／Q 版 LoRA。強度 0 時不套用 |
| `models/controlnet/` | `openpose-sdxl.safetensors` | SDXL OpenPose，例如 xinsir 或 thibaud 的 OpenPose SDXL |
| `models/controlnet/` | `canny-sdxl.safetensors` | SDXL Canny／線稿，例如 xinsir 的 Canny SDXL |

第二個 LoRA 槽可以改掛另一個 Q 版 LoRA（搜尋詞可以用 chibi、nendoroid、thick outline、game icon）。下載後把節點 3 的 `lora_name` 改成那個檔名，或把檔案重新命名成 `game-icon-chibi.safetensors`。

Clip Skip 預設 `-2`，對應 Animagine XL 3.x 的建議。Checkpoint 的 CLIP 同時帶 CLIP-L 與 CLIP-G；`CLIPSetLastLayer` 設完層數後，`CLIPTextEncodeSDXL` 把同一段提示詞送進 `text_g`（CLIP-G）和 `text_l`（CLIP-L），並寫入 1024 的寬高條件。改提示詞時改節點 5 或 6 即可，兩路會一起變。

第一次跑 Layer Diffuse 時，節點會自行把約 709 MB 的 `layer_xl_transparent_attn.safetensors` 與約 199 MB 的 `vae_transparent_decoder.safetensors` 下載到 `models/layer_model/`。需要網路。這兩個檔不在這個 repo 裡。

## 載入 UI 工作流

1. 啟動 ComfyUI。
2. 把 `workflows/xianxia-sprite.json` 拖進畫布，或用選單 Load。
3. 缺節點會顯示為缺失。裝好 custom node、重開後再載入一次。
4. 確認 checkpoint、兩份 LoRA、兩份 ControlNet 的檔名都能在下拉清單裡選到。
5. 按 Queue Prompt。輸出在 `ComfyUI/output/xianxia-sprite_*.png`。

取樣預設：1024×1024、euler、scheduler `normal`、28 步、CFG 6、seed 固定（畫布上的 seed 控制是 `fixed`）。寬高維持 64 的倍數，透明解碼才會過。

## 用 API 排隊

`xianxia-sprite-api.json` 就是 `POST /prompt` 裡的 `prompt`。ComfyUI 預設在 `http://127.0.0.1:8188`。

```bash
python3 - <<'PY'
import json, urllib.request
prompt = json.load(open("workflows/xianxia-sprite-api.json", encoding="utf-8"))
prompt["5"]["inputs"]["value"] = (
    "game asset, 2d game art, vector style, thick outlines, (single object:1.2), "
    "isolated, a purple and gold gourd, magical treasure, glowing aura, xianxia"
)
prompt["17"]["inputs"]["seed"] = 42
body = json.dumps({"client_id": "xianxia-batch", "prompt": prompt}).encode()
req = urllib.request.Request(
    "http://127.0.0.1:8188/prompt",
    data=body,
    headers={"Content-Type": "application/json"},
)
print(urllib.request.urlopen(req).read().decode())
PY
```

回傳的 `prompt_id` 可用 `GET /history/{prompt_id}` 查，圖在 ComfyUI 的 output 目錄。批次時迴圈改節點 5 的 `value` 和節點 17 的 `seed`。同一提示詞要一次多張，改節點 9 的 `batch_size`。

可覆寫的輸入都在各節點的 `inputs` 裡：

| 項目 | 節點 id | 欄位 |
| --- | --- | --- |
| Checkpoint 檔名 | 1 | `ckpt_name` |
| 國風 LoRA 檔名與強度 | 2 | `lora_name`、`strength_model`、`strength_clip` |
| Q 版 LoRA 檔名與強度 | 3 | `lora_name`、`strength_model`、`strength_clip` |
| Clip Skip | 4 | `stop_at_clip_layer` |
| 正向提示詞 | 5 | `value` |
| 負向提示詞 | 6 | `value` |
| 步數、CFG、seed、取樣器 | 17 | `steps`、`cfg`、`seed`、`sampler_name`、`scheduler` |
| OpenPose 強度 | 15 | `strength` |
| Canny 強度 | 16 | `strength` |
| 輸出選擇 | 21 | `switch`（`false` 透明解碼，`true` Rembg） |
| 姿勢圖檔名 | 23 | `image` |
| 線稿圖檔名 | 25 | `image` |

改解析度時，節點 9 的 `width`／`height` 要和節點 7、8 的 `width`、`height`、`target_width`、`target_height` 一起改，並保持 64 的倍數。

## Layer Diffuse 與 Rembg

預設走透明生成：`LayeredDiffusionApply` 使用 `SDXL, Attention Injection`，KSampler 之後先 `VAEDecode`，再由 `LayeredDiffusionDecodeRGBA`（`sd_version` = `SDXL`）把 alpha 併回 RGBA，經 `SaveImage` 存成 PNG。

Rembg 接在同一個 `VAEDecode` 後面。節點 21 是單一輸出選擇：`switch` 為 `false` 時只走透明解碼，Rembg 不會執行；設成 `true` 時改存 Rembg 的去背圖，透明解碼不會執行。畫布上只有一個 Save Image，所以不會各存一張。

適合用 Layer Diffuse 的情況：髮絲、光暈、法寶邊緣要半透明，而且提示詞已經是單一主體、乾淨背景。

適合改走 Rembg 的情況：透明解碼邊緣發灰，或你想要一般 SDXL 取樣再硬切背景。若要「完全沒有 Layer Diffuse 權重」的取樣，把節點 10 設成 Bypass（UI：右鍵 → Mode → Bypass），並把 `switch` 設為 `true`。API 沒有 Bypass：拿掉節點 10，把節點 17 的 `model` 改成 `["3", 0]`（LoRA 之後的模型）。

## ControlNet 與 Flutter 換幀

OpenPose、Canny 的強度預設都是 0，image 接的是節點內建的空白圖，所以沒有參考圖也能跑。強度為 0 時 conditioning 維持原樣。

最右側的 Load Image 與預處理器預設靜音，不在取樣路徑上。要鎖動作時：

1. 準備 1024×1024 的參考圖，腳底對齊、角色佔畫面的比例固定。`idle` 一張、`attack` 一張。
2. 放到 `ComfyUI/input/`，檔名例如 `idle.png`、`attack.png`。
3. UI：右鍵「姿勢參考圖」「OpenPose 預處理」→ Mode → Always。把 OpenPose 的 IMAGE 接到「套用 OpenPose」的 image（取代空白圖）。`strength` 約 0.7–0.85。
4. 線稿或剪影用 Canny，同樣解除靜音後接到「套用 Canny」，`strength` 約 0.4–0.6，避免把服裝細節鎖死。
5. 使用 xinsir 的 OpenPose SDXL 時，把 OpenPose 預處理的 `scale_stick_for_xinsr_cn` 改成 `enable`。thibaud 那份維持 `disable`。

API 接上已上傳的姿勢圖：

```python
prompt["23"]["inputs"]["image"] = "idle.png"
prompt["15"]["inputs"]["image"] = ["24", 0]
prompt["15"]["inputs"]["strength"] = 0.8
```

Canny 則把節點 25 的 `image` 設成檔名，節點 16 的 `image` 改成 `["26", 0]`，再提高 `strength`。

Flutter 換幀時，idle 與 attack 共用同一段提示詞、同一組 LoRA 強度與同一個 ControlNet 檔，只換參考圖（以及你要的 seed）。兩張 PNG 都帶 alpha，直接放進 Flutter assets 輪播。

## 範例提示詞

負向提示詞維持節點 6 的預設。正向提示詞用英文標籤（Animagine 這類模型吃 Danbooru 風格標籤）。

白靈雀（預設）：

```text
game asset, 2d game art, vector style, thick outlines, (single object:1.2), isolated, full body, a cute white mythical sparrow, glowing aura, xianxia
```

八卦陣：

```text
game asset, 2d game art, vector style, thick outlines, (single object:1.2), isolated, a bagua array, eight trigrams, glowing runes, purple and gold, xianxia
```

紫金葫蘆：

```text
game asset, 2d game art, vector style, thick outlines, (single object:1.2), isolated, a purple and gold gourd, magical treasure, glowing aura, xianxia
```

青龍：

```text
game asset, 2d game art, vector style, thick outlines, (single object:1.2), isolated, full body, a cute azure dragon, eastern dragon, glowing aura, xianxia, chibi
```

劍俠：

```text
game asset, 2d game art, vector style, thick outlines, (single object:1.2), isolated, full body, a cute chibi sword hero, hanfu, holding a sword, xianxia
```

負向：

```text
3d, realistic, background elements, scenery, shadow, multiple views, text, watermark, photo
```

## 載入與排隊的前提

工作流 JSON 可以在模型還沒放好時拖進畫布。下列情況會讓節點變紅，或讓 Queue／`POST /prompt` 被拒絕：

- 未安裝 LayerDiffuse、rembg 節點（含 `pip install rembg`）、comfyui_controlnet_aux。API 圖裡即使靜音路徑上的預處理器也佔了一個節點，class 必須存在。
- ComfyUI 太舊，沒有 `ComfySwitchNode`、`CLIPTextEncodeSDXL` 或 `PrimitiveStringMultiline`。
- `animagine-xl.safetensors`、兩份 LoRA、兩份 ControlNet 的佔位檔名不在對應資料夾。強度 0 仍會先做檔名清單檢查；通過之後，LoRA 強度 0 才會跳過權重載入，ControlNet 強度 0 才會原樣送出 conditioning。
- 參考圖 `pose_ref.png`／`sketch_ref.png` 還沒放進 `input/` 時就解除靜音並接到取樣路徑。預設接法用空白圖，不讀這兩個檔。
- 寬或高不是 64 的倍數時，`LayeredDiffusionDecodeRGBA` 會在解碼時失敗。
