using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web.Caching;
using System.Web.Hosting;
using System.Web.Mvc;

namespace Griffin.MvcContrib.VirtualPathProvider
{
    /// <summary>
    ///   Locates views that are embedded resources.
    /// </summary>
    public class EmbeddedViewFileProvider : IViewFileProvider
    {
        private readonly List<MappedResource> _resourceNames = new List<MappedResource>();

        #region IViewFileProvider Members

        /// <summary>
        ///   Checks if a file exits
        /// </summary>
        /// <param name="virtualPath"> Virtual path like "~/Views/Home/Index.cshtml" </param>
        /// <returns> <c>true</c> if found; otherwise <c>false</c> . </returns>
        public bool FileExists(string virtualPath)
        {
            var path = GetResource(virtualPath);
            return path != null;
        }

        /// <summary>
        ///   Creates a cache dependency based on the specified virtual paths
        /// </summary>
        /// <param name="virtualPath"> Virtual path like "~/Views/Home/Index.cshtml" </param>
        /// <param name="dependencies"> The dependencies. </param>
        /// <param name="utcStart"> The UTC start. </param>
        /// <returns> null to disable caching </returns>
        public CacheDependency GetCacheDependency(string virtualPath, IEnumerable dependencies, DateTime utcStart)
        {
            return null;
        }

        /// <summary>
        /// </summary>
        /// <param name="virtualPath"> Virtual path like "~/Views/Home/Index.cshtml" </param>
        /// <returns> CacheDependency if found; otherwise <c>false</c> . </returns>
        public string GetCacheKey(string virtualPath)
        {
            var resource = GetResource(virtualPath);
            return resource != null ? resource.FullResourceName : null;
        }

        /// <summary>
        ///   Get file hash.
        /// </summary>
        /// <param name="virtualPath"> Virtual path like "~/Views/Home/Index.cshtml" </param>
        /// <param name="dependencies"> The dependencies. </param>
        /// <returns> a new hash each time the file have changed (if file is found); otherwise null </returns>
        public string GetFileHash(string virtualPath, IEnumerable dependencies)
        {
            return null;
        }

        /// <summary>
        ///   Get the view
        /// </summary>
        /// <param name="virtualPath"> Virtual path like "~/Views/Home/Index.cshtml" </param>
        /// <returns> File </returns>
        public VirtualFile GetFile(string virtualPath)
        {
            var resource = GetResource(virtualPath);
            if (resource == null)
                return null;

            var stream = resource.Assembly.GetManifestResourceStream(resource.FullResourceName);

            // embedded views need a @inherits instruction
            if (stream != null && resource.FullResourceName.EndsWith(".cshtml"))
            {
                var fixer = DependencyResolver.Current.GetService<IEmbeddedViewFixer>();
                stream = fixer.CorrectView(virtualPath, stream);
            }


            return stream == null ? null : new EmbeddedFile(virtualPath, stream);
        }

        #endregion

        /// <summary>
        ///   Add a namespace mapping for embedded resources.
        /// </summary>
        /// <param name="mapping"> Mapping to add </param>
        public void Add(NamespaceMapping mapping)
        {
            if (mapping == null) throw new ArgumentNullException("mapping");

            Map(mapping);
        }


        /// <summary>
        ///   Get resource name by scanning all mapped namespaces.
        /// </summary>
        /// <param name="uri"> Uri to search for. </param>
        /// <returns> Full resource name if found; otherwise null. </returns>
        private MappedResource GetResource(string uri)
        {
            uri = uri.TrimStart('~', '/').TrimEnd('/');
            uri = uri.Replace('/', '.');
            var result =
                _resourceNames.FirstOrDefault(
                    resource => resource.ResourceName.Equals(uri, StringComparison.OrdinalIgnoreCase));
            if (result == null)
            {
                uri = uri.Replace("MyIT.WebClient.", "");
                result =
                    _resourceNames.FirstOrDefault(
                        resource => resource.ResourceName.Equals(uri, StringComparison.OrdinalIgnoreCase));
            }
            if (result != null)
            {
                Debug.WriteLine("Exists: " + uri);
            }

            return result;
        }

        private void Map(NamespaceMapping mapping)
        {
            var names = mapping.Assembly.GetManifestResourceNames();
            foreach (var name in names)
            {
                if (!name.StartsWith(mapping.FolderNamespace))
                    continue;


                _resourceNames.Add(new MappedResource
                                       {
                                           Assembly = mapping.Assembly,
                                           AssemblyDate = new FileInfo(mapping.Assembly.Location).CreationTimeUtc,
                                           FullResourceName = name,
                                           ResourceRoot = mapping.FolderNamespace,
                                           ResourceName = name.Remove(0, mapping.FolderNamespace.Length + 1)
                                           // include the last dot
                                       });
            }
        }

        #region Nested type: EmbeddedFile

        private class EmbeddedFile : VirtualFile
        {
            private readonly Stream _resourceStream;

            public EmbeddedFile(string virtualPath, Stream resourceStream)
                : base(virtualPath)
            {
                _resourceStream = resourceStream;
            }

            /// <summary>
            ///   When overridden in a derived class, returns a read-only stream to the virtual resource.
            /// </summary>
            /// <returns> A read-only stream to the virtual file. </returns>
            public override Stream Open()
            {
                return _resourceStream;
            }
        }

        #endregion

        #region Nested type: MappedResource

        private class MappedResource
        {
            public string ResourceRoot { get; set; }
            public string FullResourceName { get; set; }
            public string ResourceName { get; set; }

            public Assembly Assembly { get; set; }
            public DateTime AssemblyDate { get; set; }

            public override string ToString()
            {
                return ResourceName + "/" + FullResourceName;
            }
        }

        #endregion
    }
}