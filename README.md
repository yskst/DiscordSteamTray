# Discord Steam Tray

Windows の通知領域に Discord と Steam のステータスを表示する .NET 8 WinForms アプリです。メインフォームは表示せず、Discord は Bot Gateway、Steam は Web API で監視します。

このプロジェクトのライセンスは [MIT License](./LICENSE) です。

## 事前準備

1. .NET 8 SDK をインストールします。
2. Discord Developer Portal で Bot を作成し、**Presence Intent** と **Server Members Intent** を有効にします。**OAuth2 → URL Generator** で `bot` スコープを選び、生成された URL から対象 Guild に Bot を招待してください（操作アカウントに Manage Server 権限が必要です）。Bot Token、Guild ID、監視対象 User ID を用意してください。
3. Steam Web API Key と対象アカウントの SteamID64 を用意します。対象 Steam プロフィールは API から参照できる公開状態にしてください。
4. アプリを起動し、設定画面の「How to find these values」をクリックすると取得手順が表示されます。公式ページへのリンクも設定画面にあります。Steam API Key の Domain Name には、このローカルアプリ用に `localhost` を入力します。ブラウザーのログイン情報やトークンをアプリが自動取得することはありません。設定は Windows DPAPI（現在の Windows ユーザー）で暗号化し、`%LOCALAPPDATA%\DiscordSteamTray\settings.dat` に保存します。秘密情報を平文ファイルや Git に保存する必要はありません。

Bot は対象ユーザーが参加している Guild に所属している必要があります。Discord の Invisible は Offline として表示されます。Steam の状態 4（Snooze）は Away、5/6 は Online にまとめて表示します。

## 実行・発行

開発実行:

```powershell
dotnet run --project .\DiscordSteamTray.csproj
```

Release ビルド:

```powershell
dotnet build .\DiscordSteamTray.csproj -c Release
```

出力先: `bin\Release\net8.0-windows\DiscordSteamTray.exe`

Windows x64 向け自己完結型の単一実行ファイルを発行:

```powershell
dotnet publish .\DiscordSteamTray.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:DebugType=None -o .\publish
```

`publish` 内の `DiscordSteamTray.exe` を実行してください。初回起動時に設定画面が表示されます。以降はトレイアイコンを右クリックして「設定...」から変更できます。設定変更後はアプリが再起動して新しい値を適用します。「今すぐ更新」で両サービスを再取得し、「終了」で接続を閉じて終了します。

設定画面の **Start automatically when I sign in to Windows** を有効にすると、現在の Windows ユーザーのログイン時にアプリを起動します。管理者権限は不要です。

GitHub Actions は `main` への push、`main` 向け Pull Request、または Actions タブからの手動実行で Windows x64 版を発行します。完了後、実行結果の Artifacts から `DiscordSteamTray-win-x64` をダウンロードできます。

### GitHub Release の作成

Release は `v1.2.3` のような SemVer タグを push したときだけ、Windows ビルド成功後に作成されます。Release ノートは [CHANGELOG.md](./CHANGELOG.md) の該当バージョンから取得し、発行した EXE を添付します。

1. `CHANGELOG.md` の変更を `Unreleased` から `## [1.2.3] - YYYY-MM-DD` のようなバージョン見出しへ移動します（タグが `v1.2.3` なら見出しは `1.2.3`）。
2. 変更を `main` に commit/push します。
3. バージョンタグを push します。

```powershell
git tag v1.2.3
git push origin v1.2.3
```

Pull Request、通常の `main` push、Actions タブからの手動実行はビルドのみで、Release は作成しません。

Discord Bot Token と Steam API Key は Windows ユーザーごとに暗号化して保存します。同じ Windows ユーザーでログインしてアプリを実行してください。

## トラブルシューティング

`dotnet run` 実行時に `No .NET SDKs were found` と表示される場合、.NET ランタイムのみがインストールされ、SDK がありません。開発実行する場合は **.NET 8 SDK (Windows x64)** をインストールし、新しいターミナルで `dotnet --list-sdks` を実行して `8.0.xxx` が表示されることを確認してから、上記の `dotnet run` を実行してください。

発行済みアプリを使う場合は、SDK や `dotnet run` は不要です。`publish\DiscordSteamTray.exe` を直接起動してください。この EXE は自己完結型です。
