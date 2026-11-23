using System.Collections.Generic;

namespace VikingVillages
{
	internal static partial class VikingNewsLines
	{
		/// <summary>
		/// What a viking says about the player's death, by its pool - the creature or kind of hit that killed them - and
		/// about them sailing off the edge of the world
		/// </summary>
		private static readonly Dictionary<string, string[]> DeathPools = new Dictionary<string, string[]>
		{
			{ "death/greyling", new[] {
				"A greyling got you? Those little pests are worse than they look.",
				"Even the small ones bite. Glad you're back on your feet.",
				"A greyling, was it? Keep your shield up and your back to a tree next time.",
				"Don't let the others tease you about the greyling, {player}. Anyone can have a bad day.",
				"That greyling will be bragging to its friends for weeks. Rest up, friend."
			} },
			{ "death/greydwarf", new[] {
				"I heard a {creature} got the better of you, {player}. They never come alone, do they?",
				"A {creature}? Keep moving, so the stones they throw land where you were.",
				"Word came that a {creature} took you down. Burn their nests and there'll be fewer of them.",
				"A {creature}? The woods take all of us down sometimes. Glad you're back.",
				"Close in fast on a {creature} next time - don't let them circle you."
			} },
			{ "death/boar", new[] {
				"A boar, {player}? They charge harder than they look.",
				"I heard about the boar. Those tusks are no joke - glad you're whole again.",
				"Even a meadow boar catches us all once. Next time, it's roast pork for the village.",
				"Eat something before you head out. A full belly keeps you standing longer."
			} },
			{ "death/neck", new[] {
				"A neck got you? Those slippery things lurk by every stream.",
				"Mind the water's edge - necks wait in the shallows.",
				"I heard about the neck. Small teeth, but plenty of them. Glad you're back.",
				"Next time it'll be neck tails on the fire, {player}. Rest easy."
			} },
			{ "death/troll", new[] {
				"A {creature}, {player}? Nobody stands up to one of those alone. I'm glad you're back.",
				"I heard a {creature} caught you. When it lifts that log, run - don't stay to argue.",
				"Keep your distance from a {creature} - its reach is longer than you think.",
				"The ground must have shaken when that {creature} found you. Glad you're back on your feet.",
				"A {creature}? Even the skalds would forgive anyone for falling to one of those."
			} },
			{ "death/bear", new[] {
				"A {creature} got you? Those claws would tear through any armor.",
				"I heard about the {creature}. Big as a cart and twice as angry - glad you made it back.",
				"Give a {creature} plenty of room next time. They're faster than they look.",
				"A {creature}? Don't pick a fight with one unless friends are at your side, {player}."
			} },
			{ "death/skeleton", new[] {
				"The {creature} got you, {player}? Bring a club next time - bones break easier than they cut.",
				"I heard the {creature} caught you in the dark. Those old tombs are no place to linger.",
				"Glad you're back. That {creature} may be all bones, but it still swings hard.",
				"The {creature}? Bring plenty of torches - the dark is where they wait."
			} },
			{ "death/ghost", new[] {
				"A {creature}, {player}? The night out there is full of things like that.",
				"I heard a {creature} found you. Stay near the fire after dark - nothing good wanders out there.",
				"A {creature}? Brr. Just hearing about it gives me a chill. Welcome back.",
				"Keep your back to a wall when a {creature} comes drifting in."
			} },
			{ "death/root", new[] {
				"A {creature} out of the ground? Nobody sees those coming.",
				"I heard a {creature} got you. Watch where you stand - the earth itself can turn on you.",
				"Glad you're back. A {creature} can't move, so keep your distance and it can't reach you.",
				"A {creature}! The forest really does fight back sometimes. Rest up, {player}."
			} },
			{ "death/golem", new[] {
				"A stone golem, {player}? It's like fighting a mountain that wants you gone.",
				"I heard a golem crushed you. They're slow - keep moving and they'll miss more than they hit.",
				"Bring a pickaxe for the golem next time. They break like any other rock.",
				"Glad you're back. A golem's swing would flatten a longhouse."
			} },
			{ "death/draugr", new[] {
				"A {creature}, {player}? The Swamp is full of them. Watch every shadow there.",
				"I heard a {creature} cut you down. Rotten things, and they hit like they mean it.",
				"A {creature} got you? Bring friends next time - they rarely come alone.",
				"Glad you're back. A {creature} has no mercy, but we do - sit and eat.",
				"A {creature} in the Swamp mist is hard to spot until it's too late. Mind how you go."
			} },
			{ "death/blob", new[] {
				"That {creature} got you, {player}? Nasty, oozing things. Glad you're back.",
				"I heard about the {creature}. Keep your distance - they don't need to be big to be deadly.",
				"Strike the {creature} from range next time. Nothing good comes from getting close.",
				"The {creature} must have been fierce. Wash off the goo and sit by the fire."
			} },
			{ "death/leech", new[] {
				"A leech, {player}? Stay out of the Swamp water - they're waiting for you there.",
				"I heard about the leech. Those things are all teeth and hunger.",
				"Glad you're back. Next time, cross the Swamp on a raised path, not through the water.",
				"A leech got you? Brr... I'll never wade through a swamp again."
			} },
			{ "death/abomination", new[] {
				"An abomination, {player}? Those things look like tree roots until they stand up.",
				"I heard about the abomination. Anything that big in the Swamp is best avoided.",
				"Glad you're back. Watch any pile of roots in the Swamp that seems to be breathing.",
				"An abomination! Next time, run first and ask questions later. There's no shame in it."
			} },
			{ "death/surtling", new[] {
				"A surtling got you? Those fire geysers in the Swamp spit them out by the dozen.",
				"I heard a surtling burned you, {player}. Stand back and pick them off from range.",
				"Glad you're back. Some say surtlings hate water - lead them into the rain.",
				"A surtling! Burns heal, {player}. Sit by... well, maybe not by the fire just yet."
			} },
			{ "death/wolf", new[] {
				"The {creature} pack got you, {player}? One is never just one.",
				"I heard the {creature} brought you down. Keep your back to a rock so they can't circle you.",
				"Glad you're back. Never try to outrun them - that {creature} is faster than you.",
				"That {creature} left its mark on you. Warm up by the fire and rest those bites.",
				"When you hear the {creature} howl, find a narrow spot to make your stand."
			} },
			{ "death/fenring", new[] {
				"A {creature}, {player}? Those things come out of the night like a nightmare.",
				"I heard a {creature} got you. They're quicker than anything that size should be.",
				"Glad you're back. The Mountains hide worse than the cold - a {creature} among them.",
				"A {creature}! Nobody faces one of those and walks away easily. Rest now."
			} },
			{ "death/drake", new[] {
				"A drake, {player}? Those ice-spitting things never come down to fight fair.",
				"I heard a drake got you. Fire arrows bring them down quickly.",
				"Glad you're back. Keep an eye on the sky in the Mountains - drakes come from above.",
				"A drake! Frost resistance mead helps with that icy breath.",
				"Come warm yourself - you must have been frozen stiff after that drake."
			} },
			{ "death/bat", new[] {
				"A {creature}, {player}? In those frost caves they come in swarms.",
				"I heard a {creature} got you. Small, but there's always a lot of them.",
				"Glad you're back. Swing wide when a {creature} swarm comes - you're sure to hit something.",
				"A {creature}! Who'd have thought something so small could be so much trouble?"
			} },
			{ "death/fuling", new[] {
				"A {creature}, {player}? Their camps in the Plains bristle with spears.",
				"I heard a {creature} got you. They hunt in war bands - never take on a camp alone.",
				"Glad you're back. A {creature} hits harder than you'd ever expect.",
				"A {creature}! Shield up, and save your stamina for dodging.",
				"Raid their camp with friends next time, and the {creature} will be the one who's sorry."
			} },
			{ "death/deathsquito", new[] {
				"A deathsquito, {player}? You never even hear them until they hit.",
				"I heard about the deathsquito. Tiny, fast and meaner than a troll. Glad you're back.",
				"Turn and face a deathsquito - it'll catch you if you run.",
				"Deathsquitoes! Every newcomer to the Plains meets one. You'll be ready next time, {player}."
			} },
			{ "death/lox", new[] {
				"A lox, {player}? They only charge when they feel threatened - give them space.",
				"I heard a lox trampled you. Big as a hill and just as heavy.",
				"Glad you're back. Some folk tame lox with a little food and a lot of patience.",
				"A lox! There's no shame in running from one of those, {player}."
			} },
			{ "death/seeker", new[] {
				"A {creature}, {player}? The Mistlands are thick with them.",
				"I heard a {creature} got you. All claws and clicking in the mist - horrible things.",
				"Glad you're back. A {creature} hides in the fog, so bring a wisp to light the way.",
				"A {creature}! Watch for their nests - where there's one, there are more.",
				"Shield up for a {creature}. They strike quick and from strange angles."
			} },
			{ "death/tick", new[] {
				"A tick, {player}? Those little bloodsuckers latch on before you even notice.",
				"I heard about the tick. Check yourself after every trip to the Mistlands.",
				"Glad you're back. Shake off a tick fast - it won't let go on its own.",
				"A tick got you? Something that small shouldn't be so dangerous. Rest up."
			} },
			{ "death/gjall", new[] {
				"A gjall, {player}? That floating monster rains fire on anything below it.",
				"I heard about the gjall. Find cover when one drifts overhead.",
				"Glad you're back. Arrows are the only way to bring a gjall down from the sky.",
				"A gjall spits fire and ticks both - no wonder it got you, {player}.",
				"A gjall! Even the bravest hide from those. Rest now."
			} },
			{ "death/dvergr", new[] {
				"A {creature}, {player}? They're touchy - never take what's theirs.",
				"I heard a {creature} turned on you. The dvergr guard their towers fiercely.",
				"Glad you're back. A {creature} is small, but don't ever tell them that.",
				"A {creature}! Better to leave them in peace than fight them.",
				"A {creature} got you? Their mages throw fire and ice both - keep moving."
			} },
			{ "death/charred", new[] {
				"A {creature}, {player}? The Ashlands are crawling with those burned bones.",
				"I heard a {creature} got you. Everything in the Ashlands wants you gone.",
				"Glad you're back. A {creature} comes in numbers - build walls before you camp out there.",
				"A {creature}! Wear your best armor before you cross that burning sea.",
				"Rest a while. That {creature} will still be there when you're healed."
			} },
			{ "death/morgen", new[] {
				"A morgen, {player}? Those great brutes sleep in the ash until you wake them.",
				"I heard a morgen got you. Tread softly around a sleeping one.",
				"Glad you're back. A morgen hits like a falling longhouse.",
				"A morgen! Bring friends next time, {player} - that's a fight for a whole crew."
			} },
			{ "death/asksvin", new[] {
				"An asksvin, {player}? They're quick as a hawk and twice as hungry.",
				"I heard an asksvin got you. Keep your back covered - they come at a run.",
				"Glad you're back. Some brave folk even ride asksvin, but I wouldn't try.",
				"An asksvin! Nothing that fast should have that many teeth, {player}."
			} },
			{ "death/volture", new[] {
				"A volture, {player}? Those birds circle the Ashlands waiting for their chance.",
				"I heard a volture got you. Look up now and then out there.",
				"Glad you're back. Bring a bow - a volture is no match for a good arrow.",
				"A volture! Swooping down from the sky like that... you never had a fair chance."
			} },
			{ "death/valkyrie", new[] {
				"A fallen valkyrie, {player}? Once Odin's own, now turned against us.",
				"I heard a fallen valkyrie took you. Even Valhalla's lost daughters fight fiercely.",
				"Glad you're back. Keep your bow ready when the sky fills with wings.",
				"A valkyrie! Strange - they're meant to carry warriors to Valhalla, not cut them down.",
				"A fallen valkyrie? Pray to Odin she finds her way home one day."
			} },
			{ "death/serpent", new[] {
				"A serpent, {player}? Out on the open sea there's nowhere to run.",
				"I heard a serpent caught you at sea. Stay nearer the shore when a storm brews.",
				"Glad you're back. A harpoon and a good crew will drag a serpent to the beach.",
				"A serpent! The skalds sing of those, {player} - you're part of an old tale now."
			} },
			{ "death/bonemaw", new[] {
				"A bonemaw, {player}? The seas by the Ashlands are deadly enough without those.",
				"I heard a bonemaw got you. Sail the strongest ship you can build out there.",
				"Glad you're back. A bonemaw is all bones and teeth and fury.",
				"A bonemaw! Few sailors who meet one live to tell the tale. Glad you can, {player}."
			} },
			{ "death/boss/Eikthyr", new[] {
				"Eikthyr got you, {player}? That stag's lightning catches us all the first time.",
				"I heard about Eikthyr. Keep moving and stay out of the lightning.",
				"Glad you're back. Eikthyr will fall - he's only the first test the gods set.",
				"Bring plenty of arrows and keep your distance from Eikthyr next time.",
				"Facing Eikthyr already? Brave. Eat well and try again."
			} },
			{ "death/boss/gd_king", new[] {
				"The Elder, {player}? That ancient tree is older than any of us can count.",
				"I heard the Elder got you. Fire arrows - it's a tree, after all.",
				"Glad you're back. Watch for the roots that burst from the ground around the Elder.",
				"You stood against the Elder? Few would dare. Rest, and go again.",
				"Keep moving around the Elder - it's slow to turn, but it hits hard."
			} },
			{ "death/boss/Bonemass", new[] {
				"Bonemass, {player}? Brave of you. That rotting heap would turn anyone's stomach.",
				"I heard Bonemass got you. Drink a poison resistance mead before you go back.",
				"Glad you're back. A mace or club does more against Bonemass than any blade.",
				"Bonemass! Its poison clings to you long after the hit. Rest until you're well."
			} },
			{ "death/boss/Dragon", new[] {
				"Moder, {player}? Facing the dragon herself takes a brave heart.",
				"I heard Moder got you. Wrap up warm - her frost breath chills the blood.",
				"Glad you're back. Have your bow ready when Moder takes to the sky.",
				"The mother of dragons! The skalds will sing of you for trying, {player}."
			} },
			{ "death/boss/GoblinKing", new[] {
				"Yagluth got you, {player}? That old fuling king rains fire from the sky.",
				"I heard about Yagluth. A fire resistance mead might save you next time.",
				"Glad you're back. Keep moving when Yagluth calls down his meteors.",
				"Yagluth! Few dare stand before the king of the fulings, {player}. Rest well."
			} },
			{ "death/boss/SeekerQueen", new[] {
				"The Queen, {player}? Fighting her in her own nest takes courage.",
				"I heard the Queen got you. Her brood swarms anyone who gets close.",
				"Glad you're back. Clear her brood first, then go after the Queen herself.",
				"The Queen of the Mistlands! There's no shame in losing to her, {player}."
			} },
			{ "death/boss/Fader", new[] {
				"Fader, {player}? You faced the greatest terror of the Ashlands.",
				"I heard Fader got you. Even the gods would think twice before facing that beast.",
				"Glad you're back. Bring every bit of fire resistance you own when you face Fader again.",
				"Fader! The skalds will tell of the day {player} stood against it."
			} },
			{ "death/boss/FrozenKing", new[] {
				"The Shackled One, {player}? Few have even seen the frozen king, let alone fought him.",
				"I heard Kall Fimbulbringer got you. The Deep North is no place for the faint of heart.",
				"Glad you're back. Whatever chains held that king, they weren't enough.",
				"You faced Kall Fimbulbringer? Warm yourself by the fire - you've earned it."
			} },
			{ "death/miniboss/Brenna", new[] {
				"Brenna got you, {player}? Hildir's chests are guarded well.",
				"I heard about Brenna. A burial chamber is no place to linger.",
				"Glad you're back. Bring a mace for Brenna - she's nothing but bones.",
				"Hildir will wait for her chest, {player}. Rest before you go after Brenna again."
			} },
			{ "death/miniboss/Geirrhafa", new[] {
				"Geirrhafa, {player}? That frost cave is cold enough without her in it.",
				"I heard Geirrhafa got you. Wrap up warm before you go back down there.",
				"Glad you're back. Hildir asks a lot, sending you after Geirrhafa.",
				"Geirrhafa! Take a friend or two next time, {player}. She doesn't fight fair."
			} },
			{ "death/miniboss/ZilThungr", new[] {
				"Zil and Thungr, {player}? Two at once is too many for anyone.",
				"I heard the fuling brothers got you. Split them up if you can.",
				"Glad you're back. Hildir's chest will keep - you'll get it next time.",
				"Zil and Thungr! Bring a friend - it's only fair, since there are two of them."
			} },
			{ "death/player", new[] {
				"{creature} got you, {player}? With friends like that, who needs trolls?",
				"I heard {creature} cut you down. I hope it was only a sparring match.",
				"{creature} did that? Shake hands and share a mead - that's the Viking way.",
				"Watch your back around {creature}, {player} - friendly or not, a blade's a blade.",
				"Word is {creature} bested you. Train hard and you'll have your revenge in good time."
			} },
			{ "death/viking", new[] {
				"I'm sorry it came to blows with one of our own, {player}. Tempers run hot sometimes.",
				"I heard one of us struck you down. Whatever happened, I hope it's forgiven.",
				"That should never have happened. We're all on the same side out here.",
				"A Viking turning on a friend... it shames us all. I'm glad you're back, {player}.",
				"Let's put it behind us. Come, have a mead with us and no hard feelings."
			} },
			{ "death/zombieviking", new[] {
				"A zombie Viking got you, {player}? Once one of us, now one of them.",
				"I heard the dead got you. It's worse when they wear a face you might have known.",
				"Glad you're back. Burn them or bring silver - nothing else keeps a zombie Viking down.",
				"A walking corpse with an axe... I'd have fallen too, {player}."
			} },
			{ "death/creature", new[] {
				"That {creature} got you, {player}? I've never even heard of one before.",
				"I heard the {creature} brought you down. Glad you made it back to us.",
				"The {creature}? Tell me all about it by the fire - and how to stay clear of it.",
				"Whatever that {creature} was, {player}, I hope you gave it a few scars of its own.",
				"{creature}... the world grows stranger every day. Rest up, friend."
			} },
			{ "death/hit/Fall", new[] {
				"I heard you took a bad fall, {player}. The ground always wins in the end.",
				"A fall? Watch your footing on the cliffs - they're steeper than they look.",
				"Glad you're back. Next time, take the long way down.",
				"Even Odin's ravens would have thought twice about that drop, {player}."
			} },
			{ "death/hit/Drowning", new[] {
				"You drowned, {player}? Keep an eye on your stamina when you swim.",
				"I heard the water took you. Stay close to the boat next time.",
				"Glad you're back. The water's cold and deep - there's no shame in staying on shore.",
				"Drowned? Rest and dry off, {player}. Ran has nets enough without you in them."
			} },
			{ "death/hit/Burning", new[] {
				"Burned, {player}? Let's keep you away from the fire pit for a while.",
				"I heard you were caught in the flames. Fire spreads faster than anyone thinks.",
				"Fire takes the best of us. A fire resistance mead might help next time.",
				"When you catch fire, jump in the nearest water, {player}. It puts you out quick."
			} },
			{ "death/hit/Freezing", new[] {
				"You froze, {player}? The Mountains are no place without a warm cloak.",
				"I heard the cold got you. A wolf fur cape or a frost resistance mead next time.",
				"Glad you're back. Come, sit by the fire and thaw out those fingers.",
				"Frozen stiff! Carry wood for a campfire next time, {player} - it can save your life."
			} },
			{ "death/hit/Poisoned", new[] {
				"Poisoned, {player}? Those Swamp things leave their sting in you for ages.",
				"I heard the poison got you. A poison resistance mead next time, friend.",
				"Glad you're back. Eat well before you go out - a full belly helps you outlast poison.",
				"Poison is a cruel thing. Sit with us a while and get your strength back, {player}."
			} },
			{ "death/hit/Water", new[] {
				"The water got the better of you, {player}? It's stronger than it looks.",
				"I heard the water did you in. Even the strongest of us can't fight it forever.",
				"Glad you're back. Respect the water - it's not on anyone's side.",
				"Water gets into everything, {player}. Come and dry off by the fire."
			} },
			{ "death/hit/Smoke", new[] {
				"Choked on smoke, {player}? Always leave the smoke a way out of the house.",
				"I heard the smoke got you. When your eyes start to sting, get outside.",
				"Glad you're back. A fire indoors is a fine thing, until the smoke fills the room.",
				"Smoke is sneaky stuff, {player} - it gets you before you even notice it."
			} },
			{ "death/hit/Impact", new[] {
				"Something heavy came down on you, {player}? Watch out for loose rocks on the slopes.",
				"I heard you were flattened. Odin's beard, I'm glad you're back in one piece.",
				"Glad you're back. Never stand downhill from a rolling boulder.",
				"Crushed by a rock? That's bad luck, {player} - nothing more."
			} },
			{ "death/hit/Cart", new[] {
				"Your own cart, {player}? Carts have a mind of their own on a hill.",
				"I heard about the cart. Next time, let go when it starts rolling back at you.",
				"Glad you're back. A cart full of ore is heavier than any troll.",
				"Run over by your own cart! That one will stay between us, {player}."
			} },
			{ "death/hit/Tree", new[] {
				"A tree came down on you, {player}? Don't worry - it happens to every woodcutter.",
				"Glad you're back. Watch which way a tree leans before the last swing.",
				"Accidents happen. That tree never meant you any harm.",
				"Every woodcutter has a story like that. Now you have one to tell by the fire.",
				"Maybe the forest wanted some of its logs back. Rest a while, friend."
			} },
			{ "death/hit/Self", new[] {
				"Hurt by your own hand, {player}? Some power always takes its price.",
				"Glad you're back. Watch your health closely when you cast blood magic.",
				"We all push ourselves too hard sometimes. Rest a while, friend.",
				"Your own doing, {player}? Then let it be a lesson, and no more said about it."
			} },
			{ "death/hit/Structural", new[] {
				"A building came down on you, {player}? Check the supports before you go inside.",
				"I heard the roof gave way. Stone and timber need good foundations.",
				"Glad you're back. Never take out the post you're standing under.",
				"Crushed by a building? Let's rebuild it together - stronger this time."
			} },
			{ "death/hit/Turret", new[] {
				"Shot by a ballista, {player}? Make sure it knows who's a friend.",
				"I heard a ballista hit you. Those bolts don't care whose side you're on.",
				"Glad you're back. Stand well clear of a ballista's line of fire.",
				"A ballista! Built to guard us, and it turned on you. That's hard luck."
			} },
			{ "death/hit/Boat", new[] {
				"Crushed by a boat, {player}? Never stand between a ship and the dock.",
				"I heard a boat came down on you. They're heavier than they look out of the water.",
				"Glad you're back. Let the waves settle before you climb aboard.",
				"A boat, {player}? Sailors have strange tales, and now you have one too."
			} },
			{ "death/hit/Stalagtite", new[] {
				"An icicle fell on you, {player}? Those frost caves are full of them.",
				"I heard about the stalactite. Look up now and then when you're underground.",
				"Glad you're back. Bad luck, to be in just the wrong spot when it fell.",
				"A stalactite! Even the caves want a piece of you. Rest and warm up."
			} },
			{ "death/hit/Catapult", new[] {
				"A catapult stone hit you, {player}? Stay well clear of where they land.",
				"I heard about the catapult. Those stones knock down walls - and anyone near them.",
				"Glad you're back. Always know which way the catapult is aimed.",
				"A catapult? Next time, you work the catapult and let someone else stand clear."
			} },
			{ "death/hit/CinderFire", new[] {
				"Cinder fire, {player}? The Ashlands burn like nowhere else.",
				"I heard the cinders caught you. Watch for burning ground in the Ashlands.",
				"Glad you're back. Drink a fire resistance mead before you go back there.",
				"The Ashlands set even the ground ablaze. Rest and cool off a while."
			} },
			{ "death/hit/AshlandsOcean", new[] {
				"The boiling sea, {player}? Never let your boat sink near the Ashlands.",
				"I heard the Ashlands sea got you. That water would cook a lox.",
				"Glad you're back. Stay on deck and keep the hull mended out there.",
				"Boiling water! Even Ran won't swim in those seas, {player}."
			} },
			{ "death/hit/AshlandsLava", new[] {
				"Lava, {player}? Watch every step in the Ashlands.",
				"I heard about the lava. The ground glows for a reason - stay off it.",
				"Glad you're back. Even the strongest armor melts in that stuff.",
				"Lava! Surtr's own fire. Rest and cool your heels, {player}."
			} },
			{ "death/hit/Incinerator", new[] {
				"The obliterator, {player}? It's meant for junk, not for Vikings!",
				"I heard you stood too close to the obliterator. Step back before you pull the lever.",
				"Glad you're back. That thing destroys whatever is near it - stand well clear.",
				"The obliterator! Thor's lightning in a box. Keep your distance, {player}."
			} },
			{ "death/hit/DrawBridge", new[] {
				"Crushed by a drawbridge, {player}? Never stand under it when it comes down.",
				"I heard about the drawbridge. Shout before you lower it, so everyone knows.",
				"Glad you're back. A drawbridge is built to stop a troll - it'll stop anyone.",
				"A drawbridge! At least now you know it works, {player}."
			} },
			{ "death/any", new[] {
				"I heard you had a rough time out there, {player}. Glad you're back.",
				"Word came that you fell. The village was worried about you.",
				"Welcome back, {player}. Valhalla can wait a while longer.",
				"You're back! Sit down, eat something and get your strength up.",
				"We heard the news. Whatever got you, you'll beat it next time."
			} },
			{ "edge", new[] {
				"You sailed to the very edge of the world, {player}? That takes a brave heart!",
				"I heard you went where no one else dared. The skalds will sing of it.",
				"The edge of the world! What was it like out there, {player}?",
				"Sailing to the edge of the world? You're braver than any of us.",
				"Odin himself must have watched you sail out that far.",
				"To the end of the map and beyond! I'll raise my mead to that, {player}."
			} }
		};
	}
}
