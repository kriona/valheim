using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Where a village's quest is at - picked and waiting for the morning, its vikings leaving the village, or all of
	/// them gone
	/// </summary>
	internal enum QuestPhase
	{
		None,
		Waiting,
		Leaving,
		Away
	}

	/// <summary>
	/// A village's quest, noted on its ZDO (see VillageQuests) - its ID, what it's for, where it's at, the vikings on it
	/// by their GUIDs (see VikingIdentity) and names, when it set out and when it's due back, in ZNet time ticks, the
	/// spot its vikings leave from and come back to, and whether the village is trapped, so that spot is inside it
	/// </summary>
	internal class QuestRecord
	{
		public string Id = "";
		public QuestKind Kind;
		public string Item = "";
		public QuestPhase Phase;
		public List<string> Members = new List<string>();
		public List<string> Names = new List<string>();
		public long SetOutAt;
		public long Due;
		public Vector3 Exit;
		public bool Trapped;

		/// <summary>
		/// The tools each viking takes, by the same index as Members - a tool's prefab name, or "" for none
		/// </summary>
		public List<string> Tools = new List<string>();

		/// <summary>
		/// The version of how a record is written, which a reader skips a record written another way by
		/// </summary>
		private const int FormatVersion = 1;

		/// <summary>
		/// Gets the tool a viking on the quest takes
		/// </summary>
		/// <param name="member">The viking's GUID</param>
		/// <returns>The tool's prefab name, or "" for none</returns>
		public string ToolOf(string member)
		{
			int index = Members.IndexOf(member);
			return((index >= 0 && index < Tools.Count) ? Tools[index] : "");
		}

		/// <summary>
		/// Gets the names of the others on the quest, for one of them to talk about
		/// </summary>
		/// <param name="own">The name of the one talking</param>
		/// <returns>The names, without theirs and without any empty ones</returns>
		public List<string> Others(string own)
		{
			return(Names.FindAll((string name) => name.Length > 0 && name != own));
		}

		/// <summary>
		/// Writes the record as bytes
		/// </summary>
		/// <returns>The bytes</returns>
		public byte[] Encode()
		{
			ZPackage pkg = new ZPackage();
			pkg.Write(FormatVersion);
			pkg.Write(Id);
			pkg.Write((int)Kind);
			pkg.Write(Item);
			pkg.Write((int)Phase);
			pkg.Write(Members.Count);
			for (int i = 0; i < Members.Count; i++)
			{
				pkg.Write(Members[i]);
				pkg.Write((i < Names.Count) ? Names[i] : "");
				pkg.Write((i < Tools.Count) ? Tools[i] : "");
			}
			pkg.Write(SetOutAt);
			pkg.Write(Due);
			pkg.Write(Exit);
			pkg.Write(Trapped);
			return(pkg.GetArray());
		}

		/// <summary>
		/// Reads a record from the bytes it's written as
		/// </summary>
		/// <param name="bytes">The bytes, or null</param>
		/// <returns>The record, or null when there's none or it can't be read</returns>
		public static QuestRecord Decode(byte[] bytes)
		{
			if (bytes == null || bytes.Length == 0)
			{
				return(null);
			}
			try
			{
				ZPackage pkg = new ZPackage(bytes);
				if (pkg.ReadInt() != FormatVersion)
				{
					return(null);
				}
				QuestRecord record = new QuestRecord();
				record.Id = pkg.ReadString();
				record.Kind = (QuestKind)pkg.ReadInt();
				record.Item = pkg.ReadString();
				record.Phase = (QuestPhase)pkg.ReadInt();
				int count = pkg.ReadInt();
				for (int i = 0; i < count; i++)
				{
					record.Members.Add(pkg.ReadString());
					record.Names.Add(pkg.ReadString());
					record.Tools.Add(pkg.ReadString());
				}
				record.SetOutAt = pkg.ReadLong();
				record.Due = pkg.ReadLong();
				record.Exit = pkg.ReadVector3();
				record.Trapped = pkg.ReadBool();
				return(record);
			}
			catch (System.Exception)
			{
				return(null);
			}
		}
	}
}
