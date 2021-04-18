using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Text;

namespace DistributionTools
{
    public struct ApplicationDefaultCredentialsFile
    {
        [JsonProperty]
        private string ApplicationDefaultCredentialsFile_;

        public ApplicationDefaultCredentialsFile(string applicationDefaultCredentialsFile)
        {
            ApplicationDefaultCredentialsFile_ = applicationDefaultCredentialsFile;
        }

        public static explicit operator string(ApplicationDefaultCredentialsFile applicationDefaultCredentialsFile)
        {
            return applicationDefaultCredentialsFile.ApplicationDefaultCredentialsFile_;
        }

        public override string ToString()
        {
            return ApplicationDefaultCredentialsFile_;
        }
    }
}
