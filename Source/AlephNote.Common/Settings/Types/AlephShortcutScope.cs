﻿using MSHC.Lang.Attributes;

namespace AlephNote.Common.Settings.Types
{
	public enum AlephShortcutScope
	{
		[EnumDescriptor("", false)]
		None,

		[EnumDescriptor("Whole Window")]
		Window,

		[EnumDescriptor("Notes list")]
		NoteList,

		[EnumDescriptor("Folder list")]
		FolderList,

		[EnumDescriptor("Notes edit area")]
		NoteEdit,

		[EnumDescriptor("System global")]
		Global,
	}
}