import 'package:equatable/equatable.dart';

/// Mirrors the API's `ReviewScore`. The buttons use Anki-style English labels (as in
/// the design) with a Vietnamese hint underneath.
enum ReviewScore {
  forgot('Again', 'Quên'),
  hard('Hard', 'Khó nhớ'),
  good('Good', 'Nhớ'),
  easy('Easy', 'Rất dễ');

  const ReviewScore(this.labelEn, this.labelVi);

  final String labelEn;
  final String labelVi;

  String get apiValue => name;
}

/// One due flashcard from `GET /reviews/today`.
final class ReviewItem extends Equatable {
  const ReviewItem({
    required this.vocabularyId,
    required this.word,
    required this.lessonId,
    required this.photoId,
    required this.photoUrl,
    this.ipa,
    this.partOfSpeech,
    this.meaning,
    this.example,
  });

  final String vocabularyId;
  final String word;
  final String? ipa;
  final String? partOfSpeech;
  final String? meaning;
  final String? example;
  final String lessonId;
  final String photoId;
  final String photoUrl;

  factory ReviewItem.fromJson(Map<String, dynamic> json, String Function(String) resolveUrl) {
    String? opt(String key) {
      final v = json[key]?.toString();
      return (v == null || v.isEmpty) ? null : v;
    }

    return ReviewItem(
      vocabularyId: json['vocabularyId'].toString(),
      word: json['word']?.toString() ?? '',
      ipa: opt('ipa'),
      partOfSpeech: opt('partOfSpeech'),
      meaning: opt('meaning'),
      example: opt('example'),
      lessonId: json['lessonId']?.toString() ?? '',
      photoId: json['photoId']?.toString() ?? '',
      photoUrl: resolveUrl(json['photoSignedUrl']?.toString() ?? ''),
    );
  }

  @override
  List<Object?> get props => [vocabularyId];
}
