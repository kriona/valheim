using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Keeping watch through the night (see WatchJob), and when the watch's shift is up
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// Keeping watch through the night - walking from doorstep to doorstep around the village and past the fire,
		/// stopping at each to look around
		/// </summary>
		private class WatchJob : Job
		{
			private readonly List<Vector3> stops = new List<Vector3>();
			private int next;
			private float pauseUntil = -1f;
			private float nextLook;

			/// <summary>
			/// When the watch has woken its relief at midnight and waits for it to get up, and whether it has handed the
			/// watch over
			/// </summary>
			private float wakeAt = -1f;
			private bool handedOver;

			/// <summary>
			/// The player whose noise the watch is looking into, where it heard them, until when it looks before giving
			/// up, until when it stands looking around once it has found them or nothing, and when it next listens
			/// </summary>
			private Player heard;
			private Vector3 noiseAt;
			private float investigateUntil = -1f;
			private float lookUntil = -1f;
			private float nextListen;

			/// <summary>
			/// When the watch next mutters about the cold in a biome whose weather freezes, or -1 before it's picked
			/// </summary>
			private float nextMutter = -1f;

			/// <summary>
			/// When the watch next checks its torches will last its shift
			/// </summary>
			private float nextTorchCheck;

			/// <summary>
			/// In freezing weather, where the watch keeps watch from instead of patrolling - in a burning fire's warmth, or
			/// else inside its house - the fire, or null inside, whether it has picked the spot and whether it's there
			/// </summary>
			private Vector3 post;
			private Fireplace postFire;
			private bool hasPost;
			private bool atPost;

			/// <summary>
			/// Describes which stop of its patrol the watch is at or heading for, or where it keeps warm in freezing weather
			/// </summary>
			/// <returns>The description</returns>
			public override string Describe()
			{
				if (hasPost)
				{
					return("Watch " + ((postFire != null) ? "by fire" : "inside") + (atPost ? ", looking around" : ", walking"));
				}
				return("Watch, stop " + (next + 1) + " of " + stops.Count + ((pauseUntil >= 0f) ? ", looking around" : ", walking"));
			}

			/// <summary>
			/// Makes the job
			/// </summary>
			/// <param name="owner">The viking's life</param>
			public WatchJob(VillageLife owner) : base(owner)
			{
			}

			/// <summary>
			/// Lays out the patrol - the doorsteps of each house of the village's core and the fire, leaving out the
			/// outposts, too far out to watch alone at night - starting from the stop nearest the viking
			/// </summary>
			public override void Begin()
			{
				foreach (VillagePaths.House house in life.village.CoreHouses)
				{
					List<Vector3> steps = Village.Doorsteps(house);
					stops.AddRange((steps.Count > 0) ? steps : new List<Vector3> { house.Center + (house.Center - life.village.CoreMiddle).normalized * (house.HalfSize + DoorstepDistance) });
				}
				Fireplace fire = life.village.Fire();
				if (fire != null)
				{
					stops.Add(fire.transform.position + Vector3.forward * FireRing(fire, 1f));
				}
				float nearest = float.MaxValue;
				for (int i = 0; i < stops.Count; i++)
				{
					float distance = Vector3.Distance(stops[i], life.transform.position);
					if (distance < nearest)
					{
						nearest = distance;
						next = i;
					}
				}
			}

			/// <summary>
			/// Walks to the next stop and looks around there for a few seconds - or in freezing weather, keeps watch from a
			/// warm spot instead (see KeepWarm)
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once the night is over, it's no longer the watch, or its torches won't last its shift, so it
			/// can try to craft another (see TorchShort)</returns>
			public override bool Tick(float dt)
			{
				if (!IsNight() || !life.IsWatch() || stops.Count == 0)
				{
					return(true);
				}
				if (HandOver(dt) || Investigate(dt))
				{
					atPost = false;
					return(false);
				}
				// Its torches running short, it stops to craft another (see FindNightTorchWork) and keeps watch again after
				if (Time.time >= nextTorchCheck)
				{
					nextTorchCheck = Time.time + WatchTorchCheck;
					if (Time.time >= life.nightTorchAgainAt && life.TorchShort())
					{
						return(true);
					}
				}
				MutterCold();
				if (KeepWarm(dt))
				{
					return(false);
				}
				if (pauseUntil >= 0f)
				{
					life.ai.StopMoving();
					if (Time.time >= nextLook)
					{
						nextLook = Time.time + Random.Range(1.5f, 3f);
						Vector2 look = Random.insideUnitCircle.normalized;
						life.ai.LookTowards(new Vector3(look.x, 0f, look.y));
					}
					if (Time.time >= pauseUntil)
					{
						pauseUntil = -1f;
						next = (next + 1) % stops.Count;
					}
					return(false);
				}
				if (life.Go(dt, stops[next], 1f, false))
				{
					life.moveFailed = false;
					pauseUntil = Time.time + Random.Range(PatrolPauseMin, PatrolPauseMax);
				}
				return(false);
			}

			/// <summary>
			/// Has the watch mutter to itself about the cold now and then in a biome whose weather freezes (see
			/// VikingGear.FreezesAt), every ColdWatchIntervalMin to ColdWatchIntervalMax seconds, whether or not its gear
			/// keeps the frost off, and not over something else it's saying
			/// </summary>
			private void MutterCold()
			{
				if (!VikingGear.FreezesAt(life.transform.position))
				{
					nextMutter = -1f;
					return;
				}
				if (nextMutter < 0f)
				{
					nextMutter = Time.time + Random.Range(VikingWeatherSpeech.ColdWatchIntervalMin, VikingWeatherSpeech.ColdWatchIntervalMax);
				}
				if (Time.time < nextMutter)
				{
					return;
				}
				nextMutter = Time.time + Random.Range(VikingWeatherSpeech.ColdWatchIntervalMin, VikingWeatherSpeech.ColdWatchIntervalMax);
				if (!VikingSpeech.Chattering(life.humanoid, out string _))
				{
					VikingSpeech.Chatter(life.humanoid, VikingWeatherSpeech.ColdWatchRemark(life.body.HasBeard()), VikingSpeech.TalkTime);
				}
			}

			/// <summary>
			/// Keeps watch from a warm spot while the weather would freeze the watch (see VikingWeather.FreezingFor) - it
			/// walks to its post (see PickPost), picking another when its fire goes out, and stands there looking around,
			/// still listening for noise and handing over at midnight, and goes back to its patrol once the weather clears
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True while it keeps warm, so it doesn't patrol</returns>
			private bool KeepWarm(float dt)
			{
				if (!VikingWeather.FreezingFor(life.humanoid))
				{
					hasPost = false;
					atPost = false;
					return(false);
				}
				if (!hasPost || (postFire != null && !postFire.IsBurning()))
				{
					PickPost();
				}
				if (!hasPost)
				{
					return(false);
				}
				if (atPost && Utils.DistanceXZ(life.transform.position, post) > PushedDistance)
				{
					atPost = false;
				}
				if (!atPost)
				{
					life.mover = "keeping watch somewhere warm";
					if (!life.Go(dt, post, 0.75f, false))
					{
						return(true);
					}
					life.moveFailed = false;
					atPost = true;
				}
				life.ai.StopMoving();
				if (Time.time >= nextLook)
				{
					nextLook = Time.time + Random.Range(1.5f, 3f);
					Vector2 look = Random.insideUnitCircle.normalized;
					life.ai.LookTowards(new Vector3(look.x, 0f, look.y));
				}
				return(true);
			}

			/// <summary>
			/// Picks the watch's warm post - a spot in the warmth of the burning fire it would gather at (see
			/// ChooseGatherFire), clear of the flames, or else a spot on its home's floor clear of the doors (see FloorSpot)
			/// </summary>
			private void PickPost()
			{
				atPost = false;
				hasPost = false;
				Fireplace fire = life.ChooseGatherFire();
				if (fire != null && fire.IsBurning())
				{
					float min = FireRing(fire, 0f);
					float max = FireRing(fire, 1f);
					GatherJob.WarmRing(fire, ref min, ref max);
					Vector3 point = fire.transform.position + Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * Random.Range(min, max);
					post = ((Pathfinding.instance != null && NavmeshPoints.FindValidPoint(out Vector3 valid, point, GatherSnap, Pathfinding.AgentType.Humanoid)) ? valid : point);
					postFire = fire;
					hasPost = true;
					return;
				}
				postFire = null;
				if (life.Home(out VillagePaths.House house))
				{
					post = FloorSpot(house);
					hasPost = true;
				}
			}

			/// <summary>
			/// Looks into a player's noise the watch hears (see HeardPlayer) - calling out, walking over to where it heard
			/// them for up to InvestigateTime seconds, and once it sees them, or gets there and finds no one, saying so and
			/// looking around a moment before going back to its patrol
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True while it's looking into a noise, so it doesn't patrol</returns>
			private bool Investigate(float dt)
			{
				if (investigateUntil < 0f)
				{
					if (Time.time < nextListen)
					{
						return(false);
					}
					nextListen = Time.time + ListenInterval;
					heard = life.HeardPlayer();
					if (heard == null)
					{
						return(false);
					}
					noiseAt = heard.transform.position;
					investigateUntil = Time.time + InvestigateTime;
					lookUntil = -1f;
					life.ai.LookAt(noiseAt + Vector3.up);
					VikingSpeech.Say(life.humanoid, VikingSpeech.HeardNoise(), VikingSpeech.QuickTime, VikingSpeech.TalkCull);
					return(true);
				}
				if (lookUntil >= 0f)
				{
					life.ai.StopMoving();
					if (heard != null && !heard.IsDead() && Utils.DistanceXZ(heard.transform.position, life.transform.position) <= RecognizeRange)
					{
						life.Face(heard.GetEyePoint());
					}
					else if (Time.time >= nextLook)
					{
						nextLook = Time.time + Random.Range(1f, 2f);
						Vector2 look = Random.insideUnitCircle.normalized;
						life.ai.LookTowards(new Vector3(look.x, 0f, look.y));
					}
					if (Time.time >= lookUntil)
					{
						investigateUntil = -1f;
						lookUntil = -1f;
						heard = null;
					}
					return(true);
				}
				life.mover = "looking into a noise";
				// Once it sees who made the noise close by, it knows them or takes them for a traveler
				if (heard != null && !heard.IsDead() && Utils.DistanceXZ(heard.transform.position, life.transform.position) <= RecognizeRange && life.ai.CanSeeTarget(heard))
				{
					life.Face(heard.GetEyePoint());
					VikingSpeech.Say(life.humanoid, VikingSpeech.FoundNoise(heard, VikingSpeech.Knows(life.humanoid, heard.GetPlayerID())), VikingSpeech.TalkTime, VikingSpeech.TalkCull);
					lookUntil = Time.time + InvestigateLook;
					return(true);
				}
				if (Time.time >= investigateUntil || life.Go(dt, noiseAt, NoiseReach, false))
				{
					life.moveFailed = false;
					VikingSpeech.Say(life.humanoid, VikingSpeech.NothingThere(), VikingSpeech.QuickTime, VikingSpeech.TalkCull);
					lookUntil = Time.time + InvestigateLook;
				}
				return(true);
			}

			/// <summary>
			/// Hands the watch over at midnight to the viking it's given to wake (see VillageWatch.ReliefFor) - walking
			/// over to it, waking it with a word, and standing by facing it until it has taken over. When it can't get to
			/// its relief it hands over where it stands
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True while it's handing over, so it doesn't patrol</returns>
			private bool HandOver(float dt)
			{
				ZDOID reliefId = VillageWatch.ReliefFor(life.village, life.nview.GetZDO().m_uid);
				if (reliefId == ZDOID.None)
				{
					return(false);
				}
				GameObject found = ZNetScene.instance.FindInstance(reliefId);
				Character relief = ((found != null) ? found.GetComponent<Character>() : null);
				if (handedOver)
				{
					life.mover = "handing over the watch";
					if (relief != null)
					{
						life.Face(relief.transform.position);
					}
					else
					{
						life.ai.StopMoving();
					}
					return(true);
				}
				// A relief this client doesn't have loaded is left to the watch's change, which comes in time anyway
				if (relief == null)
				{
					return(false);
				}
				if (wakeAt < 0f)
				{
					if (Utils.DistanceXZ(relief.transform.position, life.transform.position) > WakeReach)
					{
						life.mover = "going to wake " + relief.GetHoverName();
						life.Go(dt, relief.transform.position, WakeReach * 0.8f, false);
						if (life.moveFailed)
						{
							life.moveFailed = false;
							handedOver = true;
							VillageWatch.HandedOver(life.humanoid);
						}
						return(true);
					}
					life.Face(relief.transform.position);
					VikingSpeech.Chatter(life.humanoid, VikingSpeech.Address(VikingSpeech.WakeRelief(), VikingNames.Get(relief), "", "", "", ""), VikingSpeech.TalkTime);
					wakeAt = Time.time + WakeTime;
					return(true);
				}
				life.Face(relief.transform.position);
				if (Time.time >= wakeAt)
				{
					handedOver = true;
					VillageWatch.HandedOver(life.humanoid);
				}
				return(true);
			}
		}

		/// <summary>
		/// Works out how long the night watch's shift has left - the first until midnight, and the second until dawn
		/// (see VillageWatch)
		/// </summary>
		/// <returns>The seconds left</returns>
		private static float WatchLeft()
		{
			if (EnvMan.instance == null)
			{
				return(0f);
			}
			return(ShiftLeft(EnvMan.instance.GetDayFraction(), EnvMan.instance.m_dayLengthSec));
		}

		/// <summary>
		/// Works out how long the night watch's shift has left at a time of day - from noon, the first shift's time
		/// until midnight, and from midnight, the second's until dawn, with none left between dawn and noon
		/// </summary>
		/// <param name="fraction">How far through the day it is, from 0 at midnight to 1 at the next</param>
		/// <param name="dayLength">How long a day is, in seconds</param>
		/// <returns>The seconds left</returns>
		internal static float ShiftLeft(float fraction, float dayLength)
		{
			float end = ((fraction < 0.5f) ? 0.25f : 1f);
			return(Mathf.Max(0f, end - fraction) * dayLength);
		}
	}
}
