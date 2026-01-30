# Gon Unsheath System (SDK3 Avatars / Unity 2022.3 LTS)

## 目的
VRChatアバターに「抜刀 / 納刀ワンボタン」システムを、ドラッグ＆ドロップ＋ワンクリックで導入できます。
Modular Avatar（nadena.dev Modular Avatar）前提で、メニュー/パラメータ/FXレイヤー差し込みを自動化します。

---

## 導入（3ステップ）
1. アバター直下に **Prefab「GonUnsheathSystem」** をドラッグ＆ドロップ。  
   （Prefabが見当たらない場合は、Unity起動時に自動生成されます）
2. Inspectorの **[Auto Setup]** を1回押す。
3. もし剣の見た目が違う場合は **SwordRoot** を差し替え → **[Build / Apply]** を1回押す。
4. そのままVRCへアップロード → Expressionメニューに「Sheathe / Unsheathe」トグルが追加されます。

> SwordRoot が未設定のときは空オブジェクトが作られるため、必要に応じて剣モデルを子に入れて差し替えできます。

---

## 仕組み（簡易）
- **Boolパラメータ**: `GON_SHEATHED`
- **トグル**: `Sheathe / Unsheathe`
- **FXレイヤー**: 専用Overrideレイヤー追加（AvatarMaskで両手＋剣＋アンカーのみ）
- **Constraints**:
  - 右手: `ParentConstraint`で `HandleTarget` へ追従（Weight 0→1）
  - 左手: `ParentConstraint`で `SheathMouthTarget` へ追従（Weight 0→1）
  - 刀: `ParentConstraint`で `SheathAnchor` / `HandAnchor` を切替

---

## 調整ポイント
- `HandleTargetOffset`: 右手が掴む位置の微調整（Transform）
- `SheathMouthTargetOffset`: 左手が添える位置の微調整（Transform）
- `TransitionTime`: 抜刀/納刀の遷移時間（0.20～0.35推奨）

---

## トラブルシュート
- **手が変な方向を向く**: `HandleTargetOffset` の回転を調整してください。
- **左手が邪魔**: `SheathMouthTarget` を移動、または `LeftHandConstraint` のWeightを下げるクリップに調整。
- **既存FXと衝突**: AvatarMaskで手以降のみになっているか、またFXレイヤーのOverride設定を確認。

---

## 生成物
`Assets/GonUnsheathSystem/Generated/<AvatarName>/` に以下が生成されます。
- `GonUnsheath_FX.controller`
- `GonUnsheath_Sheathed.anim`
- `GonUnsheath_Unsheathed.anim`
- `GonUnsheath.mask`

---

## 動作チェック（Unity）
1. Play Modeで `GON_SHEATHED` の値を切り替える
2. 刀が鞘→手、手→鞘へ移動/回転していればOK
3. 右手が柄へ、左手が鞘口へ寄っていればOK

## 動作チェック（VRChat）
1. アップロード
2. Expressionsメニューのトグルを押す
3. ネットワーク同期で破綻せずに抜刀/納刀するか確認
