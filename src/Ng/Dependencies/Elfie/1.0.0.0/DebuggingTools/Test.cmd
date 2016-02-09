@ECHO OFF
PUSHD %~dp0
SET SymbolCache=%SystemDrive%\SymbolCache

::SET BinaryPath=%WinDir%\Microsoft.NET\assembly\GAC_32\mscorlib\v4.0_4.0.0.0__b77a5c561934e089\mscorlib.dll
::SET SymbolSourceUrl=http://referencesource.microsoft.com/symbols
::SET DownloadedPDBPath=%SymbolCache%\mscorlib.pdb\EA27792383B44DA1A1025670606EA63D2\mscorlib.pdb

SET BinaryPath=%WinDir%\Microsoft.Net\assembly\GAC_MSIL\System.IO.Compression\v4.0_4.0.0.0__b77a5c561934e089\System.IO.Compression.dll
SET SymbolSourceUrl=http://referencesource.microsoft.com/symbols
SET DownloadedPDBPath=%SymbolCache%\MicrosoftPublicSymbols\System.IO.Compression.pdb\07EE6B2A831446F0A1CB35066D6E6CF52\System.IO.Compression.pdb"

::SET BinaryPath=C:\Download\Newtonsoft.Json 7.0.1\Newtonsoft.Json.dll
::SET SymbolSourceUrl=https://nuget.smbsrc.net
::SET DownloadedPDBPath=%SymbolCache%\Newtonsoft.Json.pdb\3E517103B3FE4DDA9A990BB58F5DF4BC1\Newtonsoft.Json.pdb
@ECHO ON

:: Download Symbols for target binary from configured source URL
:: Cache at %SystemDrive%\SymbolCache.
:: Log binary to PDB path mapping to symchk.log
::symchk.exe "%BinaryPath%" /s "SRV*%SymbolCache%*%SymbolSourceUrl%" /op /os /ol "symchk.log"


:: Dump PDB Symbol Source path and file list with Symbol Source Path Transformations
::pdbstr.exe -r -p:"%DownloadedPDBPath%" -s:srcsrv

:: Output Sample:
:: ===========================================================
::48FAA974C8EE45E1\Newtonsoft.Json.pdb" -s:srcsrv
::SRCSRV: ini ------------------------------------------------
::VERSION=2
::INDEXVERSION=2
::VERCTRL=http
::SRCSRV: variables ------------------------------------------
::SRCSRVTRG=https://nuget.smbsrc.net/src/%fnfile%(%var1%)/%var2%/%fnfile%(%var1%)
::SRCSRVCMD=
::SRCSRVVERCTRL=http
::SRCSRV: source files ---------------------------------------
::C:\Development\Releases\Json\Working\Newtonsoft.Json\Working-Signed\Src\Newtonsoft.Json\JsonArrayAttribute.cs*krlfUDSdbu0bQGDAWYosonN5NPNgeYFtJQhoDNboVAw=
:: ===========================================================
:: See Format Specification at https://msdn.microsoft.com/en-us/library/windows/hardware/ff551958(v=vs.85).aspx
:: %fnfile%(%varN%) = File Name only path in variable N
:: So:
:: https://nuget.smbsrc.net/src/%fnfile%(%var1%)/%var2%/%fnfile%(%var1%)
:: C:\Development\Releases\Json\Working\Newtonsoft.Json\Working-Signed\Src\Newtonsoft.Json\JsonArrayAttribute.cs*krlfUDSdbu0bQGDAWYosonN5NPNgeYFtJQhoDNboVAw=
:: -> https://nuget.smbsrc.net/src/JsonArrayAttribute.cs/krlfUDSdbu0bQGDAWYosonN5NPNgeYFtJQhoDNboVAw=/JsonArrayAttribute.cs


:: List PDB paths and resolved URLs for PDB source files
:: SrcTool documentation says it can be used to download sources, but it doesn't seem to work. It might've worked only when there was a download command to run. [SRCSRVCMD]
srctool.exe -n "%DownloadedPDBPath%"
