using Microsoft.IdentityServer.Web.Authentication.External;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.DirectoryServices;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using Claim = System.Security.Claims.Claim;

namespace ADFSMFAAdapters
{
    class YubiKeyMFAAdapter : IAuthenticationAdapter
    {
        //For identifying the AD user
        private static string upn = string.Empty;

        //Config section variables
        private static string authId = string.Empty;
        private static string apiKey = string.Empty;
        private static string server = string.Empty;

        public IAuthenticationAdapterMetadata Metadata
        {
            get
            {
                return new YubiKeyMFAMetadata();
            }
        }

        public IAdapterPresentation BeginAuthentication(Claim identityClaim, HttpListenerRequest request, IAuthenticationContext context)
        {
            //AD user should be present in the incoming claim as user@domain.com
            upn = identityClaim.Value;
            return new YubiKeyMFAPresentationForm();
        }

        public bool IsAvailableForUser(Claim identityClaim, IAuthenticationContext context)
        {
            return true;
        }

        public void OnAuthenticationPipelineLoad(IAuthenticationMethodConfigData configData)
        {
            if (configData != null)
            {
                if (configData.Data != null)
                {
                    using (StreamReader reader = new StreamReader(configData.Data, Encoding.UTF8))
                    {
                        //Config should be in a json format, and needs to be registered with the 
                        //-ConfigurationFilePath parameter when registering the MFA Adapter (Register-AdfsAuthenticationProvider cmdlet)
                        try
                        {
                            var config = reader.ReadToEnd();
                            var js = new DataContractJsonSerializer(typeof(YubiKeyMFAConf));
                            var ms = new MemoryStream(UTF8Encoding.UTF8.GetBytes(config));
                            var mfaConfig = (YubiKeyMFAConf)js.ReadObject(ms);

                            authId = mfaConfig.authId;
                            apiKey = mfaConfig.apiKey;
                            server = mfaConfig.server;

                            EventLog.WriteEntry("Application", "Config loaded with following authId: " + authId, EventLogEntryType.Information);
                        }
                        catch
                        {
                            EventLog.WriteEntry("Application", "Unable to load config data. Check that it is registered and correct.", EventLogEntryType.Information);
                            throw new ArgumentException();
                        }
                    }
                }
            }
            else
            {
                throw new ArgumentNullException();
            }
        }

        public void OnAuthenticationPipelineUnload()
        {
            //throw new NotImplementedException();
        }

        public IAdapterPresentation OnError(HttpListenerRequest request, ExternalAuthenticationException ex)
        {
            return new YubiKeyMFAPresentationForm(); ;
        }

        public IAdapterPresentation TryEndAuthentication(IAuthenticationContext context, IProofData proofData, HttpListenerRequest request, out Claim[] claims)
        {
            claims = new Claim[0];

            if (ValidateProofData(proofData, context))
            {
                //authn complete - return authn method
                claims = new[]
                {
                    new Claim( "http://schemas.microsoft.com/ws/2008/06/identity/claims/authenticationmethod",
                    "http://schemas.microsoft.com/ws/2008/06/identity/authenticationmethod/hardwaretoken" ) };

                return null;
            }
            else
            {
                return new YubiKeyMFAPresentationForm();
            }
        }

        static bool ValidateProofData(IProofData proofData, IAuthenticationContext authContext)
        {
            if (proofData == null || proofData.Properties == null || !proofData.Properties.ContainsKey("OTP"))
            {
                throw new ExternalAuthenticationException("Error - please input the OTP", authContext);
            }

            var otp = (string)proofData.Properties["OTP"];

            //Retrieve the YubiKey id stored in the user's AD object
            //Can be set with ADSI Edit or ADUC (Advanced view)
            DirectoryEntry entry = new DirectoryEntry();
            DirectorySearcher mySearcher = new DirectorySearcher(entry, "(&(objectClass=user)(objectCategory=person)(userPrincipalName=" + upn + "))");
            SearchResult dirResult = mySearcher.FindOne();
            string yubikeyId = (string)dirResult.Properties["extensionAttribute10"][0];

            var nonce = GenerateNonce(20);
            //First 12 digits/letters of OTP is the id for the YubiKey
            var userId = otp.Substring(0, 12);

            EventLog.WriteEntry("Application", "UPN: " + upn + " YubiId: " + yubikeyId, EventLogEntryType.Information);
            //Verify the user id portion of the YubiKey matches what's stored in AD
            //The reason for the 8-character substring below is prefixing the actual id with "YubiKey:", so this can be changed as desired.
            if (userId != yubikeyId.Substring(8))
            {
                EventLog.WriteEntry("Application", "YubiKey lookup in AD failed for UPN: " + upn, EventLogEntryType.Information);
                return false;
            }

            byte[] hmacKey = Convert.FromBase64String(apiKey);
            //Parameters need to be in alphabetical order to generate a valid signature
            //hmac can contain characters needing urlencoding - note the returned hmac is not urlencoded
            var hmac = HttpUtility.UrlEncode(GenerateSignature($"id={authId}&nonce={nonce}&otp={otp}", hmacKey));            
            var queryString = $"?id={authId}&otp={otp}&nonce={nonce}&h={hmac}";
            var url = $"{server}{queryString}";            

            HttpClient client = new HttpClient();
            var response = client.GetAsync(new Uri(url)).Result;
            string content = response.Content.ReadAsStringAsync().Result;

            string[] separators = new string[] { "\r\n" };
            string[] result = content.Split(separators, StringSplitOptions.RemoveEmptyEntries);            

            string outNonce = string.Empty;
            string outHmac = string.Empty;
            string outStatus = string.Empty;
            string outTimeStamp = string.Empty;
            string outSl = string.Empty;
            string outOtp = string.Empty;

            foreach (string x in result)
            {
                if (x.StartsWith("h="))
                {
                    outHmac = x.Substring(2);
                }
                if (x.StartsWith("t="))
                {
                    outTimeStamp = x.Substring(2);
                }
                if (x.StartsWith("status="))
                {
                    outStatus = x.Substring(7);
                }
                if (x.StartsWith("nonce="))
                {
                    outNonce = x.Substring(6);
                }
                if (x.StartsWith("sl="))
                {
                    outSl = x.Substring(3);
                }
                if (x.StartsWith("otp="))
                {
                    outOtp = x.Substring(4);
                }
            }

            //Need the response parameters in alphabetical order for verifying signature
            var responseList = new SortedDictionary<string, string>
            {
                {"t", outTimeStamp },
                {"nonce", outNonce },
                {"sl", outSl },
                {"status", outStatus },
                {"otp", outOtp }
            };

            StringBuilder queryBuilder = null;
            foreach (var pair in responseList)
            {
                if (queryBuilder == null)
                {
                    queryBuilder = new StringBuilder();
                }
                else
                {
                    queryBuilder.Append("&");
                }
                queryBuilder.AppendFormat("{0}={1}", pair.Key, pair.Value);
            }

            var serverSignature = queryBuilder.ToString();           
            var signatureCheck = GenerateSignature(serverSignature, hmacKey);            

            if (outNonce != nonce)
            {
                //Nonce mismatch
                EventLog.WriteEntry("Application", "YubiKey nonce mismatch for UPN: " + upn, EventLogEntryType.Information);
                return false;
            }
            if (outHmac != signatureCheck)
            {
                //Signature mismatch (server sent a different signature than expected)
                EventLog.WriteEntry("Application", "YubiKey signature mismatch for UPN: " + upn, EventLogEntryType.Information);
                return false;
            }

            if (outStatus == "OK")
            {
                //All is good
                EventLog.WriteEntry("Application", "YubiKey OK for UPN: " + upn, EventLogEntryType.Information);
                return true;
            }
            if (outStatus == "BAD_OTP")
            {
                //OTP not valid
                EventLog.WriteEntry("Application", "YubiKey BAD OTP for UPN: " + upn, EventLogEntryType.Information);
                return false;
            }
            if (outStatus == "REPLAYED_OTP")
            {
                //OTP has already been used
                EventLog.WriteEntry("Application", "YubiKey REPLAYED OTP for UPN: " + upn, EventLogEntryType.Information);
                return false;
            }
            if (outStatus == "BAD_SIGNATURE")
            {
                //Signature was incorrect
                EventLog.WriteEntry("Application", "YubiKey BAD SIGNATURE for UPN: " + upn, EventLogEntryType.Information);
                return false;
            }
            if (outStatus == "MISSING_PARAMETER")
            {
                //Something missing
                EventLog.WriteEntry("Application", "YubiKey MISSING PARAMETER for UPN: " + upn, EventLogEntryType.Information);
                return false;
            }
            if (outStatus == "NO_SUCH_CLIENT")
            {
                //Incorrect client
                EventLog.WriteEntry("Application", "YubiKey NO SUCH CLIENT for UPN: " + upn, EventLogEntryType.Information);
                return false;
            }
            if (outStatus == "OPERATION_NOT_ALLOWED")
            {
                EventLog.WriteEntry("Application", "YubiKey OPERATION NOT ALLOWED for UPN: " + upn, EventLogEntryType.Information);
                return false;
            }
            if (outStatus == "BACKEND_ERROR")
            {
                //Something wrong server side (Yubico)
                EventLog.WriteEntry("Application", "YubiKey BACKEND ERROR for UPN: " + upn, EventLogEntryType.Information);
                return false;
            }
            if (outStatus == "NOT_ENOUGH_ANSWERS")
            {
                EventLog.WriteEntry("Application", "YubiKey NOT ENOUGH ANSWERS for UPN: " + upn, EventLogEntryType.Information);
                return false;
            }
            if (outStatus == "REPLAYED_REQUEST")
            {
                EventLog.WriteEntry("Application", "YubiKey REPLAYED REQUEST for UPN: " + upn, EventLogEntryType.Information);
                return false;
            }

            return false;
        }

        //Helper method for generating a nonce
        private static string GenerateNonce(int length)
        {
            using (var random = new RNGCryptoServiceProvider())
            {
                var nonce = new byte[length];
                random.GetBytes(nonce);
                return BitConverter.ToString(nonce).Replace("-", "");
            }
        }

        //Helper method for generating signature for the OTP validation
        private static string GenerateSignature(string input, byte[] key)
        {
            using (var hmac = new HMACSHA1(key))
            {
                var signature = hmac.ComputeHash(Encoding.ASCII.GetBytes(input));
                return Convert.ToBase64String(signature);
            }
        }

    }

    class YubiKeyMFAMetadata : IAuthenticationAdapterMetadata
    {
        public string AdminName
        {
            get
            {
                return "YubiKey MFA Adapter";
            }
        }

        public string[] AuthenticationMethods
        {
            get { return new[] { "http://schemas.microsoft.com/ws/2008/06/identity/authenticationmethod/hardwaretoken" }; }
        }

        public int[] AvailableLcids
        {
            get
            {
                return new[] { new CultureInfo("en-us").LCID };
            }
        }

        public Dictionary<int, string> Descriptions
        {
            get
            {
                Dictionary<int, string> _descriptions = new Dictionary<int, string>();
                _descriptions.Add(new CultureInfo("en-us").LCID, "YubiKey Authentication");
                return _descriptions;
            }
        }

        public Dictionary<int, string> FriendlyNames
        {
            get
            {
                Dictionary<int, string> _friendlyNames = new Dictionary<int, string>();
                _friendlyNames.Add(new CultureInfo("en-us").LCID, "YubiKey Authentication");
                return _friendlyNames;
            }
        }

        public string[] IdentityClaims
        {
            get
            {
                return new[] { "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/upn" };
            }
        }

        public bool RequiresIdentity
        {
            get
            {
                return true;
            }
        }
    }

    class YubiKeyMFAPresentationForm : IAdapterPresentationForm
    {
        public string GetFormHtml(int lcid)
        {
            string htmlTemplate = Resources.LoginHtml;
            return htmlTemplate;
        }

        public string GetFormPreRenderHtml(int lcid)
        {
            return null;
        }

        public string GetPageTitle(int lcid)
        {
            return "YubiKey Authentication";
        }
    }

    //Helper class for deserializing configuration data (located in external json-formatted file)
    [DataContract]
    class YubiKeyMFAConf
    {
        [DataMember]
        public string authId = string.Empty;
        [DataMember]
        public string apiKey = string.Empty;
        [DataMember]
        public string server = string.Empty;
    }
}
