"""Cuts the UI fonts down to the characters the game shows, so the WebGL build doesn't ship full CJK fonts.

The full fonts live in Assets/UI/Fonts/Source~ (Unity ignores folders ending in ~). Each subset keeps ASCII,
Latin-1, common punctuation and arrows, all kana, and every character found in the localization tables, the UXML
and USS files and the C# scripts. Re-run after adding text with new kanji:

    python tools/subset_fonts.py

Then reimport the fonts in Unity; their dynamic FontAssets fill their atlases at runtime.
"""
import glob
import io
import os
import re

from fontTools import subset

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SOURCE_DIR = os.path.join(ROOT, 'Assets', 'UI', 'Fonts', 'Source~')
OUTPUT_DIR = os.path.join(ROOT, 'Assets', 'UI', 'Fonts')

RANGES = [
    (0x20, 0x7E),      # ASCII
    (0xA0, 0xFF),      # Latin-1
    (0x2010, 0x2044),  # General punctuation
    (0x2190, 0x21FF),  # Arrows
    (0x3000, 0x30FF),  # CJK punctuation, hiragana, katakana
    (0xFF01, 0xFF9F),  # Full-width ASCII, half-width katakana
]


def unescape(value):
    # Unity writes non-ASCII YAML strings with \uXXXX escapes.
    return re.sub(r'\\u([0-9A-Fa-f]{4})', lambda m: chr(int(m.group(1), 16)), value)


def used_characters():
    chars = set()
    for path in glob.glob(os.path.join(ROOT, 'Assets', 'Localization', '**', '*.asset'), recursive=True):
        text = io.open(path, encoding='utf-8', errors='ignore').read()
        for match in re.finditer(r'm_Localized: (.*)', text):
            chars |= set(unescape(match.group(1)))
    for pattern in ('UI/**/*.uxml', 'UI/**/*.uss', 'Scripts/**/*.cs'):
        for path in glob.glob(os.path.join(ROOT, 'Assets', pattern), recursive=True):
            chars |= set(io.open(path, encoding='utf-8', errors='ignore').read())
    for low, high in RANGES:
        chars |= {chr(c) for c in range(low, high + 1)}
    return {c for c in chars if c.isprintable() or c == ' '}


def main():
    chars = used_characters()
    options = subset.Options()
    options.layout_features = ['*']
    options.name_IDs = ['*']
    options.notdef_outline = True
    for source in sorted(glob.glob(os.path.join(SOURCE_DIR, '*.ttf'))):
        font = subset.load_font(source, options)
        subsetter = subset.Subsetter(options)
        subsetter.populate(text=''.join(chars))
        subsetter.subset(font)
        target = os.path.join(OUTPUT_DIR, os.path.basename(source))
        subset.save_font(font, target, options)
        print(f'{os.path.basename(source)}: {os.path.getsize(source) // 1024} KB -> {os.path.getsize(target) // 1024} KB')
    print(f'{len(chars)} characters kept')


if __name__ == '__main__':
    main()
