/*
 * File:    ApiClient.java
 * Author:  Imadh
 * Created: 2026-09-25
 * Purpose: The only place the app performs HTTP. Attaches the bearer token from
 *          the local session row, sets timeouts, works around Android's missing
 *          PATCH verb, and delivers results to the UI thread. Plumbing only —
 *          it never decides whether a rule passed and never composes a message
 *          for a business rule failure.
 *
 *          Parsing is deliberately not here: each vertical owns its parser in
 *          parsers/, so a success body reaches onSuccess() as raw JSON.
 */
package com.sliit.smartsolar.network;

import android.content.Context;
import android.os.Handler;
import android.os.Looper;

import com.sliit.smartsolar.BuildConfig;
import com.sliit.smartsolar.database.SessionDao;

import java.io.BufferedReader;
import java.io.BufferedWriter;
import java.io.IOException;
import java.io.InputStream;
import java.io.InputStreamReader;
import java.io.OutputStream;
import java.io.OutputStreamWriter;
import java.net.HttpURLConnection;
import java.net.URL;
import java.nio.charset.StandardCharsets;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

public final class ApiClient {

    // Without these a request hangs forever on a stale IP or a blocked port,
    // with no exception and no error on screen. Surfaced during the first IIS
    // publish: the call simply never returned.
    private static final int CONNECT_TIMEOUT_MS = 10_000;
    private static final int READ_TIMEOUT_MS = 15_000;

    /** The API's MethodOverrideMiddleware reads this and rewrites the verb. */
    private static final String HEADER_METHOD_OVERRIDE = "X-HTTP-Method-Override";
    private static final String HTTP_PATCH = "PATCH";

    // Four in flight at once; results are posted back to the main thread.
    private static final ExecutorService EXECUTOR = Executors.newFixedThreadPool(4);
    private static final Handler MAIN_HANDLER = new Handler(Looper.getMainLooper());

    private static volatile SessionDao sessionDao;

    private ApiClient() {
    }

    /**
     * Must be called once before any request, typically from
     * MainActivity.onCreate. Holds an application context, so it cannot leak.
     */
    public static void init(Context context) {
        sessionDao = new SessionDao(context.getApplicationContext());
    }

    /** GET with no body. */
    public static void get(String path, ApiCallback<String> callback) {
        enqueue("GET", null, path, null, callback);
    }

    /** POST with a JSON body. */
    public static void post(String path, String jsonBody, ApiCallback<String> callback) {
        enqueue("POST", null, path, jsonBody, callback);
    }

    /** PUT with a JSON body, for full resource updates. */
    public static void put(String path, String jsonBody, ApiCallback<String> callback) {
        enqueue("PUT", null, path, jsonBody, callback);
    }

    /** DELETE with no body. */
    public static void delete(String path, ApiCallback<String> callback) {
        enqueue("DELETE", null, path, null, callback);
    }

    /**
     * PATCH, sent as POST plus the override header.
     * <p>
     * {@link HttpURLConnection} rejects the PATCH verb outright, and the API has
     * nine PATCH routes, so the server translates an authenticated POST carrying
     * {@code X-HTTP-Method-Override: PATCH} back into a PATCH before routing.
     */
    public static void patch(String path, String jsonBody, ApiCallback<String> callback) {
        enqueue("POST", HTTP_PATCH, path, jsonBody, callback);
    }

    // Runs the request off the main thread, then posts the outcome back to it.
    private static void enqueue(String method, String overrideTo, String path,
                                String jsonBody, ApiCallback<String> callback) {

        if (sessionDao == null) {
            // A programming error rather than a failed call: report it instead of
            // letting it crash the executor thread silently.
            MAIN_HANDLER.post(() -> callback.onError(
                    ErrorParser.CODE_NO_RESPONSE,
                    "ApiClient.init(context) must be called before any request."));
            return;
        }

        EXECUTOR.execute(() -> {
            ApiResult result = execute(method, overrideTo, path, jsonBody);
            MAIN_HANDLER.post(() -> deliver(result, callback));
        });
    }

    // Hands the outcome to the UI thread. On success the raw body is passed
    // through for the caller's parser; on failure the API's own code and detail
    // are passed, so the screen shows the server's message.
    private static void deliver(ApiResult result, ApiCallback<String> callback) {
        if (result.isSuccess) {
            callback.onSuccess(result.body);
            return;
        }

        ErrorParser.ErrorInfo error = ErrorParser.parse(result.body, result.statusCode);
        callback.onError(error.code, error.detail);
    }

    // Performs one request and maps it to an ApiResult. A status of 0 means no
    // HTTP response was received at all.
    private static ApiResult execute(String method, String overrideTo, String path, String jsonBody) {
        HttpURLConnection connection = null;

        try {
            connection = (HttpURLConnection) new URL(BuildConfig.API_BASE_URL + path).openConnection();
            connection.setRequestMethod(method);
            connection.setConnectTimeout(CONNECT_TIMEOUT_MS);
            connection.setReadTimeout(READ_TIMEOUT_MS);
            connection.setRequestProperty("Accept", "application/json");

            if (overrideTo != null) {
                connection.setRequestProperty(HEADER_METHOD_OVERRIDE, overrideTo);
            }

            // Absent before login and for the anonymous routes, which is legal.
            String token = sessionDao.getToken();
            if (token != null && !token.isEmpty()) {
                connection.setRequestProperty("Authorization", "Bearer " + token);
            }

            if (jsonBody != null) {
                connection.setDoOutput(true);
                connection.setRequestProperty("Content-Type", "application/json");
                writeBody(connection, jsonBody);
            }

            int status = connection.getResponseCode();

            // The trap that costs the most marks: getInputStream() throws on 4xx,
            // so catching it and giving up loses the API's error message and every
            // rule rejection looks like a generic failure.
            String body = readBody(status >= 200 && status < 300
                    ? connection.getInputStream()
                    : connection.getErrorStream());

            return new ApiResult(status, body);
        } catch (IOException e) {
            // Timeout, unknown host, cleartext blocked, or a stale LAN IP.
            return new ApiResult(0, null);
        } finally {
            if (connection != null) {
                connection.disconnect();
            }
        }
    }

    // Writes the JSON request body.
    private static void writeBody(HttpURLConnection connection, String jsonBody) throws IOException {
        try (OutputStream output = connection.getOutputStream();
             BufferedWriter writer = new BufferedWriter(
                     new OutputStreamWriter(output, StandardCharsets.UTF_8))) {
            writer.write(jsonBody);
        }
    }

    // Reads a response stream fully, or returns null when there is no stream to
    // read (an error response without a body, for instance).
    private static String readBody(InputStream stream) throws IOException {
        if (stream == null) {
            return null;
        }

        try (BufferedReader reader = new BufferedReader(
                new InputStreamReader(stream, StandardCharsets.UTF_8))) {

            StringBuilder builder = new StringBuilder();
            String line;
            while ((line = reader.readLine()) != null) {
                builder.append(line);
            }
            return builder.toString();
        }
    }
}
