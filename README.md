## ADFS MFA Adapters


### Description

This solution contains Custom Authentication Providers for ADFS. They are tested against ADFS 2016.

## Register MFA Adapter ##
Copy the dll to your ADFS server.
Run the following script to install a custom adapter (using "dummy names and paths"):
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

## Unregister MFA Adapter ##
```
Unregister-AdfsAuthenticationProvider -Name "MFA Adapter"
net stop adfssrv
net start adfssrv
.\gacutil.exe /u "MFAAdapter, Version=1.0.0.0, Culture=neutral, PublicKeyToken=xyz, processorArchitecture=MSIL"

```
