﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

using SAModManager.Common;
using SAModManager.Ini;

namespace SAModManager
{
	public class ModInfo
	{
		public string Name { get; set; }
		public string Category { get; set; }
		public string Author { get; set; }
		public string AuthorURL { get; set; }
		public string Version { get; set; }
		public string Description { get; set; }
		public string DLLFile { get; set; }
		public string SourceCode { get; set; }
		public string Codes { get; set; }
		public string GitHubRepo { get; set; }
		public string GitHubAsset { get; set; }
		public string UpdateUrl { get; set; }
		public string ChangelogUrl { get; set; }
		public string GameBananaItemType { get; set; }
		public long? GameBananaItemId { get; set; }
		public string ModID { get; set; }
		[IniName("Dependency")]
		[IniCollection(IniCollectionMode.NoSquareBrackets, StartIndex = 1)]
		public List<string> Dependencies { get; set; }
		public bool DisableUpdate { get; set; }

		public static IEnumerable<string> GetModFiles(DirectoryInfo directoryInfo)
		{
			string modini = Path.Combine(directoryInfo.FullName, "mod.ini");
			if (File.Exists(modini))
			{
				yield return modini;
				yield break;
			}

			foreach (DirectoryInfo item in directoryInfo.GetDirectories())
			{
				if (item.Name[0] == '.')
				{
					continue;
				}

				foreach (string filename in GetModFiles(item))
					yield return filename;
			}
		}
	}


	public class SADXModInfo : ModInfo
	{
		public string CHRMODELSData { get; set; }
		public string ADV00MODELSData { get; set; }
		public string ADV01MODELSData { get; set; }
		public string ADV01CMODELSData { get; set; }
		public string ADV02MODELSData { get; set; }
		public string ADV03MODELSData { get; set; }
		public string BOSSCHAOS0MODELSData { get; set; }
		public string CHAOSTGGARDEN02MR_DAYTIMEData { get; set; }
		public string CHAOSTGGARDEN02MR_EVENINGData { get; set; }
		public string CHAOSTGGARDEN02MR_NIGHTDat { get; set; }
		public string EXEData { get; set; }
		public string EXEFile { get; set; }
		public bool RedirectMainSave { get; set; }
		public bool RedirectChaoSave { get; set; }
		public static List<string> ModCategory { get; set; } = new()
		{
			"Animations",
			"Chao",
			"Custom Level",
			"Cutscene",
			"Game Overhaul",
			"Gameplay",
			"Misc",
			"Music",
			"Patch",
			"Skin",
			"Sound",
			"Textures",
			"UI"
		};

		public static new IEnumerable<string> GetModFiles(DirectoryInfo directoryInfo)
		{
			string modini = Path.Combine(directoryInfo.FullName, "mod.ini");
			if (File.Exists(modini))
			{
				yield return modini;
				yield break;
			}

			foreach (DirectoryInfo item in directoryInfo.GetDirectories())
			{
				if (item.Name.Equals("system", StringComparison.OrdinalIgnoreCase) || item.Name[0] == '.')
				{
					continue;
				}

				foreach (string filename in GetModFiles(item))
					yield return filename;
			}
		}

		public static string[] GetAllVariablesName()
		{
			PropertyInfo[] properties = typeof(SADXModInfo).GetProperties();
			List<String> AllVariables = new();

			foreach (PropertyInfo property in properties)
			{
				AllVariables.Add(property.Name);
			}

			return AllVariables.ToArray();
		}
	}

	public class ModData
	{
		public string Name { get; set; }
		public string Author { get; set; }
		public string AuthorURL { get; set; }
		public string Version { get; set; }
		public string Category { get; set; }
		public string Description { get; set; }
		public string SourceCode { get; set; }
		public bool IsChecked { get; set; }
		public string Tag { get; set; }
	}
	public class CodeData
	{
		public Code codes { get; set; }
		public bool IsChecked { get; set; }
		public bool IsEnabled { get; set; }
    }

	public class PatchesData
	{
		public string Name { get; set; }
		public string Description { get; set; }
		public bool IsChecked { get; set; }

	}
}
