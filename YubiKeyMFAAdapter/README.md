## ADFS MFA Adapters - YubiKey


### Description

This solution contains an authentication adapter for use with YubiKeys.

Reference for the validation protocol: [https://developers.yubico.com/yubikey-val/Validation_Protocol_V2.0.html](https://developers.yubico.com/yubikey-val/Validation_Protocol_V2.0.html)  
Sample code from YubiCo: [https://github.com/Yubico/yubico-dotnet-client/blob/master/YubicoDotNetClient/YubicoClient.cs](https://github.com/Yubico/yubico-dotnet-client/blob/master/YubicoDotNetClient/YubicoClient.cs)  

#### Powershell for handling the YubiKey MFA Adapter ####
```
// Install
Set-Location "C:\install"

[System.Reflection.Assembly]::Load("System.EnterpriseServices, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")
$publish = New-Object System.EnterpriseServices.Internal.Publish

$publish.GacInstall("C:\install\YubiKeyMFAAdapter\YubiKeyMFAAdapter.dll")

$fn = ([System.Reflection.Assembly]::LoadFile("C:\install\YubiKeyMFAAdapter\YubiKeyMFAAdapter.dll")).FullName

$typeName = "ADFSMFAAdapters.YubiKeyMFAAdapter, " + $fn.ToString() + ", processorArchitecture=MSIL"
Register-AdfsAuthenticationProvider -TypeName $typeName -Name "YubiKey MFA Adapter" -ConfigurationFilePath 'C:\install\YubiKeyMFAAdapter\YubiKeyMFAAdapter.json'
net stop adfssrv
net start adfssrv

// Uninstall
Unregister-AdfsAuthenticationProvider -Name "YubiKey MFA Adapter"
net stop adfssrv
net start adfssrv
$publish.GacRemove("C:\install\YubiKeyMFAAdapter\YubiKeyMFAAdapter.dll")
```
