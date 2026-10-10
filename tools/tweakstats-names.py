"""
Writes TweakStats/NAMES.md - every item, recipe, creature, build piece and status effect with its in-game English name
and the Tweak Stats section that changes it

Usage:
	python -P tools/tweakstats-names.py

Reads the ObjectDB and ZNetScene lists from the game's _GameMain prefab, the English names from the localization files
and the enum names from assembly_valheim.dll. Needs UnityPy (python -m pip install --user UnityPy) and ilspycmd. Run it
after a game update to bring the list up to date

The game is read from the ValheimPath environment variable, or --valheim, or the default Steam folder
"""

import argparse
import csv
import datetime
import importlib.util
import io
import os
import re
import subprocess

import UnityPy

toolsFolder = os.path.dirname(os.path.abspath(__file__))
outputPath = os.path.join(os.path.dirname(toolsFolder), 'TweakStats', 'NAMES.md')

# Console builds' localization files, which only add console wording
skippedLocalizations = ('localization_xbox', 'localization_switch')

# Item types listed under Weapons by skill, with the heading each gets within a skill
weaponTypeHeadings = {
	'OneHandedWeapon': 'One-Handed',
	'TwoHandedWeapon': 'Two-Handed',
	'TwoHandedWeaponLeft': 'Two-Handed (Left Hand)',
	'Attach_Atgeir': 'Atgeir',
	'Bow': 'Ranged',
}

# Headings for weapon skills whose name doesn't read well split into words
weaponSkillHeadings = {
	'None': 'No Skill',
}

# Item types listed under Equipment - every other type that isn't a weapon is listed under Other Items
equipmentTypes = ('Ammo', 'Shield', 'Helmet', 'Chest', 'Legs', 'Shoulder', 'Utility', 'Trinket', 'Tool', 'Torch')

# Item types whose skill is worth showing outside Weapons - every other item has the default Swords skill
skilledTypes = ('Shield', 'Tool', 'Torch', 'Ammo')

# Headings for the other item types - types not listed here are headed by their own name
itemTypeHeadings = {
	'Ammo': 'Ammo',
	'AmmoNonEquipable': 'Ammo (Not Equippable)',
	'Shield': 'Shields',
	'Helmet': 'Helmets',
	'Chest': 'Chest Armor',
	'Legs': 'Leg Armor',
	'Shoulder': 'Capes',
	'Utility': 'Utility',
	'Trinket': 'Trinkets',
	'Tool': 'Tools',
	'Torch': 'Torches',
	'Consumable': 'Food and Meads',
	'Material': 'Materials',
	'Fish': 'Fish',
	'Trophy': 'Trophies',
	'Customization': 'Customization',
	'Misc': 'Miscellaneous',
}


def loadPrefabDump():
	"""
	Loads prefab-dump.py from this folder, for its bundle loading

	@return The prefab-dump module
	"""
	spec = importlib.util.spec_from_file_location('prefabDump', os.path.join(toolsFolder, 'prefab-dump.py'))
	module = importlib.util.module_from_spec(spec)
	spec.loader.exec_module(module)
	return(module)


def decompile(assemblyPath, typeName):
	"""
	Decompiles a type with ilspycmd

	@param assemblyPath The assembly holding the type
	@param typeName The full type name, with + between nested types
	@return The decompiled source
	"""
	result = subprocess.run(['ilspycmd', '-t', typeName, assemblyPath], capture_output=True, text=True, encoding='utf-8', check=True)
	return(result.stdout)


def readEnum(assemblyPath, typeName):
	"""
	Reads the names of an enum's values

	@param assemblyPath The assembly holding the enum
	@param typeName The full type name, e.g. Skills+SkillType
	@return A dict of name by value
	"""
	source = decompile(assemblyPath, typeName)
	enumName = typeName.split('+')[-1]
	match = re.search(r'enum ' + enumName + r'\b[^{]*\{(.*?)\}', source, re.DOTALL)
	if match is None:
		raise SystemExit('Couldn\'t find enum ' + typeName)
	names = {}
	value = -1
	for line in match.group(1).splitlines():
		line = line.strip().rstrip(',')
		if line == '' or line.startswith('['):
			continue
		if '=' in line:
			name, number = [part.strip() for part in line.split('=', 1)]
			value = int(number, 0)
		else:
			name = line
			value += 1
		names[value] = name
	return(names)


def readGameVersion(assemblyPath):
	"""
	Reads the game's version number

	@param assemblyPath assembly_valheim.dll
	@return The version, e.g. 1.0.17
	"""
	match = re.search(r'CurrentVersion \{ get; \} = new GameVersion\(([^)]*)\)', decompile(assemblyPath, 'Version'))
	if match is None:
		return('unknown')
	return('.'.join(part.strip() for part in match.group(1).split(',')))


def readLocalization(valheimPath):
	"""
	Reads the English text of every localization key

	@param valheimPath The Valheim install folder
	@return A dict of English text by key, without the $
	"""
	env = UnityPy.load(os.path.join(valheimPath, 'valheim_Data', 'resources.assets'))
	files = []
	for obj in env.objects:
		if obj.type.name != 'TextAsset':
			continue
		data = obj.read()
		if not data.m_Name.startswith('localization') or data.m_Name in skippedLocalizations:
			continue
		text = data.m_Script if isinstance(data.m_Script, str) else data.m_Script.decode('utf-8', 'replace')
		files.append((data.m_Name, text.lstrip('\ufeff')))
	# The main file first, so the update files' words replace its own like they do in game
	files.sort(key=lambda file: (file[0] != 'localization', file[0]))
	words = {}
	for name, text in files:
		rows = csv.reader(io.StringIO(text))
		header = next(rows, [])
		if 'English' not in header:
			continue
		column = header.index('English')
		for row in rows:
			if len(row) > column and row[0] != '' and not row[0].startswith('//') and row[column].strip() != '':
				words[row[0]] = row[column].strip()
	return(words)


def localize(words, token):
	"""
	The English text for a game name

	@param words English text by key
	@param token A name like $item_sword_iron, or text with tokens in it like Big $item_snowball
	@return The text with each token replaced by its English text, and tokens that have none in code formatting
	"""
	if not token:
		return('')
	return(re.sub(r'\$(\w+)', lambda match: words.get(match.group(1), '`' + match.group(0) + '`'), token))


def scriptName(component):
	"""
	The script class of a MonoBehaviour

	@param component A component or MonoBehaviour object reader
	@return The class name, or None when it isn't a readable MonoBehaviour
	"""
	try:
		return(component.read(check_read=False).m_Script.read().m_ClassName)
	except Exception:
		return(None)


def findComponent(prefabDump, gameObject, classNames):
	"""
	Finds the first MonoBehaviour on a GameObject whose script is one of the given classes

	@param prefabDump The prefab-dump module
	@param gameObject A GameObject reader
	@param classNames The script class names to look for
	@return (class name, component data), or (None, None)
	"""
	for component in prefabDump.components(gameObject):
		name = scriptName(component)
		if name in classNames:
			return((name, component.read(check_read=False)))
	return((None, None))


def countResources(resources, words, counts, column):
	"""
	Counts the items a recipe or build piece costs

	@param resources The recipe's or piece's m_resources list
	@param words English text by key
	@param counts Rows by item prefab name - each row is [stat, name, recipes, pieces]
	@param column The row index to count in, 2 for recipes or 3 for build pieces
	"""
	used = set()
	for requirement in resources:
		itemObject = deref(requirement.m_resItem)
		if itemObject is None:
			continue
		item = itemObject.read(check_read=False)
		prefabName = gameObjectName(item)
		if prefabName in used:
			continue
		used.add(prefabName)
		row = counts.setdefault(prefabName, ['`resources.' + prefabName + '`', localize(words, item.m_itemData.m_shared.m_name), 0, 0])
		row[column] += 1


def gameObjectName(componentData):
	"""
	The name of the GameObject a component is on

	@param componentData The component's data
	@return The GameObject's name
	"""
	return(componentData.m_GameObject.deref().read().m_Name)


def deref(pointer):
	"""
	Follows a reference, returning None for an empty one

	@param pointer A UnityPy PPtr
	@return The object reader, or None
	"""
	if pointer is None or pointer.m_PathID == 0:
		return(None)
	try:
		return(pointer.deref())
	except Exception:
		return(None)


def cell(text):
	"""
	Makes text safe for a Markdown table cell

	@param text The text
	@return The text with | escaped and line breaks removed
	"""
	return(str(text).replace('|', '\\|').replace('\r', ' ').replace('\n', ' ').strip())


def sortKey(text):
	"""
	The key that sorts names alphabetically, ignoring case

	@param text The name
	@return The sort key
	"""
	return(text.lower())


def table(headings, rows):
	"""
	Writes a Markdown table

	@param headings The column headings
	@param rows The rows, each a list of cell texts
	@return The table's lines
	"""
	lines = ['| ' + ' | '.join(headings) + ' |', '|' + ' --- |' * len(headings)]
	for row in rows:
		lines.append('| ' + ' | '.join(cell(value) for value in row) + ' |')
	return(lines)


def anchor(heading):
	"""
	The link anchor GitHub gives a heading, before a number is added to repeats

	@param heading The heading text
	@return The anchor, without #
	"""
	return(re.sub(r'[^a-z0-9 -]', '', heading.lower()).replace(' ', '-'))


def splitWords(name):
	"""
	Splits a name written in PascalCase into words

	@param name The name, e.g. ElementalMagic
	@return The words, e.g. Elemental Magic
	"""
	return(re.sub(r'(?<=[a-z])(?=[A-Z])', ' ', name))


class Document:
	"""
	Markdown lines, with a table of contents linking to their level 2 and 3 headings
	"""

	def __init__(self):
		"""
		Starts an empty document
		"""
		self.lines = []
		self.contents = []
		# Times each anchor has been used, since GitHub adds -1, -2 and so on to repeated headings
		self.anchors = {}

	def heading(self, level, text):
		"""
		Adds a heading, listing it in the table of contents when it is level 2 or 3

		@param level The heading level, 2 for ##
		@param text The heading text
		"""
		slug = anchor(text)
		count = self.anchors.get(slug, 0)
		self.anchors[slug] = count + 1
		if count > 0:
			slug += '-' + str(count)
		if level <= 3:
			self.contents.append('  ' * (level - 2) + '- [' + text + '](#' + slug + ')')
		self.lines += ['#' * level + ' ' + text, '']

	def text(self, paragraph):
		"""
		Adds a paragraph

		@param paragraph The paragraph's text
		"""
		self.lines += [paragraph, '']

	def table(self, headings, rows):
		"""
		Adds a table

		@param headings The column headings
		@param rows The rows, each a list of cell texts
		"""
		self.lines += table(headings, rows) + ['']


def main():
	"""
	Reads the game's lists and writes NAMES.md
	"""
	prefabDump = loadPrefabDump()
	parser = argparse.ArgumentParser(description='Writes TweakStats/NAMES.md from the game\'s files')
	parser.add_argument('--valheim', default=os.environ.get('ValheimPath', prefabDump.defaultValheimPath), help='Valheim install folder')
	args = parser.parse_args()
	assemblyPath = os.path.join(args.valheim, 'valheim_Data', 'Managed', 'assembly_valheim.dll')

	itemTypes = readEnum(assemblyPath, 'ItemDrop+ItemData+ItemType')
	skills = readEnum(assemblyPath, 'Skills+SkillType')
	pieceCategories = readEnum(assemblyPath, 'Piece+PieceCategory')
	factions = readEnum(assemblyPath, 'Character+Faction')
	gameVersion = readGameVersion(assemblyPath)
	words = readLocalization(args.valheim)

	root = prefabDump.loadPrefab(args.valheim, 'Assets/Systems/_GameMain.prefab')
	objectDB = None
	zNetScene = None
	for gameObject, _ in prefabDump.walk(root):
		for component in prefabDump.components(gameObject):
			name = scriptName(component)
			if name == 'ObjectDB':
				objectDB = component.read(check_read=False)
			elif name == 'ZNetScene':
				zNetScene = component.read(check_read=False)
	if objectDB is None or zNetScene is None:
		raise SystemExit('Couldn\'t find the ObjectDB and ZNetScene in _GameMain')

	# Items a player can carry - creature attacks are items too, but have no icon
	weaponsBySkill = {}
	itemsByType = {}
	pieceTables = []
	for pointer in objectDB.m_items:
		gameObject = deref(pointer)
		if gameObject is None:
			continue
		_, item = findComponent(prefabDump, gameObject, ('ItemDrop',))
		if item is None:
			continue
		shared = item.m_itemData.m_shared
		if len(shared.m_icons) == 0:
			continue
		prefabName = gameObject.read().m_Name
		typeName = itemTypes.get(shared.m_itemType, str(shared.m_itemType))
		skillName = skills.get(shared.m_skillType, str(shared.m_skillType))
		row = ['`[' + prefabName + ']`', localize(words, shared.m_name)]
		if typeName in weaponTypeHeadings:
			weaponsBySkill.setdefault(skillName, {}).setdefault(typeName, []).append(row)
		else:
			itemsByType.setdefault(typeName, []).append(row + ([skillName] if typeName in skilledTypes else []))
		buildPieces = deref(shared.m_buildPieces)
		if buildPieces is not None:
			pieceTables.append((localize(words, shared.m_name), buildPieces.read(check_read=False)))

	recipes = []
	resources = {}
	for pointer in objectDB.m_recipes:
		recipeObject = deref(pointer)
		if recipeObject is None:
			continue
		recipe = recipeObject.read(check_read=False)
		itemObject = deref(recipe.m_item)
		if itemObject is None:
			continue
		item = itemObject.read(check_read=False)
		stationObject = deref(recipe.m_craftingStation)
		station = ''
		if stationObject is not None:
			stationData = stationObject.read(check_read=False)
			station = '`' + gameObjectName(stationData) + '` (' + localize(words, stationData.m_name) + ')'
			if recipe.m_minStationLevel > 1:
				station += ' level ' + str(recipe.m_minStationLevel)
		makes = localize(words, item.m_itemData.m_shared.m_name)
		if recipe.m_amount > 1:
			makes += ' x' + str(recipe.m_amount)
		if not recipe.m_enabled:
			makes += ' (disabled)'
		recipes.append(['`[Recipe:' + gameObjectName(item) + ']`', recipe.m_Name, makes, station])
		countResources(recipe.m_resources, words, resources, 2)

	creatures = []
	for pointer in zNetScene.m_prefabs:
		gameObject = deref(pointer)
		if gameObject is None:
			continue
		className, character = findComponent(prefabDump, gameObject, ('Character', 'Humanoid'))
		if character is None:
			continue
		creatures.append(['`[Creature:' + gameObject.read().m_Name + ']`', localize(words, character.m_name), factions.get(character.m_faction, str(character.m_faction)), 'yes' if character.m_boss else ''])

	piecesByTool = []
	for toolName, pieceTable in pieceTables:
		pieces = []
		seen = set()
		for pointer in pieceTable.m_pieces:
			gameObject = deref(pointer)
			if gameObject is None:
				continue
			prefabName = gameObject.read().m_Name
			if prefabName in seen:
				continue
			seen.add(prefabName)
			_, piece = findComponent(prefabDump, gameObject, ('Piece',))
			if piece is None:
				continue
			stationObject = deref(piece.m_craftingStation)
			station = ('`' + gameObjectName(stationObject.read(check_read=False)) + '`') if stationObject is not None else ''
			pieces.append(['`[Piece:' + prefabName + ']`', localize(words, piece.m_name), pieceCategories.get(piece.m_category, str(piece.m_category)), station])
			countResources(piece.m_resources, words, resources, 3)
		piecesByTool.append((toolName, pieces))

	effects = []
	for pointer in objectDB.m_StatusEffects:
		effectObject = deref(pointer)
		if effectObject is None:
			continue
		effect = effectObject.read(check_read=False)
		effects.append(['`[Effect:' + effect.m_Name + ']`', localize(words, effect.m_name), scriptName(effectObject) or ''])

	document = Document()

	document.heading(2, 'Weapons')
	document.text('Grouped by the skill they use, then split by how they\'re held when a skill has more than one kind.')
	# Alphabetical by heading, with weapons that use no skill last since they aren't a skill
	skillHeadings = [(skill, weaponSkillHeadings.get(skill, splitWords(skill))) for skill in weaponsBySkill]
	for skill, heading in sorted(skillHeadings, key=lambda pair: (pair[0] == 'None', sortKey(pair[1]))):
		document.heading(3, heading)
		document.text('Also `[Skill:' + skill + ']` for all of them.')
		types = sorted(((typeName, weaponTypeHeadings[typeName]) for typeName in weaponsBySkill[skill]), key=lambda pair: sortKey(pair[1]))
		for typeName, typeHeading in types:
			if len(types) > 1:
				document.heading(4, typeHeading)
			document.table(['Section', 'Name'], sorted(weaponsBySkill[skill][typeName], key=lambda row: sortKey(row[0])))

	for title, isEquipment in (('Equipment', True), ('Other Items', False)):
		document.heading(2, title)
		typeHeadings = [(typeName, itemTypeHeadings.get(typeName, typeName)) for typeName in itemsByType if (typeName in equipmentTypes) == isEquipment]
		for typeName, heading in sorted(typeHeadings, key=lambda pair: sortKey(pair[1])):
			document.heading(3, heading)
			document.text('Also `[Type:' + typeName + ']` for all of them.')
			headings = ['Section', 'Name', 'Skill'] if typeName in skilledTypes else ['Section', 'Name']
			document.table(headings, sorted(itemsByType[typeName], key=lambda row: sortKey(row[0])))

	document.heading(2, 'Recipes')
	document.table(['Section', 'Recipe', 'Makes', 'Station'], sorted(recipes, key=lambda row: sortKey(row[0] + row[1])))

	document.heading(2, 'Resources')
	document.text('Every item a recipe or build piece costs, with the stat that changes how many it needs, e.g. `resources.Wood = 5` - and how many recipes and build pieces use it. Any other item can be added to a cost the same way.')
	resourceRows = [[row[0], row[1], str(row[2]) if row[2] > 0 else '', str(row[3]) if row[3] > 0 else ''] for row in resources.values()]
	document.table(['Stat', 'Name', 'Recipes', 'Build Pieces'], sorted(resourceRows, key=lambda row: sortKey(row[0])))

	document.heading(2, 'Creatures')
	document.table(['Section', 'Name', 'Faction', 'Boss'], sorted(creatures, key=lambda row: sortKey(row[0])))

	document.heading(2, 'Build Pieces')
	for toolName, pieces in sorted(piecesByTool, key=lambda pair: sortKey(pair[0])):
		document.heading(3, toolName)
		document.table(['Section', 'Name', 'Category', 'Station'], sorted(pieces, key=lambda row: sortKey(row[0])))

	document.heading(2, 'Status Effects')
	document.table(['Section', 'Name', 'Kind'], sorted(effects, key=lambda row: sortKey(row[0])))

	lines = ['# Tweak Stats - Names', '']
	lines.append('Every item, recipe, creature, build piece and status effect in Valheim ' + gameVersion + ', with its name in game and the section that changes it in `kriona.TweakStats.cfg`. Generated on ' + datetime.date.today().isoformat() + ' by `tools/tweakstats-names.py`.')
	lines.append('')
	lines.append('Items and mods\' additions can also be looked up in game with the `tweakstats find` console command - see the [README](README.md#console-commands).')
	lines.append('')
	lines.append('A name shown as a code like `$item_iceshoes` has no English name in the game\'s files - these are usually unused or unfinished.')
	lines.append('')
	lines += document.contents
	lines.append('')
	lines += document.lines

	with open(outputPath, 'w', encoding='utf-8', newline='\n') as file:
		file.write('\n'.join(lines))
	itemCount = sum(len(rows) for rows in itemsByType.values()) + sum(len(rows) for types in weaponsBySkill.values() for rows in types.values())
	pieceCount = sum(len(pieces) for _, pieces in piecesByTool)
	print('Wrote %d items, %d recipes, %d creatures, %d pieces and %d status effects to %s' % (itemCount, len(recipes), len(creatures), pieceCount, len(effects), outputPath))


if __name__ == '__main__':
	main()
