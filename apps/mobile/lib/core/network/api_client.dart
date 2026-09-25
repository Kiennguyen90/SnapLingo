import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:http/http.dart' as http;
import 'package:http_parser/http_parser.dart';

import '../config/app_config.dart';
import 'api_exception.dart';

/// The single seam between the app and the SnapLingo API. Services never touch tokens,
/// headers or status codes — they call [get]/[post]/... and receive decoded JSON or an
/// [ApiException].
class ApiClient {
  ApiClient({http.Client? httpClient, String baseUrl = AppConfig.apiBaseUrl})
      : _http = httpClient ?? http.Client(),
        _baseUri = Uri.parse(baseUrl);

  final http.Client _http;
  final Uri _baseUri;

  static const _timeout = Duration(seconds: 30);

  /// Lesson generation calls a vision LLM and can legitimately take this long.
  static const aiTimeout = Duration(seconds: 120);

  String? _accessToken;

  /// Fired when an authenticated request comes back 401 (token expired/revoked), so the
  /// auth layer can drop the session instead of every screen handling it separately.
  void Function()? onUnauthorized;

  set accessToken(String? token) => _accessToken = token;

  Future<dynamic> get(String path, {Map<String, String>? query}) =>
      _send('GET', path, query: query);

  Future<dynamic> post(String path, {Object? body, Duration? timeout}) =>
      _send('POST', path, body: body, timeout: timeout);

  Future<dynamic> patch(String path, {Object? body}) => _send('PATCH', path, body: body);

  Future<dynamic> put(String path, {Object? body}) => _send('PUT', path, body: body);

  Future<dynamic> delete(String path) => _send('DELETE', path);

  Future<dynamic> uploadFile(
    String path, {
    required File file,
    String field = 'file',
    Map<String, String> fields = const {},
  }) async {
    final request = http.MultipartRequest('POST', _uri(path))
      ..headers.addAll(_headers(json: false))
      ..fields.addAll(fields)
      ..files.add(await http.MultipartFile.fromPath(field, file.path, contentType: _mediaTypeOf(file.path)));

    return _handle(() async => http.Response.fromStream(await _http.send(request).timeout(_timeout * 2)));
  }

  /// The API signs media URLs with its own configured `PublicBaseUrl` (e.g.
  /// `https://localhost:7182`), which is unreachable from a phone or emulator. The
  /// signature only covers the path + `exp`, so re-pointing the origin at the host this
  /// client already talks to keeps the URL valid.
  String resolveMediaUrl(String url) {
    final parsed = Uri.tryParse(url);
    if (parsed == null || !parsed.hasScheme) return _baseUri.resolve(url).toString();
    return parsed
        .replace(scheme: _baseUri.scheme, host: _baseUri.host, port: _baseUri.port)
        .toString();
  }

  Uri _uri(String path, [Map<String, String>? query]) {
    final uri = _baseUri.resolve(path.startsWith('/') ? path.substring(1) : path);
    return query == null ? uri : uri.replace(queryParameters: query);
  }

  Map<String, String> _headers({bool json = true}) => {
        HttpHeaders.acceptHeader: 'application/json',
        if (json) HttpHeaders.contentTypeHeader: 'application/json',
        if (_accessToken != null) HttpHeaders.authorizationHeader: 'Bearer $_accessToken',
      };

  Future<dynamic> _send(
    String method,
    String path, {
    Map<String, String>? query,
    Object? body,
    Duration? timeout,
  }) {
    final request = http.Request(method, _uri(path, query))..headers.addAll(_headers());
    if (body != null) request.body = jsonEncode(body);

    return _handle(
      () async => http.Response.fromStream(await _http.send(request).timeout(timeout ?? _timeout)),
    );
  }

  Future<dynamic> _handle(Future<http.Response> Function() send) async {
    final http.Response response;
    try {
      response = await send();
    } on SocketException {
      throw ApiException.network;
    } on TimeoutException {
      throw const ApiException('Máy chủ phản hồi quá lâu. Vui lòng thử lại.');
    } on http.ClientException {
      throw ApiException.network;
    }

    final status = response.statusCode;
    final decoded = _tryDecode(response);

    if (status >= 200 && status < 300) return decoded;

    if (status == 401 && _accessToken != null) onUnauthorized?.call();
    throw ApiException(_messageFor(status, decoded), statusCode: status);
  }

  static dynamic _tryDecode(http.Response response) {
    if (response.bodyBytes.isEmpty) return null;
    try {
      return jsonDecode(utf8.decode(response.bodyBytes));
    } on FormatException {
      return null;
    }
  }

  static String _messageFor(int status, dynamic body) {
    if (body is Map<String, dynamic>) {
      final title = body['title'];
      // The API returns a generic English title for 500s; don't surface that verbatim.
      if (title is String && title.isNotEmpty && status < 500) return title;
    }
    return switch (status) {
      401 => 'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.',
      404 => 'Không tìm thấy dữ liệu.',
      413 => 'Ảnh quá lớn. Vui lòng chọn ảnh dưới 10 MB.',
      >= 500 => 'Máy chủ đang gặp sự cố. Vui lòng thử lại sau.',
      _ => 'Đã có lỗi xảy ra ($status).',
    };
  }

  static MediaType _mediaTypeOf(String path) {
    final lower = path.toLowerCase();
    if (lower.endsWith('.png')) return MediaType('image', 'png');
    if (lower.endsWith('.webp')) return MediaType('image', 'webp');
    return MediaType('image', 'jpeg');
  }
}
