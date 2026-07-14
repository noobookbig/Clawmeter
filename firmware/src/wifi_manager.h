#pragma once
#include <stdbool.h>
#include <stdint.h>

#ifdef WIFI_FALLBACK_ENABLED
#ifdef __cplusplus
extern "C" {
#endif

// Load SSID/pass from NVS namespace "clawdmeter_wifi" and start STA connect.
// Idempotent — safe to call from setup().
void    wifi_manager_init(void);

// True iff at least an SSID is stored (pass may still be empty).
bool    wifi_manager_creds_present(void);

// Connected to AP and got an IP.
bool    wifi_manager_connected(void);

// Initiate a STA connect (non-blocking). Idempotent — safe to call when already
// connecting or connected.
void    wifi_manager_connect(void);

// Power-saver: disable WiFi radio. Re-enable via next connect().
void    wifi_manager_disconnect(void);

// Persist new credentials to NVS and (re)connect. Used by daemon BLE push.
void    wifi_manager_set_creds(const char* ssid, const char* pass);

// Drive the connect/retry state machine. Call from loop().
void    wifi_manager_tick(void);

#ifdef __cplusplus
}
#endif
#endif  // WIFI_FALLBACK_ENABLED
