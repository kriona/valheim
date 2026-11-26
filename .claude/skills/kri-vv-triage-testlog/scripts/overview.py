"""
Prints an overview of a Viking Villages test run's file, for the kri-vv-triage-testlog skill to start from - how the run
went, the build of the mod it ran and the commits since, how many of each kind of line it wrote and where in the file,
the exceptions, who and where each kind of line was about most, each viking's lives, the snapshots' numbers and hitches,
the villages and the totals through the run, the longest quiet stretches and the totals it ended with

Usage:
	python -P .claude/skills/kri-vv-triage-testlog/scripts/overview.py [log]

The log is a path, or the name of a run's file in BepInEx\\VikingVillagesTestLog like Swamp or Swamp-2.log, or with
none given, the newest file there. The game is found from the ValheimPath environment variable, or --valheim, or the
default Steam folder
"""

import argparse
import collections
import datetime
import os
import re
import subprocess
import sys

defaultValheimPath = r'C:\Program Files (x86)\Steam\steamapps\common\Valheim'

# The repo, four folders up from this script
repoFolder = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..', '..'))

# The folders the mod is built from, for the commits since its build
modFolders = ('VikingVillages', 'Shared')

# When the run started, from its header
startedPattern = re.compile(r'^Started: (\d{4}-\d\d-\d\d \d\d:\d\d:\d\d)')

# A line the plugin wrote while the run was going, like "[Warning] [07:25:27 day 37 12:55] Stuck: ..."
entryPattern = re.compile(r'^\[(\w+)\] \[(\d\d):(\d\d):(\d\d) day (\d+) (\d\d:\d\d)\] (.*)$')

# A kind of line named before its first colon, like "Stuck" or "Given up"
kindPattern = re.compile(r'^([A-Z][A-Za-z ]{0,30}):')

# Who or what a line is about and where, right after its kind, like "Yrsa the Stout at (5209, 33, 1621)"
subjectPattern = re.compile(r'^(.+?) at \((-?[\d.]+), (-?[\d.]+), (-?[\d.]+)\)')

# A viking named by the lines that say who each viking is, like "New viking: Thorgrim at (5272, 31, 1648)"
namePattern = re.compile(r'^(?:New viking: |Death: |Snapshot of )(.+?) at \(')

# A position, like " at (5209, 33, 1621)"
positionPattern = re.compile(r' at \([^)]*\)')

# A snapshot's numbers
snapshotPattern = re.compile(r'^Snapshot: (\S+) fps on average, slowest frame (\d+)ms, (\d+) hitches - (\d+) ZDOs, (\d+) objects loaded, (\d+) items lying loaded, (\d+) vikings loaded')

# An exception counted again in a snapshot
repeatPattern = re.compile(r'^Snapshot: this exception came up (\d+) more times - (.*)$')

# A line of the totals, like "Totals - Eaten: 20 - Cooked Fish 10, Deer Stew 10"
totalPattern = re.compile(r'^Totals - (.+?): (-?[\d.]+)')

# A viking coming to the village, like "New viking: Thorgrim at (5272, 31, 1648), rolled up for the Swamp"
arrivalPattern = re.compile(r'^New viking: (.+?) at \(')

# A viking's death, like "Death: Thorgrim at (5298, 29, 1665) died - last hit by ZombieViking (EnemyHit, 33.7 damage)"
deathPattern = re.compile(r'^Death: (.+?) at \([^)]*\) died - (?:last hit by (.+?) \((\w+)|(no hit noted))')

# The job a line names, like "at its job: Loot," or "- job: Rest inside"
jobPattern = re.compile(r'job: ([A-Za-z]+)')

# A hitch, like "Hitch: a frame took 578ms"
hitchPattern = re.compile(r'^Hitch: a frame took (\d+)ms')

# Parenthesized words right after a line's subject, like "(Loot)"
leadingAsidePattern = re.compile(r'^\s*(\([^)]*\)\s*)+')

# How many points through the run the totals and the village are shown at
samplesShown = 6

# The levels, most serious first
levelOrder = {'Fatal': 0, 'Error': 1, 'Warning': 2, 'Message': 3, 'Info': 4, 'Debug': 5}

# Kinds whose subjects and places aren't listed - they're summaries, or about the whole village
unplacedKinds = ('Totals', 'Snapshot', 'Snapshot of a village', 'Snapshot of a viking', 'Snapshot of an exception', 'Snapshot of travel', 'Snapshot of neighbor news', 'Exception', 'Hitch', 'Watch')

# How big a place is when lines are grouped by where they happened, in meters
placeSize = 4

# The fewest lines of a kind for its subjects and places to be listed, and how many of each are listed
subjectsFrom = 5
subjectsListed = 5

# How many of the longest quiet stretches are listed, and the shortest that's listed, in seconds
gapsListed = 8
gapShortest = 120

# How much of a line is shown as an example, in characters
exampleLength = 150


class Entry:
	"""
	A line the plugin wrote while the run was going, with the lines that follow it without a time of their own, like a
	stuck viking's trace or an exception's stack
	"""

	def __init__(self, lineNumber, level, elapsed, day, clock, text):
		"""
		Makes an entry

		@param lineNumber Its line in the file, from 1
		@param level Its log level, like Warning
		@param elapsed Seconds since midnight on the day the run started
		@param day The game's day
		@param clock The game's time of day, like 08:31
		@param text The line after its level and time
		"""
		self.lineNumber = lineNumber
		self.level = level
		self.elapsed = elapsed
		self.day = day
		self.clock = clock
		self.text = text
		self.kind = ''
		self.more = []

	def when(self):
		"""
		Writes when the entry was written, for the overview

		@return Like "line 530 (07:25:27, day 37 12:55)"
		"""
		return('line ' + str(self.lineNumber) + ' (' + realClock(self.elapsed) + ', day ' + str(self.day) + ' ' + self.clock + ')')


def kindOf(text, names):
	"""
	Works out what kind of line a line is - the words before its first colon, like Stuck or Given up, with snapshots and
	totals told apart by what they're of, and when it has no kind before a colon, its first words without positions or
	numbers, and with each viking's name as <viking> so the lines about different vikings are one kind

	@param text The line, after its level and time
	@param names A pattern matching every viking's name, or None when there are none
	@return The kind
	"""
	if text.startswith('Totals'):
		return('Totals')
	if text.startswith('Snapshot of the village'):
		return('Snapshot of a village')
	if text.startswith('Snapshot of travel') or text.startswith('Snapshot of neighbor news'):
		return(text.split(':')[0])
	if text.startswith('Snapshot of '):
		return('Snapshot of a viking')
	if text.startswith('Snapshot: this exception'):
		return('Snapshot of an exception')
	if text.startswith('Watch of the village'):
		return('Watch')
	if text.startswith('Exception'):
		return('Exception')
	match = kindPattern.match(text)
	if match is not None:
		return(match.group(1))
	plain = positionPattern.sub('', text)
	if names is not None:
		plain = names.sub('<viking>', plain)
	words = re.sub(r'\(.*?\)|\d+', '', plain).split()
	return(' '.join(words[:3]))


def assignKinds(entries):
	"""
	Works out each entry's kind (see kindOf), once every viking's name is known from the whole run

	@param entries The entries
	"""
	found = set()
	for entry in entries:
		match = namePattern.match(entry.text)
		if match is not None and not match.group(1).startswith('the village'):
			found.add(match.group(1))
	# The longest first, so a name that starts another doesn't take its place
	names = (re.compile('|'.join(re.escape(name) for name in sorted(found, key=len, reverse=True))) if found else None)
	for entry in entries:
		entry.kind = kindOf(entry.text, names)


def spread(items, count):
	"""
	Picks items spread evenly through a list, its first and last among them

	@param items The list
	@param count How many to pick at most
	@return The items picked, in order
	"""
	if len(items) <= count:
		return(list(items))
	return([items[round(index * (len(items) - 1) / (count - 1))] for index in range(count)])


def counted(counter, listed):
	"""
	Writes the most common things a counter counted, with their counts

	@param counter The counter
	@param listed How many to write at most
	@return Like "Thorgrim 12, Liv 3"
	"""
	return(', '.join(thing + ' ' + str(count) for thing, count in counter.most_common(listed)))


def realClock(elapsed):
	"""
	Writes a time of day from seconds since midnight on the day the run started

	@param elapsed The seconds
	@return Like "07:25:27", with "+1d " in front on the next day
	"""
	days, seconds = divmod(int(elapsed), 86400)
	text = '%02d:%02d:%02d' % (seconds // 3600, (seconds // 60) % 60, seconds % 60)
	return((('+' + str(days) + 'd ') if days > 0 else '') + text)


def findLog(name, valheimPath):
	"""
	Finds the run's file to look at

	@param name A path, the name of a file in the test log folder with or without .log, or '' for the newest there
	@param valheimPath The Valheim install folder
	@return The file's path
	"""
	folder = os.path.join(valheimPath, 'BepInEx', 'VikingVillagesTestLog')
	if name and os.path.isfile(name):
		return(name)
	if name:
		path = os.path.join(folder, (name if name.lower().endswith('.log') else name + '.log'))
		if not os.path.isfile(path):
			raise SystemExit('Found no run called ' + name + ' in ' + folder)
		return(path)
	if not os.path.isdir(folder):
		raise SystemExit('Found no test log folder at ' + folder)
	logs = [os.path.join(folder, file) for file in os.listdir(folder) if file.lower().endswith('.log')]
	if not logs:
		raise SystemExit('Found no runs in ' + folder)
	return(max(logs, key=os.path.getmtime))


def readRun(path):
	"""
	Reads a run's file into its header, its entries and the line saying how it ended

	@param path The file
	@return The header's lines, the entries, and the Ended line or None when the file has none
	"""
	header = []
	entries = []
	ended = None
	dayOffset = 0
	last = 0
	with open(path, encoding='utf-8', errors='replace') as file:
		for lineNumber, line in enumerate(file, 1):
			line = line.rstrip('\n')
			match = entryPattern.match(line)
			if match is None:
				if line.startswith('Ended: '):
					ended = line
				elif entries:
					entries[-1].more.append(line)
				elif line.strip():
					header.append(line)
				continue
			level, hours, minutes, seconds, day, clock, text = match.groups()
			elapsed = int(hours) * 3600 + int(minutes) * 60 + int(seconds) + dayOffset
			# The time of day starts over at midnight
			if elapsed < last - 3600:
				dayOffset += 86400
				elapsed += 86400
			last = elapsed
			entries.append(Entry(lineNumber, level, elapsed, int(day), clock, text))
	assignKinds(entries)
	return(header, entries, ended)


def printRun(path, header, entries, ended):
	"""
	Prints how the run went - its file, header, how long it ran and how it ended

	@param path The file
	@param header The header's lines
	@param entries The entries
	@param ended The Ended line, or None
	"""
	print('# Run')
	print(path + ' - ' + str(os.path.getsize(path) // 1024) + ' KB, last written ' + datetime.datetime.fromtimestamp(os.path.getmtime(path)).strftime('%Y-%m-%d %H:%M'))
	for line in header:
		print(line)
	print(ended if ended is not None else 'No Ended line - the game may have crashed or closed without ending the run, or it is still going')
	if entries:
		first = entries[0]
		lastEntry = entries[-1]
		print('Entries from ' + first.when() + ' to ' + lastEntry.when() + ' - ' + str(round((lastEntry.elapsed - first.elapsed) / 60)) + ' minutes, game days ' + str(first.day) + ' to ' + str(lastEntry.day) + ', ' + str(len(entries)) + ' entries')
	for entry in entries:
		if entry.text.startswith('Test run'):
			print(entry.when() + ': ' + entry.text)
	folder = os.path.dirname(path)
	others = sorted(file for file in os.listdir(folder) if file.lower().endswith('.log') and os.path.join(folder, file) != path)
	if others:
		print('Other runs in the folder: ' + ', '.join(file + ' (' + datetime.datetime.fromtimestamp(os.path.getmtime(os.path.join(folder, file))).strftime('%Y-%m-%d') + ')' for file in others))
	print()


def printBuild(valheimPath, header):
	"""
	Prints when the installed mod was built, and the commits to it since, which the run may not have had

	@param valheimPath The Valheim install folder
	@param header The run's header lines, for when it started
	"""
	print('# Build')
	plugins = os.path.join(valheimPath, 'BepInEx', 'plugins')
	found = []
	for folder, _, files in os.walk(plugins):
		for file in files:
			if file.lower() == 'vikingvillages.dll':
				found.append(os.path.join(folder, file))
	if not found:
		print('Found no VikingVillages.dll in ' + plugins)
		print()
		return
	built = datetime.datetime.fromtimestamp(max(os.path.getmtime(path) for path in found))
	for path in found:
		print(path + ' - written ' + datetime.datetime.fromtimestamp(os.path.getmtime(path)).strftime('%Y-%m-%d %H:%M:%S'))
	for line in header:
		match = startedPattern.match(line)
		if match is not None and built > datetime.datetime.strptime(match.group(1), '%Y-%m-%d %H:%M:%S'):
			print('It was written after the run started, so the run had an earlier build - the commits below are from later than the run\'s')
	result = subprocess.run(['git', 'log', '--since=' + built.strftime('%Y-%m-%d %H:%M:%S'), '--format=%h %ci %s', '--'] + list(modFolders), cwd=repoFolder, capture_output=True, text=True, encoding='utf-8')
	commits = result.stdout.strip()
	print('Commits to the mod since, which the run may not have had - one a few minutes after may well have been in it, as the user often builds before committing:')
	print(commits if commits else ('None' if result.returncode == 0 else 'Couldn\'t read them: ' + result.stderr.strip()))
	print()


def printKinds(entries):
	"""
	Prints how many of each kind of line the run wrote, at each level, with where the first and last are and the first
	as an example - the most serious levels first, then the most common kinds

	@param entries The entries
	"""
	groups = collections.OrderedDict()
	for entry in entries:
		groups.setdefault((entry.level, entry.kind), []).append(entry)
	print('# Kinds of line')
	print('count | level | kind | first | last | first example')
	for (level, kind), group in sorted(groups.items(), key=lambda item: (levelOrder.get(item[0][0], 9), -len(item[1]))):
		example = group[0].text[:exampleLength]
		print(str(len(group)) + ' | ' + level + ' | ' + kind + ' | ' + group[0].when() + ' | ' + group[-1].when() + ' | ' + example)
	print()


def printExceptions(entries):
	"""
	Prints each kind of exception where it was first written, with the start of its stack, and how many more times each
	came up in all

	@param entries The entries
	"""
	print('# Exceptions')
	found = False
	for entry in entries:
		if entry.kind == 'Exception':
			found = True
			print(entry.when() + ': ' + entry.text[:300])
			for line in entry.more[:4]:
				print('    ' + line[:200])
	repeats = collections.Counter()
	for entry in entries:
		match = repeatPattern.match(entry.text)
		if match is not None:
			repeats[match.group(2)[:200]] += int(match.group(1))
	for key, count in repeats.most_common():
		print(str(count) + ' more times in all: ' + key)
	if not found:
		print('None')
	print()


def printSubjects(entries):
	"""
	Prints, for each common kind of line named before its colon, who or what it was most about, the places it happened
	most, in squares placeSize across, the two together, what the line says they were doing, the jobs it names, and what
	follows its first dash, which is often why - so one viking, item, spot or reason that comes up over and over stands
	out

	@param entries The entries
	"""
	groups = collections.OrderedDict()
	for entry in entries:
		if entry.kind in unplacedKinds or not entry.text.startswith(entry.kind + ':'):
			continue
		rest = entry.text[len(entry.kind) + 1:].strip()
		match = subjectPattern.match(rest)
		if match is None:
			continue
		groups.setdefault((entry.level, entry.kind), []).append((match, rest[match.end():]))
	print('# Who and where, for kinds with ' + str(subjectsFrom) + ' or more lines')
	for (level, kind), found in sorted(groups.items(), key=lambda item: (levelOrder.get(item[0][0], 9), -len(item[1]))):
		if len(found) < subjectsFrom:
			continue
		subjects = collections.Counter()
		places = collections.Counter()
		pairs = collections.Counter()
		doings = collections.Counter()
		jobs = collections.Counter()
		reasons = collections.Counter()
		for match, after in found:
			place = '(' + str(round(float(match.group(2)) / placeSize) * placeSize) + ', ' + str(round(float(match.group(4)) / placeSize) * placeSize) + ')'
			subjects[match.group(1)] += 1
			places[place] += 1
			pairs[match.group(1) + ' at ' + place] += 1
			plain = re.sub(r'\d[\d.]*', '', positionPattern.sub('', leadingAsidePattern.sub('', after)))
			doings[' '.join(plain.split()[:3])] += 1
			job = jobPattern.search(after)
			if job is not None:
				jobs[job.group(1)] += 1
			if ' - ' in plain:
				reasons[' '.join(plain.split(' - ', 1)[1].split()[:5])] += 1
		print(kind + ' (' + level + ', ' + str(len(found)) + ')')
		print('    who: ' + counted(subjects, subjectsListed))
		print('    where: ' + counted(places, subjectsListed))
		print('    who where: ' + counted(pairs, subjectsListed))
		for label, counter in (('doing', doings), ('jobs', jobs), ('after the dash', reasons)):
			if len(counter) > 1:
				print('    ' + label + ': ' + counted(counter, subjectsListed))
	print()


def printVikings(entries):
	"""
	Prints each viking's lives through the run - when they came, when they died and of what, and how long they lived, in
	real minutes - so vikings dying soon after they come, or one after another, stand out

	@param entries The entries
	"""
	lives = collections.OrderedDict()
	for entry in entries:
		match = arrivalPattern.match(entry.text)
		if match is not None:
			lives.setdefault(match.group(1), []).append([entry, None, ''])
			continue
		match = deathPattern.match(entry.text)
		if match is not None:
			cause = ((match.group(2) + ' (' + match.group(3) + ')') if match.group(2) is not None else match.group(4))
			runs = lives.setdefault(match.group(1), [])
			if not runs or runs[-1][1] is not None:
				runs.append([None, entry, cause])
			else:
				runs[-1][1] = entry
				runs[-1][2] = cause
			continue
		if entry.kind == 'Snapshot of a viking':
			match = namePattern.match(entry.text)
			if match is not None and match.group(1) not in lives:
				lives[match.group(1)] = [[None, None, '']]
	deaths = sum(1 for runs in lives.values() for run in runs if run[1] is not None)
	print('# Vikings - ' + str(len(lives)) + ' seen, ' + str(deaths) + ' deaths')
	print('name | came | died | lived | last hit by')
	for name, runs in lives.items():
		for arrived, died, cause in runs:
			came = (arrived.when() if arrived is not None else 'there before the run, or not seen coming')
			lived = ''
			if arrived is not None and died is not None:
				lived = str(round((died.elapsed - arrived.elapsed) / 60, 1)) + ' min'
			print(name + ' | ' + came + ' | ' + (died.when() if died is not None else 'alive at the end') + ' | ' + lived + ' | ' + cause)
	print()


def printSnapshots(entries):
	"""
	Prints the numbers of each snapshot of how the game ran, one row each, so a slide or a climb over the run shows

	@param entries The entries
	"""
	print('# Snapshots')
	print('line | real time | game time | fps | slowest ms | hitches | ZDOs | objects loaded | items lying | vikings loaded')
	for entry in entries:
		match = snapshotPattern.match(entry.text)
		if match is not None:
			print(str(entry.lineNumber) + ' | ' + realClock(entry.elapsed) + ' | day ' + str(entry.day) + ' ' + entry.clock + ' | ' + ' | '.join(match.groups()))
	hitches = []
	for entry in entries:
		match = hitchPattern.match(entry.text)
		if match is not None:
			hitches.append('day ' + str(entry.day) + ' ' + entry.clock + ' ' + match.group(1) + 'ms')
	print('Hitches written, at game times: ' + (', '.join(hitches) if hitches else 'none'))
	print()


def printVillages(entries):
	"""
	Prints the snapshots of each village at points spread through the run, a field to a line, so how the village fared
	over it shows - its vikings, houses, respawns, missing pieces, enemies and items lying around

	@param entries The entries
	"""
	villages = collections.OrderedDict()
	for entry in entries:
		if entry.kind == 'Snapshot of a village':
			villages.setdefault(entry.text.split(':')[0], []).append(entry)
	print('# Villages, at ' + str(samplesShown) + ' points through the run')
	for name, snapshots in villages.items():
		for entry in spread(snapshots, samplesShown):
			print(name + ', ' + entry.when())
			for field in entry.text.split(': ', 1)[1].split(' | '):
				print('    ' + field)
	if not villages:
		print('None')
	print()


def printTotalsOverTime(entries):
	"""
	Prints each total's count at points spread through the run, so a count that stops growing, or grows faster than it
	should, shows - like meals eaten stalling while jobs to eat keep starting

	@param entries The entries
	"""
	blocks = []
	for entry in entries:
		if entry.kind == 'Totals' and not entry.text.startswith('Totals - '):
			blocks.append((entry, {}))
			continue
		match = totalPattern.match(entry.text)
		if match is not None and blocks:
			blocks[-1][1][match.group(1)] = match.group(2)
	picked = spread(blocks, samplesShown)
	categories = []
	for _, counts in blocks:
		for category in counts:
			if category not in categories:
				categories.append(category)
	print('# Totals, at ' + str(samplesShown) + ' points through the run')
	print('total | ' + ' | '.join('day ' + str(entry.day) + ' ' + entry.clock + ' (' + realClock(entry.elapsed) + ')' for entry, _ in picked))
	for category in categories:
		print(category + ' | ' + ' | '.join(counts.get(category, '-') for _, counts in picked))
	print()


def printGaps(entries):
	"""
	Prints the longest stretches the run wrote nothing in, which can be the game frozen or paused, or the village
	unloaded

	@param entries The entries
	"""
	gaps = []
	for before, after in zip(entries, entries[1:]):
		if after.elapsed - before.elapsed >= gapShortest:
			gaps.append((after.elapsed - before.elapsed, before, after))
	print('# Quiet stretches of ' + str(gapShortest) + 's or more')
	for gap, before, after in sorted(gaps, key=lambda item: -item[0])[:gapsListed]:
		print(str(round(gap)) + 's from ' + before.when() + ' to ' + after.when())
	if not gaps:
		print('None')
	print()


def printTotals(entries):
	"""
	Prints the last totals the run wrote, which count everything from its start

	@param entries The entries
	"""
	start = None
	for index, entry in enumerate(entries):
		if entry.kind == 'Totals' and not entry.text.startswith('Totals - '):
			start = index
	print('# Last totals')
	if start is None:
		print('None')
		return
	print(entries[start].when() + ': ' + entries[start].text)
	for entry in entries[start + 1:]:
		if not entry.text.startswith('Totals - '):
			break
		print(entry.text[len('Totals - '):])


def main():
	"""
	Reads the run asked for and prints its overview
	"""
	parser = argparse.ArgumentParser(description='Prints an overview of a Viking Villages test run')
	parser.add_argument('log', nargs='?', default='', help='The run\'s file, or its name in the test log folder - the newest there when left out')
	parser.add_argument('--valheim', default=os.environ.get('ValheimPath', defaultValheimPath), help='Valheim install folder')
	arguments = parser.parse_args()
	sys.stdout.reconfigure(encoding='utf-8')
	path = findLog(arguments.log, arguments.valheim)
	header, entries, ended = readRun(path)
	printRun(path, header, entries, ended)
	printBuild(arguments.valheim, header)
	printKinds(entries)
	printExceptions(entries)
	printSubjects(entries)
	printVikings(entries)
	printSnapshots(entries)
	printVillages(entries)
	printGaps(entries)
	printTotalsOverTime(entries)
	printTotals(entries)


if __name__ == '__main__':
	main()
