/// Short activity tags the learner can attach to a photo. Stored as the key; the story
/// generator treats the tag as the learner's own statement about the moment.
const activityTags = <(String key, String labelVi, String emoji)>[
  ('work', 'Đi làm', '💼'),
  ('study', 'Học tập', '📚'),
  ('coffee', 'Cà phê', '☕'),
  ('meal', 'Ăn uống', '🍜'),
  ('family_meal', 'Bữa cơm gia đình', '👨‍👩‍👧'),
  ('relax', 'Thư giãn', '🌿'),
  ('exercise', 'Thể thao', '🏃'),
  ('shopping', 'Mua sắm', '🛍️'),
  ('travel', 'Đi chơi', '🧳'),
  ('commute', 'Di chuyển', '🛵'),
];

String? activityLabelVi(String? key) {
  for (final t in activityTags) {
    if (t.$1 == key) return '${t.$3} ${t.$2}';
  }
  return key;
}
