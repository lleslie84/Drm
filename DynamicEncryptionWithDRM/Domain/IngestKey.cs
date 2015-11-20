using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace DynamicEncryptionWithDRM {
   //https://fe.staging.drmtoday.com/frontend/documentation/integration/key_ingestion.html#post--frontend-api-keys-v2-ingest-[merchant]?ticket=[ticket]
   /*
   {
   "assets": [
     {
       "type": "CENC",
       "assetId": "asset1234",
       "variantId" : "variant1234",
       "ingestKeys": [
         {
           "keyId": "a6dHTVECSJe0MEJEOiZHQg==",
           "keyRotationId": "rotationId1",
           "streamType": "VIDEO",
           "algorithm": "AES",
           "key": "PYQN+urBJSAFpFs2t7vs+g=="
         },
         {
           "keyId": "HvcVZtHlSg+e1kXcZEXWUA==",
           "keyRotationId": "rotationId1",
           "streamType": "AUDIO",
           "algorithm": "AES",
           "key": "1aC/p6j7TVWbGpZN9v+IzA=="
         },
         {
           "keyId": "M5gJtYZzTauclhPZngZiLw==",
           "keyRotationId": "rotationId2",
           "streamType": "VIDEO",
           "algorithm": "AES",
           "key": "R+09yDfgS/W/f4ruMEIfog=="
         },
         {
           "keyId": "oQ0QPfr2QpuJUmv66zoMpw==",
           "keyRotationId": "rotationId2",
           "streamType": "AUDIO",
           "algorithm": "AES",
           "key": "n9bsag+hT3qMt2MVkbR+Fg=="
         }
       ]
     },
     {
       "type": "FAIRPLAY",
       "assetId": "asset1235",
       "variantId" : "variant1235",
       "ingestKeys": [
         {
           "streamType": "VIDEO_AUDIO",
           "algorithm": "AES",
           "key": "8NTqDEiHPNzQ+Q4jS/1gxg==",
           "iv": "w7eCWv84G9AKWf5zmskXXw=="
         }
       ]
     },
     {
       "type": "WIDEVINE",
       "assetId": "asset1236",
       "variantId" : "variant1236",
       "ingestKeys": [
         {
           "streamType": "VIDEO_AUDIO",
           "algorithm": "AES",
           "wvAssetId": "1348732146"
         }
       ]
     }
   ]
 }


    */


   [DataContract]
   public class IngestKey {
      [DataMember(Name = "keyId")]
      public string KeyId { get; set; }
      [DataMember(Name = "keyRotationId")]
      public string KeyRotationId { get; set; }
      [DataMember(Name = "streamType")]
      public string StreamType { get; set; }
      [DataMember(Name = "algorithm")]
      public string Algorithm { get; set; }
      [DataMember(Name = "key")]
      public string Key { get; set; }
      [DataMember(Name = "iv")]
      public string Iv { get; set; }
      [DataMember(Name = "wvAssetId")]
      public string WvAssetId { get; set; }
   }

   [DataContract]
   public class IngestAsset {
      [DataMember(Name = "type")]
      public string Type { get; set; }
      [DataMember(Name = "assetId")]
      public string AssetId { get; set; }
      [DataMember(Name = "variantId")]
      public string VariantId { get; set; }
      [DataMember(Name = "ingestKeys")]
      public List<IngestKey> IngestKeys { get; set; }
   }

   [DataContract]
   public class IngestObject {
      [DataMember(Name = "assets")]
      public List<IngestAsset> Assets { get; set; }
   }

}
