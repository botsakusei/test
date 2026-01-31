# 実装メモ

## 自動修正の範囲
- 空欄パラメータの削除（Expression Parameters / MA Parameters / Animator）
- 型競合の統一
  - 優先順位: Expression Parameters → Animator → MA
- Menu Installer の参照先メニュー設定

## 自動修正できないケース
- 型が Unknown のまま確定できない場合
- Expressions Menu が作成されていない場合
- MA が未導入でメニュー系のコンポーネントが存在しない場合

## 例外時の挙動
- Reflection で MA 型が見つからない場合はスキップ
- 例外で処理中断しても、Undo / Dirty 管理で最小限の変更のみ適用
