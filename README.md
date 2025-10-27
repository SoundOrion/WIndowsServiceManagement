# 🧩 Windows サービス管理ツール（cmd.exe /c ベース）

このツールは、Windows サービスを **C# コンソールアプリ**から簡単に
登録・起動・停止・削除・状態確認できるようにしたものです。

さらに、**「サービスとしてログオン（SeServiceLogonRight）」権限の自動付与**にも対応しています。
（管理者権限で実行すれば、ユーザーアカウントでもサービス実行が可能になります）

---

## 🚀 機能一覧

| No | 機能名               | 説明                               |
| -- | ----------------- | -------------------------------- |
| 1  | 登録(create)        | `sc create` を使ってサービスを新規登録        |
| 2  | 起動(start)         | `net start` でサービスを起動             |
| 3  | 停止(stop)          | `net stop` でサービスを停止              |
| 4  | 削除(delete)        | `sc delete` でサービスを削除             |
| 5  | 状態(query)         | `sc query` および `sc qc` で状態と設定を表示 |
| 6  | 説明設定(description) | `sc description` による説明文の更新       |
| 7  | 自動/手動/無効 切替       | `sc config` による起動種別変更            |
| 8  | SeServiceLogon 付与 | 指定アカウントに「サービスとしてログオン」権限を自動付与     |

---

## ⚙️ 動作の流れ

### 1️⃣ App.config で設定を定義

```xml
<appSettings>
  <add key="ServiceName" value="MyService" />
  <add key="DisplayName" value="My Sample Service" />
  <add key="BinPath" value="C:\Program Files\MyApp\MyApp.exe" />
  <add key="StartupType" value="auto" />
  <add key="Description" value="サンプルサービス" />
  <add key="ServiceUser" value=".\\svcuser" />
  <add key="ServicePassword" value="Pass123!" />
</appSettings>
```

* `ServiceUser` と `ServicePassword` を設定すると、指定ユーザーで登録します。
* `ServiceUser` を省略した場合は **LocalSystem** で動作します。

---

### 2️⃣ 管理者権限でアプリを起動

> ⚠️ 管理者として実行しないと、権限付与やサービス登録に失敗します。

起動例（コンソール）：

```cmd
WindowsServiceManagement.exe
```

---

### 3️⃣ SeServiceLogon 権限を付与

メニュー `[8] SeServiceLogon 付与` を選択します。

```
アカウント .\svcuser に『サービスとしてログオン』権限を付与しますか？
実行しますか？ (y/N): y
OK: .\svcuser に SeServiceLogonRight を付与しました。
```

これにより、ユーザー `.\svcuser` がサービスを実行できるようになります。
（GUI でいうと `secpol.msc` → 「サービスとしてログオンを許可する」に追加される）

---

### 4️⃣ サービスを登録

メニュー `[1] 登録(create)` を選びます。

内部で以下のようなコマンドが自動実行されます：

```cmd
sc create "MyService" binPath= "C:\Program Files\MyApp\MyApp.exe" start= auto DisplayName= "My Sample Service"
sc config "MyService" obj= ".\svcuser" password= "Pass123!"
sc description "MyService" "サンプルサービス"
```

---

### 5️⃣ 起動・停止・削除などをメニューから実行

| 操作   | コマンド相当                |
| ---- | --------------------- |
| 起動   | `net start MyService` |
| 停止   | `net stop MyService`  |
| 状態確認 | `sc query MyService`  |
| 削除   | `sc delete MyService` |

---

## 🔒 注意事項

* SeServiceLogonRight の付与は **管理者権限でのみ成功** します。
  権限がない場合、`LsaOpenPolicy 失敗` エラーが発生します。
* ドメイン環境では、グループポリシー(GPO) によりローカル設定が上書きされる場合があります。
* LocalSystem で登録したサービスは **UI（メモ帳など）を開けません**。
* `BinPath` のパスにスペースがある場合、必ず `"` で囲ってください。

---

## 🧩 成功確認

PowerShell または GUI で確認できます。

### PowerShell

```powershell
whoami /priv | find "SeServiceLogonRight"
```

### GUI

1. `secpol.msc` を起動
2. 「ローカルポリシー」 → 「ユーザー権限の割り当て」
3. 「サービスとしてログオンを許可する」に指定ユーザーがあることを確認

---

## ✅ 開発メモ

* `cmd.exe /c` 経由でコマンドを実行しているため、出力がすべてコンソールに表示されます。
* `RunCmd()` が標準出力・標準エラーをキャプチャして表示します。
* `Q()` は二重引用符を安全に処理するためのエスケープヘルパーです。
* 終了時はウィンドウを閉じず、キー入力待ちになります。

---

## 📘 必要権限まとめ

| 操作                | 権限                   |
| ----------------- | -------------------- |
| SeServiceLogon 付与 | 管理者                  |
| サービス登録・削除         | 管理者                  |
| サービス起動・停止         | 管理者（または対象ユーザーに十分な権限） |

---

## 💬 典型的な運用フロー

1. 管理者がツールを実行
2. `[8]` で一般ユーザーにログオン権限を付与
3. `[1]` でそのユーザーを指定してサービス登録
4. `[2]` で起動確認
5. `[5]` 状態確認で稼働チェック
6. `[3]/[4]` で停止・削除

---

## 👤 実行アカウントの指定と権限

Windows サービスは、実行アカウントを指定することで
**権限・ネットワークアクセス・UI制約**などの性質が変わります。
`App.config` の設定値（または登録時の `obj=` パラメータ）で指定します。

---

### 🔧 主なアカウント種別

| 指定例                           | 実行ユーザー             | 登録可否       | 特徴・用途                                                                           |
| ----------------------------- | ------------------ | ---------- | ------------------------------------------------------------------------------- |
| *(指定なし)*                      | **LocalSystem**    | ✅          | 既定。最高権限（SYSTEM）で動作。すべてのローカルリソースにアクセス可能。<br>⚠️ UI表示不可（セッション0）。ネットワークはマシン資格情報で接続。 |
| `.\User`                      | **ローカルユーザー**       | ✅（パスワード必須） | そのPC限定のユーザー。GUIアプリの動作権限に近い。<br>🔸 「サービスとしてログオン」権限の付与が必要。                        |
| `DOMAIN\User`                 | **ドメインユーザー**       | ✅          | Active Directory 環境で使用。共有リソースやSQLサーバーなどにアクセス可能。<br>⚠️ GPO制御により権限が上書きされる場合あり。    |
| `NT AUTHORITY\LocalService`   | **LocalService**   | ✅          | 組み込みの最小権限アカウント。ローカル操作のみ。<br>ネットワークアクセス不可。                                       |
| `NT AUTHORITY\NetworkService` | **NetworkService** | ✅          | 組み込みの軽権限アカウント。<br>マシン資格情報でネットワークアクセスが可能（Webサービスなどに便利）。                          |

---

### 🧱 登録コマンド例

#### LocalSystem（既定）

```cmd
sc create MyService binPath= "C:\Program Files\MyApp\MyApp.exe" start= auto
```

#### NetworkService

```cmd
sc create MyService binPath= "C:\Program Files\MyApp\MyApp.exe" obj= "NT AUTHORITY\NetworkService" start= auto
```

#### ユーザーアカウント

```cmd
sc create MyService binPath= "C:\Program Files\MyApp\MyApp.exe" obj= ".\svcuser" password= "P@ssw0rd" start= auto
```

> ※ `obj=` はアカウント、`password=` は該当アカウントのログオンパスワードです。
> LocalSystem / LocalService / NetworkService はパスワード不要です。

---

### 🔒 事前に必要な権限

| 対象アカウント                       | 必要な権限                 | 補足                                                             |
| ----------------------------- | --------------------- | -------------------------------------------------------------- |
| LocalSystem                   | なし                    | 既定で有効                                                          |
| LocalService / NetworkService | なし                    | 組み込み権限あり                                                       |
| `.\\User` / `DOMAIN\\User`    | `SeServiceLogonRight` | 「サービスとしてログオンを許可する」権限。<br>ツールの `[8] SeServiceLogon 付与` で自動設定可能。 |

---

### ⚠️ ドメイン環境での注意

* GPO（グループポリシー）が適用される環境では、
  ローカルで付与した `SeServiceLogonRight` が
  **ドメインポリシーによって上書き／削除される場合** があります。
* 永続的に運用する場合は、ドメイン管理者に依頼し、
  **GPO側の「サービスとしてログオンを許可する」ポリシーに該当ユーザーを追加**してください。

---

### 🧩 選択の目安

| 目的                    | 推奨アカウント                           | 理由               |
| --------------------- | --------------------------------- | ---------------- |
| ローカル常駐ツール             | `LocalSystem`                     | 管理者権限が必要な操作に強い   |
| 軽量バックグラウンドサービス        | `LocalService`                    | 最小権限・安全          |
| ネットワーク連携（API / Web通信） | `NetworkService`                  | マシン資格情報で外部通信可能   |
| ファイル共有・DBアクセス（ドメイン）   | `DOMAIN\User`                     | ドメイン資格情報で安全にアクセス |
| 特定ユーザーのスクリプト実行        | `.\\User` + `SeServiceLogonRight` | GUI権限に近く、制御しやすい  |

---

### 🧭 備考

* LocalSystem では UI 表示（notepad, MessageBox など）は不可。
  → 対話的動作が必要な場合は **UserBridge** などでセッションブリッジを行う。
* ユーザーアカウント指定時、パスワードが期限切れだと起動失敗。
* `NetworkService` はネットワーク共有にアクセスする際、
  「`<HOSTNAME>$`」として認証されます。

---

## 🧰 ビルド情報

* .NET Framework 4.8（または互換の .NET 6 以降でも動作可）
* 管理者権限のコンソールで実行
* `app.config` に設定値を定義

---

## 📝 ライセンス

このツールのコードはサンプル用途向けであり、
環境に応じて自由に改変・組み込み可能です。
