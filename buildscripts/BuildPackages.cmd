@ECHO OFF
REM ****************************************************************************
REM Copyright 2004-2019 Castle Project - http://www.castleproject.org/
REM Licensed under the Apache License, Version 2.0 (the "License");
REM you may not use this file except in compliance with the License.
REM You may obtain a copy of the License at
REM
REM     http://www.apache.org/licenses/LICENSE-2.0
REM
REM Unless required by applicable law or agreed to in writing, software
REM distributed under the License is distributed on an "AS IS" BASIS,
REM WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
REM See the License for the specific language governing permissions and
REM limitations under the License.
REM ****************************************************************************

:INITIALIZE_ARGUMENTS
SET %1
SET %2

REM ECHO arg1 = %1
REM ECHO arg2 = %2

GOTO INITIALIZE_VARIABLES


:INITIALIZE_VARIABLES
SET CONFIGURATION="Release"
SET BUILD_VERSION="5.0.0"

GOTO SET_CONFIGURATION


:SET_CONFIGURATION
IF "%config%"=="" GOTO SET_BUILD_VERSION
SET CONFIGURATION=%config%

GOTO SET_BUILD_VERSION


:SET_BUILD_VERSION
IF "%version%"=="" GOTO RESTORE_PACKAGES
SET BUILD_VERSION=%version%

ECHO ---------------------------------------------------
REM ECHO Building "%config%" packages with version "%version%"...
ECHO Building "%CONFIGURATION%" packages with version "%BUILD_VERSION%"...
ECHO ---------------------------------------------------

GOTO RESTORE_PACKAGES


:RESTORE_PACKAGES
REM dotnet restore .\tools\Explicit.NuGet.Versions\Explicit.NuGet.Versions.csproj
dotnet restore .\buildscripts\BuildScripts.csproj
dotnet restore .\src\Castle.Services.Transaction\Castle.Services.Transaction.csproj
dotnet restore .\src\Castle.Services.Transaction.Tests\Castle.Services.Transaction.Tests.csproj
dotnet restore .\src\Castle.Facilities.AutoTx\Castle.Facilities.AutoTx.csproj
dotnet restore .\src\Castle.Facilities.AutoTx.Tests\Castle.Facilities.AutoTx.Tests.csproj

GOTO BUILD


:BUILD
REM dotnet build .\tools\Explicit.NuGet.Versions\Explicit.NuGet.Versions.sln --no-restore
dotnet build Castle.Transactions.sln -c %CONFIGURATION% /p:APPVEYOR_BUILD_VERSION=%BUILD_VERSION% --no-restore

GOTO TEST


:TEST

ECHO ----------------
ECHO Running Tests...
ECHO ----------------

dotnet test .\src\Castle.Services.Transaction.Tests --no-restore || exit /b 1
dotnet test .\src\Castle.Facilities.AutoTx.Tests --no-restore || exit /b 1

REM GOTO NUGET_EXPLICIT_VERSIONS


REM :NUGET_EXPLICIT_VERSIONS

REM .\tools\Explicit.NuGet.Versions\build\nev.exe ".\build" "Castle.Services.Transaction"
REM .\tools\Explicit.NuGet.Versions\build\nev.exe ".\build" "Castle.Facilities.AutoTx"



