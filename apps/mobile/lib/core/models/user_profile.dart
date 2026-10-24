import 'package:equatable/equatable.dart';

const cefrLevels = ['A1', 'A2', 'B1', 'B2', 'C1', 'C2'];

/// Friendly difficulty label for a CEFR level (the design's "Dễ" badge).
String difficultyLabel(String cefr) => switch (cefr.toUpperCase()) {
      'A1' || 'A2' => 'Dễ',
      'B1' || 'B2' => 'Trung bình',
      _ => 'Nâng cao',
    };

final class UserProfile extends Equatable {
  const UserProfile({
    required this.userId,
    required this.email,
    required this.cefrLevel,
    this.displayName,
    this.nativeLanguage,
    this.personLabelingEnabled = false,
    this.researchConsent = false,
    this.isPro = false,
  });

  final String userId;
  final String email;
  final String? displayName;
  final String cefrLevel;
  final String? nativeLanguage;

  /// Opt-in: the learner may label people in photos (off by default).
  final bool personLabelingEnabled;

  /// The learner agreed that usage data may be used for the study.
  final bool researchConsent;

  /// Pro learners get a daily lesson allowance instead of the free tier's fixed lesson cap.
  /// No payment provider is wired up yet — Pro status is granted directly in the database, not
  /// from the app, so this is read-only here.
  final bool isPro;

  /// Name used in greetings — falls back to the part of the email before `@`.
  String get greetingName {
    final name = displayName?.trim();
    if (name != null && name.isNotEmpty) return name.split(' ').last;
    return email.split('@').first;
  }

  String get fullName {
    final name = displayName?.trim();
    return (name != null && name.isNotEmpty) ? name : email.split('@').first;
  }

  factory UserProfile.fromJson(Map<String, dynamic> json) => UserProfile(
        userId: json['userId']?.toString() ?? '',
        email: json['email']?.toString() ?? '',
        displayName: json['displayName']?.toString(),
        cefrLevel: json['cefrLevel']?.toString() ?? 'A2',
        nativeLanguage: json['nativeLanguage']?.toString(),
        personLabelingEnabled: json['personLabelingEnabled'] == true,
        researchConsent: json['researchConsentAt'] != null,
        isPro: json['isPro'] == true,
      );

  @override
  List<Object?> get props =>
      [userId, email, displayName, cefrLevel, nativeLanguage, personLabelingEnabled, researchConsent, isPro];
}
