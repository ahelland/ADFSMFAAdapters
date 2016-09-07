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