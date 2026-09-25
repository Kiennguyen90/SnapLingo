import 'package:flutter/gestures.dart';
import 'package:flutter/material.dart';

/// Splits [text] into spans, styling every whole-word, case-insensitive occurrence of
/// any of [words] with [highlight]. [onTapWord] (if given) makes highlighted words
/// tappable and receives the matched vocabulary entry. [activeRange] additionally
/// marks a character range (the word currently being spoken).
List<InlineSpan> highlightWords(
  String text,
  Iterable<String> words, {
  required TextStyle highlight,
  void Function(String word)? onTapWord,
  TextRange? activeRange,
  TextStyle? activeStyle,
}) {
  final sorted = words.where((w) => w.trim().isNotEmpty).toList()..sort((a, b) => b.length.compareTo(a.length));
  if (sorted.isEmpty && activeRange == null) return [TextSpan(text: text)];

  final pattern = sorted.isEmpty
      ? null
      : RegExp(r'\b(' + sorted.map(RegExp.escape).join('|') + r')\b', caseSensitive: false);

  // Mark each character with its style source, then merge runs — keeps overlapping
  // vocabulary and "currently spoken" highlights simple and correct.
  final vocabAt = List<String?>.filled(text.length, null);
  for (final m in pattern?.allMatches(text) ?? const <RegExpMatch>[]) {
    final entry = sorted.firstWhere((w) => w.toLowerCase() == m.group(0)!.toLowerCase());
    for (var i = m.start; i < m.end; i++) {
      vocabAt[i] = entry;
    }
  }

  bool isActive(int i) => activeRange != null && i >= activeRange.start && i < activeRange.end;

  final spans = <InlineSpan>[];
  var start = 0;
  for (var i = 1; i <= text.length; i++) {
    final boundary = i == text.length || vocabAt[i] != vocabAt[start] || isActive(i) != isActive(start);
    if (!boundary) continue;

    final vocab = vocabAt[start];
    var style = vocab != null ? highlight : null;
    if (isActive(start) && activeStyle != null) style = (style ?? const TextStyle()).merge(activeStyle);

    spans.add(TextSpan(
      text: text.substring(start, i),
      style: style,
      recognizer: (vocab != null && onTapWord != null) ? (TapGestureRecognizer()..onTap = () => onTapWord(vocab)) : null,
    ));
    start = i;
  }
  return spans;
}
