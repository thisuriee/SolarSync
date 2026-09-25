/*
 * File:    ApiCallback.java
 * Author:  Imadh
 * Created: 2026-09-25
 * Purpose: Delivers a network outcome back to the UI thread, so screens never
 *          touch status codes and never block. Plumbing only.
 */
package com.sliit.smartsolar.network;

public interface ApiCallback<T> {

    // Called on the main thread with the parsed success body. Screens parse the
    // raw JSON with their own parser before this is invoked.
    void onSuccess(T data);

    // Called on the main thread for any non-2xx outcome. The code and detail are
    // the API's own values, so the screen displays the server's message and
    // never composes one of its own.
    void onError(String code, String detail);
}
