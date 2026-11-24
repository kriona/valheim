using System.Collections.Generic;

namespace VikingVillages
{
	/// <summary>
	/// What dvergr vikings say about the news - a line of their own for each kind of event, all under DvergrPrefix,
	/// but for the Mistlands' news, whose lines they share with the living vikings (see SharedWithDvergr)
	/// </summary>
	internal static partial class VikingNewsLines
	{
		/// <summary>
		/// The start of every pool of dvergr vikings' own lines
		/// </summary>
		public const string DvergrPrefix = "dvergr/";

		/// <summary>
		/// The pools of the living vikings' lines the dvergr vikings say too - those about the Mistlands, where they live:
		/// finding it, the Queen, and dying to its creatures but the dvergr's own
		/// </summary>
		internal static readonly HashSet<string> SharedWithDvergr = new HashSet<string>
		{
			"biome/Mistlands",
			"boss/SeekerQueen",
			"death/boss/SeekerQueen",
			"death/seeker",
			"death/tick",
			"death/gjall"
		};

		/// <summary>
		/// The dvergr vikings' own lines, by pool
		/// </summary>
		private static readonly Dictionary<string, string[]> DvergrPools = new Dictionary<string, string[]>
		{
			{ DvergrPrefix + VikingNews.Boss, new[] { "{creature} is dead? The Dvergr will want to hear about that, {player}.", "You killed {creature}! I'll tell the Dvergr. They'll pretend not to be impressed.", "{creature}, slain by your hand? The Dvergr might even smile, {player}.", "Word reached the outpost - {creature} is down. Well done, {player}." } },
			{ DvergrPrefix + VikingNews.Miniboss, new[] { "{creature} beaten! Hildir is going to owe you, {player}.", "I heard you took care of {creature}, {player}. Nasty business.", "{creature}? Glad it was you and not me, {player}.", "So {creature} is done for. The Dvergr would have charged extra for that job." } },
			{ DvergrPrefix + VikingNews.Trader + "/Haldor", new[] { "You found Haldor? He's kin to my masters, you know. Same nose for a bargain.", "Haldor! The Dvergr here speak well of him. Mostly of his prices, {player}.", "So you've met Haldor. If he says a price is fair, count your coins twice, {player}.", "Haldor is a Dvergr too, {player}. He'd rather sell trinkets to Vikings than mine." } },
			{ DvergrPrefix + VikingNews.Trader + "/Hildir", new[] { "You met Hildir? A Dvergr with an eye for fine clothes. My masters think she's mad.", "Hildir! Haldor's sister, and she drives an even harder bargain than he does, {player}.", "So Hildir has a shop now? The Dvergr here say she should be mining, {player}.", "Hildir lost her chests again, did she, {player}? The Dvergr here would never be so careless." } },
			{ DvergrPrefix + VikingNews.Trader + "/BogWitch", new[] { "You found {creature}? The Dvergr trade with stranger folk than that.", "{creature}, eh? Better prices than the Dvergr, I'd wager.", "Met {creature}, did you? Keep your coin purse close, {player}.", "{creature}! Ask if they're hiring - the Dvergr don't pay in honey." } },
			{ DvergrPrefix + VikingNews.Dungeon, new[] { "Down into the dark, {player}? Give me the mist any day.", "I heard you went underground. The Dvergr dig deeper, but they don't go looking for trouble.", "Back from below, {player}? You've dirt on your boots.", "Caves and crypts, {player}? You'd make a fine Dvergr, if you were shorter." } },
			{ DvergrPrefix + VikingNews.Biome, new[] { "New lands, {player}? The Dvergr were there first, you know. They always are.", "I heard you've been exploring. Bring back a map the Dvergr haven't drawn.", "Off finding new places, {player}? Better you than me - I'm paid to stay put.", "Word is you've walked where few Vikings have, {player}. Watch your back out there." } },
			{ DvergrPrefix + VikingNews.Tame, new[] { "You tamed a {creature}? The Dvergr would sooner tame a rock.", "A tame {creature}! Bring it by - the Dvergr might like it.", "{player}, taming a {creature}? Now I've heard everything.", "A {creature} that follows you around? The Dvergr will want one." } },
			{ DvergrPrefix + VikingNews.Skill, new[] { "They say you've mastered a craft, {player}. The Dvergr respect mastery.", "Word is you're the best there is at something, {player}. The Dvergr noticed.", "A true master, {player}! The Dvergr might even hire you.", "Mastered it, have you, {player}? Don't tell the Dvergr - they'll want lessons." } },
			{ DvergrPrefix + VikingNews.AllSkills, new[] { "Every skill mastered? The Dvergr would hire you on the spot, {player}.", "Master of everything, {player}? Leave some glory for the rest of us.", "Every craft there is, {player}? Even the Dvergr can't say that.", "They say there's nothing left for you to learn, {player}. Not even from the Dvergr." } },
			{ DvergrPrefix + VikingNews.Achievement, new[] { "I heard of your latest feat, {player}. Even the Dvergr were talking.", "Another tale about you, {player}! I've stopped being surprised.", "Word of your deeds reaches even the mist, {player}.", "The skalds will run out of verses for you soon, {player}." } },
			{ DvergrPrefix + VikingNews.Leviathan, new[] { "You sailed right by a leviathan? I'd have rowed the other way, {player}.", "A leviathan, {player}! Did it so much as blink?", "They say the leviathans are older than the Dvergr. Imagine that, {player}.", "A whole island that breathes, {player}? I'll stick to the mist, thanks." } },
			{ DvergrPrefix + VikingNews.Tree, new[] { "You dropped a tree on {creature}? Ha! That's one way to do it, {player}.", "{creature}, flattened by a tree! The Dvergr would call that engineering.", "A tree on {creature}, {player}? Remind me never to stand near you while you chop.", "Timber! And there goes {creature}. Nicely done, {player}." } },
			{ DvergrPrefix + VikingNews.Serpent, new[] { "{creature}, slain at sea! Did you bring back any scales, {player}?", "You killed a {creature}? I'd have stayed in the boat and prayed.", "A {creature}! The Dvergr will want to know where it sank, {player}.", "They say you fought a {creature} from a boat, {player}. Madness." } },
			{ DvergrPrefix + VikingNews.Edge, new[] { "You sailed off the edge of the world? The Dvergr have a map of that, I'm sure.", "Past the edge, {player}? And back? Tell me everything.", "Over the edge of the world, {player}? Even the Dvergr don't dig that far.", "What's past the edge, {player}? Don't tell me - I'd only want to go." } },
			{ DvergrPrefix + "death/any", new[] { "Back on your feet, {player}? Good. Dying is bad for business.", "I heard you died, {player}. Welcome back.", "Dead and back again? The Dvergr would call that poor planning, {player}.", "You're looking well for someone who died, {player}." } },
			{ DvergrPrefix + "death/creature", new[] { "A {creature} got you? Take a Dvergr with you next time, {player}.", "Killed by a {creature}, {player}? It happens to the best of us.", "I heard about the {creature}, {player}. Want me to have a word with it?", "A {creature}, {player}? Next time, call for a sword for hire." } },
			{ DvergrPrefix + "death/boss", new[] { "{creature} got the better of you, {player}? There's no shame in that.", "I heard {creature} beat you, {player}. Rest up and try again.", "{creature} knocked you flat? Even the Dvergr keep their distance from that one.", "Lost to {creature}, {player}? Hire some help next time. I know a few swords." } },
			{ DvergrPrefix + "death/player", new[] { "{creature} killed you? I'd sleep with one eye open, {player}.", "Trouble with {creature}, {player}? Vikings, always fighting each other.", "{creature} did that? Want me to have a word, {player}?", "So {creature} got you, {player}. Settle it over a mead, not an axe." } },
			{ DvergrPrefix + "death/hit", new[] { "I heard you had an accident, {player}. Watch your step.", "Not a monster? Just bad luck, then, {player}.", "The world itself had a go at you, {player}? Careful out there.", "Clumsy, {player}. The Dvergr would never let you near their machines." } },
			{ DvergrPrefix + "death/dvergr", new[] { "The Dvergr got you, {player}? Did you touch their things?", "Never cross the Dvergr, {player}. I learned that on my first day.", "Sorry about that, {player}. Orders are orders.", "The Dvergr hit hard, don't they, {player}? Leave their chests alone." } },
			{ DvergrPrefix + ImpressedBoss, new[] { "I'm glad they hired me for the mist and not for that.", "The Dvergr couldn't pay me enough to face that.", "Not even the Dvergr would take that job.", "And here I thought guarding the Dvergr was dangerous." } },
			{ DvergrPrefix + ImpressedBiome, new[] { "Even the Dvergr don't go that far.", "That's beyond anything I've been paid to see.", "The Dvergr don't even have maps of that.", "I'll stay in the mist, thanks." } }
		};

		/// <summary>
		/// Gets the pool of lines a dvergr viking says about an event - the living vikings' for the Mistlands' news (see
		/// SharedWithDvergr), and its own for the kind of event otherwise - a trader by who they are, since Haldor and
		/// Hildir are dvergr themselves, and a death by what killed the player
		/// </summary>
		/// <param name="news">The event</param>
		/// <returns>The pool's key</returns>
		public static string DvergrPoolKey(VikingNews.Event news)
		{
			string shared = PoolKey(news);
			if (SharedWithDvergr.Contains(shared))
			{
				return(shared);
			}
			if (news.Kind == VikingNews.Trader)
			{
				return(DvergrPrefix + news.Kind + "/" + news.Subject);
			}
			if (news.Kind != VikingNews.Death)
			{
				return(DvergrPrefix + news.Kind);
			}
			string subject = news.Subject ?? "";
			if (subject == "dvergr" || subject == "player" || subject == "any")
			{
				return(DvergrPrefix + "death/" + subject);
			}
			if (subject.StartsWith("hit/", System.StringComparison.Ordinal))
			{
				return(DvergrPrefix + "death/hit");
			}
			if (subject.StartsWith("boss/", System.StringComparison.Ordinal) || subject.StartsWith("miniboss/", System.StringComparison.Ordinal))
			{
				return(DvergrPrefix + "death/boss");
			}
			return(DvergrPrefix + "death/creature");
		}
	}
}
