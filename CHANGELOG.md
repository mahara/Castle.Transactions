# Castle.Transactions (Castle.Services.Transaction &amp; Castle.Facilities.AutoTx) - Changelog

## 5.4.0 (2024-11-30)
### All

Improvements:
- Added .NET 8.0 and .NET 7.0 support

Breaking Changes:
- Changed ```Castle.Services.Transaction.ITransaction.Context``` property type from ```System.Collections.IDictionary``` to ```System.Collections.Generic.IDictionary<string, object>```


## 5.3.0 (2022-09-17)

### All

Breaking Changes:
- Upgraded to .NET 6.0 and .NET Framework 4.8
- Replaced ```Castle.Services.Transaction.TransactionMode``` with ```System.Transactions.TransactionScopeOption```
- Replaced ```Castle.Services.Transaction.IsolationMode``` with ```System.Transactions.IsolationLevel```
- Renamed ```Castle.Services.Transaction.ITransaction.IsolationMode``` property to ```Castle.Services.Transaction.ITransaction.IsolationLevel```


## 5.2.0 (2022-06-24)

### All

Improvements:
- Added ```AsyncLocalActivityManager```
- Updated [Castle.Windsor] version to 5.1.2

Breaking Changes:
- Changed default ```Castle.Services.Transaction.IActivityManager``` in ```Castle.Services.Transaction.DefaultTransactionManager``` to ```Castle.Services.Transaction.AsyncLocalActivityManager```


## 5.1.0 (2022-02-20)

### All

Improvements:
- Updated [Castle.Core] version to 4.4.1
- Updated [Castle.Windsor] version to 5.1.1


## 5.0.0 (2021-05-30)

### All

Improvements:
- Upgraded to SDK-style projects

Breaking Changes:
- Removed .NET Framework 3.5, .NET Framework 4.0, and .NET Framework 4.0 Client Profile supports
- Upgraded [Castle.Core] version to 4.4.0
- Upgraded [Castle.Windsor] version to 5.0.0


## 3.3.0 (2016-05-22)

### All

Breaking Changes:
- Upgraded [Castle.Core] version to 3.3.0
- Upgraded [Castle.Windsor] version to 3.3.0



