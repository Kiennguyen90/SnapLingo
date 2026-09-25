/// Any non-2xx response or transport failure, carrying a message that is safe to show
/// the learner. The API answers errors with RFC 7807 ProblemDetails (`{status, title}`),
/// and `title` is already written for humans.
final class ApiException implements Exception {
  const ApiException(this.message, {this.statusCode});

  final String message;
  final int? statusCode;

  bool get isUnauthorized => statusCode == 401;
  bool get isNetwork => statusCode == null;

  static const ApiException network = ApiException(
    'Không kết nối được máy chủ. Kiểm tra mạng và thử lại.',
  );

  @override
  String toString() => 'ApiException($statusCode): $message';
}
