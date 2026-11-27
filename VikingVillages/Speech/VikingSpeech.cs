using System.Collections.Generic;
using System.Text.RegularExpressions;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// What PC vikings say - words over their heads that every client nearby sees, the way the game shows a
	/// trader's - and the lines they pick from: the night watch's alarm, a call when one spots an enemy, a badly hurt
	/// viking's shout for help, a greeting for a player arriving - as a stranger until it has met them, which it
	/// remembers - what it says when a player talks to it, the talks they have with each other, and what they say by the
	/// corpse of one of their village
	/// </summary>
	/// <remarks>
	/// Every line here is listed in the mod's DIALOG.md, which changes along with them
	/// </remarks>
	internal static class VikingSpeech
	{
		/// <summary>
		/// The routed RPC that shows a viking's words, with the viking, what it says, how long it shows and how far away
		/// it can be seen
		/// </summary>
		public const string SayRpc = Plugin.PluginGuid + ".VikingSay";

		/// <summary>
		/// The routed RPC that shows what a viking says to another viking, under its health bar rather than over its head,
		/// with the viking, what it says and how long it shows
		/// </summary>
		public const string ChatterRpc = Plugin.PluginGuid + ".VikingChatter";

		/// <summary>
		/// What each viking on this client is saying to another viking, and until when in Time.time seconds
		/// </summary>
		private static readonly Dictionary<Character, KeyValuePair<string, float>> chatter = new Dictionary<Character, KeyValuePair<string, float>>();

		/// <summary>
		/// Until when each viking this client owns says nothing over a line that has to be seen, and when each last said
		/// how proud it is of a skill, in Time.time seconds
		/// </summary>
		private static readonly Dictionary<Character, float> heldUntil = new Dictionary<Character, float>();
		private static readonly Dictionary<Character, float> proudAt = new Dictionary<Character, float>();

		/// <summary>
		/// How a viking says it's proud of a skill - how long the line shows, and the least time between two, in seconds,
		/// and how near a player it always says it, in meters (see ProudChance)
		/// </summary>
		private const float ProudTime = 5f;
		private const float ProudInterval = 30f;
		private const float ProudNear = 10f;

		/// <summary>
		/// What a viking says as it reaches a new level of a skill, with the skill's name and its new level
		/// </summary>
		private static readonly string[] ProudLines =
		{
			"Ha! I'm getting better with {0}!",
			"Did you see that? My {0} is improving!",
			"Odin smiles on my {0} today.",
			"Level {1} in {0}! Not bad at all.",
			"I'm finally getting the hang of {0}!",
			"The skalds will sing of my {0} one day."
		};

		/// <summary>
		/// How long words show and how far away they can be seen - for talk and greetings, a quick remark, a call when an
		/// enemy is spotted, a raid is noticed or for help, and the night watch's alarm, in seconds and meters. Calls and
		/// alarms show longest, held over anything else the viking would say (see SayFirst), so they aren't missed
		/// </summary>
		public const float TalkTime = 6f;
		public const float TalkCull = 20f;
		public const float QuickTime = 3f;
		public const float CallTime = 8f;
		public const float CallCull = 40f;
		public const float AlarmTime = 10f;
		public const float AlarmCull = 60f;

		/// <summary>
		/// The night watch's alarm as it wakes the village to a raid, and what a viking awake calls out as a raid comes
		/// on by day
		/// </summary>
		private static readonly string[] RaidAlarmLines =
		{
			"A raid! Everyone up!",
			"They're coming for the village! On your feet!",
			"Wake up! We're under attack!",
			"Up! Up! The horde is coming!",
			"To arms! A raid on {village}!"
		};
		private static readonly string[] RaidCallLines =
		{
			"Raid! Weapons ready!",
			"Here they come - stand together!",
			"Everyone, to arms! A raid!",
			"Hold the line! They're coming!",
			"Defend {village}! Here they come!"
		};

		/// <summary>
		/// The night watch's alarm as it wakes the village, with the enemy it spotted
		/// </summary>
		private static readonly string[] AlarmLines =
		{
			"Wake up! {0} at the village!",
			"To arms! {0}!",
			"Up, all of you! {0} coming!",
			"Grab your axes! {0}!",
			"{0}! Everyone up!",
			"On your feet! {0} at the fence!",
			"{0} in {village}! Everyone up!",
			"Defend {village}! {0}!"
		};

		/// <summary>
		/// A viking's call as it spots an enemy, with the enemy
		/// </summary>
		private static readonly string[] SpotLines =
		{
			"{0}!",
			"Watch out - {0}!",
			"{0}, over there!",
			"Look sharp - {0}!",
			"Enemy! {0}!",
			"Weapons up - {0}!"
		};

		/// <summary>
		/// What the night watch says as it wakes the viking taking over at midnight, with "{name}" where it names it, and
		/// what the viking it woke says as it gets up
		/// </summary>
		private static readonly string[] WakeLines =
		{
			"Wake up, {name}. Your watch.",
			"Your turn on watch, {name}. Keep your eyes open.",
			"Up you get, {name} - the watch is yours.",
			"Your watch, {name}. It's been quiet... mostly."
		};
		private static readonly string[] WokenLines =
		{
			"Mmph... I'm up, I'm up.",
			"Already? Fine.",
			"Get some sleep. I've got it.",
			"Right. Off to bed with you."
		};

		/// <summary>
		/// What the night watch says as it hears a noise and goes to look, as it finds a player it knows, with their name,
		/// or one it doesn't, and as it finds nothing
		/// </summary>
		private static readonly string[] HeardLines =
		{
			"Hm? Who's there?",
			"What was that?",
			"Someone out there? Show yourself!",
			"I heard something...",
			"Who goes there?"
		};
		private static readonly string[] FoundKnownLines =
		{
			"Oh, it's only you, {0}.",
			"{0}! You nearly got an axe in the back.",
			"Out late, {0}? Watch yourself.",
			"Just {0}. Don't sneak up on me like that."
		};
		private static readonly string[] FoundStrangerLines =
		{
			"Just a traveler. Mind the dark.",
			"Who are you? ...A friend, I hope.",
			"Living, at least. Keep your voice down out here."
		};
		private static readonly string[] NothingLines =
		{
			"Nothing... must be the wind.",
			"Hm. Nothing there.",
			"Probably a hare. Back to it.",
			"Jumping at shadows again..."
		};

		/// <summary>
		/// What a viking says as it takes the place of one keeping watch that it saw die
		/// </summary>
		private static readonly string[] TakeWatchLines =
		{
			"The watch has fallen! I'll take over.",
			"They got the watch! I'll keep it now.",
			"I'll stand watch. Rest easy, friend.",
			"No one else falls tonight. The watch is mine."
		};

		/// <summary>
		/// A badly hurt viking's shout for help
		/// </summary>
		private static readonly string[] HelpLines =
		{
			"Help!",
			"To me!",
			"I need help here!",
			"Help me!"
		};

		/// <summary>
		/// A viking's greeting for a player it knows, with the player's name - by day and by night
		/// </summary>
		private static readonly string[] DayGreetings =
		{
			"Hail, {0}!",
			"Well met, {0}!",
			"Good to see you again, {0}.",
			"Welcome back, {0}!",
			"{0}! Come, warm yourself by the fire.",
			"Hail, {0}! Any news from the wilds?",
			"Welcome back to {village}, {0}!"
		};
		private static readonly string[] NightGreetings =
		{
			"Late to be out, {0}. Stay by the fire.",
			"Hail, {0}. Watch the dark tonight.",
			"{0}! Get inside the fence, quick.",
			"Welcome back, {0}. The dead are restless tonight.",
			"{0}! {village} keeps a fire burning for you."
		};

		/// <summary>
		/// What a viking says when a player names it, with its name - the first time, and when it had one before
		/// </summary>
		private static readonly string[] NamedLines =
		{
			"{0}? I like it.",
			"{0} it is!",
			"{0}... yes, that suits me.",
			"{0}. A name worthy of a saga!"
		};
		private static readonly string[] RenamedLines =
		{
			"{0}? Fine, call me {0}.",
			"{0} now, is it? I'll get used to it.",
			"Make up your mind! {0}, then."
		};

		/// <summary>
		/// What a viking says when a player tries to name it Viking or clear its name, which it keeps
		/// </summary>
		private static readonly string[] NamelessRefusals =
		{
			"Viking? That's what I am, not who I am. I'll keep my name.",
			"I already have a name, and I'm keeping it!",
			"Go nameless? Not a chance.",
			"A Viking with no name? Not this one."
		};

		/// <summary>
		/// What a viking says when a player tries to name it after a creature, a legend - a god, a boss or someone the
		/// players meet - a place - a biome or somewhere from the lore - or someone else in the world or the village,
		/// with the name, and when a player tries to name it or its village with text markup, with what they typed
		/// </summary>
		private static readonly string[] CreatureRefusals =
		{
			"{0}? Do I look that ugly to you?",
			"Ha! Call me {0} and the others will chase me out of the village.",
			"{0}? I'd rather not be mistaken for one, thank you.",
			"{0}? Next you'll want me to growl at you."
		};
		private static readonly string[] LegendRefusals =
		{
			"{0}? I could never live up to that name.",
			"{0}? The gods would strike me down for the cheek of it!",
			"Ha! There's only one {0}, and it isn't me.",
			"{0}? And have the skalds laugh at me? No."
		};
		private static readonly string[] PlaceRefusals =
		{
			"{0}? I'm a Viking, not a place on a map!",
			"{0}? Then travelers would come looking for me.",
			"{0}? That's somewhere you go, not someone you are.",
			"Call me {0}? The others would never let me hear the end of it."
		};
		private static readonly string[] SomeoneRefusals =
		{
			"{0}? That'd be too confusing.",
			"There's already a {0} around here - pick another!",
			"We can't have two of {0}. Who'd come when you called?",
			"{0}? Then we'd both turn around every time someone shouts it."
		};
		private static readonly string[] MarkupRefusals =
		{
			"{0} doesn't sound like a name."
		};

		/// <summary>
		/// What a viking says when a player tries to name its village after a place - a biome or somewhere from the lore -
		/// with the name
		/// </summary>
		private static readonly string[] VillagePlaceRefusals =
		{
			"Call our village {0}? Travelers would think they'd wandered into the wrong place.",
			"{0}? The skalds would never stop laughing at us.",
			"We're a village, not a corner of the map! Not {0}.",
			"{0}? Everyone would get lost looking for it."
		};

		/// <summary>
		/// What a viking says when a player tries to name its village after a legend - a god, a beast of the myths, a
		/// boss or the like - with the name
		/// </summary>
		private static readonly string[] VillageLegendRefusals =
		{
			"Name our village {0}? That's tempting fate.",
			"{0}? We'd never live it down if word got back to them.",
			"A village called {0}? Every skald from here to the Mistlands would mock us.",
			"{0}? Some names are too big for a few houses and a fire."
		};

		/// <summary>
		/// What a viking says when a player tries to leave its village without a name, and when they try to call it just
		/// Village, with the name as they typed it
		/// </summary>
		private static readonly string[] VillageNamelessRefusals =
		{
			"No name at all? We'd be just a few houses in the woods again.",
			"A village needs a name, friend. Give us a real one.",
			"Nameless? How would travelers ever find us?"
		};
		private static readonly string[] VillageGenericRefusals =
		{
			"{0}? Every village is a village. Give us a real name.",
			"Call it {0}? Then what would we call all the others?",
			"{0}... and the river can be River, I suppose?"
		};

		/// <summary>
		/// What a viking says when a player names its village, with the name - the first time, and when it had one
		/// before
		/// </summary>
		private static readonly string[] VillageNamedLines =
		{
			"{0}! Now that's a name to put on a map.",
			"{0}... I'll drink to that tonight.",
			"{0}. Has a ring to it, doesn't it?",
			"Finally, a name! {0} it is."
		};
		private static readonly string[] VillageRenamedLines =
		{
			"{0}? I'd only just got used to the old name.",
			"{0} now, is it? The skalds will have to change their songs.",
			"Again? Fine, {0} it is."
		};

		/// <summary>
		/// What a viking says when a player shares their forsaken power with it, where {0} is the power's name and {1}
		/// the player's, or what it calls a player it doesn't know (see WithStranger)
		/// </summary>
		private static readonly string[] PowerLines =
		{
			"{0}'s gift! I can feel it, {1}!",
			"By {0}! I could wrestle a troll.",
			"Thank you, {1} - {0} smiles on us all.",
			"So this is {0}'s power... I feel ten years younger.",
			"{1} shares {0}'s blessing! Skål!",
			"Ha! With {0} behind us, let them come!"
		};

		/// <summary>
		/// What a viking says as it uses a forsaken power, where {0} is the power's name
		/// </summary>
		private static readonly string[] PowerUseLines =
		{
			"{0}, lend me your power!",
			"By {0}'s might!",
			"I call on {0}!",
			"{0}, stand with us!",
			"Feel the power of {0}!",
			"{0}'s blessing on us all!"
		};

		/// <summary>
		/// What a viking says as it uses a forsaken power to teach it to another, where {0} is the power's name and
		/// {name} the other viking's (see Address)
		/// </summary>
		private static readonly string[] PowerTeachLines =
		{
			"{name}, feel {0}'s power for yourself!",
			"Here, {name} - {0}'s gift is yours now too.",
			"Remember this, {name} - this is {0}'s blessing.",
			"{0}'s power, {name}. Pass it on."
		};

		/// <summary>
		/// A viking's greeting for a player it has never met - by day and by night
		/// </summary>
		private static readonly string[] DayFirstGreetings =
		{
			"Hail, stranger!",
			"Well met, traveler. I haven't seen you before.",
			"A new face! Welcome to our village.",
			"Hail, traveler. You're not one of the dead, are you?",
			"Hail! Come in, come in - any friend of the living is welcome here.",
			"A new face! Welcome to {village}.",
			"Hail, traveler, and welcome to {village}."
		};
		private static readonly string[] NightFirstGreetings =
		{
			"Who goes there? Oh - a traveler. Come in by the fire.",
			"Hail, stranger. Don't linger in the dark.",
			"A traveler, at this hour? Quick, inside the fence.",
			"Welcome to {village}, stranger. Stay close to the fire."
		};

		/// <summary>
		/// How a viking with a name tells it to a player it has never met, after its greeting, with its name
		/// </summary>
		private static readonly string[] Introductions =
		{
			"I'm {0}.",
			"I'm called {0}.",
			"They call me {0}."
		};

		/// <summary>
		/// What a viking says when a player it knows talks to it, with the player's name - by day and by night - and
		/// when it's asleep or in a fight
		/// </summary>
		private static readonly string[] DayChat =
		{
			"What is it, {0}?",
			"Need something, {0}?",
			"Stay safe out there, {0}.",
			"The village is glad to have you, {0}.",
			"Keep your blade sharp, {0}. The dead don't rest.",
			"If you find any meat out there, bring it back for the fire.",
			"Fine day, {0}. Let's hope it stays quiet.",
			"{village} is lucky to have you, {0}."
		};
		private static readonly string[] NightChat =
		{
			"Get some rest, {0}. Morning comes soon.",
			"Keep close to the fire, {0}.",
			"Listen - hear that? Probably nothing.",
			"Can't sleep either, {0}?"
		};
		private static readonly string[] SleepyChat =
		{
			"Mmph... five more minutes...",
			"Let me sleep...",
			"Is it morning already?",
			"Zzz..."
		};
		private static readonly string[] BusyChat =
		{
			"Not now!",
			"Kind of busy here!",
			"Later - fight first!"
		};
		private static readonly string[] SwimmingChat =
		{
			"Kind of busy here - swimming!",
			"Blub... blub...",
			"*gurgle* Later!",
			"Can't talk - keeping my head above water!"
		};

		/// <summary>
		/// On a PC viking, the players it knows - having greeted them - by player ID, and the most it remembers
		/// </summary>
		private static readonly int KnownKey = VikingName.KnownKey;
		private const int KnownMax = 64;

		/// <summary>
		/// The talks two vikings have, each line spoken in turn, starting with the viking that started the talk - any
		/// time, at night, in the rain, after a fight, and when the one starting it is hurt
		/// </summary>
		private static readonly string[][] Talks =
		{
			new[] { "Did you hear something past the fence last night, {name}?", "Only the wind. I hope.", "The wind doesn't groan like that." },
			new[] { "The dead don't stay buried anymore.", "Then we burn them, or put silver through them.", "Aye, {name}. Keep the bonfire high and your blade bright." },
			new[] { "The dead keep getting back up.", "Silver puts them down for good. Nothing else does.", "Then keep your blade bright, {name}." },
			new[] { "A dead deer came at me in the meadow today.", "Dead? You're sure?", "It had no eyes left, {name}. It ran at me anyway." },
			new[] { "I heard the dead came raiding at a farm up the coast.", "The dead don't raid.", "These ones carried shields, {name}. They remembered how." },
			new[] { "The newcomers who fall to the dead get back up as one of them.", "With all their gear, too.", "Then pray it's not one of the strong ones, {name}." },
			new[] { "Odin's ravens were circling the meadow today.", "A good sign, or a warning?", "With Odin, it's always both." },
			new[] { "My axe needs a new edge.", "The workbench is free, {name}. Don't use up all the {item} this time." },
			new[] { "The boars are fat this season.", "Good. Fat boars mean full bellies." },
			new[] { "I miss the old days, {name}, before the dead walked.", "There were no old days. Only the ones we lived through." },
			new[] { "Have you been to the Black Forest?", "Once. Never again. The trees there have eyes.", "Greydwarves?", "Worse. Greydwarves that won't stay dead." },
			new[] { "Someone keeps leaving the gate open.", "Wasn't me.", "It's never anyone." },
			new[] { "Do you think the gods can see us here?", "I think they're watching to see who earns Valhalla." },
			new[] { "I found raspberries down by the river.", "Did you save any for the rest of us?", "...Some." },
			new[] { "When I fall, burn me. Don't let me rise.", "Don't talk like that.", "Promise me, {name}." },
			new[] { "The meat's nearly done on the fire.", "Don't let it burn this time, {name}.", "That was one time!" },
			new[] { "Have you seen the raven that follows the newcomers around?", "Hugin? He talks too much.", "He's usually right, though." },
			new[] { "How many of us were there, before?", "More than now.", "Then we hold on to the ones we have." }
		};
		private static readonly string[][] NightTalks =
		{
			new[] { "Long night ahead, {name}.", "Keep your shield close and your torch closer." },
			new[] { "The stars are bright tonight.", "Bright as the halls of Valhalla.", "Sleep. The watch will wake us if they come." },
			new[] { "Who has the watch tonight?", "Not me, thank the gods.", "Then sleep with one eye open anyway." }
		};
		private static readonly string[][] RainTalks =
		{
			new[] { "This rain gets into my bones.", "Sit by the fire. It'll pass.", "That's what you said yesterday." },
			new[] { "Good weather for staying inside.", "Good weather for the dead, you mean. They don't mind getting wet." }
		};
		private static readonly string[][] FightTalks =
		{
			new[] { "That was close.", "Too close. Did you see how many there were?", "We'll need more arrows." },
			new[] { "You fought well out there, {name}.", "So did you. The mead is on me tonight." },
			new[] { "Did we get them all?", "This time. They'll be back." }
		};
		private static readonly string[][] HurtTalks =
		{
			new[] { "My arm still aches from that last fight.", "Eat something and rest by the fire, {name}. You'll mend." },
			new[] { "I'm not as quick as I used to be.", "None of us are. Stay behind the shields next time." }
		};

		/// <summary>
		/// The talks two vikings have about another viking of the village with a name, where "{other}" is its name
		/// </summary>
		private static readonly string[][] OtherTalks =
		{
			new[] { "Have you seen {other} today?", "Out by the river, last I saw.", "Always wandering off, that one." },
			new[] { "{other} snores like a troll.", "I know. The whole village knows." },
			new[] { "{other} fought like a berserker last time.", "Odin himself would have been proud." },
			new[] { "Don't tell {other}, but I took the last of the honey.", "Your secret is safe with me, {name}... for a price." },
			new[] { "{other} says the dead are getting smarter.", "{other} says a lot of things." },
			new[] { "Is {other} still angry about the boar?", "Ask {other} yourself. I'm staying out of it." }
		};

		/// <summary>
		/// The talks two vikings have about one of their village who died, where "{fallen}" is its name
		/// </summary>
		private static readonly string[][] FallenTalks =
		{
			new[] { "I keep thinking about {fallen}.", "Me too. It's quieter without them.", "Too quiet." },
			new[] { "{fallen} would have laughed at that.", "{fallen} laughed at everything.", "Aye. I miss it." },
			new[] { "Do you think {fallen} made it to Valhalla, {name}?", "If anyone did, {fallen} did.", "Then save me a seat, {fallen}." },
			new[] { "Remember when {fallen} chased that boar through the gate?", "Straight into the mud, head first!", "Best day we've had in a long while." },
			new[] { "{fallen} fought bravely to the end.", "The skalds should sing of it.", "Then we'll sing it ourselves, by the fire tonight." },
			new[] { "I still set out a bowl for {fallen} at supper.", "So do I, {name}. So do I." },
			new[] { "{fallen} always kept the fire going.", "Someone has to now.", "I'll do it. For {fallen}." },
			new[] { "I hope {fallen} rests easy.", "If {fallen} ever rises, I'll be the one to put them down.", "...Thank you, {name}." },
			new[] { "{fallen} loved {village}.", "Then we'll keep it standing, for them." }
		};

		/// <summary>
		/// What a viking says as it mourns one of its village who died, by the corpse - by name, and for one with none
		/// </summary>
		private static readonly string[] MournLines =
		{
			"Rest now, {fallen}.",
			"Farewell, {fallen}. Save me a seat in Valhalla.",
			"Not you, {fallen}... not you.",
			"The Valkyries will carry you home, {fallen}.",
			"We'll remember you, {fallen}.",
			"Go to Odin's hall, {fallen}. You've earned it."
		};
		private static readonly string[] NamelessMournLines =
		{
			"Rest now, friend.",
			"Farewell, friend. Save me a seat in Valhalla.",
			"Another one gone...",
			"The Valkyries will carry you home.",
			"We'll remember you, friend.",
			"Go to Odin's hall. You've earned it."
		};

		/// <summary>
		/// What a viking says as it stands ready by the corpse of one of its village that's about to rise - by name, and
		/// for one with none
		/// </summary>
		private static readonly string[] RisingFarewells =
		{
			"Forgive me, {fallen}. I won't let you walk as one of them.",
			"Stay down, {fallen}... please stay down.",
			"If you rise, {fallen}, I'll be the one to send you back.",
			"Not like this, {fallen}. Not like this."
		};
		private static readonly string[] NamelessRisingFarewells =
		{
			"Forgive me, friend. I won't let you walk as one of them.",
			"Stay down... please stay down.",
			"If you rise, I'll be the one to send you back.",
			"Not like this, friend. Not like this."
		};

		/// <summary>
		/// What a viking says to a player as it leaves a village that was disbanded, with the player's name
		/// </summary>
		private static readonly string[] LeavingLines =
		{
			"Farewell, {0}. May Odin watch over you.",
			"So this is goodbye. Stay safe, {0}.",
			"We'll find another fire somewhere. Farewell!",
			"Thank you for the beds, {0}. Farewell.",
			"Time to move on. Take care, {0}.",
			"Goodbye, {0}. Keep the dead from your door.",
			"Farewell, {0}. I'll miss {village}."
		};

		/// <summary>
		/// What a viking leaving a village says to a player as the village is started again and it turns back, with the
		/// player's name
		/// </summary>
		private static readonly string[] TurnBackLines =
		{
			"Wait - we're staying? Back to the fire, then!",
			"Changed your mind, {0}? Good. I hadn't gone far.",
			"Ha! I knew you'd miss us.",
			"Home again already? Fine by me.",
			"I'd barely packed, {0}. Back we go.",
			"Back to {village}, then!"
		};

		/// <summary>
		/// The talks two vikings have about their village, when it has a name, where "{village}" is it
		/// </summary>
		private static readonly string[][] VillageTalks =
		{
			new[] { "{village} isn't much, {name}, but it's ours.", "It's more than most have these days." },
			new[] { "Do you think the skalds will sing of {village} one day?", "Only if we live long enough to be worth a song." },
			new[] { "I'm glad we settled in {village}.", "Me too. The fire's warm and the walls hold." },
			new[] { "Travelers have started asking the way to {village}.", "Then let's give them a reason to come." }
		};

		/// <summary>
		/// Where a village's name goes in a line
		/// </summary>
		private const string VillageToken = "{village}";

		/// <summary>
		/// The lines about the dead walking, and the talks about it by their first line, each with what makes it true of
		/// the world, so vikings only say it when it is (see ZombieLink) - each the same as it's written above
		/// </summary>
		private static readonly Dictionary<string, System.Func<bool>> ZombieLines = new Dictionary<string, System.Func<bool>>
		{
			{ "Welcome back, {0}. The dead are restless tonight.", DeadRise },
			{ "Hail, traveler. You're not one of the dead, are you?", DeadRise },
			{ "Hail! Come in, come in - any friend of the living is welcome here.", DeadRise },
			{ "Living, at least. Keep your voice down out here.", DeadRise },
			{ "Keep your blade sharp, {0}. The dead don't rest.", DeadRise },
			{ "Did you hear something past the fence last night, {name}?", DeadRise },
			{ "The dead don't stay buried anymore.", () => DeadRise() && FireKeepsDead() },
			{ "The dead keep getting back up.", () => DeadRise() && !FireKeepsDead() },
			{ "A dead deer came at me in the meadow today.", () => DeadRise() && ZombieLink.AnimalsRise() },
			{ "I heard the dead came raiding at a farm up the coast.", ZombieLink.VikingRaids },
			{ "The newcomers who fall to the dead get back up as one of them.", ZombieLink.TombstoneZombies },
			{ "I miss the old days, {name}, before the dead walked.", DeadRise },
			{ "Have you been to the Black Forest?", DeadRise },
			{ "When I fall, burn me. Don't let me rise.", () => DeadRise() && FireKeepsDead() },
			{ "Good weather for staying inside.", DeadRise },
			{ "{other} says the dead are getting smarter.", DeadRise },
			{ "I hope {fallen} rests easy.", DeadRise },
			{ "Goodbye, {0}. Keep the dead from your door.", DeadRise }
		};

		/// <summary>
		/// How likely a talk is about what's going on - the night, the rain, a fight or being hurt - when something is,
		/// rather than about anything, and about another viking with a name, when the village has one
		/// </summary>
		private const float ContextChance = 0.6f;
		private const float OtherChance = 0.25f;

		/// <summary>
		/// Registers the RPC with a world's routed RPC
		/// </summary>
		public static void Register()
		{
			ZRoutedRpc.instance.Register<ZDOID, string, float, float>(SayRpc, OnSay);
			ZRoutedRpc.instance.Register<ZDOID, string, float>(ChatterRpc, OnChatter);
			chatter.Clear();
		}

		/// <summary>
		/// Checks whether a viking is asleep - lying in bed, as every client sees it, or resting for the night on a chair
		/// or the floor, which only the client its life runs on knows
		/// </summary>
		/// <param name="viking">The viking</param>
		/// <returns>True when it is</returns>
		public static bool Asleep(Character viking)
		{
			if (viking == null)
			{
				return(false);
			}
			if (VikingPoses.SettledDown(viking, out bool lying) && lying)
			{
				return(true);
			}
			VillageLife life = viking.GetComponent<VillageLife>();
			return(life != null && life.Asleep());
		}

		/// <summary>
		/// Has a viking say something to another viking, shown under its health bar on every client - so it's only
		/// seen close by, the way a creature's health bar is - unless it's asleep
		/// </summary>
		/// <param name="viking">The viking</param>
		/// <param name="text">What it says</param>
		/// <param name="time">How long it shows, in seconds</param>
		/// <param name="evenAsleep">True for a line it says in its sleep or as it's woken</param>
		public static void Chatter(Character viking, string text, float time, bool evenAsleep = false)
		{
			if (ZRoutedRpc.instance == null || viking == null || string.IsNullOrEmpty(text) || (!evenAsleep && Asleep(viking)))
			{
				return;
			}
			ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, ChatterRpc, viking.GetZDOID(), text, time);
		}

		/// <summary>
		/// Notes what a viking says to another, when this client has it loaded, for its health bar to show
		/// </summary>
		/// <param name="sender">The client that sent it</param>
		/// <param name="id">The viking's ZDO</param>
		/// <param name="text">What it says</param>
		/// <param name="time">How long it shows, in seconds</param>
		private static void OnChatter(long sender, ZDOID id, string text, float time)
		{
			GameObject found = ((ZNetScene.instance != null) ? ZNetScene.instance.FindInstance(id) : null);
			Character viking = ((found != null) ? found.GetComponent<Character>() : null);
			if (viking == null)
			{
				return;
			}
			chatter[viking] = new KeyValuePair<string, float>(text, Time.time + time);
		}

		/// <summary>
		/// Finds what a viking is saying to another viking right now, forgetting what it said once it's over
		/// </summary>
		/// <param name="viking">The viking</param>
		/// <param name="text">What it says</param>
		/// <returns>True while it's saying something</returns>
		public static bool Chattering(Character viking, out string text)
		{
			text = null;
			if (chatter.Count == 0 || viking == null || !chatter.TryGetValue(viking, out KeyValuePair<string, float> said))
			{
				return(false);
			}
			if (Time.time >= said.Value)
			{
				chatter.Remove(viking);
				return(false);
			}
			text = said.Key;
			return(true);
		}

		/// <summary>
		/// Has a viking say something, shown over its head on every client nearby, unless it's asleep
		/// </summary>
		/// <param name="viking">The viking</param>
		/// <param name="text">What it says</param>
		/// <param name="time">How long it shows, in seconds</param>
		/// <param name="cull">How far away it can be seen, in meters</param>
		/// <param name="evenAsleep">True for a line it says in its sleep or as it's woken</param>
		public static void Say(Character viking, string text, float time, float cull, bool evenAsleep = false)
		{
			if (ZRoutedRpc.instance == null || viking == null || string.IsNullOrEmpty(text) || (!evenAsleep && Asleep(viking)))
			{
				return;
			}
			// A line that has to be seen isn't talked over while it shows
			if (heldUntil.TryGetValue(viking, out float until) && Time.time < until)
			{
				return;
			}
			ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, SayRpc, viking.GetZDOID(), text, time, cull);
		}

		/// <summary>
		/// Has a viking say a line that has to be seen - a call or an alarm, or bragging about a skill - over its head on
		/// every client nearby, holding off anything else it would say over it until it has shown. Another line that has
		/// to be seen takes its place
		/// </summary>
		/// <param name="viking">The viking</param>
		/// <param name="text">What it says</param>
		/// <param name="time">How long it shows, in seconds</param>
		/// <param name="cull">How far away it can be seen, in meters</param>
		public static void SayFirst(Character viking, string text, float time, float cull)
		{
			heldUntil.Remove(viking);
			Say(viking, text, time, cull);
			heldUntil[viking] = Time.time + time;
		}

		/// <summary>
		/// Has a viking that just reached a new level of a skill say how proud it is, every ProudInterval seconds at
		/// most, as a line nothing else it says covers up while it shows - less likely the farther it is from the nearest
		/// player (see ProudChance), so a village doesn't fill the air with bragging nobody's near enough to hear. A
		/// dvergr viking says one of its own lines (see DvergrSpeech)
		/// </summary>
		/// <param name="viking">The viking, owned by this client</param>
		/// <param name="skill">The skill</param>
		/// <param name="level">Its new level</param>
		public static void Proud(Character viking, Skills.SkillType skill, int level)
		{
			if (viking == null || (proudAt.TryGetValue(viking, out float last) && Time.time - last < ProudInterval))
			{
				return;
			}
			if (Random.value >= ProudChance(viking))
			{
				return;
			}
			proudAt[viking] = Time.time;
			string name = Localization.instance.Localize("$skill_" + skill.ToString().ToLowerInvariant()).ToLowerInvariant();
			if (name.StartsWith("["))
			{
				name = skill.ToString().ToLowerInvariant();
			}
			string[] lines = (VikingCore.IsDvergrViking(viking) ? DvergrSpeech.ProudLines : ProudLines);
			SayFirst(viking, string.Format(Pick(lines), name, level), ProudTime, CallCull);
		}

		/// <summary>
		/// Works out how likely a viking is to brag about a skill by how far it is from the nearest player - always within
		/// ProudNear, falling off evenly to never at CallCull, past which nobody would see it
		/// </summary>
		/// <param name="viking">The viking</param>
		/// <returns>The chance, from 0 to 1</returns>
		private static float ProudChance(Character viking)
		{
			float nearest = float.MaxValue;
			foreach (Player player in Player.GetAllPlayers())
			{
				if (player != null)
				{
					nearest = Mathf.Min(nearest, Vector3.Distance(player.transform.position, viking.transform.position));
				}
			}
			return(1f - Mathf.Clamp01((nearest - ProudNear) / (CallCull - ProudNear)));
		}

		/// <summary>
		/// Shows a viking's words over its head in its bubble (see VikingBubbles), when this client has it loaded
		/// </summary>
		/// <param name="sender">The client that sent it</param>
		/// <param name="id">The viking's ZDO</param>
		/// <param name="text">What it says</param>
		/// <param name="time">How long it shows, in seconds</param>
		/// <param name="cull">How far away it can be seen, in meters</param>
		private static void OnSay(long sender, ZDOID id, string text, float time, float cull)
		{
			GameObject found = ((ZNetScene.instance != null) ? ZNetScene.instance.FindInstance(id) : null);
			Character viking = ((found != null) ? found.GetComponent<Character>() : null);
			if (viking == null)
			{
				return;
			}
			VikingBubbles.Show(viking, text, time, cull);
		}

		/// <summary>
		/// Picks the night watch's alarm about an enemy
		/// </summary>
		/// <param name="enemy">The enemy</param>
		/// <param name="village">The village's name, or ""</param>
		/// <returns>The line</returns>
		public static string Alarm(Character enemy, string village)
		{
			return(string.Format(Pick(AlarmLines, village), enemy.GetHoverName()));
		}

		/// <summary>
		/// Picks the night watch's alarm as it wakes the village to a raid
		/// </summary>
		/// <param name="village">The village's name, or ""</param>
		/// <returns>The line</returns>
		public static string RaidAlarm(string village)
		{
			return(string.Format(Pick(RaidAlarmLines, village)));
		}

		/// <summary>
		/// Picks what an awake viking calls out as a raid comes on its village by day
		/// </summary>
		/// <param name="village">The village's name, or ""</param>
		/// <returns>The line</returns>
		public static string RaidCall(string village)
		{
			return(string.Format(Pick(RaidCallLines, village)));
		}

		/// <summary>
		/// Picks a viking's call as it spots an enemy
		/// </summary>
		/// <param name="enemy">The enemy</param>
		/// <returns>The line</returns>
		public static string Spotted(Character enemy)
		{
			return(string.Format(Pick(SpotLines), enemy.GetHoverName()));
		}

		/// <summary>
		/// Picks what the night watch says as it wakes its relief, to fill in with the relief's name (see Address)
		/// </summary>
		/// <returns>The line</returns>
		public static string WakeRelief()
		{
			return(Pick(WakeLines));
		}

		/// <summary>
		/// Picks what a viking says as it gets up to take over the watch
		/// </summary>
		/// <returns>The line</returns>
		public static string Woken()
		{
			return(Pick(WokenLines));
		}

		/// <summary>
		/// Picks what a viking says as it takes the place of one keeping watch that it saw die
		/// </summary>
		/// <returns>The line</returns>
		public static string TakeWatch()
		{
			return(Pick(TakeWatchLines));
		}

		/// <summary>
		/// Picks what a viking says as it mourns one of its village who died, by the corpse
		/// </summary>
		/// <param name="fallen">The dead viking's name, or "" when it had none</param>
		/// <returns>The line</returns>
		public static string Mourn(string fallen)
		{
			return((fallen.Length > 0) ? Pick(MournLines).Replace("{fallen}", fallen) : Pick(NamelessMournLines));
		}

		/// <summary>
		/// Picks what a viking says as it stands ready by the corpse of one of its village that's about to rise
		/// </summary>
		/// <param name="fallen">The dead viking's name, or "" when it had none</param>
		/// <returns>The line</returns>
		public static string RisingFarewell(string fallen)
		{
			return((fallen.Length > 0) ? Pick(RisingFarewells).Replace("{fallen}", fallen) : Pick(NamelessRisingFarewells));
		}

		/// <summary>
		/// Picks what a viking says to a player as it leaves a village that was disbanded
		/// </summary>
		/// <param name="player">The player</param>
		/// <param name="village">The village's name, or ""</param>
		/// <returns>The line</returns>
		public static string Leaving(Player player, string village)
		{
			return(string.Format(Pick(LeavingLines, village), player.GetPlayerName()));
		}

		/// <summary>
		/// Picks what a viking leaving a village says to a player as the village is started again and it turns back
		/// </summary>
		/// <param name="player">The player</param>
		/// <param name="village">The village's name, or ""</param>
		/// <returns>The line</returns>
		public static string TurnBack(Player player, string village)
		{
			return(string.Format(Pick(TurnBackLines, village), player.GetPlayerName()));
		}

		/// <summary>
		/// Picks what the night watch says as it hears a noise and goes to look
		/// </summary>
		/// <returns>The line</returns>
		public static string HeardNoise()
		{
			return(Pick(HeardLines));
		}

		/// <summary>
		/// Picks what the night watch says as it finds the player who made the noise - by name, for one it knows
		/// </summary>
		/// <param name="player">The player</param>
		/// <param name="known">Whether the watch knows them</param>
		/// <returns>The line</returns>
		public static string FoundNoise(Player player, bool known)
		{
			return(known ? string.Format(Pick(FoundKnownLines), player.GetPlayerName()) : Pick(FoundStrangerLines));
		}

		/// <summary>
		/// Picks what the night watch says as it finds nothing where it heard a noise
		/// </summary>
		/// <returns>The line</returns>
		public static string NothingThere()
		{
			return(Pick(NothingLines));
		}

		/// <summary>
		/// Picks a badly hurt viking's shout for help
		/// </summary>
		/// <returns>The line</returns>
		public static string Help()
		{
			return(Pick(HelpLines));
		}

		/// <summary>
		/// Picks a viking's greeting for a player - by name, for a player it knows, now and then about the weather (see
		/// VikingWeatherSpeech.ForPlayer), and as a stranger otherwise
		/// </summary>
		/// <param name="player">The player</param>
		/// <param name="night">Whether it's night</param>
		/// <param name="known">Whether the viking knows the player</param>
		/// <returns>The line</returns>
		/// <param name="self">The viking's own name, which it gives a stranger, or ""</param>
		/// <param name="village">The village's name, or ""</param>
		public static string Greeting(Player player, bool night, bool known, string self, string village)
		{
			if (!known)
			{
				return(Introduce(string.Format(Pick(night ? NightFirstGreetings : DayFirstGreetings, village)), self));
			}
			return(VikingWeatherSpeech.ForPlayer(player, night) ?? string.Format(Pick(night ? NightGreetings : DayGreetings, village), player.GetPlayerName()));
		}

		/// <summary>
		/// Adds a viking's name after what it says to a player it has never met, when it has one
		/// </summary>
		/// <param name="first">What it says first</param>
		/// <param name="self">Its name, or ""</param>
		/// <returns>The line</returns>
		internal static string Introduce(string first, string self)
		{
			return((self.Length > 0) ? first + " " + string.Format(Pick(Introductions), self) : first);
		}

		/// <summary>
		/// Picks what a viking says when a player names it
		/// </summary>
		/// <param name="name">Its new name</param>
		/// <param name="renamed">Whether it had a name before</param>
		/// <returns>The line</returns>
		public static string Named(string name, bool renamed)
		{
			return(string.Format(Pick(renamed ? RenamedLines : NamedLines), name));
		}

		/// <summary>
		/// Picks what a viking says when it turns a name down
		/// </summary>
		/// <param name="verdict">Why - no name at all, a creature's, a legend's, a place's, someone else's or text
		/// markup</param>
		/// <param name="name">The name, with any markup made literal (see VikingNames.Literal)</param>
		/// <returns>The line</returns>
		public static string Refused(VikingNames.Verdict verdict, string name)
		{
			string[] lines;
			if (verdict == VikingNames.Verdict.Nameless)
			{
				lines = NamelessRefusals;
			}
			else if (verdict == VikingNames.Verdict.Creature)
			{
				lines = CreatureRefusals;
			}
			else if (verdict == VikingNames.Verdict.Legend)
			{
				lines = LegendRefusals;
			}
			else if (verdict == VikingNames.Verdict.Place)
			{
				lines = PlaceRefusals;
			}
			else if (verdict == VikingNames.Verdict.Markup)
			{
				lines = MarkupRefusals;
			}
			else
			{
				lines = SomeoneRefusals;
			}
			return(string.Format(Pick(lines), name));
		}

		/// <summary>
		/// Picks what a viking says when a player tries to name it or its village with text markup
		/// </summary>
		/// <param name="name">What they typed, with its markup made literal (see VikingNames.Literal)</param>
		/// <returns>The line</returns>
		public static string MarkupRefused(string name)
		{
			return(string.Format(Pick(MarkupRefusals), name));
		}

		/// <summary>
		/// Picks what a viking says when a player tries to name its village after a place
		/// </summary>
		/// <param name="name">The name</param>
		/// <returns>The line</returns>
		public static string VillageRefused(string name)
		{
			return(string.Format(Pick(VillagePlaceRefusals), name));
		}

		/// <summary>
		/// Picks what a viking says when a player tries to name its village after a legend
		/// </summary>
		/// <param name="name">The name</param>
		/// <returns>The line</returns>
		public static string VillageLegendRefused(string name)
		{
			return(string.Format(Pick(VillageLegendRefusals), name));
		}

		/// <summary>
		/// Picks what a viking says when a player tries to leave its village without a name
		/// </summary>
		/// <returns>The line</returns>
		public static string VillageNamelessRefused()
		{
			return(Pick(VillageNamelessRefusals));
		}

		/// <summary>
		/// Picks what a viking says when a player tries to call its village just Village
		/// </summary>
		/// <param name="name">The name as they typed it</param>
		/// <returns>The line</returns>
		public static string VillageGenericRefused(string name)
		{
			return(string.Format(Pick(VillageGenericRefusals), name));
		}

		/// <summary>
		/// Picks what a viking says when a player names its village
		/// </summary>
		/// <param name="name">The village's new name</param>
		/// <param name="renamed">Whether it had a name before</param>
		/// <returns>The line</returns>
		public static string VillageNamed(string name, bool renamed)
		{
			return(string.Format(Pick(renamed ? VillageRenamedLines : VillageNamedLines), name));
		}

		/// <summary>
		/// Picks what some vikings say when a player shares their forsaken power with them, a different line for each
		/// </summary>
		/// <param name="power">The power's name</param>
		/// <param name="players">The player's name, or "" for a viking that doesn't know them, for each viking</param>
		/// <returns>The lines, one for each viking</returns>
		public static List<string> PowerShared(string power, List<string> players)
		{
			List<int> left = new List<int>();
			for (int i = 0; i < PowerLines.Length; i++)
			{
				left.Add(i);
			}
			List<string> lines = new List<string>();
			foreach (string player in players)
			{
				if (left.Count == 0)
				{
					break;
				}
				int picked = Random.Range(0, left.Count);
				string line = PowerLines[left[picked]];
				lines.Add(string.Format(((player.Length > 0) ? line : WithStranger(line, 1)), power, player));
				left.RemoveAt(picked);
			}
			return(lines);
		}

		/// <summary>
		/// Picks what a viking says as it uses a forsaken power
		/// </summary>
		/// <param name="power">The power's name</param>
		/// <returns>The line</returns>
		public static string PowerUsed(string power)
		{
			return(string.Format(Pick(PowerUseLines), power));
		}

		/// <summary>
		/// Picks what a viking says as it uses a forsaken power to teach it to another
		/// </summary>
		/// <param name="power">The power's name</param>
		/// <param name="pupil">The other viking's name, or "" when it has none</param>
		/// <returns>The line</returns>
		public static string PowerTaught(string power, string pupil)
		{
			return(Address(Pick(PowerTeachLines).Replace("{0}", power), pupil, "", "", "", ""));
		}

		/// <summary>
		/// Fills in a talk's line - the name of the viking spoken to, left out with the comma beside it when that viking
		/// has none, the name of the other viking talked about, a material the village has, one of the village's dead,
		/// and the village's own name
		/// </summary>
		/// <param name="line">The line, with "{name}" where the viking spoken to is named, "{other}" where another is,
		/// "{item}" where a material goes (see IsMaterial), "{fallen}" where one of the dead is named, "{village}" where
		/// the village is, and "{linked}" and "{biome}" where the village on the other side of its portal and its biome
		/// are</param>
		/// <param name="listener">The name of the viking spoken to, or ""</param>
		/// <param name="other">The name of the viking talked about, or ""</param>
		/// <param name="item">The name of a material the village has, or ""</param>
		/// <param name="fallen">The name of the viking of the village who died, or ""</param>
		/// <param name="village">The village's name, or ""</param>
		/// <param name="linked">The name of the village on the other side of the village's portal, or "" (see
		/// VillagePortalSpeech)</param>
		/// <param name="biome">The biome that village stands in, as a viking says it, or ""</param>
		/// <param name="newcomer">The name of a viking new to the village, talked about at "{newcomer}", or "" (see
		/// VillageNewcomers)</param>
		/// <param name="from">The name of the village a newcomer's group left, at "{from}", or ""</param>
		/// <returns>The line to say</returns>
		public static string Address(string line, string listener, string other, string item, string fallen, string village, string linked = "", string biome = "", string newcomer = "", string from = "")
		{
			if (listener.Length == 0)
			{
				line = line.Replace(", {name}", "").Replace("{name}, ", "").Replace("{name}", "");
			}
			// Filled in one pass, so a name that reads like a token is said as it was given rather than filled in again
			Dictionary<string, string> values = new Dictionary<string, string>
			{
				{ "{name}", listener },
				{ "{other}", other },
				{ "{item}", item },
				{ "{fallen}", fallen },
				{ VillageToken, village },
				{ VillagePortalSpeech.LinkedToken, linked },
				{ VillagePortalSpeech.BiomeToken, biome },
				{ VillageNewcomers.NewcomerToken, newcomer },
				{ VillageNewcomers.FromToken, from }
			};
			line = Regex.Replace(line, TalkTokens, (Match token) => values[token.Value]);
			if (listener.Length == 0 && line.Length > 0)
			{
				line = char.ToUpperInvariant(line[0]) + line.Substring(1);
			}
			return(line);
		}

		/// <summary>
		/// The tokens a talk's lines are filled in at (see Address)
		/// </summary>
		private const string TalkTokens = "\\{(?:name|other|item|fallen|village|linked|biome|newcomer|from)\\}";

		/// <summary>
		/// The materials the vikings build and craft with, by prefab name, read once the game's items are loaded
		/// </summary>
		private static HashSet<string> materials;

		/// <summary>
		/// The materials the vikings always build with, by prefab name, besides what the arrows they craft take
		/// </summary>
		private static readonly string[] BuildMaterials = { "Wood", "FineWood", "RoundLog", "Stone" };

		/// <summary>
		/// Checks whether an item is a material the vikings build or craft with - wood and stone, or anything an arrow
		/// they could craft takes - only materials, never a tool or weapon
		/// </summary>
		/// <param name="item">The item</param>
		/// <returns>True when it is</returns>
		public static bool IsMaterial(ItemDrop.ItemData item)
		{
			if (item == null || item.m_dropPrefab == null || item.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Material)
			{
				return(false);
			}
			if (materials == null || materials.Count == 0)
			{
				if (ObjectDB.instance == null)
				{
					return(false);
				}
				materials = new HashSet<string>(BuildMaterials);
				foreach (Recipe recipe in ObjectDB.instance.m_recipes)
				{
					if (recipe == null || !recipe.m_enabled || recipe.m_item == null || recipe.m_item.m_itemData.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Ammo)
					{
						continue;
					}
					foreach (Piece.Requirement requirement in recipe.m_resources)
					{
						if (requirement != null && requirement.m_resItem != null)
						{
							materials.Add(requirement.m_resItem.gameObject.name);
						}
					}
				}
			}
			return(materials.Contains(item.m_dropPrefab.name));
		}

		/// <summary>
		/// Names a material the way a viking says it in a line, in this game's language and lowercase
		/// </summary>
		/// <param name="item">The material</param>
		/// <returns>Its name</returns>
		public static string MaterialName(ItemDrop.ItemData item)
		{
			return(Localization.instance.Localize(item.m_shared.m_name).ToLowerInvariant());
		}

		/// <summary>
		/// Picks what a viking says when a player it knows talks to it - now and then about the weather (see
		/// VikingWeatherSpeech.ForPlayer)
		/// </summary>
		/// <param name="player">The player</param>
		/// <param name="night">Whether it's night</param>
		/// <param name="village">The village's name, or ""</param>
		/// <returns>The line</returns>
		public static string Answer(Player player, bool night, string village)
		{
			return(VikingWeatherSpeech.ForPlayer(player, night) ?? string.Format(Pick(night ? NightChat : DayChat, village), player.GetPlayerName()));
		}

		/// <summary>
		/// Picks what a viking says when a player talks to it while it's asleep
		/// </summary>
		/// <returns>The line</returns>
		public static string Sleepy()
		{
			return(Pick(SleepyChat));
		}

		/// <summary>
		/// Picks what a viking says when a player talks to it in a fight
		/// </summary>
		/// <returns>The line</returns>
		public static string Busy()
		{
			return(Pick(BusyChat));
		}

		/// <summary>
		/// Picks what a viking says when a player talks to it while it's swimming - PC and dvergr vikings alike
		/// </summary>
		/// <returns>The line</returns>
		public static string Swimming()
		{
			return(Pick(SwimmingChat));
		}

		/// <summary>
		/// Checks whether a viking knows a player - it has greeted them before
		/// </summary>
		/// <param name="viking">The viking</param>
		/// <param name="playerId">The player's ID</param>
		/// <returns>True when it does</returns>
		public static bool Knows(Character viking, long playerId)
		{
			return(viking.m_nview != null && viking.m_nview.IsValid() && Knows(viking.m_nview.GetZDO(), playerId));
		}

		/// <summary>
		/// Checks whether a viking knows a player from its ZDO, so it works for a viking that isn't loaded - it has
		/// greeted them before
		/// </summary>
		/// <param name="viking">The viking's ZDO</param>
		/// <param name="playerId">The player's ID</param>
		/// <returns>True when it does</returns>
		public static bool Knows(ZDO viking, long playerId)
		{
			return(Known(viking).Contains(playerId));
		}

		/// <summary>
		/// Notes that a viking knows a player, forgetting the one it met longest ago once it knows KnownMax - and that it
		/// met and talked to them now, so it only tells them news from after they met (see VikingIdentity)
		/// </summary>
		/// <param name="viking">The viking, owned by this client</param>
		/// <param name="playerId">The player's ID</param>
		public static void Meet(Character viking, long playerId)
		{
			if (viking.m_nview == null || !viking.m_nview.IsValid() || !viking.m_nview.IsOwner())
			{
				return;
			}
			ZDO zdo = viking.m_nview.GetZDO();
			List<long> known = Known(zdo);
			if (known.Contains(playerId))
			{
				return;
			}
			known.Add(playerId);
			long now = NetTime.Now();
			VikingIdentity.Talked(zdo, playerId, now);
			VikingIdentity.Met(zdo, playerId, now);
			if (known.Count > KnownMax)
			{
				known.RemoveAt(0);
			}
			zdo.Set(KnownKey, VikingName.WriteKnown(known));
		}

		/// <summary>
		/// Reads the players a viking knows
		/// </summary>
		/// <param name="zdo">The viking's ZDO</param>
		/// <returns>Their IDs, the one it met longest ago first</returns>
		private static List<long> Known(ZDO zdo)
		{
			return(VikingName.ReadKnown(zdo.GetByteArray(KnownKey)));
		}

		/// <summary>
		/// Picks a talk for two vikings - about one of the village's dead when they remember one, and otherwise more
		/// likely about what's going on when something is
		/// </summary>
		/// <param name="night">Whether it's night or dusk</param>
		/// <param name="wet">Whether it's raining</param>
		/// <param name="fought">Whether the viking starting it was in a fight a little while ago</param>
		/// <param name="hurt">Whether the viking starting it is hurt</param>
		/// <param name="other">Whether there's another viking with a name to talk about</param>
		/// <param name="material">Whether the village has a material to talk about, without which a talk about one is
		/// left out</param>
		/// <param name="fallen">Whether they talk about one of the village's dead (see VillageFallen.Remember)</param>
		/// <param name="bearded">Whether the viking starting it has a beard, without which a talk about its beard is left
		/// out</param>
		/// <param name="named">Whether the village has a name, without which a talk naming it is left out, and with
		/// which the talks about it are among those they might have</param>
		/// <param name="linked">The talks about the village on the other side of the village's portal, among those they
		/// might have, or null when there's none to talk about (see VillagePortalSpeech)</param>
		/// <returns>The talk's lines, in turn, to fill in (see Address)</returns>
		public static string[] Talk(bool night, bool wet, bool fought, bool hurt, bool other, bool material, bool fallen, bool bearded, bool named, string[][] linked)
		{
			List<string[]> remembered = Spoken(FallenTalks).FindAll((string[] lines) => named || !Names(lines));
			if (fallen && remembered.Count > 0)
			{
				return(remembered[Random.Range(0, remembered.Count)]);
			}
			string[] talk = PickTalk(night, wet, fought, hurt, other, bearded, named, linked);
			while ((!material && System.Array.Exists(talk, (string line) => line.Contains("{item}"))) || (!named && Names(talk)))
			{
				talk = PickTalk(night, wet, fought, hurt, other, bearded, named, linked);
			}
			return(talk);
		}

		/// <summary>
		/// Checks whether a talk names the village
		/// </summary>
		/// <param name="talk">The talk's lines</param>
		/// <returns>True when one of them does</returns>
		private static bool Names(string[] talk)
		{
			return(System.Array.Exists(talk, (string line) => line.Contains(VillageToken)));
		}

		/// <summary>
		/// Picks a talk for two vikings at random, more likely about what's going on when something is
		/// </summary>
		/// <param name="night">Whether it's night or dusk</param>
		/// <param name="wet">Whether it's raining</param>
		/// <param name="fought">Whether the viking starting it was in a fight a little while ago</param>
		/// <param name="hurt">Whether the viking starting it is hurt</param>
		/// <param name="other">Whether there's another viking with a name to talk about</param>
		/// <param name="bearded">Whether the viking starting it has a beard</param>
		/// <param name="named">Whether the village has a name, so the talks about it are among those they might have</param>
		/// <param name="linked">The talks about the village on the other side of the village's portal, or null</param>
		/// <returns>The talk's lines, in turn</returns>
		private static string[] PickTalk(bool night, bool wet, bool fought, bool hurt, bool other, bool bearded, bool named, string[][] linked)
		{
			List<string[]> others = Spoken(OtherTalks);
			if (other && others.Count > 0 && Random.value < OtherChance)
			{
				return(others[Random.Range(0, others.Count)]);
			}
			List<string[]> context = new List<string[]>();
			if (night)
			{
				context.AddRange(Spoken(NightTalks));
			}
			if (wet)
			{
				context.AddRange(Spoken(RainTalks));
			}
			context.AddRange(VikingWeatherSpeech.TalksNow(night, bearded));
			if (fought)
			{
				context.AddRange(Spoken(FightTalks));
			}
			if (hurt)
			{
				context.AddRange(Spoken(HurtTalks));
			}
			if (context.Count > 0 && Random.value < ContextChance)
			{
				return(context[Random.Range(0, context.Count)]);
			}
			List<string[]> talks = Spoken(Talks);
			if (named)
			{
				talks.AddRange(Spoken(VillageTalks));
			}
			if (linked != null)
			{
				talks.AddRange(Spoken(linked));
			}
			return(talks[Random.Range(0, talks.Count)]);
		}

		/// <summary>
		/// Checks whether the slain rise in this world - Zombie Apocalypse is running with a RiseChance above 0
		/// </summary>
		/// <returns>True when they do</returns>
		internal static bool DeadRise()
		{
			return(ZombieLink.RiseChance() > 0);
		}

		/// <summary>
		/// Checks whether burning keeps the slain from rising in this world - Zombie Apocalypse is running with a
		/// FireStaysDead above 0. Silver always does, so it needs no check
		/// </summary>
		/// <returns>True when it does</returns>
		internal static bool FireKeepsDead()
		{
			return(ZombieLink.FireStaysDead() > 0);
		}

		/// <summary>
		/// The viking lines are being picked for right now, and the viking it's talking with, if any - set while a PC or
		/// NPC viking's AI updates (see PlayerLikeVikingAI.UpdateAI), as a player talks to it and as it starts a talk, so
		/// an NPC viking is never given a line only PC vikings say (see Says)
		/// </summary>
		internal static Character Speaker;
		internal static Character Listener;

		/// <summary>
		/// Checks whether vikings say a line or start a talk - one about the dead walking only when that's true of the
		/// world (see ZombieLines), one only PC vikings say never when an NPC viking is the one picking it or the one it's
		/// talking with (see NPCSpeech.PCOnly), and any other always
		/// </summary>
		/// <param name="line">The line, or the talk's first line</param>
		/// <returns>True when they say it</returns>
		internal static bool Says(string line)
		{
			if (NPCSpeech.PCOnly(line) && (VikingCore.IsNPCViking(Speaker) || VikingCore.IsNPCViking(Listener)))
			{
				return(false);
			}
			return(!ZombieLines.TryGetValue(line, out System.Func<bool> holds) || holds());
		}

		/// <summary>
		/// Picks a line at random, leaving out those about the dead walking that aren't true of the world (see Says)
		/// </summary>
		/// <param name="lines">The lines</param>
		/// <returns>One of them</returns>
		private static string Pick(string[] lines)
		{
			return(Pick(lines, ""));
		}

		/// <summary>
		/// Picks a line at random, leaving out those about the dead walking that aren't true of the world (see Says), and
		/// those naming the village when it has no name - filling in its name, with any braces in it doubled, so the line
		/// is read by string.Format, which every line naming the village is
		/// </summary>
		/// <param name="lines">The lines, with "{village}" where the village's name goes</param>
		/// <param name="village">The village's name, or ""</param>
		/// <returns>One of them, or "" when there's none it can say</returns>
		internal static string Pick(string[] lines, string village)
		{
			List<string> spoken = new List<string>(lines).FindAll((string line) => Says(line) && (village.Length > 0 || !line.Contains(VillageToken)));
			if (spoken.Count == 0)
			{
				spoken = new List<string>(lines).FindAll((string line) => !line.Contains(VillageToken));
			}
			if (spoken.Count == 0)
			{
				return("");
			}
			return(WithVillage(spoken[Random.Range(0, spoken.Count)], village));
		}

		/// <summary>
		/// Fills in a village's name in a line that's read by string.Format afterwards, with any braces in the name
		/// doubled so they show as they are
		/// </summary>
		/// <param name="line">The line, with "{village}" where the village's name goes</param>
		/// <param name="village">The village's name, or ""</param>
		/// <returns>The line, for string.Format</returns>
		internal static string WithVillage(string line, string village)
		{
			return(line.Replace(VillageToken, village.Replace("{", "{{").Replace("}", "}}")));
		}

		/// <summary>
		/// What a viking calls a player it doesn't know, in place of their name (see WithStranger)
		/// </summary>
		internal const string Stranger = "traveler";

		/// <summary>
		/// Fills in what a viking calls a player it doesn't know where their name goes, in a line that's read by
		/// string.Format afterwards - "traveler" where the viking speaks to them, and "a traveler" where it speaks about
		/// them, which is where a word follows the name, as in "{1} shares {0}'s blessing!" - with a capital letter where
		/// it starts a sentence
		/// </summary>
		/// <param name="line">The line, with "{index}" where the player's name goes</param>
		/// <param name="index">The string.Format index of the player's name</param>
		/// <returns>The line, for string.Format</returns>
		internal static string WithStranger(string line, int index)
		{
			return(Regex.Replace(line, "\\{" + index + "\\}", (Match token) =>
			{
				int after = token.Index + token.Length;
				bool about = (after + 1 < line.Length && line[after] == ' ' && char.IsLetter(line[after + 1]));
				string word = (about ? "a " + Stranger : Stranger);
				return(StartsSentence(line, token.Index) ? char.ToUpperInvariant(word[0]) + word.Substring(1) : word);
			}));
		}

		/// <summary>
		/// Checks whether a sentence starts at a place in a line - at the line's start, or after the ".", "!" or "?"
		/// that ends the one before
		/// </summary>
		/// <param name="line">The line</param>
		/// <param name="at">The place</param>
		/// <returns>True when one does</returns>
		private static bool StartsSentence(string line, int at)
		{
			string before = line.Substring(0, at).TrimEnd();
			return(before.Length == 0 || before.EndsWith(".") || before.EndsWith("!") || before.EndsWith("?"));
		}

		/// <summary>
		/// Lists the talks vikings have, leaving out those about the dead walking that aren't true of the world (see Says)
		/// </summary>
		/// <param name="talks">The talks</param>
		/// <returns>The ones they have</returns>
		private static List<string[]> Spoken(string[][] talks)
		{
			List<string[]> spoken = new List<string[]>(talks);
			spoken.RemoveAll((string[] talk) => !Says(talk[0]));
			return(spoken);
		}
	}
}
