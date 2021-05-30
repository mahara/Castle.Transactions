# Castle.Transactions (Castle.Services.Transaction &amp; Castle.Facilities.AutoTx) - Changelog


## 5.1.0 (2021-05-xx)


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
- Added ```AsyncLocalActivityManager```.

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
