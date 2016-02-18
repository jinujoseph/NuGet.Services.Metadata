using Newtonsoft.Json;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Ng.Models
{
    /// <summary>
    /// Model for a NuGet registration item, https://api.nuget.org/v3/registration1/newtonsoft.json/8.0.2.json.
    /// </summary>
    public class RegistrationIndex
    {
        [JsonProperty(PropertyName = "@id")]
        public Uri Id
        {
            get;
            private set;
        }

        [JsonProperty(PropertyName = "@type")]
        public string[] Type
        {
            get;
            private set;
        }

        [JsonProperty(PropertyName = "commitId")]
        public Guid CommitId
        {
            get;
            private set;
        }

        [JsonProperty(PropertyName = "commitTimeStamp")]
        public DateTime CommitTimeStamp
        {
            get;
            private set;
        }

        [JsonProperty(PropertyName = "count")]
        public int Count
        {
            get;
            private set;
        }

        [JsonProperty(PropertyName = "items")]
        public RegistrationIndexPageItem[] Items
        {
            get;
            private set;
        }

        /// <summary> 
        /// Gets the latest stable version of this package 
        /// </summary> 
        /// <returns>The regsitration for the latest stable version of this package.</returns> 
        /// <remarks>We determine the latest stable version from the package registration. The package 
        /// registration contains the list of all the versions for this package. For each package, the  
        /// registartion entry specifies if the version is listed in the NuGet catalog. Also, in v3,  
        /// all the packages use semantic versioning, so we can use the the version number to determine  
        /// if a package is prerelease or stable. So we can find the latest stable version by finding the 
        /// package with the largest version number, where Listed==true and PackageVersion is not prerelease.</remarks> 
        public RegistrationIndexPackage GetLatestStableVersion()
        {
            // The registration index might have several pages of versions. 
            // Walk from the most recent page to the oldest page. 
            for (int i = this.Items.Length - 1; i >= 0; i--)
            {
                RegistrationIndexPageItem page = this.Items[i];
                if (page.Items == null)
                {
                    // If the registration index did not contain the page data, fetch  
                    // the registration page that includes the real data. 
                    page = page.LoadPage();
                }


                // Walk from the most recent version to the oldest version. 
                for (int j = page.Items.Length - 1; j >= 0; j--)
                {
                    RegistrationIndexPackage package = page.Items[j];


                    // If it's not listed, it can't be the latest stable version. 
                    if (!package.CatalogEntry.Listed)
                    {
                        continue;
                    }


                    // If it's prerelease, it can't be the latest stable version. 
                    NuGetVersion currentVersion = new NuGetVersion(package.CatalogEntry.PackageVersion);
                    if (currentVersion.IsPrerelease)
                    {
                        continue;
                    }


                    // We found the latest stable version 

                    // HACKHACK: For a few packages, the package.PackageContent property is empty, but the package.CatalogEntry.PackageContent
                    // property contains the correct value (the json file is incorrect.)
                    // If the package doesn't contain the download url, use the download url from the catalog
                    if (package.PackageContent == null)
                    {
                        package.PackageContent = package.CatalogEntry.PackageContent;
                    }

                    // Do some basic validation
                    if (package.PackageContent != null)
                    {
                        return package;
                    }
                }
            }


            return null;
        }

        /// <summary> 
        /// Gets the latest prerelease version of this package 
        /// </summary> 
        /// <returns>The regsitration for the latest prerelease version of this package.</returns> 
        /// <remarks>We determine the latest prerelease version from the package registration. The package 
        /// registration contains the list of all the versions for this package. For each package, the  
        /// registartion entry specifies if the version is listed in the NuGet catalog. Also, in v3,  
        /// all the packages use semantic versioning, so we can use the the version number to determine  
        /// if a package is prerelease or stable. So we can find the latest stable version by finding the 
        /// package with the largest version number, where Listed==true and PackageVersion is prerelease.</remarks> 
        public RegistrationIndexPackage GetLatestPreReleaseVersion()
        {
            // The registration index might have several pages of versions. 
            // Walk from the most recent page to the oldest page. 
            for (int i = this.Items.Length - 1; i >= 0; i--)
            {
                RegistrationIndexPageItem page = this.Items[i];
                if (page.Items == null)
                {
                    // If the registration index did not contain the page data, fetch  
                    // the registration page that includes the real data. 
                    page = page.LoadPage();
                }


                // Walk from the most recent version to the oldest version. 
                for (int j = page.Items.Length - 1; j >= 0; j--)
                {
                    RegistrationIndexPackage package = page.Items[j];


                    // If it's not listed, it can't be the latest stable version. 
                    if (!package.CatalogEntry.Listed)
                    {
                        continue;
                    }


                    // If it's prerelease, it can't be the latest stable version. 
                    NuGetVersion currentVersion = new NuGetVersion(package.CatalogEntry.PackageVersion);
                    if (currentVersion.IsPrerelease)
                    {
                        // HACKHACK: For a few packages, the package.PackageContent property is empty, but the package.CatalogEntry.PackageContent
                        // property contains the correct value (the json file is incorrect.)
                        // If the package doesn't contain the download url, use the download url from the catalog
                        if (package.PackageContent == null)
                        {
                            package.PackageContent = package.CatalogEntry.PackageContent;
                        }

                        // Do some basic validation
                        if (package.PackageContent != null)
                        {
                            return package;
                        }
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Creates a RegistrationIndex object from the contents of a URL.
        /// </summary>
        /// <param name="registrationIndexUrl">The URL that returns the registration index json.</param>
        /// <returns>A RegistrationIndex which represents the contents return by the URL.</returns>
        public static RegistrationIndex Deserialize(Uri registrationIndexUrl)
        {
            using (WebClient client = new WebClient())
            {
                string json = client.DownloadString(registrationIndexUrl);
                return RegistrationIndex.Deserialize(json);
            }
        }

        /// <summary>
        /// Creates a RegistrationIndex object from the contents of a json string.
        /// </summary>
        /// <param name="json">The json string that defines the registration index.</param>
        /// <returns>A RegistrationIndex which represents the json string.</returns>
        public static RegistrationIndex Deserialize(string json)
        {
            RegistrationIndex item = JsonConvert.DeserializeObject<RegistrationIndex>(json);

            // Do some basic validation
            if (item == null)
            {
                throw new ArgumentOutOfRangeException("The json string was not a registration index.");
            }

            if (item.Items == null)
            {
                throw new ArgumentOutOfRangeException("The json string did not have a value for the field items.");
            }

            return item;
        }
    }
}
