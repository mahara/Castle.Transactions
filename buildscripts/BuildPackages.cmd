@ECHO OFF


SET BUILD_CONFIGURATION=Release
SET BUILD_VERSION=1.0.0


:INITIALIZE_ARGUMENTS

SET %1
REM ECHO arg1 = %1
SET %2
REM ECHO arg2 = %2


:SET_BUILD_CONFIGURATION

IF "%configuration%"=="" GOTO SET_BUILD_VERSION
SET BUILD_CONFIGURATION=%configuration%


:SET_BUILD_VERSION

IF "%version%"=="" GOTO BUILD
SET BUILD_VERSION=%version%


:BUILD

ECHO ----------------------------------------------------
ECHO Building "%BUILD_CONFIGURATION%" packages with version "%BUILD_VERSION%"...
ECHO ----------------------------------------------------

dotnet build "Castle.Transactions.sln" --property:PACKAGE_VERSION=%BUILD_VERSION% --configuration %BUILD_CONFIGURATION% || EXIT /B 4

dotnet build "tools\Explicit.NuGet.Versions\Explicit.NuGet.Versions.sln" --configuration Release || EXIT /B 4
"tools\Explicit.NuGet.Versions\build\nev.exe" "build" "Castle." || EXIT /B 4


:TEST

REM https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test
REM https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-vstest
REM https://github.com/Microsoft/vstest-docs/blob/main/docs/report.md
REM https://github.com/spekt/nunit.testlogger/issues/56

ECHO ------------------------------------
ECHO Running .NET (net6.0) Unit Tests
ECHO ------------------------------------

dotnet test "src\Castle.Services.Transaction.Tests\bin\%BUILD_CONFIGURATION%\net6.0\Castle.Services.Transaction.Tests.dll" --results-directory "build\%BUILD_CONFIGURATION%" --logger "nunit;LogFileName=Castle.Services.Transaction.Tests_net6.0_TestResults.xml;format=nunit3" || EXIT /B 8
dotnet test "src\Castle.Facilities.AutoTx.Tests\bin\%BUILD_CONFIGURATION%\net6.0\Castle.Facilities.AutoTx.Tests.dll" --results-directory "build\%BUILD_CONFIGURATION%" --logger "nunit;LogFileName=Castle.Facilities.AutoTx.Tests_net6.0_TestResults.xml;format=nunit3" || EXIT /B 8

ECHO --------------------------------------------
ECHO Running .NET Framework (net48) Unit Tests
ECHO --------------------------------------------

dotnet test "src\Castle.Services.Transaction.Tests\bin\%BUILD_CONFIGURATION%\net48\Castle.Services.Transaction.Tests.dll" --results-directory "build\%BUILD_CONFIGURATION%" --logger "nunit;LogFileName=Castle.Services.Transaction.Tests_net48_TestResults.xml;format=nunit3" || EXIT /B 8
dotnet test "src\Castle.Facilities.AutoTx.Tests\bin\%BUILD_CONFIGURATION%\net48\Castle.Facilities.AutoTx.Tests.dll" --results-directory "build\%BUILD_CONFIGURATION%" --logger "nunit;LogFileName=Castle.Facilities.AutoTx.Tests_net48_TestResults.xml;format=nunit3" || EXIT /B 8
