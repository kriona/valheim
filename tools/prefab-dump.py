"""
Dumps Valheim prefabs from the game's asset bundles, for working out how a piece or item is built

Usage:
	python -P tools/prefab-dump.py find <regex>
	python -P tools/prefab-dump.py tree <prefab>
	python -P tools/prefab-dump.py materials <prefab> [--save <dir>]
	python -P tools/prefab-dump.py particles <prefab>

<prefab> is a prefab name like piece_walltorch or a path from find. The bundle holding the prefab is looked up in
the SoftRef manifest and loaded along with its dependencies, which takes a few seconds. Needs UnityPy:
	python -m pip install --user UnityPy

The game is read from the ValheimPath environment variable, or --valheim, or the default Steam folder
"""

import argparse
import os
import re
import sys

import UnityPy

defaultValheimPath = r'C:\Program Files (x86)\Steam\steamapps\common\Valheim'
softRefFolder = os.path.join('valheim_Data', 'StreamingAssets', 'SoftRef')


def formatColor(color):
	"""
	Formats a color as (r,g,b,a) with two decimals

	@param color A UnityPy color with r, g, b and a
	@return The formatted color
	"""
	return('(%.2f,%.2f,%.2f,%.2f)' % (color.r, color.g, color.b, color.a))


def formatGradient(gradient):
	"""
	Formats a gradient's color keys and alpha keys with their times from 0 to 1

	@param gradient A UnityPy gradient
	@return The color keys followed by the alpha keys
	"""
	colors = []
	for i in range(gradient.m_NumColorKeys):
		key = getattr(gradient, 'key%d' % i)
		colors.append('%s@%.2f' % (formatColor(key), getattr(gradient, 'ctime%d' % i) / 65535))
	alphas = []
	for i in range(gradient.m_NumAlphaKeys):
		key = getattr(gradient, 'key%d' % i)
		alphas.append('%.2f@%.2f' % (key.a, getattr(gradient, 'atime%d' % i) / 65535))
	return(' '.join(colors) + ' | alpha ' + ' '.join(alphas))


def formatMinMaxGradient(value):
	"""
	Formats a particle color setting in the form its mode uses

	@param value A UnityPy MinMaxGradient
	@return The mode and its colors or gradients
	"""
	mode = value.minMaxState
	if mode == 0:
		return('color ' + formatColor(value.maxColor))
	if mode == 1:
		return('gradient [' + formatGradient(value.maxGradient) + ']')
	if mode == 2:
		return('two colors ' + formatColor(value.minColor) + ' ' + formatColor(value.maxColor))
	if mode == 3:
		return('two gradients [' + formatGradient(value.minGradient) + '] [' + formatGradient(value.maxGradient) + ']')
	return('random color [' + formatGradient(value.maxGradient) + ']')


def readManifests(softRefPath):
	"""
	Reads the bundle of every asset and the dependencies of every bundle from the SoftRef manifests

	@param softRefPath The SoftRef folder
	@return A dict of bundle by asset path and a dict of dependency lists by bundle
	"""
	bundles = {}
	bundle = None
	with open(os.path.join(softRefPath, 'manifest_extended'), encoding='utf-8') as file:
		for line in file:
			line = line.strip()
			if line.startswith('bundle: '):
				bundle = line[len('bundle: '):]
			elif line.startswith('path in bundle: '):
				bundles[line[len('path in bundle: '):]] = bundle
	dependencies = {}
	current = None
	with open(os.path.join(softRefPath, 'manifest'), encoding='utf-8') as file:
		for line in file:
			if line.startswith('- bundle: '):
				current = line[len('- bundle: '):].strip()
				dependencies[current] = []
			elif current is not None and line.startswith('  - '):
				dependencies[current].append(line[len('  - '):].strip())
	return(bundles, dependencies)


def findPrefabPath(bundles, prefab):
	"""
	Finds the asset path of a prefab from its name or path, exiting with the matches when there isn't exactly one

	@param bundles Bundle by asset path
	@param prefab A prefab name like piece_walltorch, or an asset path
	@return The prefab's asset path
	"""
	if prefab in bundles:
		return(prefab)
	suffix = '/' + prefab.lower() + '.prefab'
	matches = [path for path in bundles if path.lower().endswith(suffix)]
	if len(matches) == 1:
		return(matches[0])
	if len(matches) == 0:
		sys.exit('No prefab named ' + prefab + ' - try: find ' + prefab)
	sys.exit('Several prefabs named ' + prefab + ', pass one of these paths:\n' + '\n'.join(matches))


def loadPrefab(valheimPath, prefab):
	"""
	Loads the bundle holding a prefab and every bundle it depends on

	@param valheimPath The Valheim install folder
	@param prefab A prefab name or asset path
	@return The prefab's root GameObject reader
	"""
	softRefPath = os.path.join(valheimPath, softRefFolder)
	bundles, dependencies = readManifests(softRefPath)
	path = findPrefabPath(bundles, prefab)
	needed = []
	pending = [bundles[path]]
	while pending:
		bundle = pending.pop()
		if bundle in needed:
			continue
		needed.append(bundle)
		pending.extend(dependencies.get(bundle, []))
	print('Loading ' + path + ' from ' + ', '.join(needed), file=sys.stderr)
	env = UnityPy.load(*[os.path.join(softRefPath, 'Bundles', bundle) for bundle in needed])
	for containerPath, obj in env.container.items():
		if containerPath.lower() == path.lower():
			return(obj.deref() if hasattr(obj, 'deref') else obj)
	sys.exit('Couldn\'t find ' + path + ' in its bundle')


def walk(gameObject, depth=0):
	"""
	Yields every GameObject in a hierarchy with its depth, parents before children

	@param gameObject The root GameObject reader
	@param depth The root's depth
	@return A generator of (GameObject reader, depth)
	"""
	yield (gameObject, depth)
	transform = None
	for component in components(gameObject):
		if component.type.name in ('Transform', 'RectTransform'):
			transform = component
			break
	if transform is None:
		return
	for child in transform.read().m_Children:
		yield from walk(child.read().m_GameObject.deref(), depth + 1)


def components(gameObject):
	"""
	The component readers on a GameObject, skipping any that can't be resolved

	@param gameObject A GameObject reader
	@return A list of component readers
	"""
	result = []
	for entry in gameObject.read().m_Component:
		try:
			result.append(entry.component.deref())
		except Exception:
			pass
	return(result)


def componentName(component):
	"""
	A component's type name, with the script name for MonoBehaviours

	@param component A component reader
	@return The type name, like Light or MonoBehaviour Fireplace
	"""
	name = component.type.name
	if name != 'MonoBehaviour':
		return(name)
	try:
		return('MonoBehaviour ' + component.read(check_read=False).m_Script.read().m_ClassName)
	except Exception:
		return(name)


def materials(component):
	"""
	The materials on a renderer, skipping empty slots

	@param component A renderer reader
	@return A list of Material objects
	"""
	result = []
	for pointer in component.read().m_Materials:
		if pointer.path_id == 0:
			continue
		try:
			result.append(pointer.read())
		except Exception as error:
			print('    (couldn\'t read a material: ' + str(error) + ')')
	return(result)


def shaderName(material):
	"""
	The name of a material's shader

	@param material A Material object
	@return The shader name, or ? when it can't be read
	"""
	try:
		shader = material.m_Shader.read()
		return(shader.m_ParsedForm.m_Name)
	except Exception:
		return('?')


def summarizeMaterial(material):
	"""
	A material's name, shader, keywords and color properties on one line

	@param material A Material object
	@return The summary
	"""
	colors = ', '.join(name + ' ' + formatColor(color) for name, color in material.m_SavedProperties.m_Colors)
	keywords = (getattr(material, 'm_ValidKeywords', None) or getattr(material, 'm_ShaderKeywords', None) or [])
	return(material.m_Name + ' [' + shaderName(material) + '] keywords ' + str(keywords) + ' ' + colors)


def commandFind(args):
	"""
	Lists the bundle and path of every prefab whose path matches a regex

	@param args The parsed arguments
	"""
	bundles, _ = readManifests(os.path.join(args.valheim, softRefFolder))
	pattern = re.compile(args.pattern, re.IGNORECASE)
	for path in sorted(bundles):
		if path.endswith('.prefab') and pattern.search(path):
			print(bundles[path] + '  ' + path)


def commandTree(args):
	"""
	Prints a prefab's hierarchy with each object's components, and the colors of its lights, particles and materials

	@param args The parsed arguments
	"""
	root = loadPrefab(args.valheim, args.prefab)
	for gameObject, depth in walk(root):
		indent = '    ' * depth
		data = gameObject.read()
		print(indent + data.m_Name + ('' if data.m_IsActive else ' (inactive)'))
		for component in components(gameObject):
			name = componentName(component)
			if name in ('Transform', 'RectTransform'):
				continue
			print(indent + '  - ' + name)
			if name == 'Light':
				light = component.read()
				print(indent + '      color ' + formatColor(light.m_Color) + ' intensity ' + str(light.m_Intensity) + ' range ' + str(light.m_Range))
			elif name == 'ParticleSystem':
				system = component.read()
				print(indent + '      start ' + formatMinMaxGradient(system.InitialModule.startColor))
				if system.ColorModule.enabled:
					print(indent + '      over lifetime ' + formatMinMaxGradient(system.ColorModule.gradient))
				if system.CustomDataModule.enabled:
					print(indent + '      custom data - see particles')
			elif name in ('MeshRenderer', 'SkinnedMeshRenderer', 'ParticleSystemRenderer'):
				for material in materials(component):
					print(indent + '      ' + summarizeMaterial(material))


def commandMaterials(args):
	"""
	Prints every property and texture of each material used in a prefab, saving the textures when asked

	@param args The parsed arguments
	"""
	root = loadPrefab(args.valheim, args.prefab)
	seen = set()
	for gameObject, _ in walk(root):
		for component in components(gameObject):
			if component.type.name not in ('MeshRenderer', 'SkinnedMeshRenderer', 'ParticleSystemRenderer'):
				continue
			for material in materials(component):
				if material.m_Name in seen:
					continue
				seen.add(material.m_Name)
				printMaterial(material, args.save)


def printMaterial(material, saveFolder):
	"""
	Prints a material's shader, keywords, textures, floats and colors

	@param material A Material object
	@param saveFolder Folder to save the textures into as PNGs, or None
	"""
	print('===== ' + summarizeMaterial(material))
	properties = material.m_SavedProperties
	for name, environment in properties.m_TexEnvs:
		pointer = environment.m_Texture
		if pointer.path_id == 0:
			continue
		try:
			texture = pointer.read()
			image = texture.image
			average = image.convert('RGBA').resize((1, 1)).getpixel((0, 0))
			print('  texture ' + name + ' ' + texture.m_Name + ' %dx%d' % (texture.m_Width, texture.m_Height) + ' average ' + str(average))
			if saveFolder:
				os.makedirs(saveFolder, exist_ok=True)
				image.save(os.path.join(saveFolder, texture.m_Name + '.png'))
		except Exception as error:
			print('  texture ' + name + ' (couldn\'t read: ' + str(error) + ')')
	print('  floats ' + ', '.join('%s %g' % (name, value) for name, value in properties.m_Floats))
	print('  colors ' + ', '.join(name + ' ' + formatColor(color) for name, color in properties.m_Colors))


def commandParticles(args):
	"""
	Prints every color setting of each particle system in a prefab, with the vertex streams its renderer sends

	@param args The parsed arguments
	"""
	root = loadPrefab(args.valheim, args.prefab)
	for gameObject, _ in walk(root):
		for component in components(gameObject):
			name = component.type.name
			if name == 'ParticleSystem':
				system = component.read()
				print('===== ' + gameObject.read().m_Name)
				print('  start ' + formatMinMaxGradient(system.InitialModule.startColor))
				print('  over lifetime ' + ('on ' if system.ColorModule.enabled else 'off ') + formatMinMaxGradient(system.ColorModule.gradient))
				custom = system.CustomDataModule
				if custom.enabled:
					for index in (0, 1):
						mode = getattr(custom, 'mode%d' % index)
						# Mode 2 is Color, the only mode that holds colors
						if mode == 2:
							print('  custom %d ' % (index + 1) + formatMinMaxGradient(getattr(custom, 'color%d' % index)))
						else:
							print('  custom %d mode %d' % (index + 1, mode))
			elif name == 'ParticleSystemRenderer':
				streams = getattr(component.read(), 'm_VertexStreams', b'')
				print('  vertex streams ' + str(list(streams)))


def main():
	"""
	Parses the command line and runs the command
	"""
	parser = argparse.ArgumentParser(description='Dumps Valheim prefabs from the game\'s asset bundles')
	parser.add_argument('--valheim', default=os.environ.get('ValheimPath', defaultValheimPath), help='Valheim install folder')
	commands = parser.add_subparsers(dest='command', required=True)
	find = commands.add_parser('find', help='list prefabs whose path matches a regex')
	find.add_argument('pattern')
	find.set_defaults(run=commandFind)
	tree = commands.add_parser('tree', help='print the hierarchy and components')
	tree.add_argument('prefab')
	tree.set_defaults(run=commandTree)
	materialsParser = commands.add_parser('materials', help='print every material property and texture')
	materialsParser.add_argument('prefab')
	materialsParser.add_argument('--save', help='folder to save the textures into as PNGs')
	materialsParser.set_defaults(run=commandMaterials)
	particles = commands.add_parser('particles', help='print every particle color setting')
	particles.add_argument('prefab')
	particles.set_defaults(run=commandParticles)
	args = parser.parse_args()
	args.run(args)


if __name__ == '__main__':
	main()
