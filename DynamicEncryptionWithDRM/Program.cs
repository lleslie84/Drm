﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.WindowsAzure.MediaServices.Client;
using Microsoft.WindowsAzure.MediaServices.Client.ContentKeyAuthorization;
using Microsoft.WindowsAzure.MediaServices.Client.DynamicEncryption;
using Microsoft.WindowsAzure.MediaServices.Client.Widevine;
using Newtonsoft.Json;

namespace DynamicEncryptionWithDRM {
   class Program {
      // Read values from the App.config file.
      private static readonly string _mediaServicesAccountName = ConfigurationManager.AppSettings["MediaServicesAccountName"];
      private static readonly string _mediaServicesAccountKey = ConfigurationManager.AppSettings["MediaServicesAccountKey"];

      private static readonly Uri _sampleIssuer = new Uri(ConfigurationManager.AppSettings["Issuer"]);
      private static readonly Uri _sampleAudience = new Uri(ConfigurationManager.AppSettings["Audience"]);

      // Field for service context.
      private static CloudMediaContext _context = null;
      private static MediaServicesCredentials _cachedCredentials = null;

      private static readonly string _mediaFiles = Path.GetFullPath(@"../..\Media");
      private static readonly string _singleMP4File = Path.Combine(_mediaFiles, @"BigBuckBunny.mp4");

      static void Main(string[] args) {
         //bool testData = true;
         //if (testData) {
         //   CastLabs.IngestKey("nb:cid:UUID:df5c313a-0d00-80c4-cd9d-f1e58e7e8832", "", keyObject.Key, keyObject.KeyId); //ingest key into CastLabs

         //}

         // Create and cache the Media Services credentials in a static class variable.
         _cachedCredentials = new MediaServicesCredentials(_mediaServicesAccountName, _mediaServicesAccountKey);
         // Used the cached credentials to create CloudMediaContext.
         _context = new CloudMediaContext(_cachedCredentials);

         bool tokenRestriction = true;
         string tokenTemplateString = null;

         IAsset asset = UploadFileAndCreateAsset(_singleMP4File);
         Console.WriteLine("Uploaded asset: {0}", asset.Id);
         Utils.WriteLine("Uploaded asset: {0}", asset.Id);


         IAsset encodedAsset = EncodeToAdaptiveBitrateMP4Set(asset);
         Console.WriteLine("Encoded asset: {0}", encodedAsset.Id);
         Utils.WriteLine("Encoded asset: {0}", encodedAsset.Id);

         //Console.WriteLine("Removing Locators");
         //Utils.WriteLine("Removing Locators");
         //RemoveILocators(asset.Id); //unpublish

         //Console.WriteLine("Removing dynamic PlayReady protection");
         //string oldKey = CryptoUtils.RemoveDynamicPlayReadyProtection(_context, asset); //remove protection


         //Console.WriteLine("Remove key from CastLabs");
         //if (!string.IsNullOrEmpty(oldKey))
         //   CastLabs.DeleteKey(ConvertKeyToGuid(oldKey)); //remove from CastLabs

         Console.WriteLine("Setup dynamic PlayReady protection");
         Utils.WriteLine("Setup dynamic PlayReady protection");

         KeyObject keyObject = CreateCommonTypeContentKey(encodedAsset);

         IContentKey key = keyObject.ContentKey;
         Console.WriteLine("Created key {0} for the asset {1} ", key.Id, encodedAsset.Id);
         Console.WriteLine("PlayReady License Key delivery URL: {0}", key.GetKeyDeliveryUrl(ContentKeyDeliveryType.PlayReadyLicense));
         Console.WriteLine("Widevine License Key delivery URL: {0}", key.GetKeyDeliveryUrl(ContentKeyDeliveryType.Widevine));
         Console.WriteLine();

         Utils.WriteLine("Created key(IContentKey)=> {0} for the asset(encodedAsset)=> {1} ", key.Id, encodedAsset.Id);
         Utils.WriteLine("PlayReady License Key delivery URL: {0}", key.GetKeyDeliveryUrl(ContentKeyDeliveryType.PlayReadyLicense));
         Utils.WriteLine("Widevine License Key delivery URL: {0}", key.GetKeyDeliveryUrl(ContentKeyDeliveryType.Widevine));
         Utils.WriteLine();

         //Base on media service setup
         if (tokenRestriction)
            tokenTemplateString = AddTokenRestrictedAuthorizationPolicy(key);
         else
            AddOpenAuthorizationPolicy(key);

         Console.WriteLine("Added authorization policy: {0}", key.AuthorizationPolicyId);
         Console.WriteLine();


         Utils.WriteLine("Added authorization policy: {0}", key.AuthorizationPolicyId);
         Utils.WriteLine();

         CreateAssetDeliveryPolicy(encodedAsset, key);
         Console.WriteLine("Created asset delivery policy. \n");
         Console.WriteLine();

         Utils.WriteLine("Created asset delivery policy. \n");
         Utils.WriteLine();

         if (tokenRestriction && !String.IsNullOrEmpty(tokenTemplateString)) {
            // Deserializes a string containing an Xml representation of a TokenRestrictionTemplate
            // back into a TokenRestrictionTemplate class instance.
            Utils.WriteLine("Deserializes a string containing an Xml representation of a TokenRestrictionTemplate: ", tokenTemplateString);
            TokenRestrictionTemplate tokenTemplate = TokenRestrictionTemplateSerializer.Deserialize(tokenTemplateString);

            // Generate a test token based on the the data in the given TokenRestrictionTemplate.
            // Note, you need to pass the key id Guid because we specified 
            // TokenClaim.ContentKeyIdentifierClaim in during the creation of TokenRestrictionTemplate.
            Guid rawkey = EncryptionUtils.GetKeyIdAsGuid(key.Id);
            Console.WriteLine("GetKeyIdAsGuid  {0}", rawkey);
            string testToken = TokenRestrictionTemplateSerializer.GenerateTestToken(tokenTemplate, null, rawkey, DateTime.UtcNow.AddDays(365));
            Console.WriteLine("The authorization token is:\nBearer {0}", testToken);
            Utils.WriteLine("The authorization token is - Generated from EncryptionUtils.GetKeyIdAsGuid :\nBearer {0}", testToken);
            Console.WriteLine();
            Utils.WriteLine();


            testToken = TokenRestrictionTemplateSerializer.GenerateTestToken(tokenTemplate, null, keyObject.KeyId, DateTime.UtcNow.AddDays(365));
            Console.WriteLine("The authorization token is:\nBearer {0}", testToken);
            Utils.WriteLine("The authorization token is - Generated from CreateCommonTypeContentKey :\nBearer {0}", testToken);
            Console.WriteLine();
            Utils.WriteLine();


            Console.WriteLine("Adding key to CastLabs");
            Utils.WriteLine("Adding key to CastLabs");
            Utils.WriteLine("CreateCommonTypeContentKey Key: {0}; Key Id: {1}",keyObject.Key, keyObject.KeyId);
            Utils.WriteLine("GetKeyIdAsGuid  {0}", rawkey);

            CastLabs.IngestKey(encodedAsset.Id, "", keyObject.Key, keyObject.KeyId); //ingest key into CastLabs
            Utils.WriteLine();
         }

         // You can use the http://amsplayer.azurewebsites.net/azuremediaplayer.html player to test streams.
         // Note that DASH works on IE 11 (via PlayReady), Edge (via PlayReady), Chrome (via Widevine).

         string url = GetStreamingOriginLocator(encodedAsset);
         Console.WriteLine("Encrypted DASH URL: {0}/manifest(format=mpd-time-csf)", url);
         Utils.WriteLine("Encrypted DASH URL: {0}/manifest(format=mpd-time-csf)", url);

         Console.ReadLine();
      }


      //the code for removing locators. 
      public static void RemoveILocators(string assetId) {
         var locators = _context.Locators.Where(l => l.AssetId == assetId);
         foreach (ILocator objILocator in locators) {
            objILocator.Delete();
         }
      }

      static public IAsset UploadFileAndCreateAsset(string singleFilePath) {
         if (!File.Exists(singleFilePath)) {
            Console.WriteLine("File does not exist.");
            return null;
         }

         var assetName = Path.GetFileNameWithoutExtension(singleFilePath);
         IAsset inputAsset = _context.Assets.Create(assetName, AssetCreationOptions.None);

         var assetFile = inputAsset.AssetFiles.Create(Path.GetFileName(singleFilePath));

         Console.WriteLine("Created assetFile {0}", assetFile.Name);

         var policy = _context.AccessPolicies.Create(
                                 assetName,
                                 TimeSpan.FromDays(30),
                                 AccessPermissions.Write | AccessPermissions.List);

         var locator = _context.Locators.CreateLocator(LocatorType.Sas, inputAsset, policy);

         Console.WriteLine("Upload {0}", assetFile.Name);

         assetFile.Upload(singleFilePath);
         Console.WriteLine("Done uploading {0}", assetFile.Name);

         locator.Delete();
         policy.Delete();

         return inputAsset;
      }


      static public IAsset EncodeToAdaptiveBitrateMP4Set(IAsset inputAsset) {
         var encodingPreset = "H264 Adaptive Bitrate MP4 Set 720p";

         IJob job = _context.Jobs.Create(String.Format("Encoding into Mp4 {0} to {1}",
                                 inputAsset.Name,
                                 encodingPreset));

         var mediaProcessors =
             _context.MediaProcessors.Where(p => p.Name.Contains("Media Encoder")).ToList();

         var latestMediaProcessor =
             mediaProcessors.OrderBy(mp => new Version(mp.Version)).LastOrDefault();



         ITask encodeTask = job.Tasks.AddNew("Encoding", latestMediaProcessor, encodingPreset, TaskOptions.None);
         encodeTask.InputAssets.Add(inputAsset);
         encodeTask.OutputAssets.AddNew(String.Format("{0} as {1}", inputAsset.Name, encodingPreset), AssetCreationOptions.StorageEncrypted);

         job.StateChanged += new EventHandler<JobStateChangedEventArgs>(JobStateChanged);
         job.Submit();
         job.GetExecutionProgressTask(CancellationToken.None).Wait();

         return job.OutputMediaAssets[0];
      }


      static public KeyObject CreateCommonTypeContentKey(IAsset asset) {
         // Create envelope encryption content key
         Guid keyId = Guid.NewGuid();
         byte[] contentKey = GetRandomBuffer(16);
         string contentKeyB64 = Convert.ToBase64String(contentKey);

         IContentKey key = _context.ContentKeys.Create(
                                 keyId,
                                 contentKey,
                                 "ContentKey",
                                 ContentKeyType.CommonEncryption);

         // Associate the key with the asset.
         asset.ContentKeys.Add(key);

         var keys = new KeyObject {
            Key = contentKeyB64,
            KeyId = keyId,
            ContentKey = key
         };

         return keys;
      }

      static public void AddOpenAuthorizationPolicy(IContentKey contentKey) {

         // Create ContentKeyAuthorizationPolicy with Open restrictions 
         // and create authorization policy          

         List<ContentKeyAuthorizationPolicyRestriction> restrictions = new List<ContentKeyAuthorizationPolicyRestriction>
         {
                new ContentKeyAuthorizationPolicyRestriction
                {
                    Name = "Open",
                    KeyRestrictionType = (int)ContentKeyRestrictionType.Open,
                    Requirements = null
                }
            };

         // Configure PlayReady and Widevine license templates.
         string PlayReadyLicenseTemplate = ConfigurePlayReadyLicenseTemplate();

         string WidevineLicenseTemplate = ConfigureWidevineLicenseTemplate();

         IContentKeyAuthorizationPolicyOption PlayReadyPolicy =
             _context.ContentKeyAuthorizationPolicyOptions.Create("",
                 ContentKeyDeliveryType.PlayReadyLicense,
                     restrictions, PlayReadyLicenseTemplate);

         IContentKeyAuthorizationPolicyOption WidevinePolicy =
             _context.ContentKeyAuthorizationPolicyOptions.Create("",
                 ContentKeyDeliveryType.Widevine,
                 restrictions, WidevineLicenseTemplate);

         IContentKeyAuthorizationPolicy contentKeyAuthorizationPolicy = _context.
                     ContentKeyAuthorizationPolicies.
                     CreateAsync("Deliver Common Content Key with no restrictions").
                     Result;


         contentKeyAuthorizationPolicy.Options.Add(PlayReadyPolicy);
         contentKeyAuthorizationPolicy.Options.Add(WidevinePolicy);
         // Associate the content key authorization policy with the content key.
         contentKey.AuthorizationPolicyId = contentKeyAuthorizationPolicy.Id;
         contentKey = contentKey.UpdateAsync().Result;
      }

      public static string AddTokenRestrictedAuthorizationPolicy(IContentKey contentKey) {
         string tokenTemplateString = GenerateTokenRequirements();

         List<ContentKeyAuthorizationPolicyRestriction> restrictions = new List<ContentKeyAuthorizationPolicyRestriction>
         {
                new ContentKeyAuthorizationPolicyRestriction
                {
                    Name = "Token Authorization Policy",
                    KeyRestrictionType = (int)ContentKeyRestrictionType.TokenRestricted,
                    Requirements = tokenTemplateString,
                }
            };

         // Configure PlayReady and Widevine license templates.
         string PlayReadyLicenseTemplate = ConfigurePlayReadyLicenseTemplate();

         string WidevineLicenseTemplate = ConfigureWidevineLicenseTemplate();

         IContentKeyAuthorizationPolicyOption PlayReadyPolicy =
             _context.ContentKeyAuthorizationPolicyOptions.Create("Token option",
                 ContentKeyDeliveryType.PlayReadyLicense,
                     restrictions, PlayReadyLicenseTemplate);

         IContentKeyAuthorizationPolicyOption WidevinePolicy =
             _context.ContentKeyAuthorizationPolicyOptions.Create("Token option",
                 ContentKeyDeliveryType.Widevine,
                     restrictions, WidevineLicenseTemplate);

         IContentKeyAuthorizationPolicy contentKeyAuthorizationPolicy = _context.
                     ContentKeyAuthorizationPolicies.
                     CreateAsync("Deliver Common Content Key with token restrictions").
                     Result;

         contentKeyAuthorizationPolicy.Options.Add(PlayReadyPolicy);
         contentKeyAuthorizationPolicy.Options.Add(WidevinePolicy);

         // Associate the content key authorization policy with the content key
         contentKey.AuthorizationPolicyId = contentKeyAuthorizationPolicy.Id;
         contentKey = contentKey.UpdateAsync().Result;

         return tokenTemplateString;
      }

      static private string GenerateTokenRequirements() {
         string primarySymmetricKey = ConfigurationManager.AppSettings["PrimarySymmetricKey"];
         byte[] symkeyhex = StringToByteArray(primarySymmetricKey);

         TokenRestrictionTemplate template = new TokenRestrictionTemplate(TokenType.JWT);

         template.PrimaryVerificationKey = new SymmetricVerificationKey(symkeyhex);
         template.AlternateVerificationKeys.Add(new SymmetricVerificationKey());
         template.Audience = _sampleAudience.ToString();
         template.Issuer = _sampleIssuer.ToString();
         template.RequiredClaims.Add(TokenClaim.ContentKeyIdentifierClaim);

         return TokenRestrictionTemplateSerializer.Serialize(template);
      }

      // The CastLabs system handles the strings as Hex. Therefore we need to be careful when using these 'strings' 
      // as in CastLabs system they are Hex Arrays. So convert them before use
      public static byte[] StringToByteArray(string hex) {
         return Enumerable.Range(0, hex.Length)
             .Where(x => x % 2 == 0)
             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
             .ToArray();
      }

      static private string ConfigurePlayReadyLicenseTemplate() {
         // The following code configures PlayReady License Template using .NET classes
         // and returns the XML string.

         //The PlayReadyLicenseResponseTemplate class represents the template for the response sent back to the end user. 
         //It contains a field for a custom data string between the license server and the application 
         //(may be useful for custom app logic) as well as a list of one or more license templates.
         PlayReadyLicenseResponseTemplate responseTemplate = new PlayReadyLicenseResponseTemplate();

         // The PlayReadyLicenseTemplate class represents a license template for creating PlayReady licenses
         // to be returned to the end users. 
         //It contains the data on the content key in the license and any rights or restrictions to be 
         //enforced by the PlayReady DRM runtime when using the content key.
         PlayReadyLicenseTemplate licenseTemplate = new PlayReadyLicenseTemplate();
         //Configure whether the license is persistent (saved in persistent storage on the client) 
         //or non-persistent (only held in memory while the player is using the license).  
         licenseTemplate.LicenseType = PlayReadyLicenseType.Nonpersistent;

         // AllowTestDevices controls whether test devices can use the license or not.  
         // If true, the MinimumSecurityLevel property of the license
         // is set to 150.  If false (the default), the MinimumSecurityLevel property of the license is set to 2000.
         licenseTemplate.AllowTestDevices = true;

         // You can also configure the Play Right in the PlayReady license by using the PlayReadyPlayRight class. 
         // It grants the user the ability to playback the content subject to the zero or more restrictions 
         // configured in the license and on the PlayRight itself (for playback specific policy). 
         // Much of the policy on the PlayRight has to do with output restrictions 
         // which control the types of outputs that the content can be played over and 
         // any restrictions that must be put in place when using a given output.
         // For example, if the DigitalVideoOnlyContentRestriction is enabled, 
         //then the DRM runtime will only allow the video to be displayed over digital outputs 
         //(analog video outputs won’t be allowed to pass the content).

         //IMPORTANT: These types of restrictions can be very powerful but can also affect the consumer experience. 
         // If the output protections are configured too restrictive, 
         // the content might be unplayable on some clients. For more information, see the PlayReady Compliance Rules document.

         // For example:
         //licenseTemplate.PlayRight.AgcAndColorStripeRestriction = new AgcAndColorStripeRestriction(1);

         responseTemplate.LicenseTemplates.Add(licenseTemplate);

         return MediaServicesLicenseTemplateSerializer.Serialize(responseTemplate);
      }


      private static string ConfigureWidevineLicenseTemplate() {
         var template = new WidevineMessage {
            allowed_track_types = AllowedTrackTypes.SD_HD,
            content_key_specs = new[]
             {
                    new ContentKeySpecs
                    {
                        required_output_protection = new RequiredOutputProtection { hdcp = Hdcp.HDCP_NONE},
                        security_level = 1,
                        track_type = "SD"
                    }
                },
            policy_overrides = new {
               can_play = true,
               can_persist = true,
               can_renew = false
            }
         };

         string configuration = JsonConvert.SerializeObject(template);
         return configuration;
      }

      static public void CreateAssetDeliveryPolicy(IAsset asset, IContentKey key) {
         Uri acquisitionUrl = key.GetKeyDeliveryUrl(ContentKeyDeliveryType.PlayReadyLicense);
         Uri widevineURl = key.GetKeyDeliveryUrl(ContentKeyDeliveryType.Widevine);
         Dictionary<AssetDeliveryPolicyConfigurationKey, string> assetDeliveryPolicyConfiguration =
             new Dictionary<AssetDeliveryPolicyConfigurationKey, string>
             {
                    {AssetDeliveryPolicyConfigurationKey.PlayReadyLicenseAcquisitionUrl, acquisitionUrl.ToString()},
                    {AssetDeliveryPolicyConfigurationKey.WidevineLicenseAcquisitionUrl, widevineURl.ToString()},

             };

         var assetDeliveryPolicy = _context.AssetDeliveryPolicies.Create(
                 "AssetDeliveryPolicy",
             AssetDeliveryPolicyType.DynamicCommonEncryption,
             AssetDeliveryProtocol.Dash,
             assetDeliveryPolicyConfiguration);


         // Add AssetDelivery Policy to the asset
         asset.DeliveryPolicies.Add(assetDeliveryPolicy);
      }


      /// <summary>
      /// Gets the streaming origin locator.
      /// </summary>
      /// <param name="assets"></param>
      /// <returns></returns>
      static public string GetStreamingOriginLocator(IAsset asset) {

         // Get a reference to the streaming manifest file from the  
         // collection of files in the asset. 

         var assetFile = asset.AssetFiles.Where(f => f.Name.ToLower().
                                      EndsWith(".ism")).
                                      FirstOrDefault();

         // Create a 30-day readonly access policy. 
         IAccessPolicy policy = _context.AccessPolicies.Create("Streaming policy",
             TimeSpan.FromDays(30),
             AccessPermissions.Read);

         // Create a locator to the streaming content on an origin. 
         ILocator originLocator = _context.Locators.CreateLocator(LocatorType.OnDemandOrigin, asset,
             policy,
             DateTime.UtcNow.AddMinutes(-5));

         // Create a URL to the manifest file. 
         return originLocator.Path + assetFile.Name;
      }

      static private void JobStateChanged(object sender, JobStateChangedEventArgs e) {
         Console.WriteLine(string.Format("{0}\n  State: {1}\n  Time: {2}\n\n",
             ((IJob)sender).Name,
             e.CurrentState,
             DateTime.UtcNow.ToString(@"yyyy_M_d__hh_mm_ss")));
      }

      static private byte[] GetRandomBuffer(int length) {
         var returnValue = new byte[length];

         using (var rng =
             new System.Security.Cryptography.RNGCryptoServiceProvider()) {
            rng.GetBytes(returnValue);
         }

         return returnValue;
      }
   }

   public static class Utils {
      public static void WriteLine(string data, params object[] args) {
         data = string.Format(data, args);

         try {
            string path = string.Format(@"C:\golib\drm-{0}.log", DateTime.Today.ToString("dd-MMM-yy"));
            File.AppendAllLines(path, data.Split(new[] { "\n" }, StringSplitOptions.None));
         } catch {

         }
      }

      public static void WriteLine(string data) {
         WriteLine("{0}", data);
      }

      public static void WriteLine() {
         WriteLine(string.Empty);
      }
      public static void WriteLine(Exception ex) {
         if (ex == null) {
            return;
         }

         WriteLine("{0} - {1}", ex.GetType().ToString(), ex.Message);
         WriteLine(ex.InnerException);
      }
   }
}
