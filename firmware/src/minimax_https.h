#pragma once
#include <stdbool.h>
#include <stdint.h>

#ifdef WIFI_FALLBACK_ENABLED
#ifdef __cplusplus
extern "C" {
#endif

// Load API key from NVS namespace "clawdmeter_minimax".
void    minimax_https_init(void);

// True if at least the api_key string is present and non-empty.
bool    minimax_https_creds_present(void);

// Persist a new MiniMax API key (received via daemon BLE push).
void    minimax_https_set_key(const char* api_key);

// Run the 60s polling tick: trigger a poll when due, parse the response,
// and apply via ui_update(). Cheap to call every loop iteration.
void    minimax_https_tick(void);

// Manual one-shot poll — useful from daemon BLE control path.
bool    minimax_https_poll_now(void);

#ifdef __cplusplus
}
#endif
#endif  // WIFI_FALLBACK_ENABLED
