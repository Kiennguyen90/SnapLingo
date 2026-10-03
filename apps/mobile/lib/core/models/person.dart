import 'package:equatable/equatable.dart';

/// A relation the learner can give a labeled person (served by `GET /people/relations`).
final class Relation extends Equatable {
  const Relation({required this.key, required this.english, required this.vietnamese});

  final String key;
  final String english;
  final String vietnamese;

  factory Relation.fromJson(Map<String, dynamic> json) => Relation(
        key: json['key']?.toString() ?? 'other',
        english: json['english']?.toString() ?? '',
        vietnamese: json['vietnamese']?.toString() ?? '',
      );

  @override
  List<Object?> get props => [key];
}

/// Someone the learner chose to label (opt-in). Stories may only name labeled people.
final class Person extends Equatable {
  const Person({
    required this.personId,
    required this.relation,
    required this.relationVi,
    this.displayName,
    this.photoCount = 0,
    this.gender,
  });

  final String personId;
  final String? displayName;
  final String relation;
  final String relationVi;
  final int photoCount;

  /// 'female', 'male' or null (not specified): decides he/she vs they in the story.
  final String? gender;

  /// "Tuấn (Em trai)" or just "Mẹ" when there is no name.
  String get label =>
      (displayName == null || displayName!.isEmpty) ? relationVi : '$displayName ($relationVi)';

  factory Person.fromJson(Map<String, dynamic> json) => Person(
        personId: json['personId']?.toString() ?? '',
        displayName: json['displayName']?.toString(),
        relation: json['relation']?.toString() ?? 'other',
        relationVi: json['relationVi']?.toString() ?? '',
        photoCount: (json['photoCount'] as num?)?.toInt() ?? 0,
        gender: json['gender']?.toString(),
      );

  @override
  List<Object?> get props => [personId, displayName, relation, gender];
}

/// Who is labeled in one photo, and how many visible people are still unlabeled.
final class PhotoPeople extends Equatable {
  const PhotoPeople({required this.people, this.peopleCount, this.unlabeledCount = 0});

  final List<Person> people;
  final int? peopleCount;
  final int unlabeledCount;

  factory PhotoPeople.fromJson(Map<String, dynamic> json) => PhotoPeople(
        people: (json['people'] as List? ?? const [])
            .whereType<Map>()
            .map((m) => Person.fromJson(m.cast<String, dynamic>()))
            .toList(),
        peopleCount: (json['peopleCount'] as num?)?.toInt(),
        unlabeledCount: (json['unlabeledCount'] as num?)?.toInt() ?? 0,
      );

  @override
  List<Object?> get props => [people, peopleCount, unlabeledCount];
}
