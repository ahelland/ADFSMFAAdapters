## ADFS MFA Adapters


### Description

This solution contains Custom Authentication Providers for ADFS. They are tested against ADFS 2016.  

They should work with Windows Server 2012 R2 as well, but the Microsoft.IdentityServer.Web.dll files in this repo will not work! They are from Windows Server 2016, so they will confuse your server if your operating system is at a lower build. You can find your build specific files in the _C:\Windows\ADFS_ directory of your ADFS Server.

## Register MFA Adapter ##
Copy the dlls to your ADFS server.
Run the following scripts below to install a custom adapter (using "dummy names and paths"). These are the generic approach for performing the tasks; the adapter specific scripts are in their respective readme files.

#### Gacutil Approach ####
```
.\gacutil.exe /if 'C:\install\MFA Adapter\MFAAdapter.dll'
.\gacutil.exe /l MFAAdapter

# You need the PublicKeyToken acquired by running the commands above
$typeName = "ADFSMFAdapters.MFAAdapter, MFAMFAAdapter, Version=1.0.0.0, Culture=neutral, PublicKeyToken=xyz, processorArchitecture=MSIL"
Register-AdfsAuthenticationProvider -TypeName $typeName -Name "MFA Adapter" -ConfigurationFilePath 'C:\install\MFA Adapter\MFAAdapter.json'
net stop adfssrv
net start adfssrv

Get-AdfsAuthenticationProvider
```

#### Powershell Approach ####
```
Set-Location "C:\install"
[System.Reflection.Assembly]::Load("System.EnterpriseServices, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")
$publish = New-Object System.EnterpriseServices.Internal.Publish

// Install Adapter into GAC
$publish.GacInstall("C:\install\MFA Adapter\MFAAdapter.dll")

// Get the necessary info for the next cmdlet
([System.Reflection.Assembly]::LoadFile("C:\install\MFA Adapter\MFAAdapter.dll")).FullName

$typeName = "MFAAdapter.MFAAdapter, MFAAdapter, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=xyz, processorArchitecture=MSIL"
Register-AdfsAuthenticationProvider -TypeName $typeName -Name "MFA Adapter" -ConfigurationFilePath 'C:\install\MFA Adapter\MFAAdapter.json'
net stop adfssrv
net start adfssrv
```

## Unregister MFA Adapter ##
#### Gacutil Approach ####
```
Unregister-AdfsAuthenticationProvider -Name "MFA Adapter"
net stop adfssrv
net start adfssrv
.\gacutil.exe /u "MFAAdapter, Version=1.0.0.0, Culture=neutral, PublicKeyToken=xyz, processorArchitecture=MSIL"

```

#### Powershell Approach ####
```
Unregister-AdfsAuthenticationProvider -Name "MFA Adapter"
net stop adfssrv
net start adfssrv

// Remove Adapter from GAC
$publish.GacRemove("C:\install\MFA Adapter\MFAAdapter.dll")
```