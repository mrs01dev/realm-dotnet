﻿////////////////////////////////////////////////////////////////////////////
//
// Copyright 2018 Realm Inc.
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
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Realms.Helpers;
using Realms.Schema;

namespace Realms.Sync
{
    /// <summary>
    /// A <see cref="SyncConfiguration"/> is used to setup a <see cref="Realm"/> that can be synchronized between devices using the
    /// Realm Object Server.
    /// </summary>
    /// <seealso cref="User.LoginAsync"/>
    /// <seealso cref="Credentials"/>
    public class SyncConfiguration : RealmConfigurationBase
    {
        /// <summary>
        /// Gets the <see cref="Uri"/> used to create this <see cref="SyncConfiguration"/>.
        /// </summary>
        /// <value>The <see cref="Uri"/> where the Realm Object Server is hosted.</value>
        public Uri ServerUri { get; }

        /// <summary>
        /// Gets the <see cref="User"/> used to create this <see cref="SyncConfiguration"/>.
        /// </summary>
        /// <value>The <see cref="User"/> whose <see cref="Realm"/>s will be synced.</value>
        public User User { get; }

        /// <summary>
        /// Gets or sets a callback that is invoked when download progress is made when using <see cref="Realm.GetInstanceAsync"/>.
        /// This will only be invoked for the initial download of the Realm and will not be invoked as futher download
        /// progress is made during the lifetime of the Realm. It is ignored when using
        /// <see cref="Realm.GetInstance(RealmConfigurationBase)"/>.
        /// </summary>
        public Action<SyncProgress> OnProgress { get; set; }

        /// <summary>
        /// Gets or sets a value controlling the behavior in case of a Client Resync. Default is <see cref="ClientResyncMode.RecoverLocalRealm"/>.
        /// </summary>
        public ClientResyncMode ClientResyncMode { get; set; } = ClientResyncMode.RecoverLocalRealm;

        /// <summary>
        /// Gets or sets a value indicating how detailed the sync client's logs will be.
        /// </summary>
        public static LogLevel LogLevel
        {
            get => SharedRealmHandleExtensions.GetLogLevel();
            set => SharedRealmHandleExtensions.SetLogLevel(value);
        }

        private static Action<string, LogLevel> _customLogger;

        /// <summary>
        /// Gets or sets a custom log function that will be invoked by Sync instead of writing
        /// to the standard error. This must be set before using any of the sync API.
        /// </summary>
        /// <remarks>
        /// This callback will not be invoked in a thread-safe manner, so it's up to the implementor to ensure
        /// that log messages arriving from multiple threads are processed without garbling the final output.
        /// </remarks>
        /// <value>The custom log function.</value>
        public static Action<string, LogLevel> CustomLogger
        {
            get => _customLogger;
            set
            {
                _customLogger = value;
                SharedRealmHandleExtensions.InstallLogCallback();
            }
        }

        private static string _userAgent;

        /// <summary>
        /// Gets or sets a string identifying this application which is included in the User-Agent
        /// header of sync connections.
        /// </summary>
        /// <remarks>
        /// This property must be set prior to opening a synchronized Realm for the first
        /// time. Any modifications made after opening a Realm will be ignored.
        /// </remarks>
        /// <value>
        /// The custom user agent that will be appended to the one generated by the SDK.
        /// </value>
        public static string UserAgent
        {
            get => _userAgent;
            set
            {
                Argument.NotNull(value, nameof(value));
                SharedRealmHandleExtensions.SetUserAgent(value);
                _userAgent = value;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SyncConfiguration"/> class.
        /// </summary>
        /// <param name="partition">
        /// V10TODO: document this.
        /// </param>
        /// <param name="user">
        /// A valid <see cref="User"/>. If not provided, the currently logged-in user will be used.
        /// </param>
        /// <param name="optionalPath">
        /// Path to the realm, must be a valid full path for the current platform, relative subdirectory, or just filename.
        /// </param>
        public SyncConfiguration(object partition, User user = null, string optionalPath = null)
        {
            Argument.Ensure(user != null || User.AllLoggedIn.Length == 1,
                "The user must be explicitly specified when the number of logged-in users is not 1.",
                nameof(user));

            User = user ?? User.Current;
            DatabasePath = GetPathToRealm(optionalPath ?? SharedRealmHandleExtensions.GetRealmPath(User, ServerUri));
        }

        /// <summary>
        /// Configures various parameters of the sync system, such as the way users are persisted or the base
        /// path relative to which files will be saved.
        /// </summary>
        /// <param name="mode">The user persistence mode.</param>
        /// <param name="encryptionKey">The key to encrypt the persistent user store with.</param>
        /// <param name="resetOnError">If set to <c>true</c> reset the persistent user store on error.</param>
        /// <param name="basePath">The base folder relative to which Realm files will be stored.</param>
        /// <remarks>
        /// Users are persisted in a realm file within the application's sandbox.
        /// <para>
        /// By default <see cref="Realms.Sync.User"/> objects are persisted and are additionally protected with an encryption key stored
        /// in the iOS Keychain when running on an iOS device (but not on a Simulator).
        /// On Android users are persisted in plaintext, because the AndroidKeyStore API is only supported on API level 18 and up.
        /// You might want to provide your own encryption key on Android or disable persistence for security reasons.
        /// </para>
        /// </remarks>
        public static void Initialize(UserPersistenceMode mode, byte[] encryptionKey = null, bool resetOnError = false, string basePath = null)
        {
            if (mode == UserPersistenceMode.Encrypted && encryptionKey != null && encryptionKey.Length != 64)
            {
                throw new ArgumentException("The encryption key must be 64 bytes long", nameof(encryptionKey));
            }

            SharedRealmHandleExtensions.Configure(mode, encryptionKey, resetOnError, basePath);
        }

        internal override Realm CreateRealm(RealmSchema schema)
        {
            var configuration = CreateConfiguration();

            var srHandle = SharedRealmHandleExtensions.OpenWithSync(configuration, ToNative(), schema, EncryptionKey);
            if (IsDynamic && !schema.Any())
            {
                srHandle.GetSchema(nativeSchema => schema = RealmSchema.CreateFromObjectStoreSchema(nativeSchema));
            }

            return new Realm(srHandle, this, schema);
        }

        [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The Realm instance will own its handle")]
        internal override async Task<Realm> CreateRealmAsync(RealmSchema schema, CancellationToken cancellationToken)
        {
            var configuration = CreateConfiguration();

            var tcs = new TaskCompletionSource<ThreadSafeReferenceHandle>();
            var tcsHandle = GCHandle.Alloc(tcs);
            ProgressNotificationToken progressToken = null;
            try
            {
                using (var handle = SharedRealmHandleExtensions.OpenWithSyncAsync(configuration, ToNative(), schema, EncryptionKey, tcsHandle))
                {
                    cancellationToken.Register(() =>
                    {
                        if (!handle.IsClosed)
                        {
                            handle.Cancel();
                            tcs.TrySetCanceled();
                        }
                    });

                    if (OnProgress != null)
                    {
                        progressToken = new ProgressNotificationToken(
                            observer: (progress) =>
                            {
                                OnProgress(progress);
                            },
                            register: handle.RegisterProgressNotifier,
                            unregister: (token) =>
                            {
                                if (!handle.IsClosed)
                                {
                                    handle.UnregisterProgressNotifier(token);
                                }
                            });
                    }

                    using (var realmReference = await tcs.Task)
                    {
                        var realmPtr = SharedRealmHandle.ResolveFromReference(realmReference);
                        var sharedRealmHandle = new SharedRealmHandle(realmPtr);
                        if (IsDynamic && !schema.Any())
                        {
                            sharedRealmHandle.GetSchema(nativeSchema => schema = RealmSchema.CreateFromObjectStoreSchema(nativeSchema));
                        }

                        return new Realm(sharedRealmHandle, this, schema);
                    }
                }
            }
            finally
            {
                tcsHandle.Free();
                progressToken?.Dispose();
            }
        }

        internal Native.SyncConfiguration ToNative()
        {
            return new Native.SyncConfiguration
            {
                SyncUserHandle = User.Handle,
                Url = ServerUri.ToString(),
                client_resync_mode = ClientResyncMode,
            };
        }

        internal static string GetSDKUserAgent()
        {
            var version = typeof(SyncConfiguration).GetTypeInfo().Assembly.GetName().Version;
            return $"RealmDotNet/{version} ({RuntimeInformation.FrameworkDescription})";
        }
    }
}