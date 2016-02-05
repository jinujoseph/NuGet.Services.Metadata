@ECHO OFF
PUSHD %~dp0
SET SymbolCache=%SystemDrive%\SymbolCache
SET BinaryPath=C:\Code\SourceIndex\bin\Debug\CommandLine.dll

@ECHO ON

:: Download Symbols for target binary from configured source URL
:: Cache at %SystemDrive%\SymbolCache.
:: Log binary to PDB path mapping to symchk.log
symchk.exe "%BinaryPath%" /s "SRV*%SymbolCache%" /v