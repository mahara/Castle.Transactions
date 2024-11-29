# Castle.Transactions (Castle.Services.Transaction &amp; Castle.Facilities.AutoTx) - Changelog


## 5.4.0 (2024-11-xx)

### All

Improvements:
- Added **`.NET 8.0`** and **`.NET 7.0`** supports.
- Enabled NRT (Nullable Reference Types)
  (https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/nullable-reference-types).

### Castle.Services.Transaction

Breaking Changes:
- EXPERIMENTAL: Enabled implicit distributed transactions by default on Windows for **`.NET 7.0`** and greater.


## 5.3.0 (2022-09-17)

### All

Breaking Changes:
- Upgraded to **`.NET 6.0`** and **`.NET Framework 4.8`**.

### Castle.Services.Transaction

Breaking Changes:
- Replaced ```Castle.Services.Transaction.IsolationMode``` with ```System.Transactions.IsolationLevel```.
- Renamed ```IsolationMode``` to ```IsolationLevel```.


## 5.2.0 (2022-06-24)

### All

Improvements:
- Updated **`Castle.Windsor`** version to 5.1.2.


## 5.1.0 (2022-02-20)

### All

Improvements:
- Updated **`Castle.Core`** version to 4.4.1.
- Updated **`Castle.Windsor`** version to 5.1.1.


## 5.0.0 (2021-05-30)

### All

Improvements:
- Upgraded to SDK-style .NET projects
  (https://learn.microsoft.com/en-us/dotnet/core/project-sdk/overview).
- Added **.NET (Core)** support.
- Upgraded to **.NET Framework 4.7.2**.

Breaking Changes:
- Removed **.NET Framework 3.5**, **.NET Framework 4.0**, and **.NET Framework 4.0 Client Profile** supports.
- Removed **Mono** support.
- Updated **`Castle.Core`** version to 4.4.0.
- Updated **`Castle.Windsor`** version to 5.0.0.

### Castle.Services.Transaction

Improvements:
- Added ```AsyncLocalActivityManager``` and ```ThreadLocalActivityManager```.

Breaking Changes:
- Changed default ```IActivityManager``` in ```DefaultTransactionManager``` from ```CallContextActivityManager``` to ```AsyncLocalActivityManager```.
- Changed ```Castle.Services.Transaction.ITransaction.Context``` property type from ```System.Collections.IDictionary``` to ```System.Collections.Generic.IDictionary<string, object>```.
- Renamed ```IMapPath``` to ```IPathMapper```.

### Castle.Facilities.AutoTx

Breaking Changes:
- Renamed ```AutoTxFacility```'s properties ```AllowAccessOutsideRootFolder``` to ```AllowAccessOutsideRootDirectory``` and ```RootFolder``` to ```RootDirectory```.


## 3.3.0 (2016-05-22)

### All

Breaking Changes:
- Updated **`Castle.Core`** version to 3.3.0.
- Updated **`Castle.Windsor`** version to 3.3.0.
