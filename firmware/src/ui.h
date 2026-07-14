#pragma once
#include "data.h"
#include "ble.h"

enum screen_t {
    SCREEN_SPLASH,
    SCREEN_USAGE,
    SCREEN_COUNT,
};

void ui_init(void);
void ui_update(const UsageData* data);
void ui_tick_anim(void);
void ui_show_screen(screen_t screen);
void ui_toggle_splash(void);
screen_t ui_get_current_screen(void);
uint32_t ui_get_last_screen_change_time(void);
void ui_force_view(int view);
bool ui_is_ble_connected(void);
int ui_get_view_state(void);
void ui_update_ble_status(ble_state_t state, const char* name, const char* mac);
void ui_update_battery(int percent, bool charging);

#ifdef WIFI_FALLBACK_ENABLED
// Last provider from the most recent successfully parsed payload. Used by
// the WiFi fallback (main.cpp) to gate MiniMax polling only.
UsageProvider ui_get_current_provider(void);
#endif

// Called from BLE callback when new pet animation arrives
void ui_notify_pet_changed(void);
// Called from main loop to apply the deferred pet change (LVGL work)
void ui_pet_tick(void);