# MA Auto Fixer

## 使い方（3分で完了）
1. Unity メニューから **Tools > MA Auto Fixer** を開きます。
2. Hierarchy からアバターをドラッグ&ドロップ、または **Use Selection** を押します。
3. **Scan → Fix (Dry-run) → Apply Fix** の順で確認・修正します。

### 便利なオプション
- **Scan Clone (if exists)**: `isyouhenkou(Clone)` のような ApplyOnPlay クローンも検出対象にします。
- **Verbose Log**: 走査対象や検出結果を Console に詳細出力します。
- **Allow ExpressionParameters edits**: ExpressionParameters の型変更を許可します（既定はOFF）。

## Animation Not Playing Troubleshooter
- **Tools > Animation Not Playing Troubleshooter** を開き、Play中の Clone/Original の取り違え、Animator未設定、Layer weight 0、Constraint無効などの原因を診断します。
- **Scan → Fix (Dry-run) → Apply Fix** で安全に修正案を確認できます。

## できること
- [MA-0006] パラメータ型競合の診断と自動修正
- パラメータ名が空欄の行を検出・削除
- [MA-1200] Menu Installer の参照先未設定を検出・修正
- フォント警告は「致命ではない」案内を表示

## 手作業が必要なケース
- Expression Parameters も Animator も型が不明な場合
- MA 未導入で型情報が取得できない場合
- Menu Installer の参照先メニューが未作成な場合

## 注意
- **Fix (Dry-run)** は変更せずに内容だけ表示します。
- **Apply Fix** は Undo 対応です。
