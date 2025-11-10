# Castle.Transactions (Castle.Services.Transaction &amp; Castle.Facilities.AutoTx) - Changelog


## 5.5.0 (2025-11-11)

Improvements:
- Added support for **`.NET 9.0`**.

Breaking Changes:
- Removed support for **`.NET 7.0`** and **`.NET 6.0`**.
- Replaced **`IndexRange`** with **`Microsoft.Bcl.Memory`**.


## 5.4.0 (2024-11-30)

### All

Improvements:
- Added support for **`.NET 8.0`** and **`.NET 7.0`**.
- Enabled Nullable Reference Types (NRT)
  (https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/nullable-reference-types).

### Castle.Services.Transaction

Breaking Changes:
- EXPERIMENTAL: Enabled implicit distributed transactions by default on Windows for **`.NET 7.0`** and later versions.


## 5.3.0 (2022-09-17)

### All

Breaking Changes:
- Upgraded to **`.NET 6.0`** and **`.NET Framework 4.8`**.

### Castle.Services.Transaction

Breaking Changes:
- Replaced `Castle.Services.Transaction.IsolationMode` with `System.Transactions.IsolationLevel`.
- Renamed `IsolationMode` to `IsolationLevel`.


## 5.2.0 (2022-06-24)

### All

Improvements:
- Updated **`Castle.Windsor`** to 5.1.2.


## 5.1.0 (2022-02-20)

### All

Improvements:
- Updated **`Castle.Core`** to 4.4.1.
- Updated **`Castle.Windsor`** to 5.1.1.


## 5.0.0 (2021-05-30)

### All

Improvements:
- Upgraded to SDK-style .NET projects
  (https://learn.microsoft.com/en-us/dotnet/core/project-sdk/overview).
- Added **`.NET`** support.
- Upgraded to **`.NET Framework 4.7.2`**.

Breaking Changes:
- Removed support for **`.NET Framework 3.5`**, **`.NET Framework 4.0`**, and **`.NET Framework 4.0 Client Profile`**.
- Removed **`Mono`** support.
- Updated **`Castle.Core`** to 4.4.0.
- Updated **`Castle.Windsor`** to 5.0.0.

### Castle.Services.Transaction

Improvements:
- Added `AsyncLocalActivityManager` and `ThreadLocalActivityManager`.

Breaking Changes:
- Changed the default `IActivityManager` in `DefaultTransactionManager` from `CallContextActivityManager` to `AsyncLocalActivityManager`.
- Changed the property type of `Castle.Services.Transaction.ITransaction.Context` from `System.Collections.IDictionary` to `System.Collections.Generic.IDictionary<string, object>`.
- Renamed `IMapPath` to `IPathMapper`.

### Castle.Facilities.AutoTx

Breaking Changes:
- Renamed `AutoTxFacility`'s properties `AllowAccessOutsideRootFolder` to `AllowAccessOutsideRootDirectory` and `RootFolder` to `RootDirectory`.


## 3.3.0 (2016-05-22)

### All

Breaking Changes:
- Updated **`Castle.Core`** to 3.3.0.
- Updated **`Castle.Windsor`** to 3.3.0.
