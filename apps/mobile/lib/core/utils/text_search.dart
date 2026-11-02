/// Accent- and case-insensitive matching, so "hoang hon" finds "Hoàng Hôn".
abstract final class TextSearch {
  static const _from = 'àáạảãâầấậẩẫăằắặẳẵèéẹẻẽêềếệểễìíịỉĩòóọỏõôồốộổỗơờớợởỡùúụủũưừứựửữỳýỵỷỹđ';
  static const _to = 'aaaaaaaaaaaaaaaaaeeeeeeeeeeeiiiiiooooooooooooooooouuuuuuuuuuuyyyyyd';

  static String fold(String input) {
    final lower = input.toLowerCase();
    final out = StringBuffer();
    for (final rune in lower.runes) {
      final ch = String.fromCharCode(rune);
      final i = _from.indexOf(ch);
      out.write(i < 0 ? ch : _to[i]);
    }
    return out.toString();
  }

  /// True when every word of [query] appears somewhere in [fields].
  static bool matches(String query, Iterable<String?> fields) {
    final words = fold(query).split(RegExp(r'\s+')).where((w) => w.isNotEmpty);
    if (words.isEmpty) return true;
    final haystack = fold(fields.whereType<String>().join(' '));
    return words.every(haystack.contains);
  }
}
