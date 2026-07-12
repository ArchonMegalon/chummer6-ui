Unicode true
ManifestSupportedOS all
RequestExecutionLevel user
SetCompressor /SOLID lzma
XPStyle on
BrandingText " "
ShowInstDetails show
AutoCloseWindow false

!ifndef CHUMMER_BOOTSTRAP_CONFIG
  !error "CHUMMER_BOOTSTRAP_CONFIG must point to the generated bootstrap-config.nsh file."
!endif

!include "LogicLib.nsh"
!include "FileFunc.nsh"
!include "StrFunc.nsh"
!include "WinMessages.nsh"
!include "${CHUMMER_BOOTSTRAP_CONFIG}"

Var CommandLine
Var SmokeInstallPath
Var PayloadPathOverride
Var PayloadUrlOverride
Var PayloadSha256Override
Var PayloadSizeBytesOverride
Var ClaimCode
Var LaunchHeadId
Var FirstRelaunchArg
Var EffectivePayloadPath
Var EffectivePayloadUrl
Var EffectivePayloadSha256
Var EffectivePayloadSizeBytes
Var UninstallRequested
Var AutoUpdateRequested
Var IsSmokeInstall
Var TraceHandle
Var DownloadHelperStatus
Var DownloadHelperOutput
Var BootstrapTempRoot
Var DownloadHelperPartialPath
Var DownloadHelperStartedPath
Var DownloadHelperExitCodePath
Var DownloadHelperStdErrPath
Var DownloadLastLoggedPercent
Var DownloadHelperWaitSeconds

!insertmacro GetParameters
!insertmacro GetOptions
${StrStr}

!macro ResolveHeadLaunchPath _OUT _RELATIVE_ROOT _LAUNCH_EXECUTABLE
  !if "${_RELATIVE_ROOT}" == ""
    StrCpy ${_OUT} "$INSTDIR\${_LAUNCH_EXECUTABLE}"
  !else
    StrCpy ${_OUT} "$INSTDIR\${_RELATIVE_ROOT}\${_LAUNCH_EXECUTABLE}"
  !endif
!macroend

Name "${CHUMMER_DISPLAY_NAME}"
Caption "${CHUMMER_DISPLAY_NAME} Installer"
OutFile "${CHUMMER_OUTPUT_PATH}"
InstallDir "$LOCALAPPDATA\Programs\Chummer6\${CHUMMER_INSTALL_DIR_NAME}"
Icon "${CHUMMER_ICON_PATH}"

Page instfiles

Function TraceLine
  Exch $0
  ${If} $TraceHandle != ""
    FileWrite $TraceHandle "$0$\r$\n"
  ${EndIf}
  DetailPrint "$0"
FunctionEnd

Function ResetTrace
  Call EnsureBootstrapTempRoot
  StrCpy $0 "$BootstrapTempRoot\chummer-desktop-installer-progress.log"
  ClearErrors
  FileOpen $TraceHandle "$0" w
  ${If} ${Errors}
    StrCpy $TraceHandle ""
    Return
  ${EndIf}
  FileWrite $TraceHandle "# Chummer installer trace$\r$\n"
  Push "Bootstrap temp root: $BootstrapTempRoot"
  Call TraceLine
FunctionEnd

Function CloseTrace
  ${If} $TraceHandle != ""
    FileClose $TraceHandle
    StrCpy $TraceHandle ""
  ${EndIf}
FunctionEnd

Function UpdateInstFilesStatusText
  Exch $0
  GetDlgItem $1 $HWNDPARENT 1006
  ${If} $1 != ""
    SendMessage $1 ${WM_SETTEXT} 0 "STR:$0"
  ${EndIf}
  Pop $0
FunctionEnd

Function SetInstFilesProgressPosition
  Exch $0
  ${If} $0 == ""
    StrCpy $0 "0"
  ${EndIf}
  ${IfThen} $0 < 0 ${|} StrCpy $0 "0" ${|}
  ${IfThen} $0 > 100 ${|} StrCpy $0 "100" ${|}
  ; 0x3ec is the stock NSIS InstFiles page progress bar control ID.
  GetDlgItem $1 $HWNDPARENT 0x3ec
  ${If} $1 != ""
    SendMessage $1 ${PBM_SETRANGE32} 0 100
    SendMessage $1 ${PBM_SETPOS} $0 0
  ${EndIf}
  Pop $0
FunctionEnd

Function ReadFirstLineFromFileToR9
  Exch $0
  StrCpy $9 ""
  ClearErrors
  FileOpen $1 $0 r
  ${IfNot} ${Errors}
    FileRead $1 $9
    FileClose $1
    Push $9
    Call TrimLineEnding
    Pop $9
  ${EndIf}
  Pop $0
FunctionEnd

Function ReadFileSizeBytesToR9
  Exch $0
  StrCpy $9 "-1"
  ClearErrors
  FileOpen $1 $0 r
  ${IfNot} ${Errors}
    FileSeek $1 0 END $9
    FileClose $1
  ${EndIf}
  Pop $0
FunctionEnd

Function FormatKiBAsMiBStringToR9
  Exch $0
  IntOp $1 $0 / 1024
  IntOp $2 $0 % 1024
  IntOp $2 $2 * 10
  IntOp $2 $2 / 1024
  StrCpy $9 "$1.$2"
  Pop $0
FunctionEnd

Function NormalizePathToR9
  Exch $0
  StrCpy $9 ""
  ${If} $0 == ""
    Pop $0
    Return
  ${EndIf}
  GetFullPathName $1 "$0"
  ${If} $1 != ""
    StrCpy $9 $1
  ${Else}
    StrCpy $9 $0
  ${EndIf}
  Pop $0
FunctionEnd

Function TryUseBootstrapTempRootCandidate
  Exch $0
  StrCpy $9 ""
  ${If} $0 == ""
    Pop $0
    Return
  ${EndIf}
  Push "$0"
  Call NormalizePathToR9
  ${If} $9 == ""
    Pop $0
    Return
  ${EndIf}
  CreateDirectory "$9"
  ClearErrors
  FileOpen $2 "$9\bootstrap-root-probe.tmp" w
  ${IfNot} ${Errors}
    FileWrite $2 "ok$\r$\n"
    FileClose $2
    Delete "$9\bootstrap-root-probe.tmp"
  ${Else}
    StrCpy $9 ""
  ${EndIf}
  Pop $0
FunctionEnd

Function AbortInstallWithMessage
  Exch $0
  ${If} $IsSmokeInstall != "1"
    MessageBox MB_OK|MB_ICONSTOP "$0"
  ${EndIf}
  Abort
FunctionEnd

Function ParseCommandLine
  ${GetParameters} $CommandLine
  ${GetOptions} "$CommandLine" "/smoke-install=" $SmokeInstallPath
  ${GetOptions} "$CommandLine" "--payload-path" $PayloadPathOverride
  ${If} $PayloadPathOverride == ""
    ${GetOptions} "$CommandLine" "--payload" $PayloadPathOverride
  ${EndIf}
  ${If} $PayloadPathOverride == ""
    ReadEnvStr $PayloadPathOverride "CHUMMER_INSTALLER_PAYLOAD_PATH"
  ${EndIf}
  ${GetOptions} "$CommandLine" "--payload-url" $PayloadUrlOverride
  ${If} $PayloadUrlOverride == ""
    ReadEnvStr $PayloadUrlOverride "CHUMMER_INSTALLER_PAYLOAD_URL"
  ${EndIf}
  ${GetOptions} "$CommandLine" "--payload-sha256" $PayloadSha256Override
  ${If} $PayloadSha256Override == ""
    ReadEnvStr $PayloadSha256Override "CHUMMER_INSTALLER_PAYLOAD_SHA256"
  ${EndIf}
  ${GetOptions} "$CommandLine" "--payload-size-bytes" $PayloadSizeBytesOverride
  ${If} $PayloadSizeBytesOverride == ""
    ReadEnvStr $PayloadSizeBytesOverride "CHUMMER_INSTALLER_PAYLOAD_SIZE_BYTES"
  ${EndIf}
  ${GetOptions} "$CommandLine" "--install-claim-code" $ClaimCode
  ${GetOptions} "$CommandLine" "--launch-head" $LaunchHeadId
  ${GetOptions} "$CommandLine" "--relaunch-arg" $FirstRelaunchArg

  ${StrStr} $0 " $CommandLine " " --uninstall "
  ${If} $0 != ""
    StrCpy $UninstallRequested "1"
  ${EndIf}

  ${StrStr} $0 " $CommandLine " " --auto-update "
  ${If} $0 != ""
    StrCpy $AutoUpdateRequested "1"
  ${EndIf}

  ${If} $SmokeInstallPath != ""
    StrCpy $IsSmokeInstall "1"
    StrCpy $INSTDIR $SmokeInstallPath
    Push "Smoke install target: $SmokeInstallPath"
    Call TraceLine
    SetSilent silent
  ${EndIf}
FunctionEnd

Function TrimLineEnding
  Exch $0
  StrCpy $1 $0 1 -1
  ${If} $1 == "$\n"
    StrCpy $0 $0 -1
  ${EndIf}
  StrCpy $1 $0 1 -1
  ${If} $1 == "$\r"
    StrCpy $0 $0 -1
  ${EndIf}
  Exch $0
FunctionEnd

Function EnsureBootstrapTempRoot
  ${If} $BootstrapTempRoot != ""
    Return
  ${EndIf}

  ReadEnvStr $0 "TEMP"
  ${If} $0 == ""
    ReadEnvStr $0 "TMP"
  ${EndIf}
  ${If} $0 != ""
    CreateDirectory "$0\Chummer6"
    Push "$0\Chummer6\installer-temp"
    Call TryUseBootstrapTempRootCandidate
    ${If} $9 != ""
      StrCpy $BootstrapTempRoot $9
    ${EndIf}
  ${EndIf}

  ${If} $BootstrapTempRoot == ""
    InitPluginsDir
    Push "$PLUGINSDIR"
    Call TryUseBootstrapTempRootCandidate
    ${If} $9 != ""
      StrCpy $BootstrapTempRoot $9
    ${EndIf}
  ${EndIf}

  ${If} $BootstrapTempRoot == ""
    CreateDirectory "$LOCALAPPDATA\Chummer6"
    Push "$LOCALAPPDATA\Chummer6\installer-temp"
    Call TryUseBootstrapTempRootCandidate
    ${If} $9 != ""
      StrCpy $BootstrapTempRoot $9
    ${EndIf}
  ${EndIf}

  ${If} $BootstrapTempRoot == ""
    Push "Chummer could not create a temporary installer workspace."
    Call AbortInstallWithMessage
  ${EndIf}
FunctionEnd

Function EnsurePayloadMetadata
  ${If} $PayloadUrlOverride != ""
    StrCpy $EffectivePayloadUrl $PayloadUrlOverride
  ${Else}
    StrCpy $EffectivePayloadUrl "${CHUMMER_PAYLOAD_URL}"
  ${EndIf}

  ${If} $PayloadSha256Override != ""
    StrCpy $EffectivePayloadSha256 $PayloadSha256Override
  ${Else}
    StrCpy $EffectivePayloadSha256 "${CHUMMER_PAYLOAD_SHA256}"
  ${EndIf}

  ${If} $PayloadSizeBytesOverride != ""
    StrCpy $EffectivePayloadSizeBytes $PayloadSizeBytesOverride
  ${Else}
    StrCpy $EffectivePayloadSizeBytes "${CHUMMER_PAYLOAD_SIZE_BYTES}"
  ${EndIf}
FunctionEnd

Function TryDownloadPayloadWithCurl
  Call EnsureBootstrapTempRoot
  StrCpy $DownloadHelperStatus "1"
  StrCpy $DownloadHelperOutput ""

  ${IfNot} ${FileExists} "$BootstrapTempRoot\curl.exe"
  ${OrIfNot} ${FileExists} "$BootstrapTempRoot\libcurl-x64.dll"
  ${OrIfNot} ${FileExists} "$BootstrapTempRoot\curl-ca-bundle.crt"
    StrCpy $DownloadHelperOutput "bundled curl downloader is unavailable."
    Return
  ${EndIf}

  Delete "$EffectivePayloadPath"
  StrCpy $DownloadHelperPartialPath "$BootstrapTempRoot\${CHUMMER_PAYLOAD_FILE_NAME}.partial"
  StrCpy $DownloadHelperStartedPath "$BootstrapTempRoot\download-started.txt"
  StrCpy $DownloadHelperExitCodePath "$BootstrapTempRoot\download-exit-code.txt"
  StrCpy $DownloadHelperStdErrPath "$BootstrapTempRoot\download-curl-stderr.txt"
  Delete "$DownloadHelperPartialPath"
  Delete "$DownloadHelperStartedPath"
  Delete "$DownloadHelperExitCodePath"
  Delete "$DownloadHelperStdErrPath"
  Delete "$BootstrapTempRoot\download-curl-stdout.txt"
  Delete "$BootstrapTempRoot\chummer-download-payload.cmd"
  FileOpen $6 "$BootstrapTempRoot\chummer-download-payload.cmd" w
  FileWrite $6 "@echo off$\r$\n"
  FileWrite $6 "setlocal enableextensions$\r$\n"
  FileWrite $6 ">$\"$DownloadHelperStartedPath$\" echo started$\r$\n"
  FileWrite $6 "del /q $\"$DownloadHelperPartialPath$\" 2>nul$\r$\n"
  FileWrite $6 "del /q $\"$EffectivePayloadPath$\" 2>nul$\r$\n"
  FileWrite $6 "$\"$BootstrapTempRoot\curl.exe$\" --location --fail --silent --show-error --retry 5 --retry-delay 2 --connect-timeout 20 --cacert $\"$BootstrapTempRoot\curl-ca-bundle.crt$\" --output $\"$DownloadHelperPartialPath$\" $\"$EffectivePayloadUrl$\" 1>$\"$BootstrapTempRoot\download-curl-stdout.txt$\" 2>$\"$DownloadHelperStdErrPath$\"$\r$\n"
  FileWrite $6 "set $\"EXITCODE=%ERRORLEVEL%$\"$\r$\n"
  FileWrite $6 "if $\"%EXITCODE%$\"==$\"0$\" ($\r$\n"
  FileWrite $6 "  if exist $\"$DownloadHelperPartialPath$\" ($\r$\n"
  FileWrite $6 "    move /y $\"$DownloadHelperPartialPath$\" $\"$EffectivePayloadPath$\" >nul$\r$\n"
  FileWrite $6 "  ) else ($\r$\n"
  FileWrite $6 "    set $\"EXITCODE=1$\"$\r$\n"
  FileWrite $6 "    >$\"$DownloadHelperStdErrPath$\" echo bundled curl completed without creating the payload file.$\r$\n"
  FileWrite $6 "  )$\r$\n"
  FileWrite $6 ")$\r$\n"
  FileWrite $6 ">$\"$DownloadHelperExitCodePath$\" echo %EXITCODE%$\r$\n"
  FileWrite $6 "exit /b %EXITCODE%$\r$\n"
  FileClose $6
  StrCpy $6 "$BootstrapTempRoot\chummer-download-payload.cmd"
  GetFullPathName /SHORT $7 "$BootstrapTempRoot\chummer-download-payload.cmd"
  ${If} $7 != ""
    StrCpy $6 $7
  ${EndIf}
  nsExec::ExecToStack '"$SYSDIR\cmd.exe" /C start "" /B "$SYSDIR\cmd.exe" /C call $6'
  Pop $DownloadHelperStatus
  Pop $DownloadHelperOutput

  ${If} $DownloadHelperStatus != "0"
    Return
  ${EndIf}

  StrCpy $0 "0"
  StrCpy $1 "0"
  StrCpy $2 "0"
  StrCpy $DownloadLastLoggedPercent "0"
  StrCpy $DownloadHelperWaitSeconds "0"
  IntOp $8 $EffectivePayloadSizeBytes / 1024
  ${IfThen} $8 <= 0 ${|} StrCpy $8 "1" ${|}
  Push $8
  Call FormatKiBAsMiBStringToR9
  StrCpy $8 $9
  Push "Downloading application files - 0% - 0.0 / $8 MiB - preparing"
  Call UpdateInstFilesStatusText
  Push "0"
  Call SetInstFilesProgressPosition

download_poll:
  Sleep 1000
  IntOp $DownloadHelperWaitSeconds $DownloadHelperWaitSeconds + 1

  ${If} ${FileExists} "$DownloadHelperExitCodePath"
    Goto download_done
  ${EndIf}

  ${If} $DownloadHelperWaitSeconds >= 5
  ${AndIfNot} ${FileExists} "$DownloadHelperStartedPath"
    StrCpy $DownloadHelperStatus "1"
    StrCpy $DownloadHelperOutput "bundled curl downloader did not start."
    Return
  ${EndIf}

  ${If} $DownloadHelperWaitSeconds >= 1800
    StrCpy $DownloadHelperStatus "1"
    StrCpy $DownloadHelperOutput "bundled curl download timed out."
    Return
  ${EndIf}

  Push "$DownloadHelperPartialPath"
  Call ReadFileSizeBytesToR9
  StrCpy $3 $9
  ${If} $3 == "-1"
    StrCpy $3 "0"
  ${EndIf}

  IntOp $4 $3 / 1024
  IntOp $5 $EffectivePayloadSizeBytes / 1024
  ${IfThen} $5 <= 0 ${|} StrCpy $5 "1" ${|}
  IntOp $6 $4 * 100
  IntOp $6 $6 / $5
  ${IfThen} $6 > 100 ${|} StrCpy $6 "100" ${|}
  IntOp $7 $4 - $1
  ${IfThen} $7 < 0 ${|} StrCpy $7 "0" ${|}

  Push $4
  Call FormatKiBAsMiBStringToR9
  StrCpy $3 $9
  Push $5
  Call FormatKiBAsMiBStringToR9
  StrCpy $8 $9

  ${If} $7 >= 1024
    IntOp $9 $7 * 10
    IntOp $9 $9 / 1024
    IntOp $0 $9 / 10
    IntOp $9 $9 % 10
    StrCpy $2 "$0.$9 MiB/s"
  ${Else}
    StrCpy $2 "$7 KiB/s"
  ${EndIf}

  StrCpy $0 "Downloading application files - $6% - $3 / $8 MiB - $2"
  Push $0
  Call UpdateInstFilesStatusText
  IntOp $9 $6 * 86
  IntOp $9 $9 / 100
  Push $9
  Call SetInstFilesProgressPosition

  IntOp $9 $6 % 10
  ${If} $9 == 0
  ${AndIf} $6 != $DownloadLastLoggedPercent
    Push $0
    Call TraceLine
    StrCpy $DownloadLastLoggedPercent $6
  ${EndIf}

  StrCpy $1 $4
  Goto download_poll

download_done:
  Push "$DownloadHelperExitCodePath"
  Call ReadFirstLineFromFileToR9
  StrCpy $DownloadHelperStatus $9
  Push "$DownloadHelperStdErrPath"
  Call ReadFirstLineFromFileToR9
  StrCpy $DownloadHelperOutput $9

  ${If} $DownloadHelperStatus == "0"
  ${AndIfNot} ${FileExists} "$EffectivePayloadPath"
    StrCpy $DownloadHelperStatus "1"
    ${If} $DownloadHelperOutput == ""
      StrCpy $DownloadHelperOutput "bundled curl completed without creating the payload file."
    ${EndIf}
  ${EndIf}

  ${If} $DownloadHelperStatus == "0"
    Push "$EffectivePayloadPath"
    Call ReadFileSizeBytesToR9
    StrCpy $3 $9
    ${If} $3 == "-1"
      StrCpy $3 "0"
    ${EndIf}
    IntOp $4 $3 / 1024
    IntOp $5 $EffectivePayloadSizeBytes / 1024
    ${IfThen} $5 <= 0 ${|} StrCpy $5 "1" ${|}
    IntOp $7 $4 - $1
    ${IfThen} $7 < 0 ${|} StrCpy $7 "0" ${|}
    Push $4
    Call FormatKiBAsMiBStringToR9
    StrCpy $3 $9
    Push $5
    Call FormatKiBAsMiBStringToR9
    StrCpy $8 $9
    ${If} $7 >= 1024
      IntOp $9 $7 * 10
      IntOp $9 $9 / 1024
      IntOp $0 $9 / 10
      IntOp $9 $9 % 10
      StrCpy $2 "$0.$9 MiB/s"
    ${Else}
      StrCpy $2 "$7 KiB/s"
    ${EndIf}
    StrCpy $0 "Downloading application files - 100% - $3 / $8 MiB - $2"
    Push $0
    Call TraceLine
    Push $0
    Call UpdateInstFilesStatusText
    Push "86"
    Call SetInstFilesProgressPosition
  ${EndIf}
FunctionEnd

Function EnsurePayloadPath
  Call EnsurePayloadMetadata
  Call EnsureBootstrapTempRoot
  ${If} $PayloadPathOverride != ""
    ${If} ${FileExists} "$PayloadPathOverride"
      StrCpy $EffectivePayloadPath $PayloadPathOverride
      Push "Using local payload $EffectivePayloadPath"
      Call TraceLine
      Return
    ${EndIf}
    Push "Local payload handoff was missing, falling back to payload download metadata"
    Call TraceLine
    StrCpy $PayloadPathOverride ""
  ${EndIf}

  Push "$BootstrapTempRoot\${CHUMMER_PAYLOAD_FILE_NAME}"
  Call NormalizePathToR9
  ${If} $9 != ""
    StrCpy $EffectivePayloadPath $9
  ${Else}
    StrCpy $EffectivePayloadPath "$BootstrapTempRoot\${CHUMMER_PAYLOAD_FILE_NAME}"
  ${EndIf}
  ${StrStr} $0 "$EffectivePayloadPath" ":\"
  ${If} $0 == ""
    StrCpy $1 $EffectivePayloadPath 2
    ${If} $1 != "\\"
      Push "Chummer could not resolve a writable payload download target."
      Call AbortInstallWithMessage
    ${EndIf}
  ${EndIf}
  Delete "$EffectivePayloadPath"
  Push "Payload download target: $EffectivePayloadPath"
  Call TraceLine
  Push "Downloading application files"
  Call TraceLine

  Call TryDownloadPayloadWithCurl
  ${If} $DownloadHelperStatus == "0"
    Push "Payload download completed with bundled curl"
    Call TraceLine
    Return
  ${EndIf}

  ${If} $DownloadHelperOutput != ""
    Push "Bundled curl download failed code=$DownloadHelperStatus output=$DownloadHelperOutput"
    Call TraceLine
  ${Else}
    Push "Bundled curl download failed code=$DownloadHelperStatus"
    Call TraceLine
  ${EndIf}
  Push "Payload download failed; legacy NSIS downloader is disabled for bootstrap installs"
  Call TraceLine
  ${If} $DownloadHelperOutput != ""
    Push "Chummer could not download the application files.$\r$\n$\r$\n$DownloadHelperOutput"
  ${Else}
    Push "Chummer could not download the application files. Check your connection and try again."
  ${EndIf}
  Call AbortInstallWithMessage
FunctionEnd

Function VerifyPayloadSize
  Call EnsureBootstrapTempRoot
  ${If} $EffectivePayloadSizeBytes == ""
    Return
  ${EndIf}

  ${If} $IsSmokeInstall == "1"
  ${AndIf} $PayloadPathOverride != ""
    Push "Skipping payload size verification for local payload handoff"
    Call TraceLine
    Return
  ${EndIf}

  Push "Verifying payload size"
  Call TraceLine
  Push "88"
  Call SetInstFilesProgressPosition
  Push "Verifying payload size"
  Call UpdateInstFilesStatusText
  Delete "$BootstrapTempRoot\chummer-verify-size.cmd"
  FileOpen $6 "$BootstrapTempRoot\chummer-verify-size.cmd" w
  FileWrite $6 "@echo off$\r$\n"
  FileWrite $6 "for %%I in ($\"$EffectivePayloadPath$\") do @echo %%~zI$\r$\n"
  FileClose $6
  StrCpy $6 "$BootstrapTempRoot\chummer-verify-size.cmd"
  GetFullPathName /SHORT $7 "$BootstrapTempRoot\chummer-verify-size.cmd"
  ${If} $7 != ""
    StrCpy $6 $7
  ${EndIf}
  nsExec::ExecToStack '"$SYSDIR\cmd.exe" /C call $6'
  Pop $0
  Pop $1
  ${If} $0 != "0"
    Push "Payload size command failed code=$0 output=$1 path=$EffectivePayloadPath"
    Call TraceLine
    Push "Chummer could not verify the payload size."
    Call AbortInstallWithMessage
  ${EndIf}
  Push $1
  Call TrimLineEnding
  Pop $1
  ${If} $1 == ""
    Push "Payload size output was empty"
    Call TraceLine
    Push "Chummer could not read the payload size."
    Call AbortInstallWithMessage
  ${EndIf}

  ${If} $1 != $EffectivePayloadSizeBytes
    Push "Payload size mismatch expected=$EffectivePayloadSizeBytes actual=$1"
    Call TraceLine
    Push "The downloaded Chummer payload has the wrong size."
    Call AbortInstallWithMessage
  ${EndIf}
FunctionEnd

Function VerifyPayloadSha256
  Call EnsureBootstrapTempRoot
  ${If} $EffectivePayloadSha256 == ""
    Return
  ${EndIf}

  ${If} $IsSmokeInstall == "1"
  ${AndIf} $PayloadPathOverride != ""
    Push "Skipping payload checksum verification for smoke local payload handoff"
    Call TraceLine
    Return
  ${EndIf}

  Push "Verifying payload checksum"
  Call TraceLine
  Push "93"
  Call SetInstFilesProgressPosition
  Push "Verifying payload checksum"
  Call UpdateInstFilesStatusText
  Delete "$BootstrapTempRoot\payload-hash.txt"
  Delete "$BootstrapTempRoot\chummer-verify-payload.cmd"
  FileOpen $6 "$BootstrapTempRoot\chummer-verify-payload.cmd" w
  FileWrite $6 "@echo off$\r$\n"
  FileWrite $6 "cd /d %~dp0$\r$\n"
  FileWrite $6 "7za.exe h -scrcSHA256 $\"$EffectivePayloadPath$\" > payload-hash.txt$\r$\n"
  FileClose $6
  StrCpy $6 "$BootstrapTempRoot\chummer-verify-payload.cmd"
  GetFullPathName /SHORT $7 "$BootstrapTempRoot\chummer-verify-payload.cmd"
  ${If} $7 != ""
    StrCpy $6 $7
  ${EndIf}
  nsExec::ExecToStack '"$SYSDIR\cmd.exe" /C call $6'
  Pop $0
  Pop $1
  ${If} $0 != "0"
    Push "Payload checksum command failed code=$0 output=$1"
    Call TraceLine
    Push "Chummer could not verify the payload checksum."
    Call AbortInstallWithMessage
  ${EndIf}

  StrCpy $2 ""
  FileOpen $3 "$BootstrapTempRoot\payload-hash.txt" r
  ${Do}
    ClearErrors
    FileRead $3 $4
    ${IfThen} ${Errors} ${|} ${ExitDo} ${|}
    ${StrStr} $5 $4 "SHA256 for data:"
    ${If} $5 != ""
      Push $4
      Call TrimLineEnding
      Pop $4
      StrCpy $2 $4 64 -64
      ${ExitDo}
    ${EndIf}
  ${Loop}
  FileClose $3

  ${If} $2 == ""
    Push "Payload checksum output did not contain a SHA256 line"
    Call TraceLine
    Push "Chummer could not read the payload checksum."
    Call AbortInstallWithMessage
  ${EndIf}

  ${If} $2 != $EffectivePayloadSha256
    Push "Payload checksum mismatch expected=$EffectivePayloadSha256 actual=$2"
    Call TraceLine
    Push "The downloaded Chummer payload failed checksum verification."
    Call AbortInstallWithMessage
  ${EndIf}
FunctionEnd

Function RemoveInstallDirectory
  ${If} ${FileExists} "$INSTDIR\*.*"
    RMDir /r "$INSTDIR"
  ${EndIf}
  CreateDirectory "$INSTDIR"
FunctionEnd

Function ExtractPayload
  Call EnsureBootstrapTempRoot
  Push "Extracting application files"
  Call TraceLine
  Push "97"
  Call SetInstFilesProgressPosition
  Push "Extracting application files"
  Call UpdateInstFilesStatusText
  Delete "$BootstrapTempRoot\chummer-extract-payload.cmd"
  FileOpen $6 "$BootstrapTempRoot\chummer-extract-payload.cmd" w
  FileWrite $6 "@echo off$\r$\n"
  FileWrite $6 "cd /d %~dp0$\r$\n"
  FileWrite $6 "7za.exe x -y $\"-o$INSTDIR$\" $\"$EffectivePayloadPath$\"$\r$\n"
  FileClose $6
  StrCpy $6 "$BootstrapTempRoot\chummer-extract-payload.cmd"
  GetFullPathName /SHORT $7 "$BootstrapTempRoot\chummer-extract-payload.cmd"
  ${If} $7 != ""
    StrCpy $6 $7
  ${EndIf}
  nsExec::ExecToStack '"$SYSDIR\cmd.exe" /C call $6'
  Pop $0
  Pop $1
  ${If} $0 != "0"
    Push "Payload extraction failed code=$0 output=$1"
    Call TraceLine
    Push "Chummer could not unpack the application files."
    Call AbortInstallWithMessage
  ${EndIf}
FunctionEnd

Function CopyInstallerForUninstall
  CopyFiles /SILENT "$EXEPATH" "$INSTDIR\${CHUMMER_INSTALLER_OUTPUT_NAME}.exe"
FunctionEnd

Function WritePendingClaimCodeForHead
  Exch $0
  Push $1
  Push $2
  ${If} $ClaimCode == ""
    Goto claim_done
  ${EndIf}
  CreateDirectory "$LOCALAPPDATA\Chummer6\install-linking\$0\windows\${CHUMMER_ARCH}"
  FileOpen $2 "$LOCALAPPDATA\Chummer6\install-linking\$0\windows\${CHUMMER_ARCH}\pending-claim-code.txt" w
  FileWrite $2 "$ClaimCode"
  FileClose $2
claim_done:
  Pop $2
  Pop $1
  Pop $0
FunctionEnd

Function RegisterUninstallEntry
  !insertmacro ResolveHeadLaunchPath $0 "${CHUMMER_HEAD_1_RELATIVE_ROOT}" "${CHUMMER_HEAD_1_LAUNCH_EXECUTABLE}"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Chummer6.${CHUMMER_APP_ID}" "DisplayName" "${CHUMMER_DISPLAY_NAME}"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Chummer6.${CHUMMER_APP_ID}" "DisplayVersion" "${CHUMMER_VERSION}"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Chummer6.${CHUMMER_APP_ID}" "Publisher" "${CHUMMER_PUBLISHER}"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Chummer6.${CHUMMER_APP_ID}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Chummer6.${CHUMMER_APP_ID}" "DisplayIcon" "$0"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Chummer6.${CHUMMER_APP_ID}" "UninstallString" '$\"$INSTDIR\${CHUMMER_INSTALLER_OUTPUT_NAME}.exe$\" --uninstall'
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Chummer6.${CHUMMER_APP_ID}" "QuietUninstallString" '$\"$INSTDIR\${CHUMMER_INSTALLER_OUTPUT_NAME}.exe$\" --uninstall'
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Chummer6.${CHUMMER_APP_ID}" "NoModify" 1
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Chummer6.${CHUMMER_APP_ID}" "NoRepair" 1
FunctionEnd

Function RegisterProtocol
  !insertmacro ResolveHeadLaunchPath $0 "${CHUMMER_HEAD_1_RELATIVE_ROOT}" "${CHUMMER_HEAD_1_LAUNCH_EXECUTABLE}"
  WriteRegStr HKCU "Software\Classes\chummer" "" "URL: Chummer Protocol"
  WriteRegStr HKCU "Software\Classes\chummer" "URL Protocol" ""
  WriteRegStr HKCU "Software\Classes\chummer\DefaultIcon" "" "$0"
  WriteRegStr HKCU "Software\Classes\chummer\shell\open\command" "" '$\"$0$\" --install-link-callback $\"%1$\"'
FunctionEnd

Function LaunchInstalledHead
  StrCpy $0 "$LaunchHeadId"
  ${If} $0 == ""
    StrCpy $0 "${CHUMMER_HEAD_1_ID}"
  ${EndIf}

  !if "${CHUMMER_HEAD_COUNT}" == "2"
    ${If} $0 == "${CHUMMER_HEAD_2_ID}"
      !insertmacro ResolveHeadLaunchPath $1 "${CHUMMER_HEAD_2_RELATIVE_ROOT}" "${CHUMMER_HEAD_2_LAUNCH_EXECUTABLE}"
      Goto launch_head_done
    ${EndIf}
  !endif

  !insertmacro ResolveHeadLaunchPath $1 "${CHUMMER_HEAD_1_RELATIVE_ROOT}" "${CHUMMER_HEAD_1_LAUNCH_EXECUTABLE}"
launch_head_done:
  StrCpy $2 ""
  ${If} $ClaimCode != ""
    StrCpy $2 '$2 --install-claim-code "$ClaimCode"'
  ${EndIf}
  ${If} $FirstRelaunchArg != ""
    StrCpy $2 '$2 "$FirstRelaunchArg"'
  ${EndIf}
  Exec '"$1"$2'
FunctionEnd

Function RemoveShellEntries
  Delete "$SMPROGRAMS\${CHUMMER_HEAD_1_SHORTCUT_NAME}.lnk"
  Delete "$DESKTOP\${CHUMMER_HEAD_1_SHORTCUT_NAME}.lnk"
  !if "${CHUMMER_HEAD_COUNT}" == "2"
    Delete "$SMPROGRAMS\${CHUMMER_HEAD_2_SHORTCUT_NAME}.lnk"
    Delete "$DESKTOP\${CHUMMER_HEAD_2_SHORTCUT_NAME}.lnk"
  !endif
  DeleteRegKey HKCU "Software\Classes\chummer"
  DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Chummer6.${CHUMMER_APP_ID}"
FunctionEnd

Function DeleteInstalledFiles
  RMDir /r "$INSTDIR"
  !if "${CHUMMER_HEAD_COUNT}" == "2"
    ${If} "${CHUMMER_RID_SUFFIX}" != ""
      RMDir /r "$LOCALAPPDATA\Programs\Chummer6\AvaloniaDesktop-${CHUMMER_RID_SUFFIX}"
      RMDir /r "$LOCALAPPDATA\Programs\Chummer6\BlazorDesktop-${CHUMMER_RID_SUFFIX}"
    ${EndIf}
  !endif
FunctionEnd

Function .onInit
  Call EnsureBootstrapTempRoot
  Call ResetTrace
  Call ParseCommandLine
FunctionEnd

Section "Install"
  SetShellVarContext current
  Call EnsureBootstrapTempRoot
  SetOutPath "$BootstrapTempRoot"
  File /oname=7za.exe "${CHUMMER_STAGE_DIR}/7zip/7za.exe"
  File /oname=7za.dll "${CHUMMER_STAGE_DIR}/7zip/7za.dll"
  File /oname=7zxa.dll "${CHUMMER_STAGE_DIR}/7zip/7zxa.dll"
  File /oname=curl.exe "${CHUMMER_STAGE_DIR}/curl/curl.exe"
  File /oname=libcurl-x64.dll "${CHUMMER_STAGE_DIR}/curl/libcurl-x64.dll"
  File /oname=curl-ca-bundle.crt "${CHUMMER_STAGE_DIR}/curl/curl-ca-bundle.crt"
  SetOutPath "$BootstrapTempRoot"

  ${If} $UninstallRequested == "1"
    Push "Removing Chummer"
    Call TraceLine
    Call RemoveShellEntries
    Call DeleteInstalledFiles
    Call CloseTrace
    Quit
  ${EndIf}

  Call EnsurePayloadPath
  Call VerifyPayloadSize
  Call VerifyPayloadSha256
  Call RemoveInstallDirectory
  Call ExtractPayload

  ${If} $IsSmokeInstall != "1"
    Call CopyInstallerForUninstall
    Push "${CHUMMER_HEAD_1_ID}"
    Call WritePendingClaimCodeForHead
    !insertmacro ResolveHeadLaunchPath $0 "${CHUMMER_HEAD_1_RELATIVE_ROOT}" "${CHUMMER_HEAD_1_LAUNCH_EXECUTABLE}"
    CreateShortcut "$SMPROGRAMS\${CHUMMER_HEAD_1_SHORTCUT_NAME}.lnk" "$0" "" "$0" 0
    CreateShortcut "$DESKTOP\${CHUMMER_HEAD_1_SHORTCUT_NAME}.lnk" "$0" "" "$0" 0
    !if "${CHUMMER_HEAD_COUNT}" == "2"
      Push "${CHUMMER_HEAD_2_ID}"
      Call WritePendingClaimCodeForHead
      !insertmacro ResolveHeadLaunchPath $0 "${CHUMMER_HEAD_2_RELATIVE_ROOT}" "${CHUMMER_HEAD_2_LAUNCH_EXECUTABLE}"
      CreateShortcut "$SMPROGRAMS\${CHUMMER_HEAD_2_SHORTCUT_NAME}.lnk" "$0" "" "$0" 0
      CreateShortcut "$DESKTOP\${CHUMMER_HEAD_2_SHORTCUT_NAME}.lnk" "$0" "" "$0" 0
    !endif
    Call RegisterUninstallEntry
    Call RegisterProtocol
  ${EndIf}

  Push "Install complete"
  Call TraceLine
  Push "100"
  Call SetInstFilesProgressPosition
  Push "Install complete"
  Call UpdateInstFilesStatusText

  ${If} $AutoUpdateRequested == "1"
    Call LaunchInstalledHead
  ${ElseIf} $IsSmokeInstall != "1"
    MessageBox MB_YESNO|MB_ICONQUESTION "Open Chummer now?" IDYES launch_now IDNO done
launch_now:
    Call LaunchInstalledHead
  ${EndIf}

done:
  Call CloseTrace
SectionEnd
