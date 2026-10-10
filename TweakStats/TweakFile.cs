using System;
using System.Collections.Generic;

namespace TweakStats
{
	/// <summary>
	/// One "stat = value" line of the config file
	/// </summary>
	public class TweakLine
	{
		public int LineNumber;
		public string Path;
		public TweakValue Value;
	}

	/// <summary>
	/// A [section] of the config file and the lines under it
	/// </summary>
	public class TweakSection
	{
		public int LineNumber;

		/// <summary>
		/// The text between the brackets, for messages
		/// </summary>
		public string Header;

		/// <summary>
		/// The comma-separated parts of the header, e.g. SwordIron, Skill:Swords or Recipe:Sword*
		/// </summary>
		public List<string> Selectors = new List<string>();

		/// <summary>
		/// The worlds the section applies on, from the [Worlds: ...] group it is in, or null when it applies everywhere
		/// </summary>
		public List<string> Worlds;

		public List<TweakLine> Lines = new List<TweakLine>();
	}

	/// <summary>
	/// The config file, read into sections
	/// </summary>
	public class TweakFile
	{
		public List<TweakSection> Sections = new List<TweakSection>();

		/// <summary>
		/// Lines that couldn't be read, as messages that start with the line number
		/// </summary>
		public List<string> Problems = new List<string>();

		/// <summary>
		/// Reads the config file's text
		/// </summary>
		/// <param name="text">The file's contents</param>
		/// <returns>The sections read, with a problem for each line that couldn't be read</returns>
		public static TweakFile Parse(string text)
		{
			TweakFile file = new TweakFile();
			TweakSection section = null;
			List<string> worlds = null;
			int worldsLine = 0;
			string[] lines = text.Split('\n');
			for (int i = 0; i < lines.Length; i++)
			{
				int lineNumber = i + 1;
				string line = StripComment(lines[i]).Trim();
				if (line == "")
				{
					continue;
				}
				if (line.StartsWith("["))
				{
					if (!line.EndsWith("]"))
					{
						file.AddProblem(lineNumber, "\"" + line + "\" starts with [ but doesn't end with ]");
						section = null;
						continue;
					}
					string header = line.Substring(1, line.Length - 2).Trim();
					if (IsWorldsEnd(header))
					{
						if (worlds == null)
						{
							file.AddProblem(lineNumber, "[" + header + "] doesn't have a [Worlds: ...] before it");
						}
						worlds = null;
						section = null;
					}
					else if (TryReadWorlds(header, out List<string> names))
					{
						if (worlds != null)
						{
							file.AddProblem(lineNumber, "[Worlds: ...] groups can't be inside each other - add [/Worlds] to end the group on line " + worldsLine + " first");
						}
						if (names.Count == 0)
						{
							file.AddProblem(lineNumber, "[" + header + "] doesn't name any worlds");
						}
						worlds = names;
						worldsLine = lineNumber;
						section = null;
					}
					else
					{
						section = new TweakSection
						{
							LineNumber = lineNumber,
							Header = header,
							Selectors = SplitList(header),
							Worlds = worlds
						};
						if (section.Selectors.Count == 0)
						{
							file.AddProblem(lineNumber, "[] needs the name of what to change, e.g. [SwordIron]");
							section = null;
							continue;
						}
						file.Sections.Add(section);
					}
					continue;
				}
				int equals = line.IndexOf('=');
				if (equals < 0)
				{
					file.AddProblem(lineNumber, "\"" + line + "\" needs to be stat = value");
					continue;
				}
				if (section == null)
				{
					file.AddProblem(lineNumber, "\"" + line + "\" needs a [section] above it saying what it changes, e.g. [SwordIron]");
					continue;
				}
				string path = line.Substring(0, equals).Trim();
				if (path == "")
				{
					file.AddProblem(lineNumber, "\"" + line + "\" needs the name of a stat before the =");
					continue;
				}
				if (!TweakValue.TryParse(line.Substring(equals + 1), out TweakValue value, out string error))
				{
					file.AddProblem(lineNumber, path + ": " + error);
					continue;
				}
				section.Lines.Add(new TweakLine
				{
					LineNumber = lineNumber,
					Path = path,
					Value = value
				});
			}
			if (worlds != null)
			{
				file.AddProblem(worldsLine, "[Worlds: ...] has no [/Worlds] to end it, so it runs to the end of the file");
			}
			return(file);
		}

		/// <summary>
		/// Adds a message about a line that couldn't be read
		/// </summary>
		/// <param name="lineNumber">The line's number</param>
		/// <param name="message">What's wrong with it</param>
		private void AddProblem(int lineNumber, string message)
		{
			Problems.Add("line " + lineNumber + ": " + message);
		}

		/// <summary>
		/// Removes a comment from a line - a whole line starting with # or ;, or a # with a space before it
		/// </summary>
		/// <param name="line">The line</param>
		/// <returns>The line without its comment</returns>
		private static string StripComment(string line)
		{
			string trimmed = line.TrimStart();
			if (trimmed.StartsWith("#") || trimmed.StartsWith(";"))
			{
				return("");
			}
			for (int i = 1; i < line.Length; i++)
			{
				if (line[i] == '#' && char.IsWhiteSpace(line[i - 1]))
				{
					return(line.Substring(0, i));
				}
			}
			return(line);
		}

		/// <summary>
		/// Whether a header ends a group of worlds
		/// </summary>
		/// <param name="header">The text between the brackets</param>
		/// <returns>True for /Worlds or /World</returns>
		private static bool IsWorldsEnd(string header)
		{
			return(header.Equals("/Worlds", StringComparison.OrdinalIgnoreCase) || header.Equals("/World", StringComparison.OrdinalIgnoreCase));
		}

		/// <summary>
		/// Reads the world names from a header that starts a group of worlds
		/// </summary>
		/// <param name="header">The text between the brackets</param>
		/// <param name="names">The world names</param>
		/// <returns>True when the header starts with Worlds: or World:</returns>
		private static bool TryReadWorlds(string header, out List<string> names)
		{
			names = null;
			int colon = header.IndexOf(':');
			if (colon < 0)
			{
				return(false);
			}
			string kind = header.Substring(0, colon).Trim();
			if (!kind.Equals("Worlds", StringComparison.OrdinalIgnoreCase) && !kind.Equals("World", StringComparison.OrdinalIgnoreCase))
			{
				return(false);
			}
			names = SplitList(header.Substring(colon + 1));
			return(true);
		}

		/// <summary>
		/// Splits a comma-separated list, leaving out empty entries
		/// </summary>
		/// <param name="text">The list</param>
		/// <returns>The trimmed entries</returns>
		private static List<string> SplitList(string text)
		{
			List<string> entries = new List<string>();
			foreach (string entry in text.Split(','))
			{
				string trimmed = entry.Trim();
				if (trimmed != "")
				{
					entries.Add(trimmed);
				}
			}
			return(entries);
		}
	}
}
