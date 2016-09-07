using Microsoft.IdentityServer.Web.Authentication.External;
using SRSMFAAdapter;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using Claim = System.Security.Claims.Claim;

namespace MFAadapter
{
    class SRSMFAAdapter : IAuthenticationAdapter
    {
        //Config section variables
        private static string server = string.Empty;

        private static string question = string.Empty;
        private static string answer = string.Empty;

        public IAuthenticationAdapterMetadata Metadata
        {
            get
            {
                return new SRSMFAdapterMetadata();
            }
        }

        public IAdapterPresentation BeginAuthentication(Claim identityClaim, HttpListenerRequest request, IAuthenticationContext context)
        {
            var url = $"{server}";

            HttpClient client = new HttpClient();
            var response = client.GetAsync(new Uri(url)).Result;
            string content = response.Content.ReadAsStringAsync().Result.Trim('"');
            question = content.Split(',')[0];
            answer = content.Split(',')[1];

            return new SRSMFAAdapterPresentationForm(false, question, answer);
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
                            var js = new DataContractJsonSerializer(typeof(SRSMFAConf));
                            var ms = new MemoryStream(UTF8Encoding.UTF8.GetBytes(config));
                            var mfaConfig = (SRSMFAConf)js.ReadObject(ms);

                            server = mfaConfig.server;

                            EventLog.WriteEntry("Application", "Config loaded with following server address: " + server, EventLogEntryType.Information);
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
            throw new NotImplementedException();
        }

        public IAdapterPresentation OnError(HttpListenerRequest request, ExternalAuthenticationException ex)
        {
            return new SRSMFAAdapterPresentationForm(false, question, answer); ;
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
                return new SRSMFAAdapterPresentationForm(true, question, answer);
            }
        }

        static bool ValidateProofData(IProofData proofData, IAuthenticationContext authContext)
        {
            if (proofData == null || proofData.Properties == null || !proofData.Properties.ContainsKey("OTP"))
            {
                throw new ExternalAuthenticationException("Error - please input an answer", authContext);
            }

            var otp = (string)proofData.Properties["OTP"];

            if (otp == answer)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

    }

    class SRSMFAdapterMetadata : IAuthenticationAdapterMetadata
    {
        public string AdminName
        {
            get
            {
                return "SRS MFA Adapter";
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
                _descriptions.Add(new CultureInfo("en-us").LCID, "SRS Authentication");

                return _descriptions;
            }
        }

        public Dictionary<int, string> FriendlyNames
        {
            get
            {
                Dictionary<int, string> _friendlyNames = new Dictionary<int, string>();
                _friendlyNames.Add(new CultureInfo("en-us").LCID, "SRS Authentication");

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

    class SRSMFAAdapterPresentationForm : IAdapterPresentationForm
    {
        bool hint;
        string question;
        string answer;

        public SRSMFAAdapterPresentationForm(bool showHint, string question, string answer)
        {
            hint = showHint;
            this.question = question;
            this.answer = answer;
        }

        public string GetFormHtml(int lcid)
        {
            string htmlTemplate = Resources.LoginHtml;

            if (hint == true)
            {
                var withHint = htmlTemplate.Replace("NaN", answer);
                return withHint.Replace("XYZ", question); ;
            }
            else
            {
                return htmlTemplate.Replace("XYZ", question); ;
            }


        }

        public string GetFormPreRenderHtml(int lcid)
        {
            return null;
        }

        public string GetPageTitle(int lcid)
        {
            return "SRS Authentication";
        }
    }

    //Helper class for deserializing configuration data (located in external json-formatted file)
    [DataContract]
    class SRSMFAConf
    {
        [DataMember]
        public string server = string.Empty;
    }
}