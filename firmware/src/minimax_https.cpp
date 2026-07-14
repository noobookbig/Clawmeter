#include "minimax_https.h"

#ifdef WIFI_FALLBACK_ENABLED

#include <Arduino.h>
#include <ArduinoJson.h>
#include <HTTPClient.h>
#include <Preferences.h>
#include <WiFiClientSecure.h>

#include "data.h"
#include "usage_payload.h"
#include "ui.h"
#include "wifi_manager.h"

namespace {

const char* FALLBACK_URLS[] = {
    "https://api.minimax.io/v1/token_plan/remains",
    "https://www.minimax.io/v1/token_plan/remains",
    "https://api.minimaxi.com/v1/token_plan/remains",
    "https://www.minimaxi.com/v1/token_plan/remains",
};
constexpr uint8_t URL_COUNT = sizeof(FALLBACK_URLS) / sizeof(FALLBACK_URLS[0]);

constexpr uint32_t POLL_PERIOD_MS = 60000;  // 60s
constexpr uint32_t POLL_TIMEOUT_MS = 15000;
constexpr size_t   RESPONSE_BUFSIZE = 4096;

String api_key;
uint32_t last_poll_ms = 0;

int as_pct(JsonVariant v, const char* k1, const char* k2) {
    if (v[k1].is<float>() || v[k1].is<int>()) return v[k1].as<int>();
    if (v[k2].is<float>() || v[k2].is<int>()) return v[k2].as<int>();
    return -1;
}

uint32_t as_u32(JsonVariant v, const char* k1, const char* k2) {
    if (v[k1].is<uint32_t>() || v[k1].is<int>()) return v[k1].as<uint32_t>();
    if (v[k2].is<uint32_t>() || v[k2].is<int>()) return v[k2].as<uint32_t>();
    return 0;
}

// Build a complete usage JSON exactly as build_minimax_usage_payload()
// in daemon/payloads.py does — then feed it to existing usage_parse_json so
// UI rendering stays identical to daemon-fed payload.
bool build_and_apply(JsonVariant model_item, time_t now, UsageData* out) {
    if (!out) return false;
    JsonDocument doc;
    JsonObject obj = doc.to<JsonObject>();
    obj["provider"] = "minimax";
    obj["mode"] = "window";
    obj["plan_type"] = "subscription";
    obj["ok"] = true;

    JsonObject top = obj["top"].to<JsonObject>();
    top["label"] = "Current";
    top["kind"] = "window_short";
    JsonObject bottom = obj["bottom"].to<JsonObject>();
    bottom["label"] = "Weekly";
    bottom["kind"] = "window_long";

    auto remaining_pct = [&](JsonVariant src, const char* window, JsonObject dst) {
        int total = as_pct(src, (String(window) + "_total_count").c_str(),
                              (String(window) + "TotalCount").c_str());
        if (total <= 0) total = -1;
        int usage = as_pct(src, (String(window) + "_usage_count").c_str(),
                               (String(window) + "UsageCount").c_str());
        if (total > 0 && usage >= 0) {
            dst["pct"] = (int)((double)usage / total * 100.0);
        } else {
            int explicit_pct = as_pct(src, (String(window) + "_remaining_percent").c_str(),
                                         (String(window) + "RemainingPercent").c_str());
            if (explicit_pct < 0) {
                explicit_pct = as_pct(src, "usage_percent", "usagePercent");
            }
            dst["pct"] = explicit_pct >= 0 ? explicit_pct : 0;
        }
        dst["has_reset"] = true;
        dst["kind_window"] = String(window);
    };

    JsonObject model_obj = model_item.as<JsonObject>();
    remaining_pct(model_obj, "current_interval", top);
    remaining_pct(model_obj, "current_weekly", bottom);

    auto reset_mins = [&](JsonVariant src, const char* window) -> int {
        uint32_t end = as_u32(src, (String(window) + "_end_time").c_str(),
                                 (String(window) + "EndTime").c_str());
        if (end > 10000000000ULL) end /= 1000;
        if (end > (uint32_t)now) return (int)((end - (uint32_t)now) / 60);
        uint32_t secs = as_u32(src, (String(window) + "_remains_time").c_str(),
                                   (String(window) + "RemainsTime").c_str());
        if (secs > 864000) secs /= 1000;
        return secs > 0 ? (int)(secs / 60) : 0;
    };

    top["reset_mins"] = reset_mins(model_obj, "current_interval");
    bottom["reset_mins"] = reset_mins(model_obj, "current_weekly");

    int rolling_pct = top["pct"].as<int>();
    int weekly_pct = bottom["pct"].as<int>();
    const char* status = "allowed";
    if (rolling_pct <= 10 || weekly_pct <= 10) status = "limited";
    else if (rolling_pct <= 25 || weekly_pct <= 25) status = "warning";
    obj["status"] = status;

    String payload;
    serializeJson(doc, payload);
    if (!usage_parse_json(payload.c_str(), out)) {
        Serial.println("[minimax] usage_parse_json failed");
        return false;
    }
    return true;
}

bool poll_once() {
    if (!wifi_manager_connected()) {
        Serial.println("[minimax] skipping poll, WiFi not connected");
        return false;
    }
    WiFiClientSecure client;
    client.setInsecure();  // TODO: embed MiniMax CA certificate
    HTTPClient http;
    bool last_attempt_failed = true;
    uint16_t last_status = 0;
    bool saw_auth_fail = false;
    String last_body;

    for (uint8_t i = 0; i < URL_COUNT; ++i) {
        const char* url = FALLBACK_URLS[i];
        Serial.printf("[minimax] GET %s\n", url);
        http.begin(client, url);
        http.setTimeout(POLL_TIMEOUT_MS);
        http.addHeader("Authorization", "Bearer " + api_key);
        http.addHeader("Content-Type", "application/json");
        http.addHeader("MM-API-Source", "Clawdmeter");
        http.addHeader("User-Agent", "clawdmeter/1.0");
        const int code = http.GET();
        last_status = code;
        if (code == 200) {
            last_body = http.getString();
            http.end();
            last_attempt_failed = false;

            DynamicJsonDocument doc(RESPONSE_BUFSIZE);
            DeserializationError err = deserializeJson(doc, last_body);
            if (err) { Serial.printf("[minimax] JSON parse err: %s\n", err.c_str()); return false; }

            JsonVariant model_item;
            JsonVariant root;
            if (doc["data"].is<JsonObject>()) root = doc["data"].as<JsonObject>();
            else root = doc.as<JsonObject>();

            JsonArray models;
            if (root["model_remains"].is<JsonArray>()) models = root["model_remains"].as<JsonArray>();
            else { Serial.println("[minimax] no model_remains[]"); return false; }

            int best_score = -1;
            JsonVariant best_item;
            for (JsonVariant item : models) {
                if (!item.is<JsonObject>()) continue;
                const char* name = item["model_name"] | item["modelName"] | item["service_type"] | item["serviceType"] | "";
                int score = 0;
                String lname = String(name);
                lname.toLowerCase();
                if (lname.startsWith("minimax-m")) score += 1000;
                else if (lname == "general" || lname == "text") score += 500;
                else if (lname.indexOf("minimax") >= 0) score += 10;
                if (lname.indexOf("m3") >= 0 || lname.indexOf("m2") >= 0) score += 100;
                if (item["current_interval_total_count"].is<int>()) {
                    int t = item["current_interval_total_count"].as<int>();
                    if (t > 0) score += 10000;
                }
                if (item["current_weekly_total_count"].is<int>()) {
                    int t = item["current_weekly_total_count"].as<int>();
                    if (t > 0) score += 1000;
                }
                if (score > best_score) { best_score = score; best_item = item; }
            }

            if (!best_item.is<JsonObject>()) {
                Serial.println("[minimax] no suitable model item");
                return false;
            }

            UsageData out;
            if (!build_and_apply(best_item, time(nullptr), &out)) {
                Serial.println("[minimax] build_and_apply failed");
                return false;
            }
            ui_update(&out);
            Serial.println("[minimax] poll ok");
            return true;
        }
        if (code == 401 || code == 403) {
            saw_auth_fail = true;
        }
        http.end();
    }

    if (saw_auth_fail) {
        Serial.println("[minimax] auth failed (HTTP 401/403) — check API key");
    } else {
        Serial.printf("[minimax] poll failed (last HTTP %u)\n", last_status);
    }
    (void)last_attempt_failed;
    return false;
}

}  // namespace

void minimax_https_init() {
    Preferences prefs;
    prefs.begin("clawdmeter_minimax", true);
    api_key = prefs.getString("key", "");
    prefs.end();
    if (!api_key.isEmpty()) Serial.println("[minimax] key loaded");
}

bool minimax_https_creds_present() { return !api_key.isEmpty(); }

void minimax_https_set_key(const char* key) {
    Preferences prefs;
    prefs.begin("clawdmeter_minimax", false);
    prefs.putString("key", key);
    prefs.end();
    api_key = key;
    Serial.printf("[minimax] key saved (%u chars)\n", (unsigned)api_key.length());
    last_poll_ms = 0;  // force an immediate poll on next tick
}

bool minimax_https_poll_now() {
    bool ok = poll_once();
    if (ok) last_poll_ms = millis();
    return ok;
}

void minimax_https_tick() {
    const uint32_t now = millis();
    if (api_key.isEmpty()) return;
    if (!wifi_manager_connected()) return;
    if (last_poll_ms != 0 && (now - last_poll_ms) < POLL_PERIOD_MS) return;
    if (minimax_https_poll_now()) {
        last_poll_ms = now;
    } else {
        last_poll_ms = now;  // rate-limit retries via period too
    }
}

#endif  // WIFI_FALLBACK_ENABLED
