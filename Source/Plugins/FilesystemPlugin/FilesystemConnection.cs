﻿using AlephNote.PluginInterface;
using AlephNote.PluginInterface.Impl;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AlephNote.PluginInterface.Util;
using MSHC.Util.Helper;

namespace AlephNote.Plugins.Filesystem
{
	public class FilesystemConnection : BasicRemoteConnection
	{
		private readonly FilesystemConfig _config;
		private readonly AlephLogger _logger;

		private List<string> _syncScan = null; 

		public FilesystemConnection(AlephLogger log, FilesystemConfig config)
		{
			_config = config;
			_logger = log;
		}

		public override void StartSync(IRemoteStorageSyncPersistance data, List<INote> localnotes, List<INote> localdeletednotes)
		{
			_syncScan = FileSystemUtil
				.EnumerateFilesDeep(_config.Folder, _config.SearchDepth)
				.Where(p => (Path.GetExtension(p) ?? "").ToLower() == "." + _config.Extension.ToLower())
				.ToList();

			_logger.Debug(FilesystemPlugin.Name, string.Format("Found {0} note files in directory scan", _syncScan.Count));
		}

		public override void FinishSync()
		{
			_syncScan = null;
		}

		public override bool NeedsUpload(INote inote)
		{
			var note = (FilesystemNote)inote;

			if (note.IsConflictNote) return false;
			//if (string.IsNullOrWhiteSpace(note.Title)) return false;

			if (!note.IsRemoteSaved) return true;
			if (string.IsNullOrWhiteSpace(note.PathRemote)) return true;
			if (!File.Exists(note.PathRemote)) return false;

			return false;
		}

		public override bool NeedsDownload(INote inote)
		{
			var note = (FilesystemNote)inote;

			if (note.IsConflictNote) return false;
			if (string.IsNullOrWhiteSpace(note.Title)) return false;

			if (!note.IsRemoteSaved) return false;

			if (string.IsNullOrWhiteSpace(note.PathRemote)) return false;
			if (!File.Exists(note.PathRemote)) return true;

			var remote = ReadNoteFromPath(note.PathRemote);

			return remote.IsLocked != note.IsLocked || remote.ModificationDate > note.ModificationDate;
		}

		public override RemoteUploadResult UploadNoteToRemote(ref INote inote, out INote conflict, ConflictResolutionStrategy strategy)
		{
			FilesystemNote note = (FilesystemNote)inote;

			var path = note.GetPath(_config);

			if (File.Exists(note.PathRemote) && path != note.PathRemote && !File.Exists(path))
			{
				_logger.Debug(FilesystemPlugin.Name, "Upload note to changed remote path"); // path changed and new path does not exist

				WriteNoteToPath(note, path);
				conflict = null;
				ANFileSystemUtil.DeleteFileAndFolderIfEmpty(FilesystemPlugin.Name, _logger, _config.Folder, note.PathRemote);
				note.PathRemote = path;
				return RemoteUploadResult.Uploaded;
			}
			else if (File.Exists(note.PathRemote) && path != note.PathRemote && File.Exists(path))
			{
				_logger.Debug(FilesystemPlugin.Name, "Upload note to changed remote path"); // path changed and new path does exist

				var conf = ReadNoteFromPath(note.PathRemote);
				if (conf.ModificationDate > note.ModificationDate)
				{
					conflict = conf;
					if (strategy == ConflictResolutionStrategy.UseClientCreateConflictFile || strategy == ConflictResolutionStrategy.UseClientVersion || strategy == ConflictResolutionStrategy.ManualMerge)
					{
						WriteNoteToPath(note, path);
						ANFileSystemUtil.DeleteFileAndFolderIfEmpty(FilesystemPlugin.Name, _logger, _config.Folder, note.PathRemote);
						note.PathRemote = path;
						return RemoteUploadResult.Conflict;
					}
					else
					{
						note.PathRemote = path;
						return RemoteUploadResult.Conflict;
					}
				}
				else
				{
					WriteNoteToPath(note, path);
					conflict = null;
					ANFileSystemUtil.DeleteFileAndFolderIfEmpty(FilesystemPlugin.Name, _logger, _config.Folder, note.PathRemote);
					note.PathRemote = path;
					return RemoteUploadResult.Uploaded;
				}
			}
			else if (File.Exists(path)) // normal update
			{
				var conf = ReadNoteFromPath(path);
				if (conf.ModificationDate > note.ModificationDate)
				{
					conflict = conf;
					if (strategy == ConflictResolutionStrategy.UseClientCreateConflictFile || strategy == ConflictResolutionStrategy.UseClientVersion)
					{
						WriteNoteToPath(note, path);
						if (note.PathRemote != "") ANFileSystemUtil.DeleteFileAndFolderIfEmpty(FilesystemPlugin.Name, _logger, _config.Folder, note.PathRemote);
						note.PathRemote = path;
						return RemoteUploadResult.Conflict;
					}
					else
					{
						note.PathRemote = path;
						return RemoteUploadResult.Conflict;
					}
				}
				else
				{
					WriteNoteToPath(note, path);
					conflict = null;
					note.PathRemote = path;
					return RemoteUploadResult.Uploaded;
				}
			}
			else // new file
			{
				WriteNoteToPath(note, path);
				conflict = null;
				note.PathRemote = path;
				return RemoteUploadResult.Uploaded;
			}
		}

		public override RemoteDownloadResult UpdateNoteFromRemote(INote inote)
		{
			FilesystemNote note = (FilesystemNote) inote;

			var path = note.GetPath(_config);
			var fi = new FileInfo(path);

			if (!fi.Exists) return RemoteDownloadResult.DeletedOnRemote;

			using(note.SuppressDirtyChanges())
			{
				note.Title = Path.GetFileNameWithoutExtension(path);
				note.Text = File.ReadAllText(path, _config.Encoding);
				note.PathRemote = path;
				note.SetModificationDate(fi.LastWriteTime);
				note.IsLocked = fi.IsReadOnly;
			}

			return RemoteDownloadResult.Updated;
		}

		public override List<string> ListMissingNotes(List<INote> localnotes)
		{
			var remoteNotes = _syncScan.ToList();

			foreach (var lnote in localnotes.Cast<FilesystemNote>())
			{
				var r = remoteNotes.FirstOrDefault(p => p.ToLower() == lnote.PathRemote.ToLower());
				if (r != null) remoteNotes.Remove(r);
			}

			return remoteNotes;
		}

		public override INote DownloadNote(string path, out bool success)
		{
			if (File.Exists(path))
			{
				success = true;
				return ReadNoteFromPath(path);
			}
			else
			{
				success = false;
				return null;
			}
		}

		public override void DeleteNote(INote inote)
		{
			var note = (FilesystemNote) inote;

			if (note.IsConflictNote) return;

			if (File.Exists(note.PathRemote)) ANFileSystemUtil.DeleteFileAndFolderIfEmpty(FilesystemPlugin.Name, _logger, _config.Folder, note.PathRemote);
		}

		private FilesystemNote ReadNoteFromPath(string path)
		{
			var info = new FileInfo(path);

			var note = new FilesystemNote(Guid.NewGuid(), _config);

			using (note.SuppressDirtyChanges())
			{
				note.Title = Path.GetFileNameWithoutExtension(info.FullName);
				note.Path = ANFileSystemUtil.GetDirectoryPath(_config.Folder, info.DirectoryName);
				note.Text  = File.ReadAllText(info.FullName, _config.Encoding);
				note.CreationDate = info.CreationTime;
				note.SetModificationDate(info.LastWriteTime);
				note.PathRemote = info.FullName;
				note.IsLocked = info.IsReadOnly;
			}

			return note;
		}

		private void WriteNoteToPath(FilesystemNote note, string path)
		{
			Directory.CreateDirectory(Path.GetDirectoryName(path));

			FileInfo fileBefore = new FileInfo(path);
			if (fileBefore.Exists && fileBefore.IsReadOnly) fileBefore.IsReadOnly = false;

			File.WriteAllText(path, note.Text);

			new FileInfo(path).IsReadOnly = note.IsLocked;

			note.SetModificationDate(new FileInfo(path).LastWriteTime);
		}
	}
}