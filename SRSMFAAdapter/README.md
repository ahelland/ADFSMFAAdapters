## ADFS MFA Adapters - Spaced Repetion System (SRS)


### Description

This solution contains an authentication adapter for an SRS implemented in Azure Functions.

This is intended as a Proof-of-Concept of how to build a basic MFA adapter calling into an external system, and use this in the user interface of the login experience.

The way this POC MFA Adapter works is by emulating the same learning experience as using flash cards to memorize things. The user is given a word, and is expected to know the answer. So for instance the authentication prompt will say "Hello", and the user should respond "World". If the user gets the answer wrong they will be shown the correct response, and have to type in that to proceed.

There isn't an actual SRS algorithm implemented in the function so it would not actually work that way in real life, (not to mention the fact that it would only occur during authentications which are hard to time/predict). And it clearly is not an added security layer.

The corresponding Azure Function:  
[https://gist.github.com/ahelland/76d44ca7619b5a06e730f4488ab0e20f](https://gist.github.com/ahelland/76d44ca7619b5a06e730f4488ab0e20f)

The URL of the Azure Function should be pasted into the SRSMFAAdapter.json file.

Don't go implementing this in a production system, but use it as a basis for testing/learning/playing around with MFA Adapters and/or Azure Functions :)

#### Powershell for handling the SRS MFA Adapter ####
```
// Install
Set-Location "C:\install"

[System.Reflection.Assembly]::Load("System.EnterpriseServices, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")
$publish = New-Object System.EnterpriseServices.Internal.Publish

$publish.GacInstall("C:\install\SRSMFAAdapter\SRSMFAAdapter.dll")

$fn = ([System.Reflection.Assembly]::LoadFile("C:\install\SRSMFAAdapter\SRSMFAAdapter.dll")).FullName

$typeName = "MFAadapter.SRSMFAAdapter, " + $fn.ToString() + ", processorArchitecture=MSIL"
Register-AdfsAuthenticationProvider -TypeName $typeName -Name "SRS MFA Adapter" -ConfigurationFilePath 'C:\install\SRSMFAAdapter\SRSMFAAdapter.json'
net stop adfssrv
net start adfssrv

// Uninstall
Unregister-AdfsAuthenticationProvider -Name "SRS MFA Adapter"
net stop adfssrv
net start adfssrv
$publish.GacRemove("C:\install\SRSMFAAdapter\SRSMFAAdapter.dll")
```