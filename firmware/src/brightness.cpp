#include "brightness.h"
#include "idle.h"
#include "hal/display_hal.h"
#include <Preferences.h>
#include <Arduino.h>

// (display_hal_force_set_brightness must be implemented by each board's
//  display.cpp — see boards/<name>/display.cpp.)

// Four-step ramp. The default (index 2) is 200 — identical to the prior
// hard-coded DISPLAY_DEFAULT_BRIGHTNESS, so cycling is purely additive.
static const uint8_t LEVELS[] = {64, 128, 200, 255};
#define LEVELS_COUNT (sizeof(LEVELS) / sizeof(LEVELS[0]))
#define DEFAULT_IDX  2

static uint8_t cur_idx = DEFAULT_IDX;
static uint8_t cur_pwm = 0;   // mirrors the currently-applied PWM value (0..255)

void brightness_init(void) {
    Preferences prefs;
    prefs.begin("clawdmeter", true);
    // New key (daemon path): free-form PWM 0..255.
    uint8_t saved_pwm = prefs.getUChar("brt_pwm", 0xFF);
    if (saved_pwm != 0xFF) {
        cur_pwm = saved_pwm;
        idle_set_awake_brightness(cur_pwm);
        Serial.printf("Brightness init: pwm=%u (from brt_pwm)\n", cur_pwm);
        prefs.end();
        return;
    }
    // Legacy: index into LEVELS[].
    uint8_t saved_idx = prefs.getUChar("brt_idx", 0xFF);
    prefs.end();
    if (saved_idx < LEVELS_COUNT) cur_idx = saved_idx;
    cur_pwm = LEVELS[cur_idx];
    idle_set_awake_brightness(cur_pwm);
    Serial.printf("Brightness init: pwm=%u (idx=%u)\n", cur_pwm, cur_idx);
}

void brightness_cycle(void) {
    cur_idx = (cur_idx + 1) % LEVELS_COUNT;
    cur_pwm = LEVELS[cur_idx];

    Preferences prefs;
    prefs.begin("clawdmeter", false);
    // Drop the daemon-set value so cycling wins until the daemon resends.
    prefs.remove("brt_pwm");
    prefs.putUChar("brt_idx", cur_idx);
    prefs.end();

    idle_set_awake_brightness(cur_pwm);
    Serial.printf("Brightness cycled: pwm=%u (idx=%u)\n", cur_pwm, cur_idx);
}

uint8_t brightness_get(void) {
    return cur_pwm;
}

void brightness_set_pct(uint8_t pct) {
    if (pct > 100) pct = 100;
    cur_pwm = (uint8_t)((uint16_t)pct * 255 / 100);

    Preferences prefs;
    prefs.begin("clawdmeter", false);
    prefs.putUChar("brt_pwm", cur_pwm);
    // Drop the legacy index so subsequent boots prefer brt_pwm.
    prefs.remove("brt_idx");
    prefs.end();

    idle_set_awake_brightness(cur_pwm);   // store for next fade-in / wake
    // Force PWM NOW so the change shows even when the screen is asleep or
    // mid-fade. Forcefully rebind the LEDC channel first — some CYD clones
    // see ledcAttach fail silently on the first attempt because the channel
    // gets taken by another peripheral's transient init, and the only thing
    // a subsequent ledcWrite writes is a stale binding.
    display_hal_force_set_brightness(cur_pwm);

    Serial.printf("[BRT v3] daemon: pct=%u pwm=%u (gpio=%u applied)\n",
                  pct, cur_pwm, cur_pwm > 0 ? 1 : 0);
}

uint8_t brightness_get_pct(void) {
    return (uint8_t)((uint16_t)cur_pwm * 100 / 255);
}
