﻿using AlephNote.Common.Themes;
using AlephNote.Common.Util;
using AlephNote.WPF.Dialogs;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using MSHC.WPF.MVVM;

namespace AlephNote.WPF.Windows
{
	public class ThemeEditorViewmodel : ObservableObject
	{
		public class ThemeEditorEntry : ObservableObject
		{
			private string _name = "";
			public string Name { get { return _name; } set { if (_name != value) { _name = value; OnPropertyChanged(); } } }

			private string _source = "";
			public string Source { get { return _source; } set { if (_source != value) { _source = value; OnPropertyChanged(); } } }

			private string _originalSource = "";
			public string OriginalSource { get { return _originalSource; } set { if (_originalSource != value) { _originalSource = value; OnPropertyChanged(); } } }

			private string _sourceFilename = "";
			public string SourceFilename { get { return _sourceFilename; } set { if (_sourceFilename != value) { _sourceFilename = value; OnPropertyChanged(); } } }

			public AlephTheme ParsedTheme = null;
			public AlephThemeSet ParsedSet = null;
		}

		public class ThemeEditorDV : ObservableObject
		{
			private string _key = "";
			public string Key { get { return _key; } set { if (_key != value) { _key = value; OnPropertyChanged(); } } }

			private string _default = "";
			public string Default { get { return _default; } set { if (_default != value) { _default = value; OnPropertyChanged(); } } }

			private string _value = "";
			public string Value { get { return _value; } set { if (_value != value) { _value = value; OnPropertyChanged(); } } }

			private string _typestr = "";
			public string TypeStr { get { return _typestr; } set { if (_typestr != value) { _typestr = value; OnPropertyChanged(); } } }

			private bool _changed = false;
			public bool Changed { get { return _changed; } set { if (_changed != value) { _changed = value; OnPropertyChanged(); } } }
		}

		public ObservableCollection<ThemeEditorEntry> Entries { get; set; } = new ObservableCollection<ThemeEditorEntry>();
		public ObservableCollection<ThemeEditorDV> DefaultValues { get; set; } = new ObservableCollection<ThemeEditorDV>();
		
		private ThemeEditorEntry _selectedEntry = null;
		public ThemeEditorEntry SelectedEntry { get { return _selectedEntry; } set { if (_selectedEntry != value) { _selectedEntry = value; OnPropertyChanged(); PreviewCurrent(); } } }

		private string _errorText = "";
		public string ErrorText { get { return _errorText; } set { if (_errorText != value) { _errorText = value; OnPropertyChanged(); } } }

		public ICommand UndoCommand    { get { return new RelayCommand(UndoCurrent);    } }
		public ICommand SaveCommand    { get { return new RelayCommand(SaveCurrent);    } }
		public ICommand PreviewCommand { get { return new RelayCommand(PreviewCurrent); } }
		public ICommand NewCommand     { get { return new RelayCommand(NewTheme);       } }
		public ICommand ReloadCommand  { get { return new RelayCommand(ReloadCurrent);  } }

		private readonly ThemeEditor Owner;

		public ThemeEditorViewmodel(ThemeEditor owner)
		{
			Owner = owner;
			
			var dt  = ThemeManager.Inst.Cache.GetDefault();

			foreach (var at in ThemeManager.Inst.Cache.GetAllAvailable())
			{
				var newEntry = new ThemeEditorEntry
				{
					SourceFilename = at.SourceFilename,
					Name = at.Name,
					Source = at.Source,
					OriginalSource = at.Source,
					ParsedTheme = at,
					ParsedSet = new AlephThemeSet(dt, at, new AlephTheme[0]),
				};
				Entries.Add(newEntry);
				if (at == ThemeManager.Inst.CurrentBaseTheme) _selectedEntry = newEntry;
			}

			var def = new AlephThemeSet(dt, dt, new AlephTheme[0]);

			foreach (var prop in AlephTheme.THEME_PROPERTIES)
			{
				DefaultValues.Add(new ThemeEditorDV
				{
					Key     = prop.Item1,
					Default = dt.GetXmlStr(prop.Item1.ToLower()),
					TypeStr = prop.Item2.ToString(),
					Value   = SelectedEntry?.ParsedSet?.GetStrRepr(prop.Item1),
					Changed = SelectedEntry?.ParsedSet?.GetStrRepr(prop.Item1) != def?.GetStrRepr(prop.Item1)
				});
			}
		}

		private void UndoCurrent()
		{
			ErrorText = "";
			if (SelectedEntry == null) return;

			SelectedEntry.Source = SelectedEntry.OriginalSource;
		}

		private void SaveCurrent()
		{
			ErrorText = "";

			if (SelectedEntry == null) return;

			File.WriteAllText(Path.Combine(ThemeManager.Inst.Cache.BasePath, SelectedEntry.SourceFilename), SelectedEntry.Source, Encoding.UTF8);
			SelectedEntry.OriginalSource = SelectedEntry.Source;

			UpdateSelected();
		}

		private void ReloadCurrent()
		{
			ErrorText = "";
			if (SelectedEntry == null) return;

			SelectedEntry.OriginalSource = SelectedEntry.Source = File.ReadAllText(Path.Combine(ThemeManager.Inst.Cache.BasePath, SelectedEntry.SourceFilename));

			UpdateSelected();
		}

		private void PreviewCurrent()
		{
			ErrorText = "";
			if (SelectedEntry == null) return;

			try
			{
				var parser = new ThemeParser();
				parser.LoadFromString(SelectedEntry.Source, SelectedEntry.SourceFilename);
				parser.Parse();
				var theme = parser.Generate();

				ThemeManager.Inst.Cache.ReplaceTheme(theme);

				ThemeManager.Inst.ChangeTheme(theme.SourceFilename, new string[0]);

				UpdateSelected();
			}
			catch (Exception e)
			{
				ErrorText = e.ToString();
			}
		}

		private void NewTheme()
		{
			ErrorText = "";
			try
			{
				if (!GenericInputDialog.ShowInputDialog(Owner, "Filename for new theme", "New Theme (filename)", "MyTheme.xml", out var filename)) throw new Exception("Aborted by user");

				if (!filename.ToLower().EndsWith(".xml")) throw new Exception("Filename must end with xml");
				if (Entries.Any(e => e.SourceFilename.ToLower() == filename.ToLower())) throw new Exception("Filename already exists");

				var newEntry = new ThemeEditorEntry()
				{
					OriginalSource = "",
					SourceFilename = filename,
					Name = "New Theme",
					Source = "",
				};

				Entries.Add(newEntry);
				SelectedEntry = newEntry;
			}
			catch (Exception e)
			{
				ErrorText = e.ToString();
			}
		}

		private void UpdateSelected()
		{
			if (SelectedEntry == null) return;
			try
			{
				var def = ThemeManager.Inst.Cache.GetDefaultOrFallback();
				var defset = new AlephThemeSet(def, def, new AlephTheme[0]);

				var parser = new ThemeParser();
				parser.LoadFromString(SelectedEntry.Source, SelectedEntry.SourceFilename);
				parser.Parse();
				var theme = parser.Generate();
				var set   = new AlephThemeSet(def, theme, new AlephTheme[0]);

				SelectedEntry.Name        = theme.Name;
				SelectedEntry.ParsedSet   = set;
				SelectedEntry.ParsedTheme = theme;

				foreach (var dv in DefaultValues)
				{
					dv.Value   = set.GetStrRepr(dv.Key);
					dv.Changed = set.GetStrRepr(dv.Key) != defset.GetStrRepr(dv.Key);
				}
			}
			catch (Exception e)
			{
				ErrorText = e.ToString();
			}
		}
	}
}
