using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.MediaServices.Client;

namespace DynamicEncryptionWithDRM {
   public class KeyObject {
      public IContentKey ContentKey { get; set; }
      public string Key { get; set; }
      public Guid KeyId { get; set; }
   }
}
