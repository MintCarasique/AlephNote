﻿using AlephNote.PluginInterface.Util;
using System;
using System.Collections.Generic;
using System.Net;

namespace AlephNote.PluginInterface
{
	public interface IRemotePlugin
	{
		string DisplayTitleLong { get; }
		string DisplayTitleShort { get; }

		void Init(AlephLogger logger);

		Guid GetUniqueID();
		string GetName();
		Version GetVersion(); //SemVer. set last digit <> 0 to create a debug version (will not be loaded in RELEASE)

		bool SupportsNativeDirectories         { get; }
		bool SupportsPinning                   { get; }
		bool SupportsLocking                   { get; }
		bool SupportsTags                      { get; }
		bool SupportsDownloadMultithreading    { get; }
		bool SupportsNewDownloadMultithreading { get; }
		bool SupportsUploadMultithreading      { get; }

		List<UICommand> DebugCommands { get; }

		IRemoteStorageConfiguration CreateEmptyRemoteStorageConfiguration();
		IRemoteStorageConnection CreateRemoteStorageConnection(IWebProxy proxy, IRemoteStorageConfiguration config, HierarchyEmulationConfig hierarchicalConfig);
		IRemoteStorageSyncPersistance CreateEmptyRemoteSyncData();
		INote CreateEmptyNote(IRemoteStorageConnection conn, IRemoteStorageConfiguration cfg);

		IDictionary<string, string> GetHelpTexts();
	}
}
