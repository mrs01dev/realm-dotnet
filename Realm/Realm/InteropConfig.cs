////////////////////////////////////////////////////////////////////////////
//
// Copyright 2016 Realm Inc.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
////////////////////////////////////////////////////////////////////////////

using System;
using System.IO;
using System.Reflection;

namespace Realms
{
    internal static class InteropConfig
    {
        /// <summary>
        /// Name of the DLL used in native declarations, constant varying per-platform.
        /// </summary>
        public const string DLL_NAME = "realm-wrappers";

        private static readonly Lazy<string> _defaultStorageFolder = new Lazy<string>(() =>
        {
            try
            {
                // On UWP, the sandbox folder is obtained by:
                // ApplicationData.Current.LocalFolder.Path
                var applicationData = Type.GetType("Windows.Storage.ApplicationData, Windows, Version=255.255.255.255, Culture=neutral, PublicKeyToken=null, ContentType=WindowsRuntime");
                if (applicationData != null)
                {
                    var currentProperty = applicationData.GetProperty("Current", BindingFlags.Static | BindingFlags.Public);
                    var localFolderProperty = applicationData.GetProperty("LocalFolder", BindingFlags.Public | BindingFlags.Instance);
                    var pathProperty = localFolderProperty.PropertyType.GetProperty("Path", BindingFlags.Public | BindingFlags.Instance);

                    var currentApplicationData = currentProperty.GetValue(null);
                    var localFolder = localFolderProperty.GetValue(currentApplicationData);
                    return (string)pathProperty.GetValue(localFolder);
                }
            }
            catch
            {
            }

            try
            {
                var specialFolderType = typeof(Environment).GetNestedType("SpecialFolder", BindingFlags.Public);
                if (specialFolderType != null)
                {
                    var getFolderPath = typeof(Environment).GetMethod("GetFolderPath", new[] { specialFolderType });
                    if (getFolderPath != null)
                    {
                        var personalField = specialFolderType.GetField("Personal");
                        return (string)getFolderPath.Invoke(null, new[] { personalField.GetValue(null) });
                    }
                }
            }
            catch
            {
            }

            var currentDirectory = Directory.GetCurrentDirectory();
            if (!IsDirectoryWritable(currentDirectory))
            {
                throw new InvalidOperationException("Couldn't determine a writable folder where to store realm file. Specify absolute path manually.");
            }

            var folder = Path.Combine(currentDirectory, "Documents");
            Directory.CreateDirectory(folder);
            return folder;

            bool IsDirectoryWritable(string path)
            {
                if (!Directory.Exists(path))
                {
                    return false;
                }

                try
                {
                    using (File.Create(Path.Combine(path, Path.GetRandomFileName()), 1, FileOptions.DeleteOnClose))
                    {
                    }

                    return true;
                }
                catch
                {
                    return false;
                }
            }
        });

        private static string _customStorageFolder;

        public static string DefaultStorageFolder
        {
            get => _customStorageFolder ?? _defaultStorageFolder.Value;
            set => _customStorageFolder = value;
        }
    }
}