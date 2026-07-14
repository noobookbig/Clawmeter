#include "wifi_manager.h"

#ifdef WIFI_FALLBACK_ENABLED

#include <Arduino.h>
#include <Preferences.h>
#include <WiFi.h>

namespace {
constexpr uint32_t RECONNECT_BACKOFF_MS[] = {2000, 5000, 15000, 30000, 60000};
constexpr uint8_t RECONNECT_LEVELS = sizeof(RECONNECT_BACKOFF_MS) / sizeof(RECONNECT_BACKOFF_MS[0]);

enum class State : uint8_t { Idle, Connecting, Connected, Failed };
State state = State::Idle;
String current_ssid;
String current_pass;
String pending_ssid;
String pending_pass;
uint32_t connect_attempt_started_ms = 0;
uint8_t backoff_index = 0;
uint32_t next_retry_ms = 0;
}

void wifi_manager_init() {
    Preferences prefs;
    prefs.begin("clawdmeter_wifi", true);
    current_ssid = prefs.getString("ssid", "");
    current_pass = prefs.getString("pass", "");
    prefs.end();
    if (current_ssid.isEmpty()) {
        state = State::Idle;
    } else {
        state = State::Connecting;
        connect_attempt_started_ms = millis();
        backoff_index = 0;
        WiFi.mode(WIFI_STA);
        WiFi.begin(current_ssid.c_str(), current_pass.c_str());
        Serial.printf("[wifi] connecting to '%s'\n", current_ssid.c_str());
    }
}

bool wifi_manager_creds_present() { return !current_ssid.isEmpty(); }

bool wifi_manager_connected() {
    return state == State::Connected && WiFi.status() == WL_CONNECTED;
}

void wifi_manager_connect() {
    if (current_ssid.isEmpty()) return;
    if (state == State::Connecting || state == State::Connected) return;
    Serial.println("[wifi] connect() requested");
    WiFi.mode(WIFI_STA);
    WiFi.begin(current_ssid.c_str(), current_pass.c_str());
    state = State::Connecting;
    connect_attempt_started_ms = millis();
    backoff_index = 0;
    next_retry_ms = 0;
}

void wifi_manager_disconnect() {
    if (state == State::Idle) return;
    Serial.println("[wifi] disconnect (saving power, daemon preferred)");
    WiFi.disconnect();
    WiFi.mode(WIFI_OFF);
    state = State::Idle;
}

void wifi_manager_set_creds(const char* ssid, const char* pass) {
    Preferences prefs;
    prefs.begin("clawdmeter_wifi", false);
    prefs.putString("ssid", ssid);
    prefs.putString("pass", pass);
    prefs.end();
    current_ssid = ssid;
    current_pass = pass;
    if (!current_ssid.isEmpty()) {
        Serial.printf("[wifi] credentials saved for SSID '%s'\n", current_ssid.c_str());
        WiFi.mode(WIFI_STA);
        WiFi.begin(current_ssid.c_str(), current_pass.c_str());
        state = State::Connecting;
        connect_attempt_started_ms = millis();
        backoff_index = 0;
        next_retry_ms = 0;
    }
}

void wifi_manager_tick() {
    const uint32_t now = millis();
    switch (state) {
        case State::Connecting: {
            const wl_status_t s = WiFi.status();
            if (s == WL_CONNECTED) {
                state = State::Connected;
                Serial.printf("[wifi] connected, IP=%s\n", WiFi.localIP().toString().c_str());
                backoff_index = 0;
            } else if (now - connect_attempt_started_ms > 15000) {
                // 15s connect attempt timeout — schedule retry with backoff
                if (backoff_index < RECONNECT_LEVELS) backoff_index++;
                next_retry_ms = now + RECONNECT_BACKOFF_MS[backoff_index - 1];
                Serial.printf("[wifi] connect timeout, next retry in %ums\n",
                              RECONNECT_BACKOFF_MS[backoff_index - 1]);
                WiFi.disconnect();
                state = State::Failed;
            }
            break;
        }
        case State::Failed:
            if (now >= next_retry_ms) {
                Serial.println("[wifi] retrying connection");
                WiFi.mode(WIFI_STA);
                WiFi.begin(current_ssid.c_str(), current_pass.c_str());
                state = State::Connecting;
                connect_attempt_started_ms = now;
            }
            break;
        case State::Connected:
            // Stay alive — disconnects caught on next tick by Falling back to Failed.
            if (WiFi.status() != WL_CONNECTED) {
                Serial.println("[wifi] lost connection, marking Failed");
                state = State::Failed;
                connect_attempt_started_ms = now;
                next_retry_ms = now + RECONNECT_BACKOFF_MS[0];
            }
            break;
        case State::Idle:
        default:
            break;
    }
}

#endif  // WIFI_FALLBACK_ENABLED
