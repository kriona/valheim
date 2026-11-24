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
	/// What one viking does on a quest, worked out as the quest sets out (see QuestGame.Prepare) - what it brings home,
	/// the tool it works with and the durability that tool has left after, how many times it swings it, the enemies it
	/// kills, and what it takes along to spend there, like treasure to sell
	/// </summary>
	internal class MemberPlan
	{
		public List<KeyValuePair<string, int>> Loot = new List<KeyValuePair<string, int>>();
		public string Tool = "";
		public float ToolLeft = -1f;
		public int Hits;
		public int Kills;
		public List<KeyValuePair<string, int>> Spend = new List<KeyValuePair<string, int>>();

		/// <summary>
		/// Writes the plan to a package
		/// </summary>
		/// <param name="pkg">The package</param>
		public void Write(ZPackage pkg)
		{
			QuestRecord.WriteItems(pkg, Loot);
			pkg.Write(Tool);
			pkg.Write(ToolLeft);
			pkg.Write(Hits);
			pkg.Write(Kills);
			QuestRecord.WriteItems(pkg, Spend);
		}

		/// <summary>
		/// Reads a plan from a package
		/// </summary>
		/// <param name="pkg">The package</param>
		/// <returns>The plan</returns>
		public static MemberPlan Read(ZPackage pkg)
		{
			MemberPlan plan = new MemberPlan();
			plan.Loot = QuestRecord.ReadItems(pkg);
			plan.Tool = pkg.ReadString();
			plan.ToolLeft = pkg.ReadSingle();
			plan.Hits = pkg.ReadInt();
			plan.Kills = pkg.ReadInt();
			plan.Spend = QuestRecord.ReadItems(pkg);
			return(plan);
		}
	}

	/// <summary>
	/// A village's quest, noted on its ZDO (see VillageQuests) - its ID, what it's for and how many of it, where it's
	/// at, the vikings on it by their GUIDs (see VikingIdentity) and names, when it set out and when it's due back, in
	/// ZNet time ticks, the spot its vikings leave from and come back to, whether the village is trapped, so that spot is
	/// inside it, how long the work there takes, in seconds, the quest's biome by VikingGear's order, how many vikings it
	/// takes, and what each viking does on it (see MemberPlan)
	/// </summary>
	internal class QuestRecord
	{
		public string Id = "";
		public QuestKind Kind;
		public string Item = "";
		public int Amount;
		public QuestPhase Phase;
		public List<string> Members = new List<string>();
		public List<string> Names = new List<string>();
		public long SetOutAt;
		public long Due;
		public Vector3 Exit;
		public bool Trapped;
		public float ActiveSeconds;
		public int QuestBiome;
		public int Wanted;

		/// <summary>
		/// The tools each viking takes from a chest, by the same index as Members - a tool's prefab name, or "" for none
		/// </summary>
		public List<string> Tools = new List<string>();

		/// <summary>
		/// What each viking does on the quest, by the same index as Members, once it has set out
		/// </summary>
		public List<MemberPlan> Plans = new List<MemberPlan>();

		/// <summary>
		/// The version of how a record is written, which a reader skips a record written another way by
		/// </summary>
		private const int FormatVersion = 2;

		/// <summary>
		/// Gets the tool a viking on the quest takes from a chest
		/// </summary>
		/// <param name="member">The viking's GUID</param>
		/// <returns>The tool's prefab name, or "" for none</returns>
		public string ToolOf(string member)
		{
			int index = Members.IndexOf(member);
			return((index >= 0 && index < Tools.Count) ? Tools[index] : "");
		}

		/// <summary>
		/// Gets what a viking on the quest does on it
		/// </summary>
		/// <param name="member">The viking's GUID</param>
		/// <returns>The plan, or an empty one when none was worked out</returns>
		public MemberPlan PlanOf(string member)
		{
			int index = Members.IndexOf(member);
			return((index >= 0 && index < Plans.Count) ? Plans[index] : new MemberPlan());
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
		/// Writes a list of items and how many of each to a package
		/// </summary>
		/// <param name="pkg">The package</param>
		/// <param name="items">The items</param>
		internal static void WriteItems(ZPackage pkg, List<KeyValuePair<string, int>> items)
		{
			pkg.Write(items.Count);
			foreach (KeyValuePair<string, int> item in items)
			{
				pkg.Write(item.Key);
				pkg.Write(item.Value);
			}
		}

		/// <summary>
		/// Reads a list of items and how many of each from a package
		/// </summary>
		/// <param name="pkg">The package</param>
		/// <returns>The items</returns>
		internal static List<KeyValuePair<string, int>> ReadItems(ZPackage pkg)
		{
			List<KeyValuePair<string, int>> items = new List<KeyValuePair<string, int>>();
			int count = pkg.ReadInt();
			for (int i = 0; i < count; i++)
			{
				string name = pkg.ReadString();
				items.Add(new KeyValuePair<string, int>(name, pkg.ReadInt()));
			}
			return(items);
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
			pkg.Write(Amount);
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
			pkg.Write(ActiveSeconds);
			pkg.Write(QuestBiome);
			pkg.Write(Wanted);
			pkg.Write(Plans.Count);
			foreach (MemberPlan plan in Plans)
			{
				plan.Write(pkg);
			}
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
				record.Amount = pkg.ReadInt();
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
				record.ActiveSeconds = pkg.ReadSingle();
				record.QuestBiome = pkg.ReadInt();
				record.Wanted = pkg.ReadInt();
				int plans = pkg.ReadInt();
				for (int i = 0; i < plans; i++)
				{
					record.Plans.Add(MemberPlan.Read(pkg));
				}
				return(record);
			}
			catch (System.Exception)
			{
				return(null);
			}
		}
	}

	/// <summary>
	/// How a quest went for one viking, noted on it as it leaves (see QuestGame.OutcomeFor) and played out as it comes
	/// home (see VillageLife.ReturnFromQuest) - the quest, what it was for, the others on it, what it brings home, the
	/// tool it worked with and the durability that tool has left, what it ate, drank and spent of what it carried, the
	/// share of its health it comes home at, how much each of its skills rose, the enemies it killed, when the quest set
	/// out, in ZNet time ticks, and the name of the village it set out from
	/// </summary>
	internal class QuestOutcome
	{
		public QuestKind Kind;
		public string Item = "";
		public List<string> Companions = new List<string>();
		public List<KeyValuePair<string, int>> Loot = new List<KeyValuePair<string, int>>();
		public string Tool = "";
		public float ToolLeft = -1f;
		public List<KeyValuePair<string, int>> Used = new List<KeyValuePair<string, int>>();

		/// <summary>
		/// The share of its health the viking comes home at, or 0 when it died on the quest and never comes home - one
		/// that comes home has QuestSim.HealthFloor at least
		/// </summary>
		public float HealthShare = 1f;

		/// <summary>
		/// Whether the viking died on the quest (see HealthShare)
		/// </summary>
		public bool Died
		{
			get
			{
				return(HealthShare <= 0f);
			}
		}

		public List<KeyValuePair<int, float>> Skills = new List<KeyValuePair<int, float>>();
		public int Kills;
		public long SetOutAt;
		public string Village = "";

		/// <summary>
		/// The version of how an outcome is written, which a reader skips one written another way by
		/// </summary>
		private const int FormatVersion = 1;

		/// <summary>
		/// Writes the outcome as bytes
		/// </summary>
		/// <returns>The bytes</returns>
		public byte[] Encode()
		{
			ZPackage pkg = new ZPackage();
			pkg.Write(FormatVersion);
			pkg.Write((int)Kind);
			pkg.Write(Item);
			pkg.Write(Companions.Count);
			foreach (string name in Companions)
			{
				pkg.Write(name);
			}
			QuestRecord.WriteItems(pkg, Loot);
			pkg.Write(Tool);
			pkg.Write(ToolLeft);
			QuestRecord.WriteItems(pkg, Used);
			pkg.Write(HealthShare);
			pkg.Write(Skills.Count);
			foreach (KeyValuePair<int, float> skill in Skills)
			{
				pkg.Write(skill.Key);
				pkg.Write(skill.Value);
			}
			pkg.Write(Kills);
			pkg.Write(SetOutAt);
			pkg.Write(Village);
			return(pkg.GetArray());
		}

		/// <summary>
		/// Reads an outcome from the bytes it's written as
		/// </summary>
		/// <param name="bytes">The bytes, or null</param>
		/// <returns>The outcome, or null when there's none or it can't be read</returns>
		public static QuestOutcome Decode(byte[] bytes)
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
				QuestOutcome outcome = new QuestOutcome();
				outcome.Kind = (QuestKind)pkg.ReadInt();
				outcome.Item = pkg.ReadString();
				int companions = pkg.ReadInt();
				for (int i = 0; i < companions; i++)
				{
					outcome.Companions.Add(pkg.ReadString());
				}
				outcome.Loot = QuestRecord.ReadItems(pkg);
				outcome.Tool = pkg.ReadString();
				outcome.ToolLeft = pkg.ReadSingle();
				outcome.Used = QuestRecord.ReadItems(pkg);
				outcome.HealthShare = pkg.ReadSingle();
				int skills = pkg.ReadInt();
				for (int i = 0; i < skills; i++)
				{
					int skill = pkg.ReadInt();
					outcome.Skills.Add(new KeyValuePair<int, float>(skill, pkg.ReadSingle()));
				}
				outcome.Kills = pkg.ReadInt();
				outcome.SetOutAt = pkg.ReadLong();
				outcome.Village = pkg.ReadString();
				return(outcome);
			}
			catch (System.Exception)
			{
				return(null);
			}
		}
	}
}
